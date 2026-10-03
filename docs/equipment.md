# Equipment validation

Characters → Equipment shows one weapon and five armor pieces in a shared
scroll area. Equipment names and gem names currently use English game catalogs;
interface labels support all nine languages.

![Equipment and gem sockets](equipment-zh.png)

## Inventory references

Character equipment fields contain a zero-based inventory index plus an item
type, not a global item ID. A referenced record must exist, match its stored
index and type, and contain exactly one item. Each bank contains 500 records
of `0x30` bytes:

| Slot | Character field | Inventory start | Type |
| --- | --- | --- | --- |
| Weapon | `+0x28` | `0x3B10` | 2 |
| Head | `+0x14` | `0x98D0` | 4 |
| Torso | `+0x18` | `0xF690` | 5 |
| Arms | `+0x1C` | `0x15450` | 6 |
| Legs | `+0x20` | `0x1B210` | 7 |
| Feet | `+0x24` | `0x20FD0` | 8 |

Character records begin at `0x152368`, with stride `0x138`. Appearance fields
are not edited. Unrecognized references remain visible for inspection rather
than being silently replaced.

## Gem fitting

The gem inventory begins at `0x2C380`, with 500 records of `0x2C` bytes.
Normal gems must have matching index/type, quantity one, rank I–VI, one known
effect and a clear cylinder flag. Rank and effect values are never rewritten.
Effect names come from `BTL_skilllist`. Raw effect values can have different
encodings and are not presented as uniformly decoded strengths in the GUI.

Equipment stores its socket count at `+0x15`. Normal references occupy four
bytes at `+0x18`, `+0x20` and `+0x28`; the adjacent four-byte fields hold fixed
gem references. A normal reference uses type 3. Index zero is valid; an empty
normal reference is the entire four-byte value zero. Fixed gems are read-only.

Only declared sockets up to three may be edited. All six equipment banks are
checked for occupied normal gem references, including unequipped items. A gem
cannot be fitted twice. Shared or unrecognized equipment references and an
unconfirmed campaign reject edits. Setters validate the complete request before
changing bytes. Selecting a gem applies it to the in-memory document; File →
Save or Save As writes the file.

## Evidence and limits

Field layouts were checked against the documented
[XCDESave format](https://github.com/hanyu1774/XCDE-Save-File-Editor/blob/master/XCDESave/PartyMember.cs)
and local main-story/Future Connected saves. Names were independently collected
from the game-data [item](https://xenoblade.github.io/xb1de/bdat/bdat_common/ITM_itemlist.html)
and [skill](https://xenoblade.github.io/xb1de/bdat/bdat_common/BTL_skilllist.html) tables.
No upstream parser code is included.

Core tests cover both campaigns, index-zero gems, fixed sockets, empty sockets,
duplicate use, cylinders, malformed references, invalid quantities and atomic
rejection. Real-save tests change copies in memory and require every byte outside
the intended normal reference to remain identical. CLI and GUI tests exercise
selection/removal, saving and invalid operations. GUI tests also check nine
language switches and page spacing at normal and reduced window sizes.

Weapon/armor selection, appearance editing, gem creation and effect/rank editing
are not implemented. In-game loading still needs user verification; lossless
round trips and isolated byte changes are not proof of every gameplay rule.
