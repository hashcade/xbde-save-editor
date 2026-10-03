# XBDE Save Editor

Edit Xenoblade Chronicles: Definitive Edition saves, including Future Connected.

## Features

- **Save files:** Open compatible `bfsgame*.sav` and `bfsmeria*.sav` game saves, inspect the saved party and create byte-identical copies. System saves and thumbnails are not editable.
- **CLI:** Inspect saves as JSON or copy them without changing unknown data.

## Get started

With the .NET 10 SDK installed:

```sh
dotnet run --project cli/Cli.csproj -- inspect /path/to/bfsgame00.sav
dotnet run --project cli/Cli.csproj -- copy /path/to/bfsgame00.sav /path/to/copy.sav
```

See the [development plan](docs/roadmap.md) for the next panels.

## Credits

- [damysteryman/XCDESave](https://gitlab.com/damysteryman/XCDESave): documented save-format fields used as references. This project implements its parser independently and does not include the upstream library.
- [Xenoblade DE data tables](https://xenoblade.github.io/xb1de/bdat/index.html): game-data references.

## License

[MIT](./LICENSE) License © [jinghaihan](https://github.com/jinghaihan)
