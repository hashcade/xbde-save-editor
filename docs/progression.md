# Character progression validation

## Editable fields

| Field | Offset in character record | Edit range |
| --- | --- | --- |
| Current level | `0x00` | Character/campaign introduction level–99 |
| Current EXP | `0x04` | 0–99,999,999, normalized through level costs |
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

## Level and EXP linkage

The native save writer at `0xA6DD0–0xA6FB8` and reader at
`0xA7198–0xA7214` confirm the runtime-to-save mapping:
current level `runtime+0x04 → save+0x00`, current EXP
`runtime+0x18 → save+0x04`, highest attained level
`runtime+0x08 → save+0xEC`, its EXP accumulator
`runtime+0x1C → save+0xF0`, reserve `runtime+0x20 → save+0xF4`.

`0xBA2A0` consumes each per-level `BTL_growlist.level_exp` cost, leaving
the remainder as current EXP. Level 99 retains remaining EXP. Selecting a
different level in the editor resets current EXP to zero unless explicitly
supplied. EXP edits normalize from the selected/current level; 100 EXP at
level 1 becomes level 3 with zero remaining EXP.

`0x8B7B0–0x8B834` preserves the greater of the previous highest level and
the new level. Its rebuild (`0x8AC40`) resets the highest-level EXP
accumulator. Direct edits follow that bookkeeping without granting Affinity
Coins or changing AP, reserve EXP, arts, skill unlocks or recruitment.
Loading recalculates runtime stats from the saved level.

The highest-level record is displayed read-only, not as an independently
editable second level. Original high EXP is not normalized on load or on
unrelated resource edits. Campaign-ambiguous saves and unsupported guest
records do not permit progression edits.

The game's Expert Mode tutorial describes reserving non-battle EXP and returning
EXP to the pool when lowering levels. The local executable also confirms that
level changes consume or return individual `BTL_growlist.level_exp` entries.
Direct editor level changes do not simulate spending/refunding reserve EXP or
toggle Expert Mode. Those game-menu operations remain outside this feature.

References:

- [Chinese Expert Mode menu screenshot](https://img1.gamersky.com/image2020/06/20200607_syj_380_2/image003.jpg),
  reproduced in this [guide](https://www.gamersky.com/handbook/202006/1294512.shtml).
  The Traditional Chinese heading matches the game menu; the Simplified Chinese
  heading uses the corresponding simplified characters.
- [Expert Mode tutorial](https://xenobladedata.github.io/xb1de/bdat/bdat_menu_ttrl/MNU_ttrl_page.html), entry 12.
- [Level growth data](https://xenoblade.github.io/xb1de/bdat/bdat_common/BTL_growlist.html).
- [Character introduction levels](https://xenoblade.github.io/xb1de/bdat/bdat_common/BTL_pclist.html), `lv` / `melia_lv`.

Reserve edits change only four bytes at `0xF4`. They do not automatically raise a
level or change whether Expert Mode is enabled. Use the game's Expert Mode menu
to spend the reserve. Synthetic boundary tests and byte-diff tests against both
campaigns verify field isolation; in-game testing of an edited copy is still needed.
