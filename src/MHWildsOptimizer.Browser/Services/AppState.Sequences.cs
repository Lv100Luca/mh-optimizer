using MHWildsOptimizer.Api;
using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Inputs;
using MudBlazor;

namespace MHWildsOptimizer.Browser.Services;

/// <summary>
/// The profile's named sequences, per weapon type: saved from the Conditions tab, picked again there, and shown by name
/// wherever a sequence with the same steps is the scored attack. A run keeps only the step names of its sequence, so it is
/// matched by those.
/// </summary>
public sealed partial class AppState
{
    /// <summary>Named sequences per weapon type (API kind).</summary>
    public IReadOnlyDictionary<string, List<SavedSequence>> Sequences { get; private set; } = new Dictionary<string, List<SavedSequence>>();

    public IReadOnlyList<SavedSequence> SequencesFor(string kind) => Sequences.GetValueOrDefault(kind) ?? [];

    /// <summary>The saved sequence with exactly these steps (attacks, repeats and step conditions), or null.</summary>
    public SavedSequence? SavedSequenceOf(string kind, IReadOnlyList<SequenceStep>? steps) =>
        steps is { Count: > 0 } ? SequencesFor(kind).FirstOrDefault(s => s.Steps.SequenceEqual(steps)) : null;

    /// <summary>The name of the saved sequence whose steps read like <paramref name="stepList"/> (<see cref="Attacks.StepList"/>), or null.</summary>
    public string? SequenceNameOf(string kind, string stepList)
    {
        var list = SequencesFor(kind);
        if (list.Count == 0) return null;
        WeaponType type;
        try { type = WeaponTypeInfo.FromApiKind(kind); }
        catch (ArgumentException) { return null; }
        return list.FirstOrDefault(s => Attacks.StepList(type, s.Steps) == stepList)?.Name;
    }

    /// <summary>"Offset TCS against Rey Dau, Head (raw hitzone 70)": a scored-on text with its sequence by name, for plain text.</summary>
    public string ScoredOnLabel(string scoredOn, string kind, bool withTarget = true)
    {
        var p = ScoredOnText.Parse(scoredOn);
        var name = p.IsSequence ? SequenceNameOf(kind, p.StepList!) ?? "Custom sequence" : p.Attack;
        return withTarget && p.Target is { } target ? $"{name} against {target}" : name;
    }

    /// <summary>The name of a per-attack table row: the request's own sequence ("Your sequence") by its saved name.</summary>
    public string AttackRowName(AttackRowDto row, OptimizationRequest request) =>
        row.Id == Attacks.Sequence && SavedSequenceOf(ProfileRules.WeaponKind(request), request.Conditions.AttackProfile?.Sequence) is { } saved ? saved.Name : row.Name;

    /// <summary>
    /// Saves the steps under a name. A saved sequence of the same name or with the same steps is replaced, so names and step
    /// lists stay unique.
    /// </summary>
    public void SaveSequence(string kind, string name, IReadOnlyList<SequenceStep> steps)
    {
        var copy = new SavedSequence { Name = name, Steps = steps.Select(s => s with { Conditions = new(s.Conditions) }).ToList() };
        var list = SequencesFor(kind).Where(s => s.Name != name && !s.Steps.SequenceEqual(steps)).Append(copy).ToList();
        StoreSequences(kind, list, $"Saved the sequence {name}");
    }

    public void RenameSequence(string kind, string from, string to) =>
        StoreSequences(kind, SequencesFor(kind).Select(s => s.Name == from ? s with { Name = to } : s).ToList(), $"Renamed {from} to {to}");

    public void DeleteSequence(string kind, string name) =>
        StoreSequences(kind, SequencesFor(kind).Where(s => s.Name != name).ToList(), $"Deleted the sequence {name}");

    private void StoreSequences(string kind, List<SavedSequence> list, string done)
    {
        if (Profile is null) return;
        try
        {
            Sequences = _store.SaveSequences(Profile, kind, list).Sequences ?? new Dictionary<string, List<SavedSequence>>();
            Toast(done);
        }
        catch (IOException e)
        {
            Toast($"Could not save the sequences: {e.Message}", Severity.Error);
        }
        Notify();
    }
}
