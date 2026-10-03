# Colony 6 reconstruction validation

Main includes a separate Colony 6 card with saved facility levels, overall
level, development and population. Max Reconstruction completes the remaining
upgrades of all four facilities and their shared overall level, without spending
money or materials. It does not recruit residents or force development/population
to a fixed total.

The operation requires an identified main-story format-7 save and at least one
facility already upgraded in-game. It does not start the reconstruction quest or
bypass the initial story prerequisites. Future Connected and ambiguous campaigns
do not expose this card. Unverified formats and invalid levels/linked states are
read-only. All validation finishes before any writes.

## Native evidence

The inspected NSO build is `7E1DF8E08D60544BBDCA1E333C153C97`.

- Getter `0xB4FC0` reads `context + 0xCA0 + 0x708 + category`.
  Setter `0xB5020` uses descriptor `0x09280008`; the generic setter at
  `0xAEB10` places these bytes in `Gameflag8[0x708 + category]`.
  The format-7 saved Gameflag8 block starts at `0x5F0`, giving **`0xCF8`**,
  not `0xC98`. The getter's runtime offset is `0x13A8`.
- Upgrade function `0x27C170` increments the selected category, capped at 5,
  and applies one row from
  [CL6_uplist](https://xenobladedata.github.io/xb1de/bdat/bdat_common/CL6_uplist.html).
  Housing uses rows 1–5, Commerce 6–10, Nature 11–15, Special 16–20 and
  overall reconstruction 21–25.
- The upgrade controller at `0x393BD0–0x393CD0` advances overall category 6
  only when all four facilities exceed its current level. This is the minimum
  of their levels, not their maximum. Overall levels above that minimum are
  rejected as inconsistent rather than silently repaired.
- Each applied row adds `rev_LV` to development and `rev_people` to population.
  It sets `rev_effflag1` via descriptor `0x278A012C`, `rev_selflag` via
  `0x08C40064`, and `rev_qstflag` via `0x02200514` with value **200**.
  These are the game's upgrade writes, not a blanket quest-completion operation.

| Saved offset | Field |
| --- | --- |
| `0xCF8` | Development |
| `0xCF9` | Population |
| `0xCFA` | Housing level |
| `0xCFB` | Commerce level |
| `0xCFC` | Nature level |
| `0xCFD` | Special level |
| `0xCFE` | Overall reconstruction level |

For a nonzero effect flag, the saved bit is `0x1D6A + rev_effflag1`, starting
at offset `0x50`. Self flags are bytes at `0xC94 + rev_selflag`; linked quest
flags are bytes at `0x5F0 + rev_qstflag`. Only rows for still-missing levels
are applied. Existing past-level flags, invitations at `0xD00` onward, the
reserved byte at `0xCFF`, unrelated quest/story flags, inventory, EXP, money,
affinity and achievement records remain untouched.

Development and population retain their existing contribution from residents
and events. Their additions are checked against byte storage bounds; overflow
rejects the whole operation instead of wrapping. Linked self flags above 1 and
quest states above 200 also reject the entire batch. Opening a save never
normalizes unusual values. Already-maximized reconstruction is byte-identical
and repeated maximum operations do not add progress or population again.

Runtime camera/sound notifications and reward distribution are not simulated.
English facility names are retained while original localized game text is
unavailable; ordinary interface/action labels follow the selected UI language.

## Verification

Core tests cover all 1,295 started combinations of the four facility levels,
varying valid overall levels, and compare the entire output file against an
independent replay of the native table rows. Tests also cover repeated maximum
operations, pre-existing linked flags, completed colonies, malformed levels,
counter overflow, unsupported flags, unstarted reconstruction and campaign/
format protections. GUI tests cover the independent natural-height Main card,
plain-text values, saved output, preserved character progression drafts and all
nine interface languages at minimum window size. CLI tests cover inspection,
in-place maximum and rejection without replacing an existing output.

The four supplied main-story saves have development 100, population 150 and
all five level fields at 5. Their Commerce level-2 self flag is already 2,
showing that it can advance after the original upgrade's write of 1; it must
not be reset by replaying a past level. Their reconstruction operation is a no-op. The
four Future Connected saves have zeros in the corresponding block and remain
protected. Tests use generated saves or in-memory copies, not original files.
Loading a newly upgraded copy in-game has not yet been verified.
