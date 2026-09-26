# MVP Anthem

Custom round-MVP anthems for CS2 servers running SwiftlyS2. Players can browse categories, preview sounds, select an anthem or random mode, and save their preferred listening volume.

[Download the latest release](https://github.com/T3Marius/SW2-MVP_Anthem/releases/latest)

## Requirements

| Dependency | Required for |
| --- | --- |
| SwiftlyS2 1.4.6 or newer | Running MVP Anthem |
| [Cookies](https://github.com/SwiftlyS2-Plugins/Cookies) | Saving player selections |
| [Volume.API](https://github.com/a2Labs-cc/Volume.API) | Per-player anthem volume |
| [T3Menu by T3Marius](https://github.com/T3Marius/T3Menu) | The default `t3` menu |

**Install [T3Menu](https://github.com/T3Marius/T3Menu) before MVP Anthem to use the default menu.** Follow its installation instructions, including the [required Workshop addon](https://steamcommunity.com/sharedfiles/filedetails/?id=3790988631) for players. Bundling `T3Menu.Contract.dll` does not install the T3Menu plugin.

The built-in SwiftlyS2 menu is also available with `MenuType: "core"`. If T3Menu is unavailable, MVP Anthem logs a warning and falls back to Core. Cookies is required for either menu.

## Installation

1. Install the dependencies above and load them before MVP Anthem.
2. Download the [latest release ZIP](https://github.com/T3Marius/SW2-MVP_Anthem/releases/latest).
3. Extract both `plugins/` and `data/` into `game/csgo/addons/swiftlys2/`.
4. Start the server to generate `config.jsonc`.
5. Configure your anthems and menu, then reload MVP Anthem or restart the server.

## Player features

- Category browsing with only accessible anthems shown.
- Anthem selection, private previews, random mode, and remove confirmation.
- Persistent volume per listener, including mute at 0%.
- Access control through permission flags or SteamID64.
- Localized menus, chat announcements, and center-HTML messages.
- Native CS2 named game sound events.

Use `!mvp` to open the menu. Command aliases are configured in `Main.Settings.MVPCommands`.

## Choose a menu

Set `Main.Settings.MenuType` in `config.jsonc`:

| Value | Menu | Configuration |
| --- | --- | --- |
| `"t3"` (default) | [T3Menu](https://github.com/T3Marius/T3Menu) | Navigation, style, and sounds come from T3Menu |
| `"core"` | SwiftlyS2 built-in menu | Uses the freeze, sound, and gradient settings under `Main.Menu` |

Both menus use the same selection, preview, and permission logic. Permissions are checked again when selecting or previewing an anthem. Listening volume is managed by Volume.API through its `!volume` and `!vol` commands.

## Configuration

Configuration lives under `Main` in `config.jsonc`.

### General settings � `Main.Settings`

| Setting | Default | Purpose |
| --- | --- | --- |
| `MenuType` | `"t3"` | Select `t3` or `core` |
| `MVPCommands` | `["mvp"]` | Menu command aliases |
| `DefaultVolume` | `0.2` | Fallback listening volume when Volume.API is unavailable |
| `GiveRandomMVPOnFirstConnect` | `true` | Assign an accessible anthem on first join |
| `MVPMaxDuration` | `10` | Duration of the center-HTML announcement, in seconds |
| `RemovePlayerInGameMvp` | `true` | Reset the MVP player's native MVP and music-kit MVP counters |
| `SoundEventFiles` | `[]` | Resource paths to precache for custom sound events |

`GiveRandomMVPOnFirstJoin` remains an alias for `GiveRandomMVPOnFirstConnect`; configure one of them. `MenuTypes` is retained for compatibility, but `MenuType` controls the menu. `ShakePlayerScreen` is currently unused.

### Menu settings � `Main.Menu`

| Setting | Default | Applies to |
| --- | --- | --- |
| `FreezePlayer` | `true` | Core menu |
| `EnableSounds` | `true` | Core menu |
| `GradientTitleColor` | `true` | Core menu |

### Anthems � `Main.MVPs`

Each category contains anthem IDs and their settings. Keep anthem IDs unique across categories; saved selections use these IDs.

```jsonc
"MVPs": {
  "category.public_mvp": {
    "mvp_1": {
      "DisplayName": "mvp_1.name",
      "Sound": "Weapon_AK47.Single",
      "EnablePreview": true,
      "ShowHtml": true,
      "ShowChat": true,
      "Permissions": []
    }
  }
}
```

| Field | Purpose |
| --- | --- |
| `DisplayName` | Translation key for the anthem's name |
| `Sound` | Named CS2 game sound event |
| `EnablePreview` | Show the private preview action |
| `ShowHtml` / `ShowChat` | Enable round-MVP announcements |
| `Permissions` | Allowed permission flags or SteamID64 strings |

An empty permission list grants access to everyone. Otherwise, matching any entry grants access. Random mode also respects these restrictions.

## Sounds and translations

Set `Sound` to a named CS2 game sound event, such as `"Weapon_AK47.Single"`. Custom sound-event resources can be listed under `Main.Settings.SoundEventFiles` for precaching.

Add translation entries for each category and anthem:

| Config entry | Translation key |
| --- | --- |
| Category `category.public_mvp` | `category.public_mvp` |
| Display name `mvp_1.name` | `mvp_1.name` |
| Anthem ID `mvp_1` | `mvp_1.chat` and `mvp_1.html` |

Round announcements receive the MVP player's name as `{0}` and the translated anthem name as `{1}`. Each listener hears the anthem at their own saved volume. HTML announcements expire after `MVPMaxDuration`.

## Build and test

Requires the .NET 10 SDK.

```powershell
dotnet build MVP_Anthem.slnx -c Release
dotnet run --project Tests/RegressionChecks/RegressionChecks.csproj -c Release
```

Build output is placed in `build/plugins/MVP_Anthem/` and `build/data/MVP_Anthem/`. Tagged releases package both folders as `MVP_Anthem-vX.Y.Z.zip`.

The regression checks cover cookie caching and slot reuse, volume validation, menu navigation, permission changes, selection persistence, and menu cleanup. Actual playback and input handling still require a running CS2 server.

## Support

[Buy me a coffee](https://buymeacoffee.com/t3marius)
