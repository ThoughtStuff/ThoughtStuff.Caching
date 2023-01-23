// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

namespace ThoughtStuff.Caching.Example;

public class SlowExampleService : ISlowExampleService
{
    private readonly ILogger<SlowExampleService> _logger;

    public SlowExampleService(ILogger<SlowExampleService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public int GetMeaningOfLife()
    {
        _logger.LogInformation("Computing the meaning of life (slow)...");
        Thread.Sleep(5_000);
        return 42;
    }
}
