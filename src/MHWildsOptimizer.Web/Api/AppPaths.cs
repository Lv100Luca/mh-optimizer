using MHWildsOptimizer.Core.Data;

namespace MHWildsOptimizer.Web.Api;

/// <summary>Where the dataset and the saved configurations live. Configure with Paths:Data / Paths:Inputs (or --data / --inputs).</summary>
public sealed record AppPaths(string Data, string Inputs)
{
    public static AppPaths Resolve(IConfiguration configuration)
    {
        var data = configuration["Paths:Data"] is { Length: > 0 } d ? Path.GetFullPath(d) : FindData();
        var inputs = configuration["Paths:Inputs"] is { Length: > 0 } i
            ? Path.GetFullPath(i)
            : Path.Combine(Directory.GetParent(data)!.FullName, "inputs");
        return new AppPaths(data, inputs);
    }

    private static string FindData()
    {
        try { return GameDataLoader.FindDataDirectory(); }
        catch (DirectoryNotFoundException) { return GameDataLoader.FindDataDirectory(Directory.GetCurrentDirectory()); }
    }
}

/// <summary>Shown at / when the client has not been built yet.</summary>
public static class SetupPage
{
    public const string Html = """
        <!doctype html><html><head><meta charset="utf-8"><title>MH Wilds Optimizer</title>
        <style>body{font:16px system-ui;background:#14161b;color:#e6e6e6;max-width:52em;margin:4em auto;padding:0 1em}code,pre{background:#22252c;padding:.2em .4em;border-radius:4px}pre{padding:1em}a{color:#f0c674}</style></head>
        <body><h1>MH Wilds Optimizer</h1>
        <p>The API is running, but the web client has not been built yet. Build it once with node/npm:</p>
        <pre>cd src/MHWildsOptimizer.Web/client
        npm install
        npm run build</pre>
        <p>then reload this page. For development run <code>npm run dev</code> in that folder and open <a href="http://localhost:5173">http://localhost:5173</a>, which proxies <code>/api</code> to this server.</p>
        <p>API: <a href="/api/catalog">/api/catalog</a>, <a href="/api/profiles">/api/profiles</a></p>
        </body></html>
        """;
}
