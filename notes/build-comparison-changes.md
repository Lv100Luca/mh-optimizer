# Build comparison: changes to make

Collected from the user while trying the branch `claude/build-comparison-ux-d98b9e` on http://localhost:5240.

## Done (2026-10-09, on the branch, not merged or pushed)

All twelve, in 39b3a63 (8, 10), c4da6da (2), ecfb80b (3), 32775d1 (5), 0a8d172 + b431b65 (4, 6, 7's limit and scrolling),
58a0186 (1, 7, 9, 11), 4db3a89 (12); 1df4e24 gives `serve.py` a listen backlog (page loads failed now and then).
Before / after: `mockups/build-comparison/changes-before-after.html`. Screenshot steps: `tools/ui-shots/comparison-changes-steps.json`
(needs a profile with a weapon "Pairs" run in optimize mode on a sequence; the session made one in `bin/claude-build/shots-inputs`).

- Saved sequences: `ProfileSettings.Sequences` (per weapon type, in profile.json, merged on import), `IProfileStore.SaveSequences`,
  `AppState.Sequences.cs`. The request carries no name: a sequence shows by the saved one with the same steps (runs keep only
  the step names, matched through `Attacks.StepList`), so renaming renames it everywhere, old runs included.
  `Components/Common/ScoredAttack.razor` is the chip, `InfoHover.razor` the lazy (i).
- Slot icons: cut out of the decoration icons by `tools/cut_slot_icons.py` into `src/MHWildsOptimizer.Web/client/public/icons/ui/`
  (the tracked icon source; the browser app copies it into wwwroot on build). The wiki has no separate slot icons.
- The Landed hits per minute input is gone; a value set earlier still counts and shows with a "Use the attack's pace" link.

2026-10-10, asked by the user ("conditions may vary to steer the build, but a different target will skew the results"):
the Compare tab has a **Target** switch next to the basis (its own / training dummy weak point / the baseline's), kept in
the tray state (`CompareTarget`, `AppState.OnTarget`, a monster part re-read for each column's weapon type), and a warning
of its own whenever the columns hit different targets, on every basis. The target left the condition-difference list and
"Scored on" there compares the attack only. Both switches moved from the card header into a row of their own. Before/after:
`mockups/build-comparison/target-switch-before-after.html`.
Same day: hovering a jewel shows its skill cards (`DecoIcon` builds the tooltip on the first hover, `Card="false"` in the
pickers), Compare's slot labels say they are against the baseline ("other piece than the baseline", "baseline's piece, other
decorations", with what the baseline wears in the tooltip), see `jewel-hover-before-after.html`; and the Inventory can
duplicate a weapon (request, builds and last run, opened right away). Merged into main by the user's request.

## Handoff (state on 2026-10-09)

- Branch `claude/build-comparison-ux-d98b9e`, on top of main `b274ddc`: d1d64ac (attack as true raw, element as in game),
  2cd1757 (Compare tab, results vs baseline, inventory head to head, drafts), 410bf10, 4e47f1b, ca8a00f (fixes),
  af872c5 (mockups + before/after page). Not merged into main and not pushed: main's checkout has uncommitted edits in
  overlapping files (`app.css`, `ReviewPanel.razor`, CLI/Core). Ask before merging; never push without asking (a push to
  main deploys).
- Code map: `Browser/Services/AppState.Compare.cs` (BuildRef lookup and scoring with reference-keyed caches, tray,
  per-weapon baseline), `AppTypes.cs` (`Tab.Compare`, `BuildRef`, `CompareBasis`, `ScoredRef`), `Api/BuildDiff.cs`
  (+ `tests/MHWildsOptimizer.Web.Tests/BuildDiffTests.cs`), `Components/Compare/*` (SlotStrip, SkillDeltas, SwapList,
  TrayBar, TrayToggle, RefSelect), `Panels/ComparePanel.razor`, `ResultsPanel.razor`, `InventoryPanel.razor`,
  `BuildsPanel.razor` (drafts via `BuildInput.DraftOf`, one-piece upgrades), `EquipmentEditor.razor` (side-by-side mode),
  `wwwroot/css/app.css` (comparison sections at the end). Mockups: `mockups/build-comparison/`.
- Build: `dotnet build src/MHWildsOptimizer.Browser -p:BuildSolver=false`. Tests:
  `dotnet test tests/MHWildsOptimizer.Web.Tests -p:BuildClient=false` and
  `dotnet test tests/MHWildsOptimizer.Tests --filter "FullyQualifiedName!~Benchmark"` (all green at ca8a00f).
- Run for the user on **http://localhost:5240** (they asked for that port; kill whatever listens there):
  `dotnet publish src/MHWildsOptimizer.Browser -c Release -p:BuildSolver=false -o bin/claude-build/browser-publish -p:IntermediateOutputPath=<worktree>/bin/claude-build/browser-pub-obj/`,
  copy `src/MHWildsOptimizer.Browser/wwwroot/solver` from the main checkout into `bin/claude-build/browser-publish/wwwroot/`,
  then `python spikes/WasmSpeedTest/serve.py 5240 bin/claude-build/browser-publish/wwwroot` in the background.
  `serve.py` binds with address reuse: make sure only one process listens on the port. Republishing into the same folder
  updates a running server.
- Screenshots (the user wants before/after for UI changes): serve a copy on another port, then
  `ONLY_EXTRA=1 node tools/ui-shots/shots.mjs http://127.0.0.1:<port> <main checkout>/inputs/profiles/Luca <out> Current tools/ui-shots/compare-steps.json`
  (Inventory head to head, Results table / cards / tray, My builds, Compare tab, a draft); `tools/ui-shots/debug.mjs` loads
  one URL with timeouts and prints console errors. Shots import the profile into a throw-away Edge profile.
- Bash heredocs with apostrophes break in this shell: write scripts to a file and run them.

## Open questions (answered by the user on 2026-10-09)

- Item 2: keep the "Landed hits per minute" input on the Conditions tab? **No: remove it** (left unticked).
- Item 4: "Stats" goes right after Damage, "What it takes" right after Equipment.
- Item 5: saved sequences per weapon type in the profile (like condition presets); single moves show as a chip too, its
  (i) gives kind, hits, MV and target.
- Item 11: "Compare with" / "View" on the first pair's heading only; solver details into a tooltip.
- Item 12: disable buttons that make a layout random talismans cannot roll; keep slots in game order (weapon slot first,
  then armor slots highest level first).
- Item 1: the pair ranking's "Best" shows damage, "vs #1" the percent of that.

## To do

1. **Results → Skill pair ranking: "vs #1" as a percentage.** The column shows the absolute score difference to the
   best pair (0.00, -8.13, -16.13, …); show it in percent instead (e.g. 641.52 vs 649.65 → −1.3%).
   Seen in `ResultsPanel.razor`, the "Skill pair ranking" section (optimize mode, several pairs).
   - My observation, not asked for: "Best" there is still the score per 100 MV, while the rest of Results now shows damage.

2. **Drop damage per minute (almost) everywhere.** Unreliable and adds nothing. Where it shows today:
   - `BuildCard.razor`: "· 27,061 per minute" next to the damage in the card header, and the "Damage … per minute" line in
     the stats block of the details.
   - `ResultsPanel.razor`: the "Per minute" column of the results table (baseline row and build rows).
   - `ComparePanel.razor`: the "Per minute" row under Damage.
   - `BuildsPanel.razor`, "Damage per attack" table: the "Per minute" column with its bars; the hint text mentions it too.
     The bars could show damage per attack instead, or go.
   - `ConditionsPanel.razor` line ~215: "… per minute (results show its damage per … and per minute)".
   - `LoadoutReport.cs` (plain-text report / Download .txt): "Damage … per attack, … per minute (… hits per minute)".
   - Keep: the "Landed hits per minute" input on the Conditions tab feeds the cooldown of procs (Bad Blood every 2 s), so it
     changes the score; ask before removing it. The numbers can stay in the DTOs (`DamagePerMinute`); only the UI drops them.

3. **Hits and MV of an attack behind a small (i) hover, everywhere except where the optimization attack / sequence is set.**
   Keep the attack name (and the "scored on" badge); kind, hits and MV go into an (i) with a hover tooltip.
   - `ComparePanel.razor`, `Attacks(...)`: the "move · 2 hits · MV 283" line under each attack name.
   - `BuildsPanel.razor`, "Damage per attack" table: the Hits and MV columns go, into the (i) next to the name.
   - `DamageBreakdown.razor` (the build card's damage breakdown): the "283 MV" next to single hits / each hit row.
   - `AttackDetailView.razor` (a row of the damage table opened): the MV column of the hit list.
   - Not here (stays as it is): the Conditions tab, where the attack or sequence is chosen: attack picker, `SequenceEditor`
     ("… attacks · … MV", each step's MV share), the attack's hit table, the "… hits, … MV" profile summary.
   - Not hits/MV of an attack, so they stay: "score per 100 MV" labels (score, procs).

4. **Compare tab: new row order and content** (`ComparePanel.razor`, `Body(...)`):
   1. **Damage** (damage of the scored attack, every condition on, score; per minute goes with item 2).
   2. **Skills**: each column just lists the skills its build has (chips with levels), no differences.
      Expandable to show the differences (today's "Skills that differ" grid with the deltas against the baseline).
   3. **Set bonuses / group skills**: likewise a plain list per column, expandable to the differences.
   4. **Equipment** (as today: pieces with decorations, changed cells marked).
   5. **Available empty slots** (new): the free decoration slots each build has left, by level, telling weapon and armor
      slots apart (e.g. "armor 2× lv1, 1× lv2 · weapon none"); from the `null` decorations of weapon, armor and talisman.
   6. **Damage per attack** last.
   - Open: "What it takes" (swap / re-gem / gains / losses) and "Stats" (attack, affinity, crit, element, procs, weapon line)
     are not in this list: drop them, or where do they go?

5. **Named, saved sequences; show the name instead of the step list.** Today the scored-on text spells out every step
   ("Sequence: Offset Rising Slash Lv3 + Follow-up Cross Slash Lv3 + … against Custom (raw hitzone 70)"), which fills the
   Compare tab's row header. Instead:
   - On the Conditions tab, a custom sequence can be given a name and saved, and saved sequences can be picked again.
   - Wherever the scored attack is shown, a sequence shows as its name in an outlined chip with an (i), e.g.
     `( Offset TCS Sequence ⓘ )`; hovering the (i) shows the steps (and the target).
   - Where it shows today: the Compare tab's "Damage per …" row header, Results' "Scored on …" line, the inventory head
     to head's verdict, the build card's stats ("Scored on"), the per-attack tables ("Your sequence").
   - Open: where saved sequences live: per weapon type in the profile (like the condition presets; steps are per type),
     or per weapon? And single moves: also a chip (no (i) needed), or plain text?
   - The name has to travel with the run: `StatsDto.ScoredOn` is built in Core/Api and saved with results.

6. **Compare tab: drop the "Procs (per 100 MV)" row.** Proc damage (Dark Arts shockwave, Bad Blood) is already in the
   damage of every attack it applies to, so the separate row adds nothing. `ComparePanel.razor`, Stats section.
   - Not asked: other places that show procs separately stay (the "EFR + EFE + procs = score" breakdown in tooltips and on
     cards of old runs without damage, the "procs +84.8 per 100 MV from …" in a build's description text).

7. **Results: a "Compare all" button.** Instead of ticking ⇄ on every row, one button puts the builds of a skill pair
   (next to the pair heading, e.g. "#1 Gore Magala's Tyranny + Lord's Soul") into the Compare tab and opens it.
   `ResultsPanel.razor`, the `pairhead`.
   - Decided: one button per pair (no extra "best of each pair" button). It adds that pair's builds (and keeps the
     "Compare with" build as the baseline).
   - Decided: the compare limit goes up a lot (`AppState.TrayLimit`, now 5; e.g. 20), and when the columns don't fit the
     Compare tab scrolls sideways: columns keep a sensible minimum width instead of being squeezed (undo the fixed
     equal-width layout from 410bf10 for many columns; keep the row headers visible while scrolling, sticky left).
     The tray bar at the bottom has to cope with many builds too (wrap or scroll).

8. **Bigger collapse / expand icons on cards.** The ▸ / ▾ in front of every card title ("Skill pair ranking", …) is tiny
   and faint. It is the `card-toggle` button of `Section.razor`, styled in `app.css` (`.card-toggle`: 12px font, muted,
   14×20px, squeezed into the header's left padding). Make it larger and easier to see (and hit), across the app.
   - Probably the same for the other small row toggles (▸ on attack rows, results rows, "▸ Skill sources, stats…").

9. **Results: always sort by damage; drop the Sort control.** Remove "Sort: Damage | Fewest changes"
   (`ResultsPanel.razor`, `SortOptions` / `Sorted`). Sort by the damage shown, highest first: after a stale run is
   rescored that can differ from the run's rank order (today "Damage" means the run's order). The "To get there" column
   still shows how many pieces each build changes. (2026-10-13: the summary cards above the results — compared with,
   "N pieces away", best of the run — were dropped: how many pieces change does not matter, and every build already shows
   its gain against the compared build next to the "Compare with" picker.)

10. **Card titles not vertically centered next to the header buttons.** With the Results card collapsed, "Results" sits at
    the top while Run again / Download .txt / Copy are taller, so the title looks too high. `.card-head` uses
    `align-items: flex-start` (and the ▸ toggle is absolutely placed at `top: 15px`). Center the title (and toggle) against
    the actions at least when the card is collapsed or has no hint; check all cards with header buttons.

11. **Results: "Compare with" and "View" out of the Results card, into the pair heading.**
    - The pair heading (`pairhead` in `ResultsPanel.razor`) keeps only the rank and the Gogma weapon pair
      ("#1 Gore Magala's Tyranny + Lord's Soul"). Drop the rest: "best 5,066 damage per sequence · 5 CP-SAT solves ·
      candidates after pruning: Waist 22, … ; cp-sat 5 solve(s): 5 optimal" is flavor text.
    - In the space that frees up, on the right of that heading: "Compare with [select]" and "View [Table | Cards]"
      (Sort is gone with item 9), next to item 7's "Compare all" button.
    - The Results card itself keeps run again / download / copy, the run summary, the log, stale notice and the
      upgrade-path cards.
    - Open: with several skill pairs, the two controls on the first pair's heading only (they apply to all), or a row of
      their own above the pairs? The solver details (solves, candidates) could live on in the search log or a tooltip.

12. **Talisman slots: kind icons on the slot squares, and button groups instead of the dropdown.**
    - Display: every slot square ("2", "1", …) gets the game's small icon in its top-left corner: a helmet for an armor
      slot, a sword for a weapon slot. Seen in the Talismans list's "Slots" column; the squares come from
      `Components/Common/Slots.razor` (talisman notation `Named`: "weapon1", "armor2"), so wherever talisman slots show
      (build cards, equipment editor) they get it too. Icons: the in-game slot icons, from monsterhunterwiki.org like the
      others (see memory `wilds-icon-sources`), into `wwwroot/icons/ui/`.
    - Adding / editing a talisman (`TalismansPanel.razor`, "Decoration slots" `SelectBox` with "a1", "w1 · a1", …): replace
      the dropdown with one row per slot:
      ```
      1: [weapon | armor]  [x | 1 | 2 | 3]
      2: [weapon | armor]  [x | 1 | 2 | 3]
      3: [weapon | armor]  [x | 1 | 2 | 3]
      ```
      (x = no slot; use the sword / helmet icons on the kind buttons.)
    - Check: the dropdown lists only the layouts random talismans can roll (`SlotPatterns` from the data, with
      `FallbackSlotPatterns`); with free buttons, either disable choices that make an impossible layout or warn about one.
      Order the slots like the game (highest first?).
    - Note: the `feature/build-sync` worktree has uncommitted edits to the talisman panel (TalismanPool refactor);
      check before changing `TalismansPanel.razor`.
