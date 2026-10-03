# Development plan

Each feature is committed separately with core tests and matching CLI support.

- [x] Independent lossless parser and atomic file saving.
- [x] Inspect/copy CLI and cross-platform CI.
- [x] Main panel: money and Noponstones, nine UI languages and shared save actions.
- [x] Characters: AP/Affinity Coin editing and general/Expert Mode inspection.
- [x] Characters: verified AP cap, single/bulk AP maximum and reserve EXP editing.
- [x] Characters: linked current level/EXP editing with verified highest-level bookkeeping.
- [x] Arts: verified levels, single/character-wide maximum and CLI support.
- [x] Arts: ordinary level-based learning and single/character-wide/global learning and maximum, with protected story/quest arts and matching CLI support.
- [ ] Arts: validated story/quest unlock editing, only where linked progression can be preserved.
- [x] Skills: inspect hidden-tree unlocks, edit learned count/remaining SP and maximize unlocked trees, with CLI support.
- [x] Skills: single hidden-branch unlocking and selected-character/party-wide unlock-and-learn operations, with native shared-Fiora flags, format protections and matching CLI support.
- [ ] Skills: in-game verification of newly unlocked hidden branches.
- [x] Skills: source-group skill-link editing with verified shapes, saved unlock indices, learned-skill, story-availability and coin-budget validation, plus matching CLI support.
- [x] Equipment: resolve equipped inventory items, inspect fixed gems and fit/remove owned normal gems, with CLI support.
- [x] Equipment: validated owned weapon/armor switching with character and armor-skill restrictions, preserving gems and protecting story weapons.
- [x] Items: normal gem effect/rank/strength editing, decoded labels, validated ranges and CLI support.
- [x] Items: normal gem creation/deletion and collectable/material/manual quantity, favorite, add/delete and bulk maximum editing, with CLI support.
- [x] Items: weapon/armor inventory creation, per-character/per-bank missing-equipment filling, favorite flags and protected deletion, with matching CLI support.
- [x] Achievements: validated main-story completion bits/counters, single/bulk unlock, filters and CLI support.
- [ ] Achievements: in-game verification of newly completed records.
- [x] Character affinity: single/bulk maximum, direct points editing and monotonic skill-link slot synchronization, with matching CLI support.
- [x] Region affinity: linked points/stars for all five main-story areas, single/bulk maximum and matching CLI support.
- [x] Colony 6: inspect facilities/development/population and maximize started reconstruction with native linked writes, all-or-nothing validation and matching CLI support.
- [x] Collectopaedia: verified campaign-specific item/reward catalogs and read-only registration inspection, with CLI support.
- [x] Collectopaedia: native category/page reward mapping, gem reward metadata and duplicate-safe read-only completion previews, with CLI support.
- [x] Collectopaedia: single/page/campaign completion with original rewards, linked achievement records, atomic inventory allocation and matching GUI/CLI operations; character EXP and collectable stocks are preserved.
- [ ] Collectopaedia: in-game verification of edited registrations and reward visibility.

Prioritize grind reduction, bulk edits and actions that are difficult or unavailable
in-game. Basic party sorting is intentionally excluded; it adds little value to
a save editor and does not justify another panel. Cosmetic appearance changes and ordinary menu settings, including
the Expert Mode enabled switch, are also excluded. Direct level and reserve EXP
editing remain available for progression changes without grinding.

## Remaining priorities

1. Reduce remaining NPC affinity and Colony 6 recruitment grinding where all linked state can be validated.
2. Assess protected Art unlocks only where they remove a concrete progression obstacle and preserve linked quest state.
3. Assess Time Attack rewards and Ponspectors against the same usefulness and write-safety criteria before committing to implementation. An unverified or low-value candidate is deferred, not added merely to expand feature coverage.

NPC affinity, quests, Colony 6 initial quest/recruitment, Time Attack, Ponspectors and system-save
unlocks need further format validation. They are not promised or exposed as editable fields.
Crystals/cylinders and story-weapon switching are not completion requirements; implement them only if they solve a concrete progression or grinding problem safely.

## Deferred game-data localization

Localized item, equipment, Art, Skill and achievement catalogs are skipped until genuine game text is available.
Keep the existing UI languages. Do not synthesize official translations, spend time reconstructing missing language packs or block verified features on missing text.

## Verification

Synthetic saves are generated in tests. Real game saves and executable files
are never committed. Run the local round-trip checks with:

```sh
dotnet run --project test/Core.Test.csproj -- --save-directory /path/to/saves
```

Round-trip and byte-diff tests do not replace loading an edited copy in-game.

See [progression validation](progression.md) for the verified resource limits and
the verified level/EXP linkage and reserve pool edits.
See [Arts validation](arts.md) for campaign limits, manual flags and linked records.
See [Skills validation](skills.md) for branch flags, learning costs and write boundaries.
See [Equipment validation](equipment.md) for inventory references, gem ownership and write boundaries.
See [Gem validation](gems.md) for effect limits, encoding and inventory editing.
See [Inventory validation](inventory.md) for stack limits, safe allocation and serial counters.
See [Achievement validation](achievements.md) for condition types, completion flags and write boundaries.
See [Affinity validation](affinity.md) for canonical pairs, point limits and directed skill-link unlocks.
See [Region affinity validation](region-affinity.md) for star thresholds, area IDs and isolated point writes.
See [Colony 6 validation](colony6.md) for native upgrade rows, linked flags and reconstruction write boundaries.
