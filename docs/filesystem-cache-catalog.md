# Filesystem cache catalog design

The filesystem package owns capacity and recency. The consumer configures its
budget and owns any application-specific policy, legacy access imports, and
re-download metrics.

## Agreed behavior

| Topic | Decision |
| --- | --- |
| Budget | Best-effort limit on published payload and metadata bytes. |
| Overhead | Track staging, orphan files, and SQLite overhead separately. |
| Timing | Trim at an opportune time. Admission is the initial trigger. |
| Startup | Inventory existing entries. An oversized cache may open as-is. |
| Capacity bypass | Return the computed value and skip caching when it cannot fit. |
| Cache hit | Count a successful payload read even if decoding later fails. |
| Process sharing | Support one process per directory and multiple instances. |
| Process detection | Warn on detection of another process and allow it to continue. |
| Eviction history | Deferred. |
| Interrupted file-pair replacement | Deferred as a separate issue. |

## Public API

```csharp
public class LocalFileCacheOptions
{
    // Existing members omitted.

    /// <summary>
    /// Best-effort limit on published payload and metadata bytes.
    /// A non-null value enables capacity management for the directory.
    /// </summary>
    public long? SoftSizeLimitBytes { get; set; }
}
```

```text
New public interface members
  ITextCache     none
  ITypedCache    none
  ICacheManager  none
```

## Store operations

```text
Open
  Share a process-wide coordinator for the normalized directory path.
  Use a temporary file to verify case-only directory aliases and to
  detect whether filenames within the cache directory ignore case.
  Inventory file lengths and expiration metadata without decoding payloads.
  Create and populate the catalog from the files if it is missing.

Contains
  Check existence and expiration.
  Remove expired entries through the shared file-pair removal path.

GetString
  Check expiration and read the payload with its entry version.
  Queue a hit for that version on success.
  Coalesce hits per entry version in a bounded buffer.
  Flush using the newest timestamp, only for the matching entry version.
  Unflushed timestamps may be lost on abrupt process exit.

SetString
  Bypass an entry larger than SoftSizeLimitBytes before evicting anything.
  If the write would put the cache above the limit, try to make enough
  room for it within that same limit, with at most 32 removals per write.
  Measure staged metadata before eviction so custom serializers are counted.
  Generate a fresh EntryVersion token in the metadata companion for each write.
  Prefer expired entries, then order by last use or publication time.
  Consult buffered hits as well as the catalog before choosing a victim.

Remove / expiry / wildcard removal
  Use the shared file-pair removal path.
  For a trim candidate, recheck the entry version and recency under
  the same coordinator used to publish files before removing the pair.
  Remove the catalog row once the pair is gone.
```

The payload read is the hit boundary. Existence probes and read misses leave
recency alone. Decode errors follow the existing error path.

## SQLite schema

```sql
CREATE TABLE cache_entries (
    payload_path        TEXT PRIMARY KEY NOT NULL, -- Relative path, normalized for casing.
    entry_version       TEXT NOT NULL,             -- Metadata token and file stamps.
    payload_size_bytes  INTEGER NOT NULL,          -- Payload file length in bytes.
    metadata_size_bytes INTEGER NOT NULL,          -- Companion length in bytes, or zero if absent.
    published_at_utc    INTEGER NOT NULL,          -- Payload write time in UTC ticks.
    expires_at_utc      INTEGER,                   -- Expiry in UTC ticks, or null if unknown.
    last_used_at_utc    INTEGER                    -- Last successful read in UTC ticks, or null.
);

CREATE INDEX cache_entries_expiration
    ON cache_entries (expires_at_utc, payload_path);

CREATE INDEX cache_entries_recency
    ON cache_entries (COALESCE(last_used_at_utc, published_at_utc), payload_path);
```

## Catalog reconciliation

```text
Write or replace an entry
  Update the catalog after publishing the files.

Reconcile the catalog against files
  Complete publication absent from catalog -> insert the live row.
  Catalog row describes another version    -> replace the live row.
  Payload is gone                          -> remove the stale live row.
  Legacy payload without metadata          -> use the existing default policy.
```

```text
New entry version    EntryVersion token + payload and metadata file stamps
Legacy entry version payload and metadata file stamps
Matching version     preserve recorded recency
```

Rebuilding from files recovers live byte counts and expiration. Hit timestamps
stored only in a lost catalog are lost with it.

| Catalog condition | Proposed handling |
| --- | --- |
| Missing | Build it from existing payloads and metadata. This is the normal upgrade path. |
| Present and out of sync | Reconcile rows against files and verified entry versions. |
| Unreadable or unwritable | Report the storage error. Defer a separate degraded mode. |

Other catalog failures include permissions, disk exhaustion, I/O errors, and
corruption. [SQLite result codes](https://www.sqlite.org/rescode.html)

## Process scope and logging

```text
Warning                  injected ILogger<T>, structured, best-effort detection
Caller documentation     capacity options, publication and removal,
                         reads and probes that can remove expired entries
Multi-process comments   directory coordinator, reservations, publication,
                         identity-checked deletion, hit buffer, reconciliation
```

SQLite serializes its database writers across processes. The package would
also need to coordinate changes to files and process-local state to support
shared-directory writes.
[SQLite transactions](https://www.sqlite.org/lang_transaction.html)
