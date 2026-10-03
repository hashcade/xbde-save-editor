# Character progression validation

## Editable fields

| Field | Offset in character record | Edit range |
| --- | --- | --- |
| AP | `0x08` | 0–99,999,999 |
| Affinity Coins | `0x0C` | 0–999, identified main story only |
| Expert Mode reserve EXP | `0xF4` | 0–199,999,998 |

Offsets are cross-referenced against the published
[XCDESave format documentation](https://github.com/hanyu1774/XCDE-Save-File-Editor/blob/master/XCDESave/PartyMember.cs).
The parser and edit operations here are independently implemented.

The caps were checked in the user's local Definitive Edition executable,
build ID `7E1DF8E08D60544BBDCA1E333C153C97` (NSO virtual addresses):

- `0xBAA30–0xBAA44`: AP gain is clamped to 99,999,999.
- `0xBA9D0–0xBA9F0`: reserve EXP gain is clamped to 199,999,998.
- `0xBAEF8–0xBAF10`: returning EXP to the reserve pool uses the same cap.
- `0xBAAD0–0xBAAE0`: Affinity Coins are clamped to 999.

Existing amounts above these caps are displayed and preserved when unrelated
fields are edited. A new value above the cap is rejected before any fields are
written. Bulk AP operates on joined records, never unused character slots.

## Still read-only

The saved level, accumulated EXP, Expert Mode level and current EXP are not yet
editable. The game has separate current, accumulated and reserve pools; assigning
one EXP value to all of them would not reproduce its level-changing behavior.

The game's Expert Mode tutorial describes reserving non-battle EXP and returning
EXP to the pool when lowering levels. The local executable also confirms that
level changes consume or return individual `BTL_growlist.level_exp` entries.
The remaining level bookkeeping must be checked before exposing direct level edits.

References:

- [Expert Mode tutorial](https://xenobladedata.github.io/xb1de/bdat/bdat_menu_ttrl/MNU_ttrl_page.html), entry 12.
- [Level growth data](https://xenoblade.github.io/xb1de/bdat/bdat_common/BTL_growlist.html).

Reserve edits change only four bytes at `0xF4`. They do not automatically raise a
level or change whether Expert Mode is enabled. Use the game's Expert Mode menu
to spend the reserve. Synthetic boundary tests and byte-diff tests against both
campaigns verify field isolation; in-game testing of an edited copy is still needed.
