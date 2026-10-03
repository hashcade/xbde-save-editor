# Development plan

Each feature is committed separately with core tests and matching CLI support.

- [x] Independent lossless parser and atomic file saving.
- [x] Inspect/copy CLI and cross-platform CI.
- [ ] Main panel: money and Noponstones.
- [ ] Characters: general records and Expert Mode values.
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
