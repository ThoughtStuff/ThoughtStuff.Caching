# Repository instructions

## Coding Style

- Wrap and align long parameter and argument lists.
- Wrap and align long call chains.
- Use C# idioms for the current version of C#.
  - When touching a file that has older idioms, update the file to use newer idioms.
  - Examples include:
    - Primary constructors
    - New extension method declarations
    - Square bracket initializer lists `[a, b, c]`
    - Implicit constructor `return new(arg)`

## Versioning

Bump the three NuGet package versions together based on the changes since the
last published release, following [SemVer](https://semver.org/spec/v2.0.0.html).
Use `MAJOR.MINOR.PATCH` in the package projects and `vMAJOR.MINOR.PATCH` for
GitHub release titles and tags.

## Documentation and Comments

Write comments for a maintainer who has never seen the change. Name the type,
member, or data the comment refers to. Explain what it does, why it is needed,
and why the implementation takes a non-obvious approach. Describe the concrete
failure it prevents when that helps explain the choice.

All public methods must be documented. When applicable member implementations should inherit docs from the base.
Document important internal types and members with XML comments. Explain non-obvious
invariants, ordering, and failure handling in code comments.

## Testing

Directly unit test all added or changed behavior, including internal classes
such as the SQLite catalog. Cover meaningful branches, edge cases, and failure
paths with distinct, deterministic tests. A host-dependent test alone can miss
an outcome on a developer's machine. Small records and state types that only
hold data do not need dedicated tests. Use `InternalsVisibleTo` for test access
when needed.

Use one to three words in each test method name. Set an explicit `DisplayName`
on every xUnit `[Fact]` and `[Theory]` test, using a more descriptive phrase to
explain the behavior it verifies.

Tests should have 3 "paragraphs" following the standard structure of Arrange, Act, Assert.
A simple comment like "// Arrange" is not needed".

Tests should be simple and should not test multiple scenarios or paths.
Each test should verify a single easy-to-understand scenario.
