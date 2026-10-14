using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace MHWildsOptimizer.Browser.Components.Common;

/// <summary>
/// The lazily created MudTooltip of <see cref="SkillHover"/>, <see cref="InfoHover"/> and <see cref="DecoIcon"/>: a stand-in
/// until the pointer or the focus arrives (<see cref="Arm"/>), then the tooltip, already showing.
/// MudTooltip closes on its own pointerleave only, which never comes when the page scrolls the anchor away under a still
/// pointer, or when the pointer left again before the tooltip was there to hear it; so while it shows, the page watches the
/// pointer (mhwo.watchHover) and closes it once the pointer is somewhere else.
/// </summary>
public abstract class HoverTip : ComponentBase, IAsyncDisposable
{
    private static int _nextId;

    [Inject] private IJSRuntime JS { get; set; } = null!;

    /// <summary>The element the tooltip is anchored on, inside the MudTooltip's content.</summary>
    protected ElementReference Anchor;
    protected bool Armed { get; private set; }
    protected bool Visible { get; private set; }

    private readonly int _id = Interlocked.Increment(ref _nextId);
    private DotNetObjectReference<HoverTip>? _self;
    private bool _watching;
    private double? _x, _y;

    /// <summary>The pointer (or the focus) arrived before the tooltip existed: create it, already showing.</summary>
    protected void Arm(PointerEventArgs e) => Open(e.ClientX, e.ClientY);

    protected void ArmFocus(FocusEventArgs _) => Open(null, null);

    private void Open(double? x, double? y)
    {
        Armed = true;
        Visible = true;
        (_x, _y) = (x, y);
    }

    protected async Task OnVisibleChanged(bool visible)
    {
        Visible = visible;
        if (!visible) await Unwatch();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!Visible || _watching || Anchor.Context is null) return;
        _watching = true;
        _self ??= DotNetObjectReference.Create(this);
        (double? x, double? y) = (_x, _y);
        (_x, _y) = (null, null);
        // without the watch the tooltip still closes the way MudTooltip does
        try { await JS.InvokeVoidAsync("mhwo.watchHover", _id, Anchor, _self, x, y); }
        catch (JSException) { _watching = false; }
    }

    /// <summary>The page saw the pointer away from the anchor (or the page scrolled it away).</summary>
    [JSInvokable]
    public void HoverLeft()
    {
        _watching = false;
        if (!Visible) return;
        Visible = false;
        StateHasChanged();
    }

    private async Task Unwatch()
    {
        if (!_watching) return;
        _watching = false;
        try { await JS.InvokeVoidAsync("mhwo.unwatchHover", _id); }
        catch (JSException) { }
    }

    public async ValueTask DisposeAsync()
    {
        try { await Unwatch(); }
        catch (JSDisconnectedException) { }
        _self?.Dispose();
        GC.SuppressFinalize(this);
    }
}
