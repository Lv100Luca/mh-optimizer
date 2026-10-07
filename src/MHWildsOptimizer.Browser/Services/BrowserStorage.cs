using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.JSInterop;

namespace MHWildsOptimizer.Browser.Services;

/// <summary>Thrown when the browser refuses to store more (localStorage is limited to a few MB per site).</summary>
public sealed class StorageFullException(string key) : IOException($"The browser storage is full (writing {key}). Export and delete weapons or old results to make room.");

/// <summary>
/// localStorage, synchronously (Blazor WebAssembly can call JavaScript in-process). Values are JSON; large ones
/// (<see cref="SetCompressed{T}"/>) are gzipped and base64-encoded.
/// </summary>
public sealed class BrowserStorage(IJSRuntime js)
{
    private const string CompressedPrefix = "gz:";
    private readonly IJSInProcessRuntime _js = (IJSInProcessRuntime)js;

    public string? GetString(string key) => _js.Invoke<string?>("mhwo.storage.get", key);

    public void SetString(string key, string value)
    {
        if (!_js.Invoke<bool>("mhwo.storage.set", key, value)) throw new StorageFullException(key);
    }

    public void Remove(string key) => _js.InvokeVoid("mhwo.storage.remove", key);

    public IReadOnlyList<string> Keys(string prefix) => _js.Invoke<string[]>("mhwo.storage.keys", prefix);

    /// <summary>Characters stored under <paramref name="prefix"/>; browsers allow about 5 million per site.</summary>
    public long Size(string prefix) => _js.Invoke<long>("mhwo.storage.size", prefix);

    public T? Get<T>(string key, JsonSerializerOptions options)
    {
        var text = GetString(key);
        if (text is null) return default;
        if (text.StartsWith(CompressedPrefix, StringComparison.Ordinal))
        {
            using var input = new MemoryStream(Convert.FromBase64String(text[CompressedPrefix.Length..]));
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            return JsonSerializer.Deserialize<T>(gzip, options);
        }
        return JsonSerializer.Deserialize<T>(text, options);
    }

    public void Set<T>(string key, T value, JsonSerializerOptions options) => SetString(key, JsonSerializer.Serialize(value, options));

    /// <summary>Stores the JSON gzipped (results are large and repetitive: about a tenth of the size).</summary>
    public void SetCompressed<T>(string key, T value, JsonSerializerOptions options)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
            JsonSerializer.Serialize(gzip, value, options);
        SetString(key, CompressedPrefix + Convert.ToBase64String(output.ToArray()));
    }
}
