using MHWildsOptimizer.Api;
using MHWildsOptimizer.Browser.Services;
using Microsoft.AspNetCore.Components;

namespace MHWildsOptimizer.Browser.Components;

/// <summary>
/// Base of the panels and the shell: re-renders on every <see cref="AppState.Changed"/>. Small display components (chips,
/// icons) take parameters instead, so a state change does not re-render each of them.
/// </summary>
public abstract class AppComponent : ComponentBase, IDisposable
{
    [Inject] protected AppState App { get; set; } = default!;

    protected Catalog Catalog => App.Catalog;
    protected CatalogIndex Cat => App.Index;

    protected override void OnInitialized() => App.Changed += OnChanged;

    private void OnChanged() => InvokeAsync(StateHasChanged);

    public virtual void Dispose()
    {
        App.Changed -= OnChanged;
        GC.SuppressFinalize(this);
    }
}
