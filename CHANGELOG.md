# Changelog

[**中文**](CHANGELOG.zh.md) | English

All user-facing changes to this plugin are recorded per version. The release
pipeline extracts each version's release notes from this file — every released
version must have exactly one `## <version>` section whose heading matches
`thunderstore/manifest.json`'s `version_number` exactly (see `docs/RELEASING.md`).

## 0.1.0

- **Force kick** — server-side unilateral teardown (destroys all of the client's objects + broadcasts the removal) plus Steam P2P reconnect refusal; client-side anti-kick mods cannot block it.
- **Whitelist** — toggleable; only whitelisted players may join; permanent (persisted to file) + temporary (per-lobby).
- **Blacklist** — toggleable; refused at the Steam P2P layer before any handshake (shows as "connection failed"); already-inside players are force-kicked every second.
- **Host-only** — interception/kicking only applies when you are the host; auto-disabled for clients or single-player.
- **In-game IMGUI manager window** — Players / Whitelist / Blacklist / Friends / Settings tabs; default `Ctrl+Shift+W` (configurable in the Settings tab or `.cfg`; on macOS `Ctrl` matches `Cmd`).
- **Multi-language UI** — `BepInEx/config/MyAwesomeWhitelist/lang/*.txt`, missing translations auto-appended after upgrades.
- **List persistence** — `whitelist.json` and `blacklist.json`; permanent entries survive restarts.