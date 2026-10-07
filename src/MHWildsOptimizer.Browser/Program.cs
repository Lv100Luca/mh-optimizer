using MHWildsOptimizer.Api;
using MHWildsOptimizer.Browser;
using MHWildsOptimizer.Browser.Services;
using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Inputs;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using MudBlazor;
using MudBlazor.Services;

// The optimizer as a static web app: the dataset is fetched once, profiles live in browser storage and CP-SAT runs in
// WebAssembly (CpSatBridge). Nothing here talks to a server beyond loading the files.
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var http = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
builder.Services.AddSingleton(http);
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomCenter;
    config.SnackbarConfiguration.VisibleStateDuration = 4000;
    config.SnackbarConfiguration.ShowTransitionDuration = 150;
    config.SnackbarConfiguration.HideTransitionDuration = 150;
});

// the dataset, before anything renders (index.html shows "Loading" meanwhile)
var files = await Task.WhenAll(GameDataLoader.FileNames.Select(async name =>
{
    using var response = await http.GetAsync("data/" + name);
    return (Name: name, Bytes: response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync() : null);
}));
var data = GameDataLoader.Load(name => files.FirstOrDefault(f => f.Name == name).Bytes is { } bytes ? new MemoryStream(bytes) : null, "data/");
builder.Services.AddSingleton(data);
// built on first use, after OptimizerOptions.ProcessorCount is known (the catalog reports it)
builder.Services.AddSingleton(sp => Catalog.Build(sp.GetRequiredService<GameData>()));
builder.Services.AddSingleton<BrowserStorage>();
builder.Services.AddSingleton<BrowserProfileStore>();
builder.Services.AddSingleton<IProfileStore>(sp => sp.GetRequiredService<BrowserProfileStore>());
builder.Services.AddSingleton<CpSatBridge>();
builder.Services.AddScoped<AppState>();
builder.Services.AddScoped<ProfileTransfer>();

var host = builder.Build();
// CP-SAT's workers are web workers: the browser's core count is what a run can use (Environment.ProcessorCount is 1 here)
OptimizerOptions.ProcessorCount = Math.Max(1, ((IJSInProcessRuntime)host.Services.GetRequiredService<IJSRuntime>()).Invoke<int>("mhwo.cores"));
await host.RunAsync();
