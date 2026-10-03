# Collectopaedia validation

## Current scope

The core and `collectopaedia <save>` CLI command inspect saved registrations.
There is no completion setter or GUI completion button yet. Registration,
reward creation, achievement notification and inventory consumption must be
handled together before exposing a completion operation.

The catalog contains 300 main-story entries (IDs 1–300, 21 map pages) and 28
Future Connected entries (IDs 319–346, two map pages). IDs 301–318 are excluded
placeholder rows. Native bounds also admit 347–349, but the extracted table does
not define those IDs. They are not exposed. Unknown campaigns and save formats
other than version 7 return an unsupported, empty inspection result.

Item IDs are joined through actual `ITM_itemlist` references, not calculated from
Collectopaedia IDs. In particular, Future Connected entry 319 references global
item 3956, whereas entry 346 references item 4091. Categories use native numeric
types 1–8; official localized names are deferred.

Sources:

- [ITM_collectlist](https://xenobladedata.github.io/xb1de/bdat/bdat_common/ITM_collectlist.html)
- [MNU_col reward rows](https://xenobladedata.github.io/xb1de/bdat/bdat_menu_item/MNU_col.html)

`tools/fetch_collectopaedia_catalog.py` prints the entry TSV; `--rewards` prints
the separate 133-row reward TSV. The scripts do not save game files or alter
repository catalogs. Native executable and personal saves are not distributed.

## Registration layout

Inspected NSO build ID: `7E1DF8E08D60544BBDCA1E333C153C97`.

- Setter `0x81310`: accepts IDs 1–349 and uses bit index `0x19E9 + id`.
- Getter `0x813D0`: reads the same word and mask.
- Runtime word base: context `+0x14941C`.
- Version-7 serialized base: `0x148D6C`; the runtime save object begins at
  context `+0x6B0` (`0xAF7EC`–`0xAF800`). The serialized prefix is copied as a
  single range by the save-object serializer (`0xE02A0`, descriptor `0x9D1270`).
- Word offset: `0x148D6C + 4 * ((0x19E9 + id) >> 5)`.
- Mask: `1u << ((id + 9) & 31)`.

The equivalent byte read uses `0x148D6C + ((0x19E9 + id) >> 3)` and mask
`1 << ((0x19E9 + id) & 7)`. Tests independently construct the native word mask
for every exposed ID and check the byte reader against it. Inspection never
changes inventory, counters, flags, EXP or unknown data. Stored registration
flags remain authoritative; owning a collectable does not imply registration.

All eight supplied real saves round-trip byte-identically after inspection.
The four main-story saves have all 300 registration bits set; the four Future
Connected saves have all 28 set. This describes these supplied files, not every
save or proof that edited completion/rewards have been tested in-game.

## Verified native call paths

The following addresses distinguish item registration from reward processing:

1. `0x2EEEB0` calls registration handler `0x1F6C70`, then checks whether its
   category is complete (`0x1F6F80`).
2. `0x1F6C70` calls the registration setter at `0x1F6D00`, checks collection
   achievements through `0x7D660`, refreshes the category and removes one owned
   collectable through `0xBDF40`. Its category animation callback is not an
   inventory reward setter.
3. Category reward state `0x2EF0B0` checks the category animation, obtains an
   item ID through `0x1F83B0`, builds an item packet through `0xEBD60`, and
   creates a reward task through `0x331E90`.
4. Page reward state `0x2EF3B0` obtains an item ID through `0x1F83F0` and follows
   the same packet/task path. The task creates the inventory acquisition UI via
   `0x357E80`.

Both reward getters call `0x1F3B70`. It reads table enum `0x88` (`MNU_col`),
field `itemID` (string `0x9AD9EE`). Page reward argument is zero; category reward
argument is the displayed category ordinal plus one, **not the raw category
type**. The map eligibility mask is `0x19EBEEFF` for IDs 2–30. Base reward rows
are stored at `0x9D5BE0`:

```text
1, 8, 15, 22, 26, 32, 39, 46, 1, 50, 54, 60, 1, 64, 71,
78, 84, 88, 1, 95, 1, 102, 109, 116, 120, 1, 1, 124, 129
```

`0xEBD60` generates equipment/gem packets using `0xBD790`, `0xBDAC0` and
`0xBE1B0`. Fixed gem acquisition may choose a strength from a native range when
the item table has no fixed value; it cannot be replaced blindly by a maximum
crafted gem. Generated reward item IDs and native socket/effect defaults must
be preserved.

Collection achievements are 132 (first entry), 133 (one complete page) and 134
(all main-story pages). The checker at `0x7D660` explicitly skips 301–318.
Reward EXP and actual award writes have not yet been fully traced here.

## Remaining completion requirements

- Trace the acquisition task to the final inventory write and verify its packet
  fields against the existing equipment/gem allocators.
- Verify the displayed category ordering and page visibility, including the
  special Memory Space page and campaign-specific pages.
- Establish repeat/no-op behavior from pre-edit registration bits and validate
  newly completed category/page rewards without granting old rewards again.
- Resolve linked achievement/EXP behavior before choosing completion semantics.
- Preflight all inventory banks, serial counters and references; a failed batch
  must leave the whole save unchanged.
- Add single-entry, page-wide and campaign-wide core/CLI/GUI operations only
  after their linked effects are validated. Test edited copies in-game.
