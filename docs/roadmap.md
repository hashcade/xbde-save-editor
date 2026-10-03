# Development plan

Each feature is committed separately with core tests and matching CLI support.

- [x] Independent lossless parser and atomic file saving.
- [x] Inspect/copy CLI and cross-platform CI.
- [x] Main panel: money and Noponstones, nine UI languages and shared save actions.
- [x] Characters: AP/Affinity Coin editing and general/Expert Mode inspection.
- [x] Characters: verified AP cap, single/bulk AP maximum and reserve EXP editing.
- [ ] Characters: validated level/experience and Expert Mode editing.
- [ ] Arts and skills: limits, unlock state and learned progress.
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
the level/EXP operations that still remain read-only.
