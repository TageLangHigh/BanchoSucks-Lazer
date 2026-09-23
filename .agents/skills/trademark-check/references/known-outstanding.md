# Already-triaged trademark items (g0v0 fork)

Read this before reporting findings so long-standing items are not presented as new. Everything here
was reviewed once already; the human decided what to do (or to leave it).

## Not findings — never report these as trademark leakage

| Item | Why it stays |
|---|---|
| MIT headers: `Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.` on unmodified upstream files, `Copyright (c) GooGuTeam.` on fork-created files, `Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam.` on fork-modified files | Required attribution; British-spelled (`Licence`). Upstream files' second line points at `LICENCE-OSU`, the ppy licence in this fork. |
| `PackageReference Include="ppy.osu.Framework"` / NuGet package id `g0v0.osu.Game.Resources` | 上游 framework 身份保留；资源包是 fork 自己发布的 NuGet 身份（上游对应包 `ppy.osu.Game.Resources` 仅作来源说明）。 |
| NuGet `PackageId` such as `g0v0.osu.Game`, `PackageProjectUrl` → GooGuTeam | Already rebranded; the `osu` inside is the codebase identity. |
| Namespaces, type and file names `osu.Game.*`, `OsuMod*`, `osu.iOS`, `osu.Android` | Code identity, not branding. Renaming them is a breaking change nobody asked for. |
| `.osu` file format, `osu!stable`, `osu!direct`, `osu!supporter`, `osu! wiki`, game mode names in comments and docs | Nominative references to the original game, explicitly allowed by `README.md`. |
| Medal asset ids `assets/medals/**/osu-*.svg`, `all-intro-*.svg` | Internal file ids, not displayed branding. |
| `LICENCE.md`, `LICENCE-OSU`, `README.md` attribution and the "not affiliated with / endorsed by ppy Pty Ltd" notice | Deliberate legal notices. |
| `Copyright`/`Authors`/`Company` fields crediting ppy Pty Ltd alongside GooGuTeam | Attribution in package metadata. |
| Translated strings in `g0v0-resources/osu.Game.Resources/Localisation/*.resx` mentioning osu! (hundreds, e.g. `BeatmapOverlayStrings.*.resx` "Featured Artists … osu!") | Community translations of upstream text; the game *is* about osu! beatmaps. Low priority, never bulk-rewrite. |

## Pre-existing leakage, reviewed and deliberately left (as of 2026-09-10)

| Location | Item | Note |
|---|---|---|
| `assets/lazer.png`, `assets/lazer-nuget.png` | Upstream osu! logo (byte-identical, same md5) used as NuGet `PackageIcon` in `osu.Game/osu.Game.csproj` | Commit `5a37771179` updated Desktop/Android/iOS icons but missed these two. Human deferred. |
| `Templates/Rulesets/ruleset-example/**`, `Templates/Rulesets/ruleset-scrolling-example/**` | `Pippidon` sample ruleset, incl. `Resources/Textures/character.png` (pippi art) | Upstream template content; human deferred. |
| `g0v0-resources/osu.Game.Resources/Skins/Legacy/pippidonclear.png` | Filename still pippi-branded; content already replaced by a placeholder | Content clean, name retained. |
| `g0v0-resources/osu.Game.Resources/Textures/Online/supporter-pippi.png` | Filename still pippi-branded; content already replaced with g0v0 art | Content clean, name retained. |
| `g0v0-resources/osu.Game.Resources/Samples/Intro/Welcome/*.mp3`, `Samples/Intro/*.mp3` | Intro stingers kept while intro tracks/backgrounds were removed | Human deferred. |

## Cleanup already completed — do not redo

Parent repo (precedent diffs worth reading before doing similar work):

- `54cf5c46da` Rename all things related to "osu!" to "g0v0!" — user-visible strings, endpoints, settings
- `4b343f28f3` Replace ruleset name to remove "osu!" — ruleset display name `osu!` → `standard`
- `2ed0779764` Remove and replace upstream related contact links
- `b3d19a77ca` Update all github urls
- `51e4771e99` Update logo, `5a37771179` Update icons for Desktop/Android/iOS
- `cad30950b3` Remove all commercial fonts and add open-source alternative
- `0e39dcb4f4`/`c0981a376d` Remove intro settings and Retro skin
- `705965928c` Change settings header from "osu!" to "standard"

`g0v0-resources` repository（独立 clone）:

- Mascot art replaced: `Skins/Legacy/comboburst@2x.png`, `fruit-catcher-*@2x.png`
- Logo/wordmark replaced: `Textures/Icons/Logo.png`, `Textures/Menu/logo.png`
- Online art replaced (`Textures/Online/not-found.png`, `supporter-required.png`, `avatar-guest.png`) and
  `Textures/Online/RankedPlay/**` deleted (8 files)
- Intro removed: `Textures/Intro/**` (11 files), `Tracks/*.osz` (4 files)
- Commercial fonts removed (27 files) → open-source set: Inter, KTXPYingRound, MapleMono, MgenPlus, Noto
- Retro skin deleted (114 files)
- Backgrounds replaced: `Textures/Backgrounds/registration.*`, `bg3.*`

## How to re-verify

```bash
.agents/skills/trademark-check/scripts/detect.sh --incoming
```

Then compare its output against the tables above, group findings as *introduced by this change* vs
*pre-existing*, and ask the human before changing anything.
