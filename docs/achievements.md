# Achievement validation

## Catalog

The Definitive Edition tables contain 200 achievements: 150 Trials and 50 Records.
Names, conditions, ordering and EXP rewards are joined by ID from
[JNL_playaward](https://xenoblade.github.io/xb1de/bdat/bdat_common/JNL_playaward.html),
[JNL_playaward_ms](https://xenoblade.github.io/xb1de/bdat/bdat_common_ms/JNL_playaward_ms.html)
and [MNU_playaward](https://xenoblade.github.io/xb1de/bdat/MNU_playaward.html).
Regenerate the catalog with `tools/fetch_achievement_catalog.py` using BeautifulSoup.
Names and condition text currently use English game data.

## Save fields

Verified against executable build ID `7E1DF8E08D60544BBDCA1E333C153C97`
and format-version-7 game saves. Addresses are relative to the executable image.

- Getter `0xB5BC0` reduces the ID modulo 200 and tests flag
  `0x2838 + (id % 200)` at runtime `context + 0x700`.
- The saved object is embedded at `context + 0x6B0` (`0xAF7EC–0xAF800`),
  with its flag block at offset `0x50` (`0x735C4–0x735D0`). Completion bits
  occupy save bytes `0x557–0x56F`. ID 200 uses the first bit.
- Checker `0x7AF60` reads `comp_type` and `comp_value`. Types 3/4 use
  descriptor `0x004000C8` with getters/setters `0xB2620`/`0xAEB10`.
  Unsigned 16-bit counters are at `0xE30 + 2 * (id % 200)`, capped at
  65,535. Type 3 compares equality; type 4 compares at least the threshold.
- Types 0/1/2 are event or single-action conditions, not cumulative counters.
  A 100,000-damage single-action threshold must not be written into a 16-bit count.
- Native setter `0xB5C50` also invokes EXP reward code `0xB5CC0`. The editor
  sets completion directly, without invoking rewards or adding character EXP.

## Editing rules

Only confirmed main-story saves with format version 7 are editable. Future
Connected does not use these achievements; its zeroed records are not shown as
200 missing achievements. Unconfirmed campaigns and unverified versions are protected.

Unlocking an incomplete cumulative achievement sets its required counter before
setting its completion bit. Higher counts are retained for type 4. Event
achievements only set the bit. Already-completed entries are byte-identical
no-ops, even if counters are below conditions. Status follows the game's flag.

Unlock All processes every entry, independent of visible filters. No character
EXP, resources, quest state or story flags are modified. Related world actions
are not completed by unlocking an achievement.

Tests cover all 200 IDs, exact byte boundaries, event thresholds, idempotence,
higher counters, unsupported campaigns/versions, CLI output and GUI filtering/saving.
The supplied four main-story saves have all 200 completion flags; the four
Future Connected saves have zeroed records. Actual in-game display after editing
a previously incomplete save still needs in-game verification.
