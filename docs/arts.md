# Arts validation

The 188 saved art records occupy `0x1536E8–0x15385F`, two bytes each, indexed
by art ID minus one. The first byte is the learned level (zero means unlearned).
The second byte contains cumulative manual flags: `1`, `3`, `7` permit levels
7, 10, 12 respectively; no flag permits level 4. Unrelated flag bits are preserved.

## Limits and behavior

- Main-story arts with Master books support levels 1–12.
- Early Fiora's four ordinary arts have no Master books and stop at 10.
- Future Connected ordinary arts stop at 10; its characters have no Master books.
- Monado arts stop at 10 and also respect the saved story permission. Editing
  their level does not change that permission. Enchant is not editable in Future Connected.
- Talent arts have fixed levels and are inspectable, not editable.
- Melia's six discharge records derive their levels from the corresponding
  summon arts. Editing a summon synchronizes its discharge level, preserving
  the discharge's flags. Discharge records are not separate editing options.
- Only joined, supported characters and learned, upgradeable arts can be edited.
  Bulk maximum does not unlock missing arts, add party members, spend AP, change
  equipment/palettes or alter story progress. Raising an ordinary art grants only
  the manual permission required for its selected level; lowering it retains manuals.
- Existing invalid/high levels are preserved on loading or unrelated edits.
  Explicit maximum operations replace them with the applicable maximum.
- Unconfirmed campaigns do not permit art editing.

The catalog covers both Fiora records, Shulk, Reyn, Dunban, Sharla, Riki, Melia,
Kino and Nene. Temporary guests and Ponspector actions are not editable. UI labels
and states support all nine interface languages. Art names currently use the
verified English table; other game-language text has not yet been supplied.

## Evidence

The field layout is cross-checked against published
[format documentation](https://github.com/hanyu1774/XCDE-Save-File-Editor/blob/master/XCDESave/XCDESaveData.cs)
and independently inspected native code from the user's local executable,
build ID `7E1DF8E08D60544BBDCA1E333C153C97`:

- `0x75E90–0x75F5C`: two-byte record initialization, learned/level accessors,
  manual masks and permission checks.
- `0x1926D0–0x1928CC`: level access/increment and permission tier mapping
  to 4/7/10/12. The debug override at `0x1BCE20` returns false in this build.
- `0x47D6A0–0x47D7C0`: Melia's summon-to-discharge level copies:
  103→97, 104→98, 105→99, 111→100, 112→101, 113→102.

Catalog IDs, ownership, talent flags and menu order come from the extracted
[pc_arts table](https://xenoblade.github.io/xb1de/bdat/bdat_common/pc_arts.html).
Master-book availability is cross-checked against `memory_type=4` in
[ITM_artslist](https://xenoblade.github.io/xb1de/bdat/bdat_common/ITM_artslist.html).
Only compact factual catalog fields are included; no executable, save file,
upstream source code or art descriptions are distributed.

Core, GUI and CLI tests cover both campaigns, manual thresholds, fixed and
unlearned arts, Monado limits, both Fiora records, Kino/Nene, all six discharge
links, field isolation and rejected edits. Real-save checks edit copies in memory
and verify that all bytes outside the art table remain unchanged. They do not
substitute for loading an edited copy in-game.
