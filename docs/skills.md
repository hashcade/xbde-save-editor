# Skill tree validation

![Skills panel with Chinese interface](skills-zh.png)

Characters → Skills displays all five branches as cards. The learned count and
remaining SP are separate save fields. Choosing a count resets remaining SP;
maximum operations write five learned skills and zero remaining SP. No operation
selects a different active tree, changes skill links or grants hidden-tree task
unlocks. Party-wide maximum applies to joined characters, not search results.

The first branch contains an innate zero-cost skill, so its count accepts 1–5.
Other branches accept 0–5. Remaining SP must be below the next skill's cost and
cannot be edited on a completed branch. Existing higher SP is retained on load
and unrelated edits. Invalid drafts prevent saving instead of being silently
clamped. Future Connected does not have skill trees; its records are read-only.

## Catalog and native checks

The catalog contains 200 nodes in 40 branches, covering character IDs 1–8. Both
Fiora forms have separate node blocks and share the same hidden-branch flags.

- [BTL_PSVskill](https://xenoblade.github.io/xb1de/bdat/bdat_common/BTL_PSVskill.html): node names and `point_PP` learning costs.
- [BTL_PSVlink](https://xenoblade.github.io/xb1de/bdat/bdat_common/BTL_PSVlink.html): hidden-branch flag indices.
- [MNU_personally_name_ms](https://xenoblade.github.io/xb1de/bdat/bdat_common_ms/MNU_personally_name_ms.html): branch names.

Local executable checks use build ID `7E1DF8E08D60544BBDCA1E333C153C97`:

- `0x845DC–0x84600` reads `point_PP` and multiplies by 100. `point_SP` is a separate skill-link coin cost and is not used as the learning threshold.
- `0x8519C–0x852CC` consumes SP, advances the learned count and stores the remainder.
- Character record offsets `+0x7C` and `+0x90` store five unsigned 32-bit SP values and learned counts respectively.
- `0x85570` and `0xB26A0–0xB26E4` read hidden flag `f` through packed bit `0x2CDD + f`, based at save offset `0x50`.
- The skill initializer at `0x83D70` handles IDs 1–8; Future Connected bypasses tree setup.

Synthetic tests cover all eight owners, native flag mapping, cost boundaries,
rejected edits, locked branches, bulk operations and byte isolation. Local real
saves are tested on in-memory copies only. GUI and CLI checks cover the same
operations. These checks do not substitute for loading an edited copy in-game.
