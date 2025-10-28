using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using BlazorUI;
using BlazorUI.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HttpClient for OrderApi
builder.Services.AddScoped(sp => new HttpClient 
{ 
    BaseAddress = new Uri("https://localhost:7027") 
});

// Configure named HttpClient for ERPApi
builder.Services.AddHttpClient("ERPApi", client =>
{
    client.BaseAddress = new Uri("https://localhost:7033");
});

// Configure HttpClient for SeqLogService (via ERPApi proxy to avoid CORS)
builder.Services.AddHttpClient<SeqLogService>(client =>
{
    client.BaseAddress = new Uri("https://localhost:7033");
});

await builder.Build().RunAsync();
