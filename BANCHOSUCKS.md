# BanchoSucks Lazer

Windows x64 client based on GooGuTeam/g0v0 v2026.913.0-g0v0
(fb9d767658b361887830bd475a42efe00721966b).

Website: https://banchosucks.cc/lazer (lazer profiles, rankings and beatmaps live
under the /lazer prefix of the main Banchosucks site; the client's links point there)
Lazer web interface: https://lazer.banchosucks.cc
API: https://lazer-api.banchosucks.cc

Extract the entire ZIP, then run BanchoSucks-Lazer.exe. Accounts are created on
https://banchosucks.cc/register and work on both the stable and the lazer server
(the in-game register button opens that page). No server URL needs to be entered.
Keep the Custom API Server URL setting empty to use the built-in endpoints.

Game data is stored separately in %APPDATA%/banchosucks-lazer.
This edition does not automatically replace itself with upstream updates.
Updates are installed manually. The executable is not code-signed.

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
- Discord Rich Presence still uses g0v0's Discord application id (`osu.Desktop/DiscordRichPresence.cs`);
  a Banchosucks Discord application with the `osu_logo_lazer` and `mode_*` assets is needed to change that.
