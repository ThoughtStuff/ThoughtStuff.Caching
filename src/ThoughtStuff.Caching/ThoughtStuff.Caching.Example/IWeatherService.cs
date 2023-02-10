// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

namespace ThoughtStuff.Caching.Example;

public record WeatherForecast(double ChanceOfRain, double Temperature);

public interface IWeatherService
{
    Task<WeatherForecast> GetWeatherForecast(DateOnly date);
}
