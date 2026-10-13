using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;

namespace MHWildsOptimizer.Tests;

public class LooseSearchTests
{
    // what the armor picker searches: name, set and skills
    private static string ArmorText(ArmorPiece p) => string.Join(' ', [p.Name, p.Set ?? "", .. p.Skills.Select(s => s.Skill), .. p.SetBonus, p.GroupSkill ?? ""]);

    private static string DecoText(Decoration d) => string.Join(' ', [d.Name, .. d.Skills.Select(s => s.Skill)]);

    private static List<ArmorPiece> Heads => TestData.Data.Armor.Where(a => a.Piece == ArmorPieceKind.Head).ToList();

    [Theory]
    [InlineData("ark gamma")]
    [InlineData("arkvulcan γ")]
    [InlineData("gamma arkv helm")]
    public void GreekLettersGoByTheirNames(string query)
    {
        var (exact, _) = LooseSearch.Split(Heads, ArmorText, query);
        Assert.Equal("Arkvulcan Helm γ", Assert.Single(exact).Name);
    }

    [Fact]
    public void ATypoStillFindsThePieceWhenNothingMatchesExactly()
    {
        var (exact, close) = LooseSearch.Split(Heads, ArmorText, "arc gamma");
        Assert.Empty(exact);
        Assert.Equal("Arkvulcan Helm γ", close.First().Name);
        Assert.Empty(LooseSearch.Split(Heads, ArmorText, "ark gamma").Close);
    }

    [Theory]
    [InlineData("focus dragon", "Focus/Dragon Jewel [3]")]
    [InlineData("focus/dragon", "Focus/Dragon Jewel [3]")]
    [InlineData("dragon focus", "Dragon/Focus Jewel [3]")]
    public void JewelsByTheirSkillsInAnyOrderTheTypedOrderFirst(string query, string first)
    {
        var (exact, _) = LooseSearch.Split(TestData.Data.Decorations, DecoText, query);
        Assert.Equal([first, .. new[] { "Focus/Dragon Jewel [3]", "Dragon/Focus Jewel [3]" }.Where(n => n != first)], exact.Select(d => d.Name));
    }

    [Fact]
    public void WordsRunTogetherFollowTheirOrder()
    {
        var (exact, _) = LooseSearch.Split(TestData.Data.Decorations, DecoText, "focusdragon");
        Assert.Equal("Focus/Dragon Jewel [3]", Assert.Single(exact).Name);
    }

    [Fact]
    public void ExactMatchesKeepTheirOrderAndComeFirst()
    {
        string[] items = ["Attack Boost", "Critical Eye", "Atack Jewel", "Weakness Exploit"];
        Assert.Equal(["Attack Boost"], LooseSearch.Filter(items, s => s, "attack"));
        Assert.Equal(["Attack Boost"], LooseSearch.Filter(items, s => s, "attakc"));
        Assert.Equal(items, LooseSearch.Filter(items, s => s, "  "));
    }

    [Theory]
    [InlineData("arc", "ark", 1)]
    [InlineData("acr", "ark", 2)]
    [InlineData("arc", "ar", 3)]
    [InlineData("arkvled", "arkveld", 1)]
    [InlineData("arkvelld", "arkveld", 1)]
    [InlineData("tenderiser", "tenderizer", 1)]
    public void PrefixDistance(string word, string text, int expected) =>
        Assert.Equal(expected, LooseSearch.PrefixDistance(word, text, 2));

    [Fact]
    public void TwoLetterWordsAllowNoTypos()
    {
        Assert.False(LooseSearch.Matches("Wex", "wz"));
        Assert.True(LooseSearch.Matches("Wex", "wez"));
        Assert.True(LooseSearch.Matches("Weakness Exploit", "weak expl"));
        Assert.True(LooseSearch.Matches("Weakness Exploit", "weekness"));
        Assert.False(LooseSearch.Matches("Weakness Exploit", "veakness"));
    }
}
