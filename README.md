# XBDE Save Editor

Edit Xenoblade Chronicles: Definitive Edition saves, including Future Connected.

![Main editor](docs/main.png)

## Features

- **Save files:** Open compatible `bfsgame*.sav` and `bfsmeria*.sav` game saves, inspect the saved party and create byte-identical copies. System saves and thumbnails are not editable.
- **Main:** Edit money and Noponstones. Save and Save As are available in the File menu; unrecognized layouts are rejected and unknown bytes remain untouched.
- **Characters:** Search joined characters, edit AP and main-story Affinity Coins, and inspect saved levels, experience and Expert Mode records. Character names switch with the interface language. Future Connected does not expose Affinity Coin editing.
- **CLI:** Inspect saves as JSON, copy them or edit resources without changing unknown data.

English is the default language. The interface also supports Simplified Chinese,
Traditional Chinese, Japanese, Korean, German, French, Spanish and Italian.
Help → About shows the version and repository link.

## Get started

With the .NET 10 SDK installed:

```sh
dotnet run --project gui/XbdeEditor.Gui.csproj -c Release
dotnet run --project cli/Cli.csproj -- inspect /path/to/bfsgame00.sav
dotnet run --project cli/Cli.csproj -- copy /path/to/bfsgame00.sav /path/to/copy.sav
```

Resource inputs currently validate unsigned 32-bit storage bounds, not an
independently verified in-game currency cap. Existing high amounts are preserved
on opening; they are not silently normalized.
AP uses the same storage-bound validation. Affinity Coin edits are restricted
to 0–999. Level and Expert Mode edits remain disabled until their linked
progression rules have been verified.

See the [development plan](docs/roadmap.md) for the next panels.

## Credits

- [damysteryman/XCDESave](https://gitlab.com/damysteryman/XCDESave): documented save-format fields used as references. This project implements its parser independently and does not include the upstream library.
- [Xenoblade DE data tables](https://xenoblade.github.io/xb1de/bdat/index.html): game-data references.

## License

[MIT](./LICENSE) License © [jinghaihan](https://github.com/jinghaihan)
