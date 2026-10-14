using System.Diagnostics;

namespace MHWildsOptimizer.Core.Optimize;

/// <summary>
/// Hands the thread back to the host now and then during a long stretch of synchronous work. In the browser .NET shares the
/// page's only thread: the damage model of a search took about a second there without a break, a frozen page right after
/// "Run optimizer" was clicked. Natively a pause is a yield to the thread pool now and then.
/// </summary>
/// <param name="yield">Lets the host run, e.g. <see cref="ICpSatBackend.YieldAsync"/> (never a timer in the browser, see there).</param>
/// <param name="ct">Checked after every yield, so a cancelled run stops in the middle of the work.</param>
/// <param name="sliceMs">Work between two yields; the browser counts a task over 50 ms as a long one.</param>
public sealed class Pacer(Func<Task> yield, CancellationToken ct = default, double sliceMs = 30)
{
    /// <summary>Never yields: the work runs through, for synchronous callers.</summary>
    public static readonly Pacer Never = new(() => Task.CompletedTask, default, double.PositiveInfinity);

    private long _since = Stopwatch.GetTimestamp();

    /// <summary>Completes at once until the slice is used up; then lets the host run.</summary>
    public ValueTask PauseAsync() => Stopwatch.GetElapsedTime(_since).TotalMilliseconds < sliceMs ? default : new ValueTask(YieldAsync());

    private async Task YieldAsync()
    {
        await yield();
        ct.ThrowIfCancellationRequested();
        _since = Stopwatch.GetTimestamp();
    }
}
