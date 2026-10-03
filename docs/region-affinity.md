# Region affinity validation

Affinity → Regions edits the five main-story area records. Each card exposes
linked points and stars; the bulk action sets all five regions to 10,000 points.
Selecting a different star rating sets the minimum for that tier. Selecting the
existing rating preserves its points. Five stars begin at 8,000, below the cap.

Only identified main-story saves with format version 7 permit writes. Future
Connected and ambiguous campaigns do not expose these records. Unverified
main-story formats show read-only values. Existing unusual values remain intact
on inspection; explicit point edits must satisfy the native range.

## Native evidence

The local NSO build is `7E1DF8E08D60544BBDCA1E333C153C97`:

- Getter `0xB48B0–0xB4904` reads one u16 at context
  `0x14A0 + 2 * (regionId + 1)`. The saved state begins at context `+0x6B0`,
  yielding save offset `0xDF2 + 2 * regionId`.
- Adder `0xB4910–0xB49B4` clamps the updated points to 0–10,000 and calls
  the generic state setter with descriptor `0x00210007` and the region ID.
- The chart UI at `0x24F0FC–0x24F148` calculates
  `min(points / 2000 + 1, 5)` using integer division.
- Achievement checks at `0x7DDB0–0x7DF94` examine region IDs 1–5. Three-star
  checks use 4,000; five-star checks use 8,000.

| ID | Verified English chart name | Save offset |
| --- | --- | --- |
| 1 | Colony 9 Area | `0xDF4` |
| 2 | Colony 6 Area | `0xDF6` |
| 3 | Central Bionis | `0xDF8` |
| 4 | Upper Bionis | `0xDFA` |
| 5 | Hidden Village | `0xDFC` |

The chart names are entries 27–31 in
[MNU_relate_ms](https://xenobladedata.github.io/xb1de/bdat/bdat_common_ms/MNU_relate_ms.html).
Other game-language names require their original text data; interface labels
are localized independently.

Writes change only these five u16 fields. Reserved neighboring fields, story
progress, character affinity, NPC links, quests and achievement flags remain
unchanged. The editor does not simulate runtime notifications or award EXP.

## Verification

Core tests cover every region and tier boundary, invalid IDs/ranges, unchanged
ratings, anomalous values, repeated maximum operations and campaign/version
protection. Exact whole-file comparisons constrain single writes to two bytes
and bulk writes to the ten-byte region range.

CLI tests exercise inspection, both edit options, single/bulk maximum, in-place
outputs and protected layouts. GUI checks exercise tier linkage, invalid drafts,
single-card edits, bulk maximum, saving, read-only layouts and all nine interface
languages at minimum window size.

All four supplied main-story saves already contain 10,000 points in all five
regions. All four Future Connected saves contain zeros in the corresponding
range. Tests work on in-memory copies or generated files; original saves remain
unchanged. Loading an edited copy in-game has not yet been verified.
