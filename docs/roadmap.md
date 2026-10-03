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
- [ ] Arts: localized game names and explicit unlock editing.
- [ ] Skills: unlock state and learned progress.
- [ ] Equipment: inventory references, gems and appearance.
- [ ] Items: localized catalogs and validated editing.
- [ ] Party: reorder existing members without altering recruitment.

Affinity, quests, achievements, Colony 6, Time Attack, Ponspectors and system-save
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
