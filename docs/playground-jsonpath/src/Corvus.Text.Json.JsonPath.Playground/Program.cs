using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Corvus.Text.Json.JsonPath.Playground;
using Corvus.Text.Json.JsonPath.Playground.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp =>
    new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddScoped<WorkspaceService>();
builder.Services.AddScoped<FunctionsCompilationService>();
builder.Services.AddScoped<IntelliSenseService>();
builder.Services.AddScoped<EvaluationService>();

await builder.Build().RunAsync();
