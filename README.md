# MyAwesomeWhitelist

Host-side whitelist/blacklist manager for Human: Fall Flat lobbies.

[**中文**](README.zh.md) | English

## Features

- **Force kick** — server-side unilateral teardown (destroys all of the client's objects + broadcasts the removal) plus Steam P2P reconnect refusal; client-side anti-kick mods (e.g. ones that ignore Kick messages) cannot block it.
- **Whitelist** — toggleable; only whitelisted players may join; permanent (persisted to file) + temporary (per-lobby, lost when you rehost).
- **Blacklist** — toggleable; blacklisted players are refused at the Steam P2P layer before any handshake; permanent + temporary entries.
- **Host-only** — interception/kicking only applies when you are the host; auto-disabled for clients or in single-player.
- **In-game IMGUI manager window** — toggleable with a configurable combo hotkey (default `Ctrl+Shift+W`; on macOS `Ctrl` matches `Cmd`).

## Install

1. Install [BepInEx 5](https://thunderstore.io/c/human-fall-flat/p/BepInEx/BepInExPack/) (5.4.21 or newer)
2. Drag the mod's zip into [r2modman](https://thunderstore.io/c/human-fall-flat/), or manually unpack `plugins/MyAwesomeWhitelist.dll` into `BepInEx/plugins/`

## Usage

1. Host a multiplayer lobby (you are the host).
2. Press `Ctrl+Shift+W` to open the window — in a level it auto-enters the Esc pause state to free the mouse; drag the title bar to move it.
3. **Players** tab: with the whitelist enabled, click once to enforce a whitelist kick; blacklisted players are force-kicked automatically every second.
4. **Whitelist / Blacklist** tab: the list box shows entries; switch between permanent/temporary; remove entries or add one manually by SteamID64.
5. **Friends** tab: sorted by name with prefix search, one-click add to whitelist/blacklist.
6. **Settings** tab: change the window hotkey and the UI language (dropdown).

Permanent lists are stored under:
`BepInEx/config/MyAwesomeWhitelist/whitelist.json` and `blacklist.json`

Configuration: `BepInEx/config/MyAwesomeWhitelist.cfg`

UI language files: `BepInEx/config/MyAwesomeWhitelist/lang/*.txt` (missing translations are auto-appended after upgrades)

## Notes

- Temporary lists die with the lobby; permanent lists survive restarts.
- Blacklisted players are refused before the handshake: they see a "connection failed" rather than "kicked"; anyone already inside is swept and force-kicked every second.
- **Kicks are not bans**: vanilla permanently locks out kicked players until the host restarts; here plain kicks and whitelist rejections are one-shot (the player can rejoin), only blacklisting keeps the P2P ban — and turning the blacklist off, deleting an entry, or hosting a new lobby lifts it automatically.

## Build

```bash
dotnet build -c Release          # requires a local game install (see Directory.Build.props)
scripts/pack.sh                  # produces dist/MyAwesomeWhitelist-<ver>.zip (Thunderstore)
```

Releasing is handled by CI — see [docs/RELEASING.md](docs/RELEASING.md).