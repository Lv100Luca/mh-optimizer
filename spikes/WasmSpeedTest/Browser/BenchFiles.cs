using System.Diagnostics;
using MHWildsOptimizer.Core.Data;

namespace WasmSpeedTest;

/// <summary>Fetches the game data and configs into the runtime's in-memory file system, since Core reads files.</summary>
public static class BenchFiles
{
    public const string Root = "/bench";
    public const string Weapons = Root + "/inputs/weapons";

    /// <returns>The parsed game data and a line saying how long fetching and parsing took.</returns>
    public static async Task<(GameData Data, string Timing)> LoadAsync(HttpClient http)
    {
        var sw = Stopwatch.StartNew();
        foreach (var file in Bench.DataFiles)
            await CopyAsync(http, $"bench/data/{file}", $"{Root}/data/{file}");
        await CopyAsync(http, "bench/inputs/talismans.json", $"{Root}/inputs/talismans.json");
        foreach (var name in Bench.Cases.Select(c => c.Config).Distinct())
            await CopyAsync(http, $"bench/inputs/weapons/{name}.json", $"{Weapons}/{name}.json");
        var fetched = sw.Elapsed.TotalSeconds;
        var data = GameDataLoader.Load($"{Root}/data");
        return (data, $"Fetched in {fetched:F2}s, parsed in {sw.Elapsed.TotalSeconds - fetched:F2}s.");
    }

    private static async Task CopyAsync(HttpClient http, string url, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, await http.GetByteArrayAsync(url));
    }
}
