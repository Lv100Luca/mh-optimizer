using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;

namespace MHWildsOptimizer.Tests;

/// <summary>Loads the real dataset once per test run (found by walking up from the test binary to the repo's data/ folder).</summary>
public static class TestData
{
    private static readonly Lazy<GameData> Lazy = new(() => GameDataLoader.Load(GameDataLoader.FindDataDirectory()));

    public static GameData Data => Lazy.Value;

    public static ArmorPiece PieceOf(string set, ArmorPieceKind kind) =>
        Data.Armor.First(a => a.Set == set && a.Piece == kind);
}
