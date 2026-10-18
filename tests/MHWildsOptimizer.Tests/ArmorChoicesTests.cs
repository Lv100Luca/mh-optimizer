using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Inputs;

namespace MHWildsOptimizer.Tests;

public class ArmorChoicesTests
{
    private const string Set = "Rey Dau β";
    private static readonly string[] Pieces = ["Rey Sandhelm β", "Rey Sandmail β", "Rey Sandbraces β", "Rey Sandcoil β", "Rey Sandgreaves β"];

    private static ArmorPieceKind? KindOf(string name) => TestData.Data.ArmorByName.TryGetValue(name, out var a) ? a.Piece : null;

    [Fact]
    public void TheDatasetHasTheSetUsedHere() =>
        Assert.Equal(Pieces, TestData.Data.Armor.Where(a => a.Set == Set).Select(a => a.Name));

    [Fact]
    public void SinglePiecesGoToExcludeArmorAndTheLastOneFoldsIntoTheSet()
    {
        var o = new OptimizerOptions();
        o = o.AllowPiece(Set, Pieces, Pieces[0], false);
        Assert.Equal([Pieces[0]], o.ExcludeArmor);
        Assert.False(o.Allows(Set, Pieces[0]));
        Assert.True(o.Allows(Set, Pieces[1]));

        foreach (var p in Pieces.Skip(1)) o = o.AllowPiece(Set, Pieces, p, false);
        Assert.Equal([Set], o.ExcludeSets);
        Assert.Empty(o.ExcludeArmor);
    }

    [Fact]
    public void AllowingAPieceOfAnExcludedSetExcludesTheOthersOneByOne()
    {
        var o = new OptimizerOptions { ExcludeSets = [Set, "Sororal α"] }.AllowPiece(Set, Pieces, Pieces[2], true);
        Assert.Equal(["Sororal α"], o.ExcludeSets);
        Assert.Equal(Pieces.Where(p => p != Pieces[2]), o.ExcludeArmor);
        Assert.True(o.Allows(Set, Pieces[2]));
    }

    [Fact]
    public void TheSetCheckboxAllowsOrDisallowsEveryPiece()
    {
        var partly = new OptimizerOptions { ExcludeArmor = [Pieces[0], "Sororal Helm α"], IncludeArmor = [Pieces[1]] };
        var on = partly.AllowSet(Set, Pieces, true);
        Assert.Equal(["Sororal Helm α"], on.ExcludeArmor);
        Assert.All(Pieces, p => Assert.True(on.Allows(Set, p)));

        var off = on.AllowSet(Set, Pieces, false);
        Assert.Equal([Set], off.ExcludeSets);
        Assert.Empty(off.IncludeArmor);
    }

    [Fact]
    public void LockingMatchesTheResultsLockAndAllowsAnExcludedPiece()
    {
        var o = new OptimizerOptions { ExcludeArmor = [Pieces[0]], IncludeArmor = ["G. Arkveld Helm β", "Gore Mail β"] };
        o = o.ToggleLock(Set, Pieces, Pieces[0], ArmorPieceKind.Head, KindOf);
        Assert.Equal(["Gore Mail β", Pieces[0]], o.IncludeArmor);
        Assert.Empty(o.ExcludeArmor);
        Assert.Contains(Pieces[0], o.LockedPieces(KindOf));

        o = o.ToggleLock(Set, Pieces, Pieces[0], ArmorPieceKind.Head, KindOf);
        Assert.Equal(["Gore Mail β"], o.IncludeArmor);

        // turning a locked piece off drops the lock
        o = o.ToggleLock(Set, Pieces, Pieces[1], ArmorPieceKind.Chest, KindOf).AllowPiece(Set, Pieces, Pieces[1], false);
        Assert.Empty(o.IncludeArmor);
        Assert.Equal([Pieces[1]], o.ExcludeArmor);
    }
}
