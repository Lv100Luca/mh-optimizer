using MHWildsOptimizer.Core.Domain;

namespace MHWildsOptimizer.Core.Inputs;

/// <summary>
/// Edits of the armor options as the browser app's set list and lock buttons make them. A whole set off is kept in
/// <see cref="OptimizerOptions.ExcludeSets"/>, single pieces in <see cref="OptimizerOptions.ExcludeArmor"/>, and a lock makes a piece the
/// only one of its kind in <see cref="OptimizerOptions.IncludeArmor"/>.
/// </summary>
public static class ArmorChoices
{
    public static bool Allows(this OptimizerOptions o, string set, string piece) =>
        !o.ExcludeSets.Contains(set) && !o.ExcludeArmor.Contains(piece);

    /// <summary>Allows or disallows every piece of a set; disallowing drops its locks too.</summary>
    public static OptimizerOptions AllowSet(this OptimizerOptions o, string set, IReadOnlyCollection<string> pieces, bool allow)
    {
        var open = o with
        {
            ExcludeSets = o.ExcludeSets.Where(s => s != set).ToList(),
            ExcludeArmor = o.ExcludeArmor.Where(n => !pieces.Contains(n)).ToList(),
        };
        return allow ? open : open with { ExcludeSets = [.. open.ExcludeSets, set], IncludeArmor = o.IncludeArmor.Where(n => !pieces.Contains(n)).ToList() };
    }

    /// <summary>Allows or disallows one piece of a set; the last piece turned off folds the set into the excluded sets.</summary>
    public static OptimizerOptions AllowPiece(this OptimizerOptions o, string set, IReadOnlyCollection<string> pieces, string piece, bool allow)
    {
        if (allow == o.Allows(set, piece)) return o;
        if (allow)
            return o.ExcludeSets.Contains(set)
                ? o.AllowSet(set, pieces, true) with { ExcludeArmor = [.. o.ExcludeArmor.Where(n => !pieces.Contains(n)), .. pieces.Where(n => n != piece)] }
                : o with { ExcludeArmor = o.ExcludeArmor.Where(n => n != piece).ToList() };
        if (pieces.All(n => n == piece || !o.Allows(set, n))) return o.AllowSet(set, pieces, false);
        return o with { ExcludeArmor = [.. o.ExcludeArmor, piece], IncludeArmor = o.IncludeArmor.Where(n => n != piece).ToList() };
    }

    /// <summary>Allowed armor pieces that are the only one allowed for their kind: every run has to use them.</summary>
    public static HashSet<string> LockedPieces(this OptimizerOptions o, Func<string, ArmorPieceKind?> kindOf) =>
        o.IncludeArmor.GroupBy(kindOf).Where(g => g.Key is not null && g.Count() == 1).Select(g => g.First()).ToHashSet();

    /// <summary>Locking makes the piece the only allowed one of its kind (allowing it if it was off); unlocking opens the kind again.</summary>
    public static OptimizerOptions ToggleLock(this OptimizerOptions o, string set, IReadOnlyCollection<string> pieces, string piece, ArmorPieceKind kind,
                                              Func<string, ArmorPieceKind?> kindOf)
    {
        if (o.LockedPieces(kindOf).Contains(piece)) return o with { IncludeArmor = o.IncludeArmor.Where(n => n != piece).ToList() };
        return o.AllowPiece(set, pieces, piece, true) with { IncludeArmor = [.. o.IncludeArmor.Where(n => kindOf(n) != kind), piece] };
    }
}
