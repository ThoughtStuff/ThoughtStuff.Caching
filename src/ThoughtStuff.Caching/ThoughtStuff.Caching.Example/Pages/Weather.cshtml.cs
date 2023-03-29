using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ThoughtStuff.Caching.Example.Pages;

public class WeatherModel : PageModel
{
    private readonly IWeatherService _weatherService;

    public WeatherModel(IWeatherService weatherService)
    {
        _weatherService = weatherService ?? throw new ArgumentNullException(nameof(weatherService));
    }

#if NET7_0
    [BindProperty(SupportsGet = true)]
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);
#else
    [BindProperty(SupportsGet = true)]
    public DateTime Date { get; set; } = DateTime.Today;
#endif

    public WeatherForecast? WeatherForecast { get; set; }

    public async Task OnGetAsync()
    {
        WeatherForecast = await _weatherService.GetWeatherForecast(DateOnly.FromDateTime(Date));
    }
}
