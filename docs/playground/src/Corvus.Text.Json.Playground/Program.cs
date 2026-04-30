using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Corvus.Text.Json.Playground;
using Corvus.Text.Json.Playground.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var httpClient = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
builder.Services.AddScoped(sp => httpClient);

builder.Services.AddSingleton<CodeGenerationService>();
builder.Services.AddScoped<WorkspaceService>();
builder.Services.AddScoped<CompilationService>();
builder.Services.AddScoped<ExecutionService>();
builder.Services.AddScoped<IntelliSenseService>();
builder.Services.AddScoped<UrlStateService>();

await builder.Build().RunAsync();
