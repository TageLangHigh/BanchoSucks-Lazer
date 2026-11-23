# BanchoSucks Lazer

Windows x64 client based on GooGuTeam/g0v0 v2026.913.0-g0v0
(fb9d767658b361887830bd475a42efe00721966b).
On top of that base the official osu!lazer changes up to ppy/osu `fe16a2c0` (2026-10-31) were taken
over directly (three-way merge against ppy/osu `c729c6d7`, the state the g0v0 base contains), with
`ppy.osu.Framework` 2026.1030.0 and `g0v0.osu.Game.Resources` 2026.1021.0. g0v0's own framework
fork (`g0v0.Framework`) is not used.

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
The stable-style song select, footer and results screen come from Torii by ShikkesoraSIM
(https://github.com/ShikkesoraSIM/torii-osu, MIT; its textures are part of the g0v0 resources package,
CC BY-NC 4.0), taken over by way of the g0v0 port. See `docs/torii-legacy-port.md`.

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

- **Taken over from g0v0** (not our own work, kept close to their code so later updates stay easy):
  jump to the next / previous miss in replays (`ReplayPlayer`), the customisable toolbar
  (`osu.Game/Overlays/Toolbar/`, four optional icons here because news, changelog and wiki do not
  exist on this server), and the stable look (Settings > User Interface: legacy song select, legacy
  footer, results screen style "Stable (Torii)"; files `Legacy*.cs` under `osu.Game/Screens/Select`,
  `osu.Game/Screens/Ranking` and `osu.Game/Skinning/Select`, hooks in `SongSelect`, `ScreenStackFooter`,
  `ScreenFooter`, `ResultsScreen`, `ScalingContainer`). g0v0's "V2" results screen, ScoreV1/V2 scoring
  and its own framework fork are deliberately not included.
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
- **Practise a section** (song select: right-click a difficulty or Options > "Practise section"):
  `osu.Game/Screens/Banchosucks/Practice/`. `BanchosucksPracticeSetupScreen` picks start, end (sliders over
  the map, shown as mm:ss) and speed (50 to 100 %); `BanchosucksPracticePlayer` plays that section in a loop
  and restarts at the start point when the end point is reached, when everything is judged, or on a fail,
  until the player leaves through the pause menu. It is a plain `Player`, not a `SubmittingPlayer`: no score
  token, no submission, no spectator data, "last played" stays untouched; the local score import is switched
  off, no replay is recorded and there is no results screen. Starting inside a map works like the editor's
  test play. `BanchosucksPracticeSection` holds the arithmetic. Tests: `BanchosucksPracticeSectionTest`,
  `TestSceneBanchosucksPractice`, `TestSceneBanchosucksPracticeNavigation`.
- **Retry from the miss** (fail menu of solo play, between Retry and Quit): `Player` remembers the map time
  of the fail (`BanchosucksFailTime`); the button calls `BanchosucksPracticeLauncher.StartFromFail`, which
  leaves the failed play like Quit and opens the practice setup with the section from five seconds before
  the fail to the end of the map, started right away. Only `SoloPlayer` enables it. Hooks:
  `GameplayMenuOverlay.AddBanchosucksButtons`, `FailOverlay.BanchosucksPracticeFromFail`.
- **Miss review** (results screen > extended statistics): `osu.Game/Screens/Ranking/Statistics/
  BanchosucksMissReview.cs` lists every miss and combo break of a score with its map time and a reason; a
  click opens the replay one second before it if the replay is stored locally (`OsuGame.PresentScore(...,
  banchosucksMissTime)` > `ReplayPlayerLoader` > `ReplayPlayer.BanchosucksInitialMissTime`).
  `BanchosucksMissClassifier` is pure and works on the score's hit events only: too early, not hit in time
  (a later press never reaches the object, so "too late" and "not pressed" cannot be told apart), aim and
  "click did not count" for osu! (via `IBanchosucksHitAreaProvider` on `OsuRuleset`), judged inside the hit
  window (wrong key in taiko, skipped object in osu!), combo breaks on ticks. At most 100 lines.
  Tests: `BanchosucksMissClassifierTest`, `TestSceneBanchosucksMissReview`,
  `TestSceneBanchosucksMissReviewNavigation`.
- **pp if FC** (skin layout editor > `BanchosucksPerformanceIfFcCounter`): an optional HUD counter that shows
  what the run so far would be worth without combo breaks. `osu.Game/Scoring/
  BanchosucksFullComboProjection.cs` holds the rule: misses become the ruleset's best basic result, slider
  breaks become tick hits, mania hold breaks become `IgnoreHit`; 100s, 50s and dropped slider ends stay.
  The pp always comes from the ruleset's own `PerformanceCalculator` through the hook
  `PerformancePointsCounter.AdjustScoreBeforeCalculation`. The test skin `modified-default-20261123.osk`
  covers the component. Tests: `BanchosucksFullComboProjectionTest`,
  `TestSceneBanchosucksPerformanceIfFcCounter`.
- **Daily goal and activity log** (Settings > General > Daily goal): minutes played and/or maps passed per
  day (`OsuSetting.BanchosucksDailyGoalMinutes` / `...Maps`, 0 = off). `osu.Game/Screens/Banchosucks/Stats/
  BanchosucksActivityLog.cs`, a component of `OsuGame`, records every finished real play (no replays,
  autoplay or editor test play; unpassed plays under 10 s are dropped) with end time in local time, active
  seconds, ruleset, passed and accuracy into `<game data>/banchosucks/activity-log.json` (+ `.bak.json`,
  atomic, versioned, 400 days). The file never leaves the PC; it contains the PC's UTC offset, so a later
  feature that uploads any of it would have to strip that. `BanchosucksDailyGoalDisplay` shows today's
  progress and the streak top left on the main menu; reaching the goal posts one notification. Saved
  scores from before the log existed are taken over once as entries marked `estimated`. Tests:
  `BanchosucksActivityLogTest`, `TestSceneBanchosucksDailyGoal`.
- **Personal statistics** (Settings > General > Daily goal > Open personal statistics, or a click on the
  daily goal on the main menu): `BanchosucksStatsScreen` shows the local activity log: totals, bests, the
  goal streak, and bar charts of the last 30 days (`BanchosucksBarChart`). Numbers come from
  `BanchosucksActivityStats`. Tests: `TestSceneBanchosucksStatsScreen`.
- **"No score yet" filter** (song select): the search keyword `score=no` (also `score=nein`, the opposite
  `score=yes` / `score=ja`) limits the list to difficulties without an own local score in the selected
  ruleset; the button next to "Show converts" only writes that keyword into the search box, so the filter
  is always visible, also in the stable look. `BanchosucksLocalScoreTracker` reads the ids of the scored
  difficulties once on the filter thread and keeps them until a score is added or removed. Hooks in
  `FilterQueryParser`, `FilterCriteria`, `BeatmapCarouselFilterMatching`, `FilterControl`. Tests:
  `BanchosucksLocalScoreFilterTest`, `TestSceneBanchosucksNoScoreFilter`.
- **pp preview** (song select, difficulty wedge next to the star rating): `BanchosucksPerformancePreviewLine`
  shows what the selected difficulty is worth with the selected mods and a full combo at 95 %, 98 % and
  100 % accuracy, from the ruleset's own calculators (in osu!mania the server formula), off the update
  thread; 0 or a failure is shown as a dash. `BanchosucksFullComboPlay` turns an accuracy into judgement
  counts without breaking combo: osu! and taiko GREAT to OK only; mania no miss, near 100 % PERFECT to
  GREAT, below that `MANIA_PERFECT_TO_GREAT_RATIO` (2, a modelling choice) PERFECT per GREAT and GOOD for
  the rest; catch only tiny droplets missed. Tests: `BanchosucksFullComboPlayTest`,
  `TestSceneBanchosucksPerformancePreview`.
- **Clean up library** (Settings > Maintenance > Beatmaps): `BanchosucksLibraryCleanupButton` scans the local
  library in the background (`BanchosucksLibraryScanner`, read-only) and shows the counts. Removing needs a
  second, held confirmation that names the number of sets and is the game's normal soft delete.
  `BanchosucksLibraryCleanup` decides: duplicates are intact sets with the same online id or exactly the
  same .osu hashes, every copy with local scores stays, otherwise the most recently imported one; broken
  sets have no difficulty or lack the .osu or the named audio file for every difficulty; sets with at least
  one playable difficulty are only reported. Tests: `BanchosucksLibraryCleanupTest`,
  `TestSceneBanchosucksLibraryCleanup`.
- **Settings backup** (General > Settings backup): exports the player's setup to one JSON file in `exports`
  (`banchosucks-settings-<date>.json`, format version 1) and imports such a file again. Content: allow-listed
  game settings, frame limiter / threading / volumes, mouse sensitivity and raw input, key bindings of all
  rulesets and the global actions, per-ruleset settings, saved key profiles and the name of the selected
  skin. What may travel is listed by hand in `osu.Game/Configuration/BanchosucksSettingsAllowList.cs`;
  account, server address, Discord application id and anything describing the PC are never written and
  never read. A new `OsuSetting` is NOT exported until it is added to that list. An import shows what
  changes (`BanchosucksChangePreview`), asks once and first writes the current setup to
  `<game data>/banchosucks/settings-backup.json`, which "Restore backup" brings back. Code:
  `BanchosucksSettingsDocument`, `BanchosucksSettingsDiff`, `BanchosucksSettingsTransfer`,
  `BanchosucksSettingsBackupSettings`. Tests: `BanchosucksSettingsExportTest`,
  `TestSceneBanchosucksSettingsBackup`.
- **Key profiles** (Input > key bindings > a ruleset, button above each variant): saves the bindings of one
  ruleset and variant under a name, switches between saved profiles and deletes them
  (`osu.Game/Input/BanchosucksKeyProfileManager.cs`, `BanchosucksKeyProfileButton.cs`). Profiles live in
  `<game data>/banchosucks/key-profiles.json` and share their text form with the settings backup. A switch
  says how many keys change, asks once and keeps the replaced bindings as the profile "Sicherung / Backup".
  Tests: `BanchosucksKeyProfilesTest`, `TestSceneBanchosucksKeyProfiles`.
- **Take over osu!stable settings** (General > Take over from osu!stable, desktop): reads
  `osu!.<Windows user>.cfg` of the stable installation (found like the stable import; read-only) and offers
  key bindings for all four rulesets, universal offset, mouse sensitivity and raw input, the three volumes,
  cursor size, background dim and the skin if one of that name is imported. Mapping and its reasoning are
  in `osu.Game/Configuration/BanchosucksStableConfig.cs`: offset in ms with inverted sign, percent to
  fractions, XNA key names into the first binding slot. Only the listed keys are parsed, so the stable
  account in that file is never read. The stable key names were written from memory and are only tested
  against a hand-made sample cfg: check against a real file before relying on it. Tests:
  `BanchosucksStableConfigTest`, `TestSceneBanchosucksStableSettings`.
- **Skin sync from osu!stable** (General > Take over from osu!stable, off by default,
  `OsuSetting.BanchosucksSkinSync`): `osu.Game/Skinning/BanchosucksSkinSync.cs` looks at the stable `Skins`
  folder 20 s after startup, every ten minutes and on "Check skins now", and imports folders that are new
  or whose files changed through the normal skin importer. `BanchosucksSkinSyncRecord` keeps folder name,
  newest file time, total size and file count in `<game data>/banchosucks/skin-sync.json`. Stable is only
  read and nothing runs during gameplay. A changed folder behaves like a manual re-import: same skin.ini
  replaces the previous import (the selection follows), a changed skin.ini adds a second skin. Tests:
  `BanchosucksSkinSyncTest`, `TestSceneBanchosucksSkinSync`.
- **Do not disturb** (General > Comfort and streaming, on by default): while the player is in a map
  (playing, break or pause) notifications that are neither critical, errors nor running tasks are held by
  `BanchosucksNotificationGate` and, once the map is left, put into the notification overlay silently and
  announced by one summary toast. Rules and bookkeeping are the pure `BanchosucksNotificationHold<T>`; the
  hook is `NotificationOverlay.Post` / `NotificationOverlay.Banchosucks.cs`. Tests:
  `BanchosucksNotificationHoldTest`, `TestSceneBanchosucksDoNotDisturb`.
- **Streamer mode** (General > Comfort and streaming, off by default; rebindable key without default,
  `GlobalAction.BanchosucksToggleStreamerMode`): the chat overlay cannot be opened
  (`ChatOverlay.Banchosucks.cs`); private message, mention and friend notifications are held by the same
  gate and summarised when the mode ends; multiplayer invitations and duels show no player name, room name
  or avatar; the account field of the login form shows dots (`BanchosucksStreamerTextBox`). Server
  announcements still appear. Not hidden: chat in multiplayer rooms and gameplay, names on leaderboards,
  spectator list, dashboard and profiles. Component: `osu.Game/Overlays/BanchosucksStreamerMode.cs`.
- **Stutter report** (General > Comfort and streaming, on by default): `osu.Game/Performance/
  BanchosucksStutterMonitor.cs` records update and draw thread frame times while the player is actively
  playing (histogram, no allocation per frame) and posts one notification on the results screen when there
  were noticeable stutters: frames above 25 ms and longer than three times the thread's median, at least
  three of them or one of 100 ms, with a hint when a known cause applies. Analysis and texts:
  `BanchosucksStutterAnalysis.cs`.
- **What's new**: the files `release-notes/<version>.md` are embedded into osu.Game
  (`banchosucks-release-notes/<version>.md`). After an update the client shows the notes of the running
  version once when the main menu comes up (`osu.Game/Updater/BanchosucksWhatsNewOverlay.cs`), in German or
  English by client language (`OsuSetting.BanchosucksWhatsNewShownVersion`). The notes file must be
  committed before the release is built. Parsing: `BanchosucksReleaseNotes.cs`.
- **Overlay data source** (General > Comfort and streaming, off by default): the client answers tosu's v2
  API itself so stream overlays written for tosu work: `ws://127.0.0.1:24050/websocket/v2` (document on
  connect and on change, at most ten per second) and `GET /json/v2`, loopback only, through the game's own
  `WebSocketServer` (`RequestFilter`). `osu.Game/IPC/BanchosucksOverlayDataSource.cs` collects the state,
  `BanchosucksOverlayJson.cs` is the only place with tosu's field names (written from memory and not yet
  checked against tosu or a real overlay; see its remarks for the unsure ones), `BanchosucksOverlayServer.cs`
  sends. Never contains password, tokens, e-mail, IP, country, chat or local paths. Web pages that are not
  local are refused by origin. Overlay files in `<game data>/banchosucks-overlays/<name>/` are served at
  `http://127.0.0.1:24050/<name>/`. tosu cannot run at the same time (same port).
- **Render replays as video** is also offered on the results screen (camera button,
  `BanchosucksRenderVideoButton`) and in the context menu of scores in the profile overlay
  (`BanchosucksProfileScoreActions`). Requests no longer get refused while a render runs:
  `BanchosucksRenderQueue` lets up to five wait, each with its own notification showing its place; closing
  the notification of a waiting request removes it. osu!standard only.
- **Beatmap previews in chat** (`osu.Game/Overlays/Chat/BanchosucksChatBeatmap*.cs`,
  `osu.Game/Online/Chat/BanchosucksChatBeatmap*.cs`): a message with a link to a beatmap or set on our
  website, or an osu://b, osu://s or osu://dl link, gets a compact card under it, built from the beatmap
  listing's card parts. `BanchosucksChatBeatmapLookup` asks our own API once per set, throttled by
  `BanchosucksLookupThrottle` (one request per 400 ms, two at a time), and only for lines that have been on
  screen for half a second. Off switch: `OsuSetting.BanchosucksChatBeatmapPreviews` (Online > Alerts and
  Privacy).
- **Share cards** (`osu.Game/Screens/Banchosucks/Share/`): the results screen has a button that saves a
  1200 x 630 picture of the score, and the profile overlay one for the profile. `BanchosucksCardExporter`
  renders the card off screen (`BanchosucksDrawableCapture`: a buffered draw node plus
  `IRenderer.ExtractFrameBufferData`), saves it as PNG under `screenshots/cards` and copies it to the
  clipboard; the client uploads nothing. A marker strip below the card tells top from bottom after the
  read-back; images without it are discarded. The read-back has never run on a real renderer (the headless
  test host does not draw): this is the first thing to try by hand. The profile card uses only what the
  overlay has loaded; the best play is the first entry of the "best performance" list
  (`BanchosucksProfileBestScores`).
- **Replay comparison** (osu!standard): on a leaderboard, mark one score and pick a second one on the same
  beatmap (`BanchosucksReplayCompare`). The first replay plays normally; the second is drawn as a ghost
  cursor by `osu.Game.Rulesets.Osu/UI/BanchosucksGhostCursorOverlay.cs` (drawing only, fed by
  `BanchosucksGhostTrack`, never by an input handler). `BanchosucksReplayComparison` refuses pairs whose
  playfields would not match (different map version; HR, EZ, MR, DA, RD, TP, DP, WG, TR or rate mods that
  differ; AS, MG, RP always). The ghost travels through `ReplayPlayer.BanchosucksGhostScore` and
  `OsuGame.PresentScore(..., banchosucksGhostScore:)`.
- **"Gives pp" in mod select** (mod select overlay, bottom right; "Unranked" badge on song select's Mods
  button): `osu.Game/Online/Banchosucks/BanchosucksRankedModRules.cs` says whether the selected mods give pp
  on this server and why not: Classic and Double Time at every speed (1.01x to 2.00x) give pp, Difficulty
  Adjust in osu!, taiko and catch inside the plugin's limits, osu!mania only with the neutral mod list and
  never on converts; every other mod follows `Mod.Ranked`. It is a hand-written mirror of the plugins
  `banchosucks_rankedmods/rules.py`, `banchosucks_stablescores` (Classic) and `banchosucks_maniapp`
  (NEUTRAL_MODS) and has to be changed together with them. Open point: Hard Rock and Constant Speed in
  mania follow the neutral list here; check against the server's `ranked_mods.json`.
- **Stable badge** (song select leaderboard rows, results panel): `BanchosucksStableBadge` shows "STABLE" on
  scores imported from the stable server. `BanchosucksScoreOrigins` asks
  `banchosucks_stablescores/origin/{id}`, one score per request, so `BanchosucksScoreOriginCache` keeps it
  sparse: only scores on screen, only scores with Classic and no mod settings (every import looks like
  that), answers kept for the session, two requests at a time and four per second at most, nothing during
  gameplay. A batch endpoint would only change `BanchosucksScoreOrigins.Update`.
- **Top plays of a player** (profile overlay > Ranks > Best Performance): `BanchosucksTopPlaysActions` offers
  "download missing maps" (sets of the listed top plays, or all 100, that are not in the library; one
  confirmation with the count; `BanchosucksBeatmapDownloadQueue`, two at a time, nothing during gameplay)
  and "top plays as collection" (creates or extends the local collection "Top-Plays von NAME" with the
  difficulties' MD5 hashes, including maps not downloaded yet; only ever adds).
- **Server status in the toolbar** (right of the clock): `BanchosucksServerStatusIndicator` shows the api
  connection as a dot and the round-trip time of the last status request; the tooltip adds players online,
  open rooms and the daily challenge from `banchosucks_status/summary`. One request every two minutes while
  the toolbar is visible, never more than one per minute (`BanchosucksRefreshGate`). A countdown to a
  planned restart appears once the plugin sends `maintenance_at` / `maintenance_message` (not yet).
- **Weekly tile** (main menu, top left under the daily goal): `osu.Game/Screens/Menu/BanchosucksWeeklyCard.cs`
  names the map of the week and the community goal's progress; a click opens a panel with the map (show in
  song select or download) and the goal. Data from `banchosucks_motw/current` and
  `banchosucks_goal/current`, every five minutes while visible. Mod of the week has a marked place in the
  code and needs an endpoint on `banchosucks_modweek`.
- **Medal overview** (profile overlay > Medals, switched on here): `BanchosucksMedalList` lists the server's
  catalogue (`banchosucks_medals/catalogue`, kept for the session) grouped by origin (Banchosucks, g0v0,
  osu!) with the shown player's medals highlighted and counted. The catalogue has no conditions or
  progress, so a medal is achieved or not.
- **Server endpoints the client is prepared for but which do not exist yet**: `banchosucks_modweek/current`;
  `banchosucks_stablescores/origin?ids=` (batch) or a flag on leaderboard scores; `maintenance_at` and
  `maintenance_message` in `banchosucks_status/summary`; `grouping`/`ordering` in the medal catalogue and
  `banchosucks_medals/progress`; `banchosucks_rankedmods/rules` to replace the hand mirror.
- **Warm-up** (song select: Options > Warm-up > Start warm-up, or the rebindable action "Warm-up" without a
  default key): `osu.Game/Screens/Banchosucks/Warmup/`. `BanchosucksWarmupController` reads the player's
  own local scores of the last 42 days in the selected ruleset and the local library;
  `BanchosucksWarmupSelector` (pure) takes the 75th percentile of the star ratings of maps passed with at
  least 90 % accuracy as the level (at least 10 needed) and draws one map each around 70 %, 80 % and 90 %
  of it: 1 to 4 minutes long, not played today, three different sets. With too few scores or no fitting map
  a notification says what is missing and nothing starts. `ResultsScreen` shows
  `BanchosucksWarmupNextButton` while a series runs. Nothing is requested from or sent to the server.
- **Lobby templates** (multiplayer room settings > Templates): `osu.Game/Screens/Banchosucks/Lobby/
  BanchosucksLobbyTemplate*.cs`. "Save as template" stores room name (placeholders `{user}`, `{date}`), game
  type, queue mode, auto start, intro skipping and the waiting queue under a name in
  `<game data>/banchosucks/lobby-templates.json`. The format has no field for the password. "Load template"
  asks the server for the maps through the existing beatmap lookup, shows what will change and asks once;
  `BanchosucksLobbyTemplateManager` then changes a hosted room only through
  `MultiplayerClient.ChangeSettings` and `AddPlaylistItem`. Nothing of the multiplayer client is bound.
- **Lobby tournament tools** (multiplayer room, button "Tournament tools" above the chat, host only):
  `BanchosucksLobbyToolsPanel` offers three helpers that run on the host's client and announce their result
  in the room chat as ordinary messages from the host. Draw teams (`BanchosucksLobbyDraw`, seeded and
  repeatable; in team versus only the host's own team is set, the game has no way for a host to move other
  players), roll (1 to 100 for ticked players, or "Send !roll" for the server bot), ban/pick board
  (`BanchosucksBanPickBoard`, maps from the queue or a lobby template). Nothing here changes the room, a
  score or a result.
