using MudBlazor;

namespace MHWildsOptimizer.Browser.Services;

/// <summary>The profile's favorite decorations: starred in a slot picker, listed first in every slot picker.</summary>
public sealed partial class AppState
{
    public IReadOnlySet<string> FavoriteDecorations { get; private set; } = new HashSet<string>();

    public void ToggleFavoriteDecoration(string name)
    {
        if (Profile is null) return;
        var list = FavoriteDecorations.Contains(name) ? FavoriteDecorations.Where(n => n != name).ToList() : [.. FavoriteDecorations, name];
        try
        {
            FavoriteDecorations = (_store.SaveFavoriteDecorations(Profile, list).FavoriteDecorations ?? []).ToHashSet();
        }
        catch (IOException e)
        {
            Toast($"Could not save the favorites: {e.Message}", Severity.Error);
        }
        Notify();
    }
}
