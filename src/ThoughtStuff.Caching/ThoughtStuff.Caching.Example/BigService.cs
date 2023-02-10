// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

namespace ThoughtStuff.Caching.Example;

/// <summary>
/// Example of a service that has multiple functions.
/// For method caching the separate return types require separate interfaces.
/// </summary>
public class BigService : IWeatherService, IStockPriceService
{
    public async Task<WeatherForecast> GetWeatherForecast(DateOnly date)
    {
        await Task.Delay(2000);
        return new WeatherForecast(0.42, 42);
    }

    public async Task<DailyStockPrice> GetStockPrice(string symbol, DateOnly date)
    {
        await Task.Delay(2000);
        return new DailyStockPrice(4.2m, 42, 0.42m, 42);
    }
}
