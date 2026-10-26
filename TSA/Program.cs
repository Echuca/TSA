using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using TSA;
using TSA.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// One game per browser tab; in WebAssembly a scoped service lives for the whole session.
builder.Services.AddScoped<GameSession>();

await builder.Build().RunAsync();
