# BanchoSucks Lazer

Windows x64 client based on GooGuTeam/g0v0 v2026.913.0-g0v0
(fb9d767658b361887830bd475a42efe00721966b).

Website: https://banchosucks.cc/lazer (lazer profiles, rankings and beatmaps live
under the /lazer prefix of the main Banchosucks site; the client's links point there)
Lazer web interface: https://lazer.banchosucks.cc
API: https://lazer-api.banchosucks.cc

Install with BanchoSucksLazer-win-Setup.exe (installs to %LocalAppData%\BanchoSucksLazer and
adds a start menu entry). From then on the game updates itself like osu!stable: on every start
it asks our GitHub releases (mirror as fallback) and installs a newer version right away, with
a popup and progress bar in the loader, then restarts into it (`osu.Game/Screens/Loader.cs`,
`osu.Game/Updater/StartupUpdate.cs`, `osu.Desktop/Updater/VelopackUpdateManager.cs`). During a
session it checks every 30 minutes, downloads in the background and shows a notification whose
click restarts into the new version. Game data and settings are not touched by updates.
Releases 2026.913.0 to .14 never checked at all (`OsuGameDesktop.CreateUpdateManager` returned
the no-op base class, found 2026-10-17); they have to be reinstalled once with the setup.
Accounts are created on
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

- **Mania star rating and pp** (`osu.Game.Rulesets.Mania/Difficulty/Banchosucks/`): `ManiaRating` is a
  line-by-line port of the server's Python package (bancho.py `app/maniadiff`, sunnyxxy's Star Rating
  Rebirth, 1-10 keys); `ManiaDifficultyCalculator` uses it for mania-specific maps (converts keep the
  official calculation) with the map's own OD and the clock rate rounded to two decimals, and
  `ManiaRatingProgress` serves the timed attributes of the in-game pp counter from one full run.
  `ManiaPerformance` is the server's pp formula (8.68 multiplier, quadratic accuracy term from 80 %,
  NF 0.75, EZ 0.5, neutral mod list, everything else and converts 0 pp). Server and client must stay
  identical: change both, bump `ManiaRating.ALGORITHM_VERSION` / `ManiaDifficultyCalculator.Version`
  (the client then recalculates every stored mania star rating once). Reference values from the
  Python package: `osu.Game.Rulesets.Mania.Tests/BanchosucksManiaRatingTest.cs`.
- **Stored star ratings** (`osu.Game/Database/BackgroundDataStoreProcessor.cs`): song select sorts and
  groups by the star rating stored in the local database, the panels show a freshly calculated one.
  `verifyBanchosucksStarRatings` recalculates every stored mania rating once per
  `BANCHOSUCKS_STAR_RATING_CHECK` and writes the ones that differ, in place and resumable (position in
  `OsuSetting.BanchosucksStarRatingCheckPosition`); bump the constant to run it again on every client.
  The background processing looks every second whether gameplay has ended (upstream: every 30 s), so
  it gets through a large library between maps. Tests:
  `osu.Game.Rulesets.Mania.Tests/BanchosucksStoredStarRatingTest.cs`.
- **Input / audio rate** (Graphics > Renderer): 1000 to 8000 Hz for the input and audio threads,
  kept applied by `osu.Game/Graphics/BanchosucksThreadRateManager.cs`.
- **Menu opacity** (User Interface > General): alpha of menu screens; gameplay and the editor
  always stay opaque (`OsuGame.applyMenuOpacity`).
- **Boss key** (Pause by default, rebindable under Input > Global): minimises the window and mutes
  the game; gameplay pauses through the normal focus-loss handling and the mute is lifted when the
  window is active again (`OsuGame.bossKey`).
- **Presets** (General > Presets): "Smooth" (frame limiter 2x, multithreading, WASAPI, raw input,
  storyboard / video / hit lighting / blur off, thread rates back to default) and "Stable feel"
  (classic skin, Classic mod, which is ranked on this server, classic score display, beatmap skins /
  colours / hitsounds, key overlay, no hold-to-confirm delay): `osu.Game/Configuration/BanchosucksPresets.cs`
  and `osu.Game/Overlays/Settings/Sections/General/BanchosucksPresetSettings.cs`. A one-time
  notification points at them after the update (`OsuSetting.BanchosucksPresetsHintShown`).
- **Render replays as video** (right-click a score on the song select leaderboard):
  `osu.Game/Scoring/BanchosucksReplayRenderer.cs` renders with danser-go on the player's PC.
  danser-go (GPL-3.0, bundles FFmpeg) is not shipped with the client. It is downloaded from
  its GitHub release only after the player agrees, checked against a pinned SHA-256 and kept
  in `<game data>/banchosucks-renderer/`. Antivirus programs sometimes quarantine its
  ffmpeg; the client then says so.
- **Minigames** (main menu > Play > minigames): `osu.Game/Screens/Banchosucks/`, currently
  "osu! Clicker" (season 3 since 2026-09-29), an idle clicker in osu! style. The game itself is
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
  (floor(cbrt(lifetime PP / 1e11))) spent in a 34-node tree and on three prestige buildings
  that survive rebirths, respec for 10 % of the points. The prestige bonus grows linearly up to
  50 points and with the square root beyond (season 2 showed the linear bonus feeding on itself:
  two players finished everything in hours).
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
  (`old_season`); the season 2 reset (2026-09-27) and the season 3 rebalance reset (2026-09-29) each wiped
  every save and record once.
- **Private Server Tycoon** (main menu > Play > minigames, second tile, since 2026.913.15):
  `osu.Game/Screens/Banchosucks/Tycoon/`, an idle game built like a tycoon game: the plot is the game.
  Buy pads on the floor show the next building and its price, green when affordable, red when not
  (long names shrink to fit, the full text is in the tooltip); buying turns the pad into the building
  and unlocks the next pad. Marketing needs matching hardware (Reddit from the laptop, YouTube from
  the Mini PC, streamers from the VPS, tournaments from the dedicated server, campaigns from the
  second server, influencers from the cluster), so nobody buys YouTube videos for a Raspberry Pi;
  a pad locked that way shows the hardware it needs. Players leave at 0.25 %/s, 8 %/s above capacity,
  10 % over capacity is tolerated, lag costs 1.5 reputation per second per 100 % overload (capped at 5).
  The player runs an osu! private server:
  marketing (Discord, Reddit, YouTube, streamers, tournaments, campaigns, influencers; cost x1.15
  per piece) brings players, players donate (0.05 per player and second times a reputation factor
  0.5 to 1.5), donations buy hardware (nine tiers from Raspberry Pi with 10 players to cloud with
  200,000; above capacity players leave at 5 %/s of the excess and the reputation drops), twelve
  one-time features (donation and inflow multipliers, Discord bot for faster reputation recovery,
  anticheat/backups/Cloudflare/tests that turn the bad events harmless). A post in the Discord
  (button or Space) adds players by hand. Events every 75 to 150 s: cheater wave (ban or ignore
  without anticheat), DDoS, database crash (rebuild from logs or shut down without backups),
  streamer and weekend boosts, broken deploy ("Illegal mix of collations"), a medal crashing score
  submission, a generous donor, a lazer release. Season restart from one million donations in a
  season: legacy = floor(sqrt(run total / 1e6)), +5 % on donations and inflow per point, everything
  else starts over. `TycoonRules.cs` holds the economy, mirrored by the plugin
  `banchosucks_tycoon/economy.py`; the save (JSON, see `TycoonState`) goes to the plugin every 30 s
  and on leaving, plus a local file `banchosucks-tycoon.json` in the osu! storage for offline play;
  on load up to eight hours of offline progress are simulated. The plot (20x13 cells): the gate on
  the left, the walkway in the middle, the seven marketing buildings (the droppers) above and below
  it with their buy/upgrade pads, the server block (grows from a Pi to rows of racks, LEDs blink with
  load, red when lagging) and the collector box on the right, thirteen feature kiosks along the
  bottom (sixteen features), eleven decoration pads (ten along the top, one by the gate; comfort
  +0.5 % donations per point, capped at +15 %; right click sells for half), a boost machine left of
  the lower droppers with three consumables bought with cash and on a four-minute cooldown (pizza:
  donations x1.5 for 3 min, giveaway: inflow x2 for 2 min, maintenance: reputation +20; client only,
  the cash just goes down). Players walk out of the droppers along the walkway into the servers;
  donations drop as coins into the collector box (`pending` in the save) and are collected by
  clicking it, or automatically with the auto collector feature. The team office (from the Mini PC
  on): moderators, developers, community managers and designers (comfort +2 per head), five per
  role, every hire doubles the price, small multipliers per head (`staff` in the save, mirrored in
  economy.py). The hint arrow only shows before the first hardware upgrade and points at the laptop
  first. The tab is called Prestige; a season is one run. Eighteen quests in a
  fixed order guide the run (`TycoonQuests.cs`; `quest` in the save is the index of the next one and
  survives seasons), rewards are seconds of income or a boost. Supply drops land next to the walkway
  every few minutes and give a random reward when clicked within 25 seconds. Life on the plot
  (`TycoonScreen.Extras.cs`): a four-minute day and night with lamps, chatter bubbles, a trophy
  shelf for the milestones, a floor that changes with the hardware, smoke and a LAG sign when
  overloaded, event effects (cheaters walking in and getting banned, DDoS rings, sparks, confetti,
  coin showers, cloud shadows, bobbing avatars). Legacy thresholds unlock perks for good (`LegacyPerks`,
  mirrored in economy.py): quick start (laptop and 100 donations, counted as earned), 10 % off
  marketing, 15 % off hardware (integer percentages so both sides round alike), 50 regulars at the
  start of a season, 12 hours of offline progress; seasons carry titles and stars on the owner sign,
  the restart plays an animation. Side panel: a help overlay ("How to play"), Shift buys ten marketing
  buildings at once, the income line has a tooltip with every factor, the stats tab starts with an
  income graph (last five minutes), a Season tab holds the season title, legacy, the progress to the
  next legacy point, the restart button and the perks (a one-line summary with a bar stays at the
  bottom of the panel; hidden event cards take no room, so the panel fits 720p), and an Audio tab
  binds the osu! master, music and effect volumes with music controls. The plot keeps a margin above
  and below the grid so the top row's labels are not under the border. Two
  sounds (`UI/notification-pop`, `UI/check-on`). The "autopost" feature lets the bot post half a click
  per second (part of the inflow, mirrored). Three bad events (DDoS, broken deploy, medal crash) are
  crises: the server block wants eight clicks within ten seconds for an emergency reboot, which halves
  the damage (`TycoonEvent.Crisis` with severity 1 or 0.5). A well-known player walks the conveyor
  every few minutes, a click gets an autograph and a reward; a drone delivers the supply crate; dust
  flies when something is built; the cash and player numbers roll; a bouncing arrow points at the
  cheapest affordable pad when nothing was bought for 40 s; the owner sign shows the player's avatar.
  The layout and the team are part of the save and of the server's spending check. The server bounds what a save may have earned since the last one
  and refuses saves that spent more than they earned. A stats tab lists play time, posts, events,
  seasons, team, quests and sixteen milestones. Leaderboards
  by lifetime donations, player record and seasons in the screen and on banchosucks.cc/lazer/tycoon.
  Tests: `osu.Game.Tests/NonVisual/BanchosucksTycoonRulesTest.cs` (same numbers as the plugin
  tests) and `TestSceneBanchosucksMinigames`.
- **Background Guesser** (minigames, fourth tile, since 2026.913.15): `osu.Game/Screens/Banchosucks/Guess/BackgroundGuessScreen.cs`,
  the second game on the shared `GuessScreen`: the set's background is shown close up and zooms out over twelve
  seconds, four titles are offered; library mode uses `WorkingBeatmap.GetBackground()`, popular mode the official
  `cover@2x.jpg` of the pool's sets. Same rules, points and plugin, boards `background` and `background_endless`.
- **Song Guesser** (minigames, third tile, since 2026.913.15): `osu.Game/Screens/Banchosucks/Guess/`.
  Four answers each round, 15 s per round; a correct answer gives 100 points plus 10 per second left,
  streaks add up to 50. Library mode (board `song`, ten rounds) plays the preview point of one of the
  player's own beatmap sets (the global music pauses, the cover and title appear once answered) and
  needs at least four sets with different songs. Popular mode (board `song_endless`) asks about the
  server's pool of the most played sets on banchosucks until the first mistake: the client streams the
  official preview by set id through `PreviewTrackManager` and shows the cover from assets.ppy.sh once
  answered. Overlays are closed during a round so the toolbar cannot spoil the answer. `GuessRules.cs` is mirrored by the plugin
  `banchosucks_guess/rules.py`, which only bounds results: best run per player and board,
  leaderboards in the screen and on banchosucks.cc/lazer/guess. Tests:
  `osu.Game.Tests/NonVisual/BanchosucksGuessRulesTest.cs` and `TestSceneBanchosucksMinigames`.
