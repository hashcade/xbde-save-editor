# XBDE Save Editor

Edit Xenoblade Chronicles: Definitive Edition saves, including Future Connected.

![Main editor](docs/main.png)

## Features

- **Save files:** Open compatible `bfsgame*.sav` and `bfsmeria*.sav` game saves, inspect the saved party and create byte-identical copies. System saves and thumbnails are not editable.
- **Main:** Edit money and Noponstones. A separate Colony 6 card shows facility levels, development and population; maximize reconstruction after starting it in-game, without grinding materials. Missing upgrades synchronize their native linked flags and population/development increases; resident recruitment stays unchanged. Save and Save As are available in the File menu; unrecognized layouts are rejected and unknown bytes remain untouched.
- **Characters:** Search joined characters, select a level and edit linked EXP, AP, main-story Affinity Coins and Expert Mode reserve EXP. Maximize AP for one character or everyone already joined, or fill a character's reserve EXP. The highest attained level is available for inspection. Character names switch with the interface language. Future Connected does not expose Affinity Coin editing.
- **Arts:** In Characters → Arts, learn missing ordinary arts, choose a level or maximize a single learned art. Main-story format-7 saves also support learning Mind Blast and Final Cross without completing their associated events. Learn and maximize supported arts for the selected character or all joined characters, without spending AP. Campaign limits, Master books and Monado story permissions are respected; missing Monado arts and fixed Talent Arts stay protected. Melia's discharge levels remain linked to their summon arts. Matching CLI commands are included.
- **Skills:** In Characters → Skills, view each passive skill tree, change how many skills are learned and edit remaining SP. Unlock a hidden branch, or unlock and learn every skill for the selected character or all joined characters without marking the associated tasks complete. In Skill Links, edit each available source character's five slots without typing IDs: selectors show learned skills matching the slot shape and available Affinity Coins. Duplicate links are excluded, existing slot unlocks are preserved, and selecting None removes a link. Skill trees and links are not available in Future Connected. Matching CLI commands are included.
- **Equipment:** In Characters → Equipment, change weapons and armor to compatible items already in inventory. Other characters' equipped items are excluded, and each selected item keeps its gems. Medium/heavy armor checks learned skills and existing skill links; Mechon Fiora uses her own equipment types. Story-flagged weapons remain protected. Fit or remove owned normal gems in existing sockets; gems fitted to any other item are excluded, even when that item is unequipped. Built-in gems and appearance remain unchanged. Matching CLI commands are included.
- **Items:** Separate tabs for gems, collectables, materials, art manuals, key items, weapons and armor. Add ordinary stacks, edit their quantities and favorite flags, delete stacks or maximize all existing quantities in a category. Quantities are limited to 99. Art manuals distinguish Intermediate, Advanced and Master books. Quest items remain read-only. Create ordinary weapons/armor with their original sockets and built-in gems, or fill missing equipment for a joined character in the current category. Existing equipment remains unchanged; insufficient space rejects the entire batch. Referenced and story-protected equipment cannot be deleted.
- **Gems:** Search owned normal gems and edit their effect, rank and strength. Create gems using the same validated ranges, or delete gems not fitted to any equipment. Inputs follow the game's per-effect/per-rank limits, with a maximum-value button and fixed activation chances. Equipped gems retain their socket references, and incompatible effect changes are rejected. Matching CLI commands are included.
- **Affinity:** Separate Characters and Regions tabs. Adjust character-pair affinity or maximize all joined pairs without repeated gifting; raising points also unlocks their skill-link slots. Edit each of the five regions' points and linked star rating, or maximize every region at once. Character points are capped at 5,000 and region points at 10,000. Future Connected and unverified formats are protected. Matching CLI commands are included.
- **Achievements:** Search the 200 main-story achievements, filter by category or completion, inspect conditions, counters and EXP rewards, and unlock one or all. Entries already unlocked with unmet counters are identified separately; the total counts stored completion flags. A separate single/bulk repair fills their unmet counters without unlocking other entries. Ordinary unlocking leaves completed records unchanged. Neither operation adds party EXP or completes related quests. Future Connected and unverified save formats are protected. Matching CLI commands are included.
- **Collectopaedia:** Register an item, complete a page or fill every missing entry in the current campaign without gathering each collectable. Newly completed categories/pages grant their original equipment and gem rewards; repeating an operation grants nothing again. Related main-story achievement records are synchronized without changing character EXP or levels. Existing collectables remain untouched. Any inventory capacity or serial failure rejects the entire batch. Supports 300 main-story entries and 28 Future Connected entries, with matching CLI commands.
- **CLI:** Inspect saves as JSON, copy them or edit resources without changing unknown data.

English is the default language. The interface also supports Simplified Chinese,
Traditional Chinese, Japanese, Korean, German, French, Spanish and Italian.
Help → About shows the version and repository link.
Art, skill, equipment, gem, region and achievement names currently use the verified English catalogs; other game-language names
are not yet available.

## Get started

With the .NET 10 SDK installed:

```sh
dotnet run --project gui/XbdeEditor.Gui.csproj -c Release
dotnet run --project cli/Cli.csproj -- inspect /path/to/bfsgame00.sav
dotnet run --project cli/Cli.csproj -- copy /path/to/bfsgame00.sav /path/to/copy.sav
```

Money and Noponstones edits are limited to the native cap of 999,999,999.
Existing higher amounts are preserved on opening and unrelated edits; they are
not silently normalized.
AP edits are restricted to 0–99,999,999, Affinity Coins to 0–999, and Expert Mode
reserve EXP to 0–199,999,998. Existing higher amounts are preserved unless edited.
Level editing respects each character's campaign-specific minimum and the level
99 cap. Changing level resets current EXP; entered EXP advances through the
game's level costs. Apply Changes commits the linked fields, and File → Save
writes the save. Direct level edits leave reserve EXP untouched. Changing reserve
EXP alone does not immediately change level; spend it in-game in Expert Mode.
Campaign identification currently uses joined characters. When only characters
shared by both campaigns are present, the campaign is shown as unconfirmed and
Affinity Coin editing is unavailable.

See the [CLI reference](CLI.md) and [development plan](docs/roadmap.md).

## Development

```sh
dotnet build XbdeEditor.slnx -c Release
dotnet run --project test/Core.Test.csproj -c Release --no-build
dotnet run --project test/Gui.Smoke.csproj -c Release --no-build
python3 scripts/test_cli.py --cli cli/bin/Release/net10.0/XbdeEditor.Cli.dll
```

Releases use `python3 scripts/release.py`. The script tests the current branch,
creates the version commit and pushes its tag. GitHub Actions packages the
desktop app and CLI with a shared runtime and generates release notes.
There is no published release yet.

## Credits

- [damysteryman/XCDESave](https://gitlab.com/damysteryman/XCDESave): documented save-format fields used as references. This project implements its parser independently and does not include the upstream library.
- [Xenoblade DE data tables](https://xenoblade.github.io/xb1de/bdat/index.html): game-data references.

## License

[MIT](./LICENSE) License © [jinghaihan](https://github.com/jinghaihan)
