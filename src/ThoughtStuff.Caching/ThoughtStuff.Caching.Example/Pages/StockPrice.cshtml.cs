using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ThoughtStuff.Caching.Example.Pages;

public class StockPriceModel : PageModel
{
    private readonly IStockPriceService _stockPriceService;

    public StockPriceModel(IStockPriceService stockPriceService)
    {
        _stockPriceService = stockPriceService ?? throw new ArgumentNullException(nameof(stockPriceService));
    }

    [BindProperty(SupportsGet = true)]
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    [BindProperty(SupportsGet = true)]
    public string Symbol { get; set; } = "ABCD";

    public DailyStockPrice? StockPrice { get; set; }

    public async Task OnGetAsync()
    {
        StockPrice = await _stockPriceService.GetStockPrice(Symbol, Date);
    }
}
