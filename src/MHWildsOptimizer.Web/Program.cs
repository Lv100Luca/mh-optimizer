using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Web.Api;

// Web front end for the optimizer: serves the Vite client from wwwroot and a JSON API under /api.
//   dotnet run --project src/MHWildsOptimizer.Web [-- --data <dir> --inputs <dir>]
var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddCommandLine(args, new Dictionary<string, string>
{
    ["--data"] = "Paths:Data",
    ["--inputs"] = "Paths:Inputs",
});

builder.Services.ConfigureHttpJsonOptions(o => ApiJson.Configure(o.SerializerOptions));
builder.Services.AddSingleton(sp => AppPaths.Resolve(sp.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton(sp => GameDataLoader.Load(sp.GetRequiredService<AppPaths>().Data));
builder.Services.AddSingleton(sp => Catalog.Build(sp.GetRequiredService<GameData>()));
builder.Services.AddSingleton<ConfigStore>();

var app = builder.Build();

var paths = app.Services.GetRequiredService<AppPaths>();
var data = app.Services.GetRequiredService<GameData>();
app.Logger.LogInformation("Loaded {Armor} armor pieces, {Skills} skills, {Decorations} decorations, {Pairs} Gogma skill pairs from {Data}; configurations in {Inputs}",
    data.Armor.Count, data.Skills.Count, data.Decorations.Count, data.GogmaSkillPairs.Count, paths.Data, paths.Inputs);

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapApi();

var index = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "index.html");
if (File.Exists(index))
    app.MapFallbackToFile("index.html");
else
    app.MapFallback(() => Results.Content(SetupPage.Html, "text/html"));

app.Run();

/// <summary>Exposed so integration tests can host the app with WebApplicationFactory.</summary>
public partial class Program;
