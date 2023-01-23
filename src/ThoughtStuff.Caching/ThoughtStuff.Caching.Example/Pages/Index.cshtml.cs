// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ThoughtStuff.Caching.Example.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly ISlowExampleService _slowExampleService;

    public int Value { get; set; }

    public IndexModel(ILogger<IndexModel> logger, ISlowExampleService slowExampleService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _slowExampleService = slowExampleService ?? throw new ArgumentNullException(nameof(slowExampleService));
    }

    public void OnGet()
    {
        _logger.LogInformation("Fetching the meaning of life (will it be cached?)");
        Value = _slowExampleService.GetMeaningOfLife();
        _logger.LogInformation("Done.");
    }
}
