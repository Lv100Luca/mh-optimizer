using Spectre.Console;
using Spectre.Console.Rendering;

namespace MHWildsOptimizer.Cli.Interactive;

/// <summary>Thrown when the user presses Esc inside a prompt; the editor catches it and returns to the previous menu.</summary>
public sealed class PromptCancelledException : Exception
{
    public PromptCancelledException() : base("Prompt cancelled with Esc") { }
}

/// <summary>Wraps the console so that Esc in any prompt raises <see cref="PromptCancelledException"/>.</summary>
public sealed class EscapeCancellingConsole : IAnsiConsole
{
    private readonly IAnsiConsole _inner;

    public EscapeCancellingConsole(IAnsiConsole inner)
    {
        _inner = inner;
        Input = new EscapeInput(inner.Input);
    }

    public Profile Profile => _inner.Profile;
    public IAnsiConsoleCursor Cursor => _inner.Cursor;
    public IAnsiConsoleInput Input { get; }
    public IExclusivityMode ExclusivityMode => _inner.ExclusivityMode;
    public RenderPipeline Pipeline => _inner.Pipeline;
    public void Clear(bool home) => _inner.Clear(home);
    public void Write(IRenderable renderable) => _inner.Write(renderable);
    public void WriteAnsi(Action<AnsiWriter> action) => _inner.WriteAnsi(action);

    private sealed class EscapeInput(IAnsiConsoleInput inner) : IAnsiConsoleInput
    {
        public bool IsKeyAvailable() => inner.IsKeyAvailable();

        public ConsoleKeyInfo? ReadKey(bool intercept)
        {
            var key = inner.ReadKey(intercept);
            if (key?.Key == ConsoleKey.Escape) throw new PromptCancelledException();
            return key;
        }

        public async Task<ConsoleKeyInfo?> ReadKeyAsync(bool intercept, CancellationToken cancellationToken)
        {
            var key = await inner.ReadKeyAsync(intercept, cancellationToken);
            if (key?.Key == ConsoleKey.Escape) throw new PromptCancelledException();
            return key;
        }
    }
}
