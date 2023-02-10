// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

namespace ThoughtStuff.Caching.Example;

public record DailyStockPrice(decimal Open, decimal High, decimal Low, decimal Close);

public interface IStockPriceService
{
    Task<DailyStockPrice> GetStockPrice(string symbol, DateOnly date);
}
