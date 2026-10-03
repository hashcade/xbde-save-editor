# CLI

## Collectopaedia inspection

```sh
XbdeEditor.Cli collectopaedia bfsgame00.sav
XbdeEditor.Cli collectopaedia bfsmeria00.sav
```

Reports registered totals and per-map entries for the identified campaign:
300 main-story entries or 28 Future Connected entries. Placeholder rows are
excluded. Registration is read from saved flags, not inventory ownership.
Unverified formats and unidentified campaigns return `Supported: false` with
an empty catalog. This command is read-only. See [validation](docs/collectopaedia.md).

```sh
XbdeEditor.Cli collectopaedia-plan bfsgame00.sav
XbdeEditor.Cli collectopaedia-plan bfsgame00.sav --page 2
XbdeEditor.Cli collectopaedia-plan bfsmeria00.sav --entry 319
```

These read-only previews report missing registrations and rewards for newly
completed categories/pages. With no option, the preview covers the identified
campaign; `--page` uses a map ID from inspection, and `--entry` uses an entry ID.
Already-completed categories/pages do not award rewards again. The JSON includes
native reward item IDs and gem effect/rank/fixed-strength data. Zero fixed
strength denotes a native random range, not a maximum crafted gem. Invalid IDs,
other campaigns and unverified formats reject previewing. Previews do not check
inventory capacity, allocate rewards, change registrations or save files.

```sh
XbdeEditor.Cli complete-collectopaedia bfsgame00.sav edited.sav
XbdeEditor.Cli complete-collectopaedia bfsgame00.sav edited.sav --page 2
XbdeEditor.Cli complete-collectopaedia bfsmeria00.sav edited.sav --entry 319
```

Completion registers missing entries without requiring or consuming owned
collectables. It adds the original equipment/gem rewards for categories/pages
newly completed by this edit, never rewards for previously completed groups.
Main-story collection achievements are synchronized without adding EXP or
changing character levels, matching the standalone achievement editor. Future
Connected does not change main-story achievement records. Existing items,
equipment references, quests and other flags remain unchanged. Any bank capacity
or acquisition-serial failure rejects the entire batch before saving, including
when earlier rewards would fit. Repeating a completed selection is byte-identical.

## Colony 6

```sh
XbdeEditor.Cli colony6 bfsgame00.sav
XbdeEditor.Cli max-colony6 bfsgame00.sav edited.sav
```

Inspection reports the four facility levels, overall reconstruction level,
development, population and whether reconstruction can be maximized. Other
campaigns return `null`. Maximum applies only missing native upgrade rows,
including their linked effect/self/quest flags and development/population
increases. It leaves materials, money, recruitment, EXP and achievements alone.
At least one facility must already have an upgrade; the command does not start
the reconstruction quest. Invalid levels, counter overflow, unsupported linked
states and unverified formats reject the entire operation before saving.
Already-maximized colonies remain byte-identical. See [validation](docs/colony6.md).

## Achievements

```sh
XbdeEditor.Cli achievements bfsgame00.sav
XbdeEditor.Cli unlock-achievement bfsgame00.sav edited.sav 7
XbdeEditor.Cli unlock-all-achievements bfsgame00.sav edited.sav
```

Inspection reports support status, flags, conditions, rewards and cumulative
counters. Unlocking requires a confirmed main-story save with format version 7,
and does not add party EXP or change quests. ID 7 is the 5,000-enemy achievement.

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
XbdeEditor.Cli skill-links bfsgame00.sav 1
XbdeEditor.Cli skill-link bfsgame00.sav edited.sav 1 2 1 37
XbdeEditor.Cli skill-link bfsgame00.sav edited.sav 1 2 1 none
XbdeEditor.Cli equipment bfsgame00.sav 1
XbdeEditor.Cli equip bfsgame00.sav edited.sav 2 Weapon 7
XbdeEditor.Cli equipment-gem bfsgame00.sav edited.sav 1 Weapon 1 3
XbdeEditor.Cli equipment-gem bfsgame00.sav edited.sav 1 Weapon 1 none
XbdeEditor.Cli equipment-inventory bfsgame00.sav Weapon
XbdeEditor.Cli add-equipment bfsgame00.sav edited.sav 2
XbdeEditor.Cli fill-missing-equipment bfsgame00.sav edited.sav Weapon 2
XbdeEditor.Cli equipment-favorite bfsgame00.sav edited.sav Weapon 0 true
XbdeEditor.Cli delete-equipment bfsgame00.sav edited.sav Weapon 0
XbdeEditor.Cli gems bfsgame00.sav
XbdeEditor.Cli gem bfsgame00.sav edited.sav 3 --effect 26 --rank 6 --value 200
XbdeEditor.Cli max-gem bfsgame00.sav edited.sav 3
XbdeEditor.Cli add-gem bfsgame00.sav edited.sav --effect 26 --rank 6 --value 200
XbdeEditor.Cli delete-gem bfsgame00.sav edited.sav 3
XbdeEditor.Cli inventory bfsgame00.sav Materials
XbdeEditor.Cli add-item bfsgame00.sav edited.sav 1852 --quantity 5
XbdeEditor.Cli item bfsgame00.sav edited.sav Materials 0 --quantity 99
XbdeEditor.Cli max-items bfsgame00.sav edited.sav Materials
XbdeEditor.Cli delete-item bfsgame00.sav edited.sav Materials 0
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

`skill-links` lists the selected character's available source groups, slot shapes,
saved unlock indices, assignments, eligible skills and used/owned Affinity Coins.
`skill-link` assigns a global skill ID to a one-based slot, or removes its link
with `none`. The source must be joined and available at the saved story stage;
the slot must already be unlocked. Skills must be learned, match the slot shape,
and not duplicate another link from that source. Replacements reuse the previous
link's coin budget; coins are reserved, not spent. Existing overspent links can be
replaced without increasing their total cost. Unknown assignments remain unchanged
on inspection and can be explicitly removed. These commands do not raise affinity,
unlock slots, learn skills, or modify unrelated link bytes. Future Connected,
unconfirmed campaigns and unverified save versions are protected.

`equipment` lists the current weapon and armor references, socket contents and
available items and normal gems as JSON. Equipment slots use `Weapon`, `Head`, `Torso`,
`Arms`, `Legs` or `Feet`; sockets are one-based. Gem indices are zero-based
inventory references returned in `AvailableGems`, not effect IDs. Gem index zero
is valid; use `none` to empty a normal socket. The equipment JSON gem `Value` is
raw storage; `Strength`, `Chance` and `Label` are decoded values.

`equipment-gem` changes only one existing socket. It rejects fixed gems,
cylinders, missing/invalid gems, already-fitted gems, unsupported references and
out-of-range indices. Ownership checks include unequipped inventory equipment.
It does not create gems or change equipment, appearance, rank or effect strength.
See [equipment validation](docs/equipment.md).

`equip` switches one equipment reference to an index returned in `AvailableItems`.
Indices are zero-based and specific to that slot's inventory bank, not global
item IDs. Selection requires an owned, compatible item not equipped by another
joined character. Main-story armor permissions account for learned skills and
existing links; story-flagged weapons remain protected. The target equipment and
all of its gems stay byte-identical. No command creates equipment or changes its
appearance. `CanSwitch: false` means the current item cannot safely be switched.

`gems` lists owned normal gems as JSON with zero-based inventory indices,
effect IDs, rank, decoded strength/chance, editability and the selected rank's
minimum/maximum. Cylinders are excluded. `gem` changes the specified fields;
omitted fields retain their current values, so changing rank alone can reject a
strength outside the new range. `--value` is actual strength, not the packed raw
value. Activation chance follows the effect/rank table and is not independently
editable. `max-gem` maximizes the current effect/rank, without raising rank.

Edits reject invalid records, unused effects, invalid ranks, incompatible fitted
effects and strengths outside the game-data range. Equipment ownership includes
unequipped items. `add-gem` creates a normal gem in a free, unreferenced slot;
all three options are required. `delete-gem` marks an unfitted normal gem absent
without reindexing any other gem. Cylinders are not edited by these commands.
See [gem validation](docs/gems.md).

`inventory` accepts `Collectables`, `Materials`, `ArtManuals` or `KeyItems` and
returns existing records plus a catalog. Use catalog IDs with `add-item`, but
zero-based inventory indices with `item` and `delete-item`. Quantities accept
whole numbers from 1 to 99. Adding an existing item increases its stack without
exceeding 99; duplicate stacks reject that operation. `max-items` maximizes all
existing editable stacks in the selected category, regardless of GUI filters.
Key items remain read-only. New records update acquisition serial counters;
deleted records do not shift indices. See [inventory validation](docs/inventory.md).

## Equipment inventory

`equipment-inventory` lists the specified 500-slot bank and its creation catalog.
Use global catalog IDs with `add-equipment`, but zero-based inventory indices
with `equipment-favorite` and `delete-equipment`. Equipment is never stacked;
each addition creates one independent record with the original socket count and
built-in gems, without equipping it or consuming a normal gem.

`fill-missing-equipment` creates one copy of each missing ordinary definition for
the joined character in the specified bank. It does not grant armor skills or
promise that every added item can be worn immediately. Existing records and
character references remain untouched. Insufficient space or serial overflow
rejects the whole batch before writing. Repeating the command adds nothing.
Dummy and story-protected weapons are excluded. Deletion rejects malformed or
referenced records, including inactive-character references, and never shifts
indices. Both campaigns require format version 7; unconfirmed campaigns reject
mutations. See [equipment validation](docs/equipment.md).

## Character affinity

```sh
xbde-save-editor affinities input.sav
xbde-save-editor affinity input.sav output.sav 1 2 --points 3000
xbde-save-editor max-affinity input.sav output.sav 1 8
xbde-save-editor max-all-affinity input.sav output.sav
```

`affinities` lists joined canonical pairs, points, directed slot counts and
editability as JSON. Character IDs are the joined IDs shown by `inspect`.
Fiora's IDs 3/8 share one pair; self/alternate-form pairs are rejected.
Points accept 0–5,000. Raising points also raises the saved skill-link slot
indices in both directions, including Fiora's inactive alias; lowering points
never relocks them. `max-all-affinity` targets all joined pairs, not a subset.
Future Connected, ambiguous campaigns and unverified formats reject writes.
No command changes coins, equipped links, world affinity or achievement state.
See [affinity validation](docs/affinity.md).

## Region affinity

```sh
XbdeEditor.Cli region-affinities input.sav
XbdeEditor.Cli region-affinity input.sav output.sav 3 --points 10000
XbdeEditor.Cli region-affinity input.sav output.sav 3 --stars 5
XbdeEditor.Cli max-region-affinity input.sav output.sav 3
XbdeEditor.Cli max-all-region-affinity input.sav output.sav
```

| Region ID | Area |
| --- | --- |
| 1 | Colony 9 Area |
| 2 | Colony 6 Area |
| 3 | Central Bionis |
| 4 | Upper Bionis |
| 5 | Hidden Village |

Points accept 0–10,000. Stars accept 1–5: changing a rating sets its minimum
points (0, 2,000, 4,000, 6,000 or 8,000). Selecting the current rating preserves
points within that tier. Maximum commands set 10,000, not just the five-star
minimum. The inspection command returns points, stars and editability as JSON.
Only identified main-story saves with format version 7 permit writes. Region
edits leave character affinity, NPC relationships, quests and achievements alone.
See [region validation](docs/region-affinity.md).
