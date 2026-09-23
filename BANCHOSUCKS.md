# BanchoSucks Lazer

Windows x64 client based on GooGuTeam/g0v0 v2026.913.0-g0v0
(fb9d767658b361887830bd475a42efe00721966b).

Website: https://lazer.banchosucks.cc
API: https://lazer-api.banchosucks.cc

Extract the entire ZIP, then run BanchoSucks-Lazer.exe. Register on the website
and use that account in the client. No server URL needs to be entered.
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
dotnet publish osu.Desktop/osu.Desktop.csproj -c Release -r win-x64 --self-contained true -o ../artifacts/BanchoSucks-Lazer-win-x64 -p:Version=2026.913.0 -p:AssemblyVersion=2026.913.0 -p:FileVersion=2026.913.0 -p:InformationalVersion=2026.913.0-banchosucks
```

The server must allow the MD5 hash of the resulting osu.Game.dll in its client
version list. This identifies the exact build; it does not prove trustworthiness.
