# BanchoSucks Lazer

Windows x64 client based on GooGuTeam/g0v0 v2026.913.0-g0v0
(fb9d767658b361887830bd475a42efe00721966b).

Website: https://banchosucks.cc/lazer (lazer profiles, rankings and beatmaps live
under the /lazer prefix of the main Banchosucks site; the client's links point there)
Lazer web interface: https://lazer.banchosucks.cc
API: https://lazer-api.banchosucks.cc

Install with BanchoSucksLazer-win-Setup.exe (installs to %LocalAppData%\BanchoSucksLazer and
adds a start menu entry). From then on the game updates itself like osu!stable: it checks our
GitHub releases on start and every 30 minutes, downloads the update in the background and
shows a notification; clicking it restarts into the new version. Game data and settings are
not touched by updates. Accounts are created on
https://banchosucks.cc/register and work on both the stable and the lazer server
(the in-game register button opens that page). No server URL needs to be entered.
Keep the Custom API Server URL setting empty to use the built-in endpoints.

Game data is stored separately in %APPDATA%/banchosucks-lazer.
Updates come only from https://github.com/TageLangHigh/BanchoSucks-Lazer/releases, never from
upstream. The executable and the installer are not code-signed (SmartScreen warns once).

This is a server-configured fork, not a completely new game or visual theme.
Existing game artwork and most interface strings are from g0v0.

## Attribution

Based on osu!lazer by ppy Pty Ltd and g0v0 by GooGuTeam.
https://github.com/ppy/osu
https://github.com/GooGuTeam/g0v0
Code: MIT; see LICENCE and LICENCE-OSU.
Game resources: CC-BY-NC 4.0, ppy Pty Ltd and GooGuTeam, non-commercial use.
https://github.com/GooGuTeam/g0v0-resources
https://creativecommons.org/licenses/by-nc/4.0/
Not affiliated with or endorsed by ppy Pty Ltd or GooGuTeam.

## Build

Requires .NET SDK 10.0.100 or newer compatible feature band.

```powershell
dotnet publish osu.Desktop/osu.Desktop.csproj -c Release -r win-x64 --self-contained true -o ../artifacts/BanchoSucks-Lazer-win-x64-2026.913.3 -p:Version=2026.913.3 -p:AssemblyVersion=2026.913.3 -p:FileVersion=2026.913.3 -p:InformationalVersion=2026.913.3-banchosucks
```

Linux x64 (portable tarball; built the same way, cross-published from Windows works):

```powershell
dotnet publish osu.Desktop/osu.Desktop.csproj -c Release -r linux-x64 --self-contained true -o ../artifacts/BanchoSucks-Lazer-linux-x64-2026.913.3 -p:PublishSingleFile=false -p:PublishTrimmed=false -p:Version=2026.913.3 -p:AssemblyVersion=2026.913.3 -p:FileVersion=2026.913.3 -p:InformationalVersion=2026.913.3-banchosucks
```

Players extract the tarball and run `./BanchoSucks-Lazer` (needs a desktop with
OpenGL, PulseAudio/PipeWire; Ubuntu 22.04+, Fedora, Arch are fine).

The server must allow the MD5 hash of the resulting osu.Game.dll in its client
version list. This identifies the exact build; it does not prove trustworthiness.
Register each platform's hash with `register-client-build.py <md5> <version> <Windows|Linux>`
and restart the app container.

## Branding

- `osu.Game/Resources/Textures/**` holds Banchosucks textures (menu logo, logo icon). They are
  embedded and registered before the g0v0 resources package in `OsuGameBase`, so same-named
  files win. Regenerate them from the website logo (circle, ring, dot; `#ff4964`).
- `osu.Desktop/lazer.ico` is the Banchosucks icon (window, taskbar, file associations).
- English texts come from `osu.Game/Localisation/*.cs`; other languages come from the g0v0
  package and are rewritten at runtime in `ResourceManagerLocalisationStore` ("g0v0!" ->
  "BanchoSucks Lazer").
- Discord Rich Presence (`osu.Desktop/DiscordRichPresence.cs`) reads
  `/api/plugins/banchosucks_client/config` from the lazer server at startup: the Discord
  application id (Discord shows its name as "Spielt ..."), logo and mode icon urls, and the
  website for the "Auf Banchosucks spielen" button. The last id is kept in
  `OsuSetting.BanchosucksDiscordAppId`; without one the built-in g0v0 id is used.

## Banchosucks features

- **Input / audio rate** (Graphics > Renderer): 1000 to 8000 Hz for the input and audio threads,
  kept applied by `osu.Game/Graphics/BanchosucksThreadRateManager.cs`.
- **Menu opacity** (User Interface > General): alpha of menu screens; gameplay and the editor
  always stay opaque (`OsuGame.applyMenuOpacity`).
- **Render replays as video** (right-click a score on the song select leaderboard):
  `osu.Game/Scoring/BanchosucksReplayRenderer.cs` renders with danser-go on the player's PC.
  danser-go (GPL-3.0, bundles FFmpeg) is not shipped with the client. It is downloaded from
  its GitHub release only after the player agrees, checked against a pinned SHA-256 and kept
  in `<game data>/banchosucks-renderer/`. Antivirus programs sometimes quarantine its
  ffmpeg; the client then says so.
- **Minigames** (main menu > Play > minigames): `osu.Game/Screens/Banchosucks/`, currently
  "osu! Clicker" (season 2 since 2026-09-27), an idle clicker in osu! style. The game itself is
  `Clicker/ClickerEngine.cs`, a component of `OsuGame` that keeps producing in every screen
  (menu, gameplay, editor; one second per frame at most, longer gaps count as offline time),
  saves atomically to `<game data>/banchosucks/osu-clicker.json` (+ `.bak.json`) and submits a
  snapshot to the lazer plugin `banchosucks_clicker` every minute while the screen is open,
  every five minutes otherwise, never during a play. Every number lives in
  `Clicker/clicker-balance.json` (embedded; the plugin ships a byte-identical copy):
  11 buildings (three unlocked by the prestige tree), 10 mod upgrades, building stars,
  synergies, Kiai Time / farm map abilities, mod stances, spinner and slider events,
  tournament expeditions, Encore (passed online maps pay), daily-challenge bonus, weekly server
  modifier, 38 medals (+2 % production each), rebirths with prestige points
  (floor(cbrt(lifetime PP / 1e8))) spent in a 34-node tree, respec for 10 % of the points.
  `Clicker/ClickerEconomy.cs` derives all values from the state and is mirrored line by line by
  the plugin's `economy.py`; both are checked against `osu.Game.Tests/Resources/Banchosucks/
  clicker-balance-fixture.json`. Texts are English and German by client language
  (`Clicker/ClickerStrings.cs`). Tapping speed is shown as stream BPM (taps per second * 15,
  `TapBpmMeter`); taps faster than 500 BPM are refused. Screen: `OsuClickerScreen.cs` (tabs
  Shop / Prestige / Medals / Stats / Leaderboard, volume popover, ability buttons),
  `ClickerPanels.cs` (prestige tree, medals, stats, stance, expeditions), `ClickerDrawables.cs`
  (circle, spinner, slider event, shop rows), `ClickerLeaderboard.cs` (PP, BPM, prestige and
  medal boards). The server clamps implausible growth instead of rejecting it, keeps a cloud
  save, awards five real profile medals and rejects saves from an older season
  (`old_season`); the season 2 reset wiped every save and record once.
