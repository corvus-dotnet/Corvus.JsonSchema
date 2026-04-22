using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Corvus.Text.Json.JsonLogic.Playground;
using Corvus.Text.Json.JsonLogic.Playground.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp =>
    new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddScoped<EvaluationService>();
builder.Services.AddScoped<WorkspaceService>();
builder.Services.AddScoped<OperatorCompilationService>();
builder.Services.AddScoped<IntelliSenseService>();

await builder.Build().RunAsync();
