using MHWildsOptimizer.Core.Damage;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Gogma;

namespace MHWildsOptimizer.Tests;

/// <summary>The attack lists of every weapon type (motion value sheet 1.040).</summary>
public class AttackDataTests
{
    public static TheoryData<WeaponType> WeaponTypes() => [.. Enum.GetValues<WeaponType>()];

    [Theory]
    [MemberData(nameof(WeaponTypes))]
    public void EveryWeaponTypeButTheBowgunsHasAttacksAndADefault(WeaponType type)
    {
        var attacks = Attacks.For(type);
        if (type.IsBowgun())
        {
            Assert.Empty(attacks);
            Assert.Null(Attacks.DefaultFor(type));
            return;
        }
        Assert.NotEmpty(attacks);
        Assert.NotNull(Attacks.Find(type, Attacks.DefaultFor(type)));
        Assert.Equal(attacks.Count, attacks.Select(a => a.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(attacks, a =>
        {
            Assert.NotEmpty(a.Hits);
            Assert.True(a.Seconds > 0);
            Assert.True(a.Hits.Sum(h => h.Mv * h.Count) > 0, a.Id);
            Assert.All(a.Hits, h => Assert.True(h.Mv >= 0 && h.Element >= 0 && h.Count >= 1, $"{a.Id}: {h.Name}"));
        });
    }

    [Theory]
    [InlineData(WeaponType.SwordAndShield, 40 + 40 + 45 + 60 + 80 + 34 * 3)]
    [InlineData(WeaponType.DualBlades, 14 + 15 + 7 + 13 + 13 + 9 + 9 + 18 + 18)]
    [InlineData(WeaponType.Hammer, 20 + 20 + 200 + 50 + 210)]
    [InlineData(WeaponType.HuntingHorn, 40 + 27 + 50 + 27 + 20 + 75)]
    [InlineData(WeaponType.Lance, 26 + 26 + 35)]
    [InlineData(WeaponType.Gunlance, 38 + 57 + 40 + 3 * 9)]
    [InlineData(WeaponType.ChargeBlade, 16 + 78 + 45 + 5 * 28)]
    [InlineData(WeaponType.InsectGlaive, 24 + 12 + 24 + 18 + 28 + 26 + 18 + 40 + 22 + 48)]
    public void TheDefaultAttackScoresItsMotionValues(WeaponType type, double totalMv)
    {
        var weapon = new GogmaWeaponSpec { Type = type, Focus = GogmaFocus.Attack }.Resolve(TestData.Data);
        Assert.Equal(totalMv, Conditions.Default.Attack(weapon).TotalMv, 1e-9);
    }

    [Fact]
    public void ScaledLevelsMatchTheSheet()
    {
        Assert.Equal([19.2, 7.2, 10.8, 10.8, 10.8, 27.6], Attacks.Find(WeaponType.InsectGlaive, "strong-descending-slash-0")!.Hits.Select(h => h.Mv));
        Assert.Equal(73.1, Attacks.Find(WeaponType.Lance, "charge-counter-3")!.Hits.Single().Mv);
        Assert.Equal((7.0, 0.0), DamageConstants.Shell(GunlanceShelling.Wide, GunlanceShot.WyrmstakeTick, 3));
        Assert.Equal((8.0, 0.0), DamageConstants.Shell(GunlanceShelling.Long, GunlanceShot.WyrmstakeTick, 3));
    }
}
