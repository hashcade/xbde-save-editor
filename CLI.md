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
XbdeEditor.Cli progression bfsgame00.sav edited.sav 1 --level 50
XbdeEditor.Cli progression bfsgame00.sav edited.sav 1 --level 1 --exp 101
XbdeEditor.Cli arts bfsgame00.sav 1
XbdeEditor.Cli art bfsgame00.sav edited.sav 1 12 --level 10
XbdeEditor.Cli max-art bfsgame00.sav edited.sav 1 12
XbdeEditor.Cli max-arts bfsgame00.sav edited.sav 1
XbdeEditor.Cli skills bfsgame00.sav 1
XbdeEditor.Cli skill-tree bfsgame00.sav edited.sav 1 1 --learned 3
XbdeEditor.Cli skill-tree bfsgame00.sav edited.sav 1 1 --sp 500
XbdeEditor.Cli max-skill-tree bfsgame00.sav edited.sav 1 1
XbdeEditor.Cli max-skills bfsgame00.sav edited.sav 1
XbdeEditor.Cli max-all-skills bfsgame00.sav edited.sav
XbdeEditor.Cli equipment bfsgame00.sav 1
XbdeEditor.Cli equipment-gem bfsgame00.sav edited.sav 1 Weapon 1 3
XbdeEditor.Cli equipment-gem bfsgame00.sav edited.sav 1 Weapon 1 none
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

`progression` selects a character's level and/or edits current EXP. A changed
level defaults to zero current EXP. Supplied EXP is consumed through the game's
per-level thresholds: level 1 with 101 EXP becomes level 3 with 1 EXP. Levels
cannot fall below the character's campaign-specific introduction level or exceed
99; EXP accepts 0–99,999,999. A level change preserves the highest attained level
and resets its EXP accumulator. It leaves reserve EXP, AP, coins and unlocks alone.
Unconfirmed campaigns and unsupported guests reject progression editing.

`max-ap` sets AP to 99,999,999 for every joined, editable character in either
campaign. It does not unlock arts or skills, spend AP, add characters, or change
level/EXP. `--reserve-exp` edits only the Expert Mode reserve pool, not the current
level or EXP. Spend it through the game's Expert Mode menu to raise a level.

`arts` lists a character's art IDs, levels, learned state and applicable maxima
as JSON. `art --level` sets a learned art's level; `max-art` maximizes one art;
`max-arts` maximizes all learned, upgradeable arts for that character, independent
of GUI search filters. Ordinary arts grant the required manual permission without
spending AP. Monado arts retain their story permission, and missing/talent arts
are never upgraded. Melia's discharge levels follow their summon arts. See the
[validated limits](docs/arts.md); art IDs are returned by `arts`, not list positions.

`skills` lists five trees and their nodes, learned counts, remaining SP and unlock
state as JSON. Tree indices are one-based, as returned by that command. Selecting
`--learned` resets the tree's remaining SP to zero. Each tree contains five skills;
the first tree's free initial skill cannot be removed. `--sp` must be below the
next skill's learning cost and is unavailable once all five skills are learned.

Maximum commands learn all five skills and reset remaining SP. Character-wide
and party-wide operations skip locked hidden trees and do not alter task unlocks
or skill links. Future Connected and unconfirmed campaigns reject skill edits.
Existing unusual values remain intact unless explicitly normalized. See
[skill validation](docs/skills.md).

`equipment` lists the current weapon and armor references, socket contents and
available normal gems as JSON. Equipment slots use `Weapon`, `Head`, `Torso`,
`Arms`, `Legs` or `Feet`; sockets are one-based. Gem indices are zero-based
inventory references returned in `AvailableGems`, not effect IDs. Gem index zero
is valid; use `none` to empty a normal socket. The JSON gem `Value` is the raw
stored effect value, not a uniformly decoded percentage or strength.

`equipment-gem` changes only one existing socket. It rejects fixed gems,
cylinders, missing/invalid gems, already-fitted gems, unsupported references and
out-of-range indices. Ownership checks include unequipped inventory equipment.
It does not create gems or change equipment, appearance, rank or effect strength.
See [equipment validation](docs/equipment.md).
