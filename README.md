# Sortierknopf (sort button)

Adds the sort button that the stash already has to the secure container and backpack as well.

## Requirements

- SPT 4.1.6 (EFT 0.16.9). BepInEx ships with SPT.

## Installation

1. Download `Sortierknopf-1.0.0.zip` from the [Releases](https://github.com/Kaanori/spt-sortierknopf/releases) page.
2. Extract it **into your SPT game folder** (the folder that contains `EscapeFromTarkov.exe`).
   The ZIP already has the right layout; the mod ends up in `BepInEx/plugins/`.
   On Windows, make sure "Extract All" does not add an extra subfolder.
3. Restart the game.

## Uninstall

Delete the mod's files from `BepInEx/plugins/` (settings: `BepInEx/config/` for client mods) and restart the game.

## Settings (F12 → Sortierknopf)

| Setting | Effect |
|---|---|
| **Eingeschaltet** (enabled) | on/off |
| **Im Sicherheitsbehälter** / **Im Rucksack** / **In jedem Behälter** (in the secure container / in the backpack / in every container) | where the button appears |
| **Auch im Raid sortieren** (also sort in raid) | also offer the button during the raid |
| Position: offset, anchor, layer, size | where the button sits and how big it is |

## Building from source

Needs the .NET 10 SDK and an SPT 4.1.6 installation to build against.

```bash
./build.sh             # Linux: build only
./build.sh --install   # build and copy into the game folder
```
```powershell
.\build.ps1            # Windows
.\build.ps1 -Install
```

The game folder is set in `Directory.Build.props` (Windows default `C:/SPT`)
or via the environment variable `SPT_DIR`.

## Support

Bugs and questions: [GitHub issues](https://github.com/Kaanori/spt-sortierknopf/issues).

## License

MIT — see [LICENSE](LICENSE).
