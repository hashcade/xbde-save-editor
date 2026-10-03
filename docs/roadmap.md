# Development plan

Each feature is committed separately with core tests and matching CLI support.

- [x] Independent lossless parser and atomic file saving.
- [x] Inspect/copy CLI and cross-platform CI.
- [x] Main panel: money and Noponstones, nine UI languages and shared save actions.
- [x] Characters: AP/Affinity Coin editing and general/Expert Mode inspection.
- [x] Characters: verified AP cap, single/bulk AP maximum and reserve EXP editing.
- [x] Characters: linked current level/EXP editing with verified highest-level bookkeeping.
- [ ] Expert Mode: simulate spending/refunding reserve EXP and edit its enabled state.
- [x] Arts: verified levels, single/character-wide maximum and CLI support.
- [x] Arts: ordinary level-based learning and single/character-wide/global learning and maximum, with protected story/quest arts and matching CLI support.
- [ ] Arts: localized game names and validated story/quest unlock editing.
- [x] Skills: inspect hidden-tree unlocks, edit learned count/remaining SP and maximize unlocked trees, with CLI support.
- [ ] Skills: localized game names and skill-link editing.
- [x] Equipment: resolve equipped inventory items, inspect fixed gems and fit/remove owned normal gems, with CLI support.
- [x] Equipment: validated owned weapon/armor switching with character and armor-skill restrictions, preserving gems and protecting story weapons.
- [ ] Equipment: story-weapon switching, appearance and localized game names.
- [x] Items: normal gem effect/rank/strength editing, decoded labels, validated ranges and CLI support.
- [x] Items: normal gem creation/deletion and collectable/material/manual quantity, favorite, add/delete and bulk maximum editing, with CLI support.
- [ ] Items: weapon/armor inventory creation, crystals/cylinders and localized game catalogs.
- [x] Achievements: validated main-story completion bits/counters, single/bulk unlock, filters and CLI support.
- [ ] Achievements: localized game text and in-game verification of newly completed records.

Prioritize grind reduction, bulk edits and actions that are difficult or unavailable
in-game. Basic party sorting is intentionally excluded; it adds little value to
a save editor and does not justify another panel.

Affinity, quests, Colony 6, Time Attack, Ponspectors and system-save
unlocks need further format validation. They are not promised or exposed as editable fields.

## Verification

Synthetic saves are generated in tests. Real game saves and executable files
are never committed. Run the local round-trip checks with:

```sh
dotnet run --project test/Core.Test.csproj -- --save-directory /path/to/saves
```

Round-trip and byte-diff tests do not replace loading an edited copy in-game.

See [progression validation](progression.md) for the verified resource limits and
the verified level/EXP linkage and the remaining Expert Mode operations.
See [Arts validation](arts.md) for campaign limits, manual flags and linked records.
See [Skills validation](skills.md) for branch flags, learning costs and write boundaries.
See [Equipment validation](equipment.md) for inventory references, gem ownership and write boundaries.
See [Gem validation](gems.md) for effect limits, encoding and inventory editing.
See [Inventory validation](inventory.md) for stack limits, safe allocation and serial counters.
See [Achievement validation](achievements.md) for condition types, completion flags and write boundaries.
