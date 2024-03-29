// Copyright (c) ThoughtStuff, LLC.
// Licensed under the ThoughtStuff, LLC Split License.

using ThoughtStuff.Caching;
using ThoughtStuff.Caching.Example;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// Cache method results of the following interfaces which return given return type
builder.Services.AddMethodCaching()
                .AddTransientWithCaching<ISlowExampleService, SlowExampleService, int>()
                .AddTransientWithCaching<IWeatherService, BigService, WeatherForecast>()
                .AddTransientWithCaching<IStockPriceService, BigService, DailyStockPrice>();

//builder.Services.AddCachingWithAzureBlobs(builder.Configuration);

var app = builder.Build();

// Configure method caching policies
var methodCachePolicies = app.Services.GetRequiredService<IMethodCacheOptionsLookup>();
methodCachePolicies.AddRelativeExpiration<ISlowExampleService>(TimeSpan.FromSeconds(30));

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.Run();
