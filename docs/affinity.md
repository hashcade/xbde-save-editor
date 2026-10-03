# Character affinity validation

The Affinity page lists pairs of joined main-story characters. Edit points from
0 to 5,000, maximize one pair, or maximize all joined pairs independently of the
search filter. Fiora's two forms share one pair record; the joined later form is
used for display when present. This does not edit region/NPC affinity, complete
quests, claim achievements, grant EXP or add Affinity Coins.

Increasing points also raises the pair's directed skill-link unlock indices.
Lowering points never relocks previously unlocked slots. Link assignments,
learned skills and SP remain unchanged. Unknown higher points/counts are preserved
on opening; newly entered points must satisfy the native cap. Unrecognized slot
counts are displayed as unknown rather than presented as five valid slots.

## Native layout

The checks use local NSO build `7E1DF8E08D60544BBDCA1E333C153C97`:

- `0xB6210` reads a pair through the symmetric 8×8 i32 matrix at `0x9D46DC`.
  It enumerates the 21 unordered pairs of canonical character IDs 1–7 in
  lexicographic order. ID 8 aliases ID 3. Self pairs and 3↔8 have index -1.
- `0xB62C0–0xB6350` clamps the signed points input to 0–5,000, then writes one
  u16 at runtime `0x14A0 + 2 * (index + 8)`. The state save begins at runtime
  `+0x6B0`, so the saved offset is `0xE00 + 2 * index`, with one-based indices
  1–21. Shulk–Reyn is at `0xE02`, Shulk–Fiora at `0xE04`, Riki–Melia at
  `0xE2A`. Reserved bytes `0xE00–E01` are untouched.
- `0xBC240–0xBC7B4` rebuilds skill-link availability for character IDs 1–8.
  Points at 0, 1,000, 2,000, 3,000 and 4,000 correspond to highest unlocked
  indices 0–4. `0x857A0–0x857C0` only raises each stored index.
- A directed index is saved at character `+0xA4 + 4 * (sourceId - 1)`.
  Ordinary pairs synchronize two indices. Fiora pairs synchronize four:
  `(3,k)`, `(8,k)`, `(k,3)` and `(k,8)`, including the inactive form.
- The direct setter does not dispatch affinity-gain notifications, set pair
  change flags or award achievement progress. Those occur on other gain paths.
  The editor does not simulate them or write their state.

Main-story format version 7 is required for writes. Future Connected must be
protected explicitly: the native getter still maps Shulk–Melia to a pair index,
even though this campaign does not use ordinary character affinity.

## Verification

Tests independently encode the native matrix and cover every pair, both Fiora
aliases, threshold boundaries, signed/range rejection, monotonic lowering,
unjoined pairs, anomalous values and unsupported campaigns/formats. Exact-byte
comparisons constrain each edit to its pair u16 and directed unlock u32s.
GUI checks include single/bulk maximum, search-independent bulk behavior,
fractional rejection, saving, all nine interface languages and minimum window size.
CLI tests verify the same writes and in-place output copies.

All four supplied main-story saves have 21 pairs at 5,000; the four Future
Connected saves have zero affinity records. Their 512 saved directed indices
match the derived thresholds, including inactive aliases. All source files are
unchanged. Actual loading of an edited copy still requires in-game verification.
