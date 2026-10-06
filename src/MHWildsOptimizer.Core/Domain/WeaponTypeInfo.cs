namespace MHWildsOptimizer.Core.Domain;

public static class WeaponTypeInfo
{
    private static readonly Dictionary<WeaponType, string> ApiKinds = new()
    {
        [WeaponType.GreatSword] = "great-sword",
        [WeaponType.SwordAndShield] = "sword-shield",
        [WeaponType.DualBlades] = "dual-blades",
        [WeaponType.LongSword] = "long-sword",
        [WeaponType.Hammer] = "hammer",
        [WeaponType.HuntingHorn] = "hunting-horn",
        [WeaponType.Lance] = "lance",
        [WeaponType.Gunlance] = "gunlance",
        [WeaponType.SwitchAxe] = "switch-axe",
        [WeaponType.ChargeBlade] = "charge-blade",
        [WeaponType.InsectGlaive] = "insect-glaive",
        [WeaponType.Bow] = "bow",
        [WeaponType.HeavyBowgun] = "heavy-bowgun",
        [WeaponType.LightBowgun] = "light-bowgun",
    };

    private static readonly Dictionary<string, WeaponType> ByApiKind =
        ApiKinds.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.OrdinalIgnoreCase);

    /// <summary>The weapon kind string used by wilds.mhdb.io and the dataset files.</summary>
    public static string ApiKind(this WeaponType type) => ApiKinds[type];

    public static WeaponType FromApiKind(string kind) =>
        ByApiKind.TryGetValue(kind, out var t) ? t : throw new ArgumentException($"Unknown weapon kind '{kind}'", nameof(kind));

    /// <summary>Display attack / bloat = true raw. Derived from the API (raw vs display) on every weapon.</summary>
    public static double Bloat(this WeaponType type) => type switch
    {
        WeaponType.GreatSword => 4.8,
        WeaponType.LongSword => 3.3,
        WeaponType.SwordAndShield => 1.4,
        WeaponType.DualBlades => 1.4,
        WeaponType.Hammer => 5.2,
        WeaponType.HuntingHorn => 4.2,
        WeaponType.Lance => 2.3,
        WeaponType.Gunlance => 2.3,
        WeaponType.SwitchAxe => 3.5,
        WeaponType.ChargeBlade => 3.6,
        WeaponType.InsectGlaive => 3.1,
        WeaponType.LightBowgun => 1.3,
        WeaponType.HeavyBowgun => 1.5,
        WeaponType.Bow => 1.2,
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    public static bool IsGunner(this WeaponType type) =>
        type is WeaponType.Bow or WeaponType.HeavyBowgun or WeaponType.LightBowgun;

    public static bool IsBowgun(this WeaponType type) =>
        type is WeaponType.HeavyBowgun or WeaponType.LightBowgun;

    public static bool HasSharpness(this WeaponType type) => !type.IsGunner();
}
