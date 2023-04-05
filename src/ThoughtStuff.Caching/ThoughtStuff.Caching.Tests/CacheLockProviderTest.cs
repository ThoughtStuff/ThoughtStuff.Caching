// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

namespace ThoughtStuff.Caching.Tests;

public class CacheLockProviderTest
{
    [Theory(DisplayName = "Return Same Object for Same Key"), AutoMoq]
    public void SameKey(CacheLockProvider cacheLockProvider, string key)
    {
        // Act
        var lockObject1 = cacheLockProvider.GetCacheLockObject(key);
        var lockObject2 = cacheLockProvider.GetCacheLockObject(key);

        // Assert
        lockObject1.Should().BeSameAs(lockObject2);
    }

    [Theory(DisplayName = "Return Different Object for Different Key"), AutoMoq]
    public void DifferentKey(CacheLockProvider cacheLockProvider, string key1, string key2)
    {
        // Act
        var lockObject1 = cacheLockProvider.GetCacheLockObject(key1);
        var lockObject2 = cacheLockProvider.GetCacheLockObject(key2);

        // Assert
        lockObject1.Should().NotBeSameAs(lockObject2);
    }

    [Theory(DisplayName = "Return Same Object for Same Key in Parallel"), AutoMoq]
    public void SameKeyParallel(CacheLockProvider cacheLockProvider, string key)
    {
        // Arrange
        const int numCalls = 5;

        // Act
        object[] lockObjects = new object[numCalls];
        Parallel.Invoke(
            () => lockObjects[0] = cacheLockProvider.GetCacheLockObject(key),
            () => lockObjects[1] = cacheLockProvider.GetCacheLockObject(key),
            () => lockObjects[2] = cacheLockProvider.GetCacheLockObject(key),
            () => lockObjects[3] = cacheLockProvider.GetCacheLockObject(key),
            () => lockObjects[4] = cacheLockProvider.GetCacheLockObject(key));

        // Assert
        lockObjects.Should().OnlyContain(obj => obj == lockObjects[0]);
    }

    [Theory(DisplayName = "Return Different Object for Expired Key"), AutoMoq]
    public async Task ExpiredKey(string key)
    {
        // Arrange
        var cacheLockProvider = CacheLockProvider.WithExpiration(TimeSpan.FromSeconds(2));

        // Act
        var lockObject1 = cacheLockProvider.GetCacheLockObject(key);
        await Task.Delay(TimeSpan.FromSeconds(3)); // wait for lock to expire
        var lockObject2 = cacheLockProvider.GetCacheLockObject(key);

        // Assert
        lockObject1.Should().NotBeSameAs(lockObject2);
    }
}
