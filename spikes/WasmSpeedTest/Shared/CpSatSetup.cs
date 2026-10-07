using MHWildsOptimizer.Core.Data;
using MHWildsOptimizer.Core.Domain;
using MHWildsOptimizer.Core.Gogma;
using MHWildsOptimizer.Core.Inputs;
using MHWildsOptimizer.Core.Optimize;

namespace WasmSpeedTest;

/// <summary>The inputs of one CP-SAT search, set up the way Optimizer.Search does for a fixed skill pair.</summary>
public sealed record CpSatSetup(
    GameData Data, Relevance Rel, GogmaWeaponStats Weapon, ResolvedRequest Request, DecorationFiller Filler,
    Dictionary<ArmorPieceKind, List<ArmorCandidate>> Armor, ArmorPieceKind[] Kinds, List<TalismanCandidate> Talismans)
{
    public static CpSatSetup ForFixedPair(GameData data, ResolvedRequest r)
    {
        if (!r.IsValid) throw new InvalidOperationException(string.Join("; ", r.Errors));
        if (r.SkillPair.Mode != SkillPairMode.Fixed) throw new NotSupportedException("only fixed skill pair configs are supported");

        var pair = r.SkillPairCandidates.FirstOrDefault();
        var weapon = r.Weapon with { SetBonus = pair?.SetBonus ?? r.Weapon.SetBonus, GroupSkill = pair?.GroupSkill ?? r.Weapon.GroupSkill };
        var rel = Relevance.Build(weapon, r, data);
        var armor = Candidates.Armor(data, rel, r.Options);
        return new CpSatSetup(data, rel, weapon, r, new DecorationFiller(data, rel), armor,
            Enum.GetValues<ArmorPieceKind>().OrderBy(k => armor[k].Count).ToArray(), Candidates.Talismans(r.Talismans, rel));
    }
}
