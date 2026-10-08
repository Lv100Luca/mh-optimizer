using System.Text.Json;

namespace MHWildsOptimizer.Browser.Services;

/// <summary>Small display preferences kept in this browser: which cards are collapsed.</summary>
public sealed class UiState(BrowserStorage storage)
{
    private const string CollapsedKey = "mhwo:ui:collapsed";

    private Dictionary<string, bool>? _collapsed;

    private Dictionary<string, bool> Collapsed => _collapsed ??= storage.Get<Dictionary<string, bool>>(CollapsedKey, JsonSerializerOptions.Default) ?? [];

    /// <summary>Whether the card <paramref name="key"/> is collapsed; <paramref name="fallback"/> when the user never toggled it.</summary>
    public bool IsCollapsed(string key, bool fallback) => Collapsed.TryGetValue(key, out var v) ? v : fallback;

    public void SetCollapsed(string key, bool collapsed, bool fallback)
    {
        if (collapsed == fallback) Collapsed.Remove(key);
        else Collapsed[key] = collapsed;
        try
        {
            if (Collapsed.Count == 0) storage.Remove(CollapsedKey);
            else storage.Set(CollapsedKey, Collapsed, JsonSerializerOptions.Default);
        }
        catch (StorageFullException)
        {
            // a preference is not worth an error
        }
    }
}
