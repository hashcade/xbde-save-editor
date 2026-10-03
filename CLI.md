# CLI

Run commands using `dotnet run --project cli/Cli.csproj -- <command>`, or use
the standalone `XbdeEditor.Cli` executable from a platform download.

```sh
XbdeEditor.Cli inspect bfsgame00.sav
XbdeEditor.Cli copy bfsgame00.sav copy.sav
XbdeEditor.Cli resources bfsgame00.sav edited.sav --money 100000 --noponstones 1000
XbdeEditor.Cli character bfsgame00.sav edited.sav 1 --ap 100000 --coins 999
XbdeEditor.Cli character bfsgame00.sav edited.sav 1 --reserve-exp 1000000
XbdeEditor.Cli max-ap bfsgame00.sav edited.sav
```

`inspect` prints JSON containing campaign, resources and joined characters,
including their IDs and Expert Mode records. Character IDs in CLI commands
are the IDs returned by inspection, not the zero-based storage indices.

Money and Noponstones accept unsigned 32-bit integers. AP accepts 0–99,999,999;
reserve EXP accepts 0–199,999,998. Affinity Coins accept 0–999 and are available
only in identified main-story saves. Fields not specified remain unchanged,
including existing amounts above these edit limits. Duplicate options, fractions,
negative values, overflow and edits to absent characters are rejected before
writing. `copy` requires a different output path. Editing commands may explicitly
use the source path as their output; saving uses an atomic file replacement.

No command modifies system saves or thumbnail files.

`max-ap` sets AP to 99,999,999 for every joined, editable character in either
campaign. It does not unlock arts or skills, spend AP, add characters, or change
level/EXP. `--reserve-exp` edits only the Expert Mode reserve pool, not the current
level or EXP. Spend it through the game's Expert Mode menu to raise a level.
