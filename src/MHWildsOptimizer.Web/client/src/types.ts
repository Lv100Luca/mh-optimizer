// Mirrors the API DTOs (snake_case, lower-case enum names) of src/MHWildsOptimizer.Web/Api.

export type SkillKind = 'armor' | 'weapon' | 'set' | 'group';
export type ArmorPieceKind = 'head' | 'chest' | 'arms' | 'waist' | 'legs';
export type Element = 'none' | 'fire' | 'water' | 'thunder' | 'ice' | 'dragon';
export type SharpnessColor = 'red' | 'orange' | 'yellow' | 'green' | 'blue' | 'white' | 'purple';
export type GogmaFocus = 'attack' | 'affinity' | 'element';
export type SkillPairMode = 'fixed' | 'optimize';
export type ResonanceMode = 'none' | 'local' | 'remote';

export interface SkillGrant { skill: string; skill_id?: number; level: number }

export interface SkillRank { level: number; name: string | null; pieces_required: number | null; description: string | null }

export interface Skill {
  id: number; name: string; kind: SkillKind; max_level: number; description: string | null; icon: string | null; ranks: SkillRank[];
}

export interface Decoration { id: number; name: string; kind: SkillKind; slot: number; rarity: number; icon_color: string | null; skills: SkillGrant[] }

export interface ArmorSetPiece { id: number; name: string; kind: ArmorPieceKind; rarity: number; skills: SkillGrant[]; slots: number[]; slots_transcended: number[] }

/** A craftable charm at one rank; the optimizer uses the max rank of each line. */
export interface Charm { name: string; rarity: number; is_max_rank: boolean; skills: SkillGrant[] }

export interface ArmorSet { name: string; rarity: number; set_bonus: string[]; group_skill: string | null; pieces: ArmorSetPiece[] }

export interface SharpnessBar { red: number; orange: number; yellow: number; green: number; blue: number; white: number; purple: number }

export interface GogmaVariant { name: string; raw: number; display_attack: number; affinity: number; sharpness: SharpnessBar | null; slots: number[] }

/** The weapon type's attack profile preset; an unset request value falls back to it. */
export interface AttackProfilePreset { hits_per_minute: number; average_mv: number; charged_lv3_share: number; element_hitzone_ratio: number }

/** conditions.attack_profile: how the hunter attacks, turns element and proc damage (once per hit) into damage per 100 MV. null = weapon preset. */
export interface AttackProfile { hits_per_minute: number | null; average_mv: number | null; charged_lv3_share: number | null; element_hitzone_ratio: number | null }

export interface WeaponType {
  kind: string; label: string; gunner: boolean; supported: boolean; bloat: number;
  core_skills: { skill: string; level: number }[];
  element_base_display: number; infusion_bonus_display: number;
  attack_profile: AttackProfilePreset;
  variants: Record<string, GogmaVariant>;
}

export interface ReinforcementOption {
  type: string; tier: string; value: string; label: string; needs_element: boolean; melee_only: boolean; gunner_only: boolean;
}

export interface SkillPool { set_bonuses: string[]; group_skills: string[]; pairs: { set_bonus: string; group_skill: string }[] }

export interface PoolSkill { skill: string; max_level: number }

export interface TalismanPool { primary: PoolSkill[]; secondary: PoolSkill[]; slot_patterns: string[][]; rarities: number[] }

export interface ConditionMeta { key: string; property: string; label: string; group: string; description: string; skills: string[]; default: boolean }

export interface Catalog {
  game_version: string;
  skills: Skill[];
  decorations: Decoration[];
  armor_sets: ArmorSet[];
  charms: Charm[];
  weapon_types: WeaponType[];
  reinforcements: ReinforcementOption[];
  elements: Element[];
  sharpness_colors: SharpnessColor[];
  skill_pool: SkillPool;
  talisman_pool: TalismanPool | null;
  conditions: ConditionMeta[];
  resonance_modes: ResonanceMode[];
  craftable_talisman_count: number;
  default_request: OptimizationRequest;
  conditions_default: Conditions;
  conditions_all_on: Conditions;
  conditions_all_off: Conditions;
  /** logical processors of the server machine (options.max_threads 0 = all of them) */
  processor_count: number;
}

// ---------------------------------------------------------------- request (inputs/<name>.json)

export interface WeaponSpecInput {
  type: string; focus: GogmaFocus; element: Element; infused: boolean; attack_parts: number; reinforcements: string[];
}

export interface WeaponInput {
  spec: WeaponSpecInput | null;
  type: string | null;
  attack: number | null;
  attack_is_display: boolean;
  affinity: number;
  element: Element;
  element_display: number;
  sharpness: SharpnessColor;
  slots: number[];
  set_bonus: string | null;
  group_skill: string | null;
}

export interface Conditions {
  [key: string]: boolean | ResonanceMode | Record<string, number> | AttackProfile | undefined;
  resonance: ResonanceMode;
  skill_limits: Record<string, number>;
  /** count proc damage (Dark Arts shockwave, Bad Blood) in the score */
  proc_damage: boolean;
  attack_profile: AttackProfile;
}

export type OptimizerEngine = 'beam' | 'cp_sat';

export interface OptimizationRequest {
  weapon: WeaponInput;
  skill_pair: { mode: SkillPairMode; top_n: number };
  /** required minimum levels; set bonuses and group skills go here too with the tier as the level (set I = 2 pieces, II = 4, group = 3) */
  target_skills: Record<string, number>;
  conditions: Conditions;
  talismans: { file: string | null; include_craftable: boolean };
  options: {
    allow_transcendence: boolean; top_n: number; min_rarity: number; exclude_sets: string[];
    require_weapon_core_skills: boolean; max_states_per_depth: number;
    /** worker threads for the search; 0 = all processors */
    max_threads: number;
    /** beam state search (fast, approximate) or the exact CP-SAT model; missing in configurations saved before the option existed */
    engine?: OptimizerEngine;
    /** CP-SAT only: time limit per solve (one solve per reported build) */
    cp_sat_time_limit_seconds?: number;
  };
}

export interface TalismanInput { name: string; rarity: number; skills: Record<string, number>; slots: string[] }

// ---------------------------------------------------------------- hand-entered builds (inputs/<name>.builds.json)

/** Decorations by name in slot order, null for an empty slot. */
export interface BuildArmorInput { piece: string; transcended: boolean; decorations: (string | null)[] }

/** One of the configuration's random talismans or a craftable charm, by name. */
export interface BuildTalismanInput { name: string; decorations: (string | null)[] }

/** A build entered by hand; the weapon is the request's, set_bonus / group_skill replace its rolled pair when not null. */
export interface BuildInput {
  name: string;
  set_bonus: string | null;
  group_skill: string | null;
  weapon_decorations: (string | null)[];
  head: BuildArmorInput | null;
  chest: BuildArmorInput | null;
  arms: BuildArmorInput | null;
  waist: BuildArmorInput | null;
  legs: BuildArmorInput | null;
  talisman: BuildTalismanInput | null;
  /** the weapon's chosen build: the one the inventory compares (at most one per weapon) */
  picked?: boolean;
}

/** A requirement checked against a build: levels for skills, pieces for set bonuses and group skills. */
export interface TargetCheck { skill: string; kind: SkillKind; label: string; required: number; actual: number; met: boolean; from_core: boolean }

/** A hand-entered build scored under the request's conditions; build is null when the weapon cannot be resolved. */
export interface EvaluatedBuild { name: string; errors: string[]; warnings: string[]; build: Build | null; targets: TargetCheck[] }

export interface ConfigPayload { request: OptimizationRequest; talismans: TalismanInput[]; builds?: BuildInput[] }

/** A weapon as the client saves it: the request and its hand-entered builds (the talismans belong to the profile). */
export interface WeaponPayload { request: OptimizationRequest; builds: BuildInput[] }

// ---------------------------------------------------------------- profiles (inputs/profiles/<profile>/...)

export interface ProfileSummary { name: string; weapons: number; talismans: number; modified: string }

/** Account-wide data: the talisman pool every weapon draws from and the condition preset of each weapon type. */
export interface Profile { name: string; talismans: TalismanInput[]; condition_presets: Record<string, Conditions> }

/** Result of saving a weapon-type preset: saved weapons of that type followed it except where they override it. */
export interface PresetSaved { profile: Profile; updated_weapons: string[] }

export interface WeaponSummary { name: string; type: string; modified: string; has_results: boolean; summary: string | null }

/** A weapon of a profile: its request and hand-entered builds (inputs/profiles/<profile>/weapons/<name>.json). */
export interface WeaponFile { profile: string; name: string; request: OptimizationRequest; builds: BuildInput[]; has_results: boolean }

/** One weapon of the inventory: its picked build scored now, the best build of its last run, stale = inputs changed since that run. */
export interface InventoryEntry {
  name: string; type: string; modified: string; summary: string; weapon: WeaponStats | null; errors: string[]; targets: ResolvedTarget[];
  builds: number; picked: EvaluatedBuild | null; last_run: { completed_at: string; pair_label: string; build: Build } | null; stale: boolean;
}

// ---------------------------------------------------------------- resolve

export interface WeaponStats {
  type: string; label: string; focus: GogmaFocus | null; true_raw: number; display_attack: number; affinity: number; element: Element;
  element_display: number; element_true: number; sharpness: SharpnessColor | null; sharpness_bar: SharpnessBar | null; sharpness_bonus: number;
  slots: number[]; set_bonus: string | null; group_skill: string | null;
}

/** A requirement as the optimizer sees it: skills by level; set bonuses by tier (level 1 or 2) and pieces; group skills by pieces. */
export interface ResolvedTarget { skill: string; level: number; kind: SkillKind; pieces: number | null; label: string; from_core: boolean }

export interface Resolved {
  is_valid: boolean;
  errors: string[];
  warnings: string[];
  weapon: WeaponStats | null;
  targets: ResolvedTarget[];
  skill_pair_mode: SkillPairMode;
  skill_pair_candidates: number;
  talismans: { total: number; random: number; craftable: number };
  baseline: { attack: number; affinity: number; crit_multiplier: number; efr: number; efe: number; procs: number; total: number; total_all_on: number } | null;
  relevance: { skills: string[]; set_bonuses: string[]; group_skills: string[] } | null;
}

// ---------------------------------------------------------------- results

export interface Deco { name: string; slot: number; kind: SkillKind; icon_color: string | null; skills: SkillGrant[] }

export interface DecoCount extends Deco { count: number }

export interface BuildWeapon {
  type: string; label: string; true_raw: number; display_attack: number; affinity: number; element: Element; element_display: number;
  sharpness: SharpnessColor | null; set_bonus: string | null; group_skill: string | null; slots: number[]; decorations: (Deco | null)[];
}

export interface BuildArmor {
  kind: ArmorPieceKind; id: number; name: string; set: string | null; rarity: number; transcended: boolean; slots: number[];
  decorations: (Deco | null)[]; skills: SkillGrant[]; set_bonus: string[]; group_skill: string | null; defense_max: number;
}

export interface BuildTalisman { name: string; rarity: number; source: 'crafted' | 'random'; skills: SkillGrant[]; slots: string[]; decorations: (Deco | null)[] }

export interface BuildSkill { skill: string; level: number; effective: number; kind: SkillKind; icon: string | null; sources: string[]; wasted: number }

export interface SetBonusState { name: string; pieces: number; tier: number; tier_name: string | null; active: boolean }

export interface GroupSkillState { name: string; pieces: number; rank_name: string | null; active: boolean }

export interface Stats {
  true_raw: number; display_attack: number; affinity: number; crit_multiplier: number; crit_factor: number; sharpness: SharpnessColor | null;
  sharpness_raw: number; sharpness_element: number; element_true: number; element_display: number; element_cap: number; crit_element: number;
  efr: number; efe: number; procs: number; total: number; modifiers: string[];
}

export interface BuildSummary {
  attack: number; display_attack: number; base_attack: number; affinity: number; base_affinity: number; crit_multiplier: number;
  sharpness: SharpnessColor | null; element: Element; element_true: number; element_display: number; efr: number; efe: number; procs: number; total: number;
  total_all_conditions: number; active_set_bonuses: string[]; active_group_skills: string[];
  affinity_sources: string[]; raw_sources: string[]; element_sources: string[]; proc_sources: string[]; depends_on: string[]; description: string;
}

export interface Build {
  rank: number; score: number; efr: number; efe: number; procs: number; summary: BuildSummary; weapon: BuildWeapon; armor: BuildArmor[]; talisman: BuildTalisman | null;
  decorations: DecoCount[]; skills: BuildSkill[]; set_bonuses: SetBonusState[]; group_skills: GroupSkillState[];
  stats_requested: Stats; stats_all_on: Stats; text: string;
}

export interface PairResult {
  rank: number; label: string; set_bonus: string; group_skill: string; best_score: number; states_evaluated: number;
  /** what states_evaluated counts ("final states scored" or "CP-SAT solves"); missing in results saved before the CP-SAT engine */
  work_label?: string; candidate_summary: string; builds: Build[];
}

export interface OptimizationResult {
  completed_at: string; elapsed_seconds: number; skill_pair_mode: SkillPairMode; pairs: PairResult[]; text: string;
  /** fingerprint of the request and talismans of the run; null in results saved before profiles */
  inputs_hash?: string | null;
}

export type OptimizeEvent =
  | { type: 'validation'; errors: string[]; warnings: string[] }
  | { type: 'progress'; message: string }
  | { type: 'result'; result: OptimizationResult }
  | { type: 'error'; message: string };
