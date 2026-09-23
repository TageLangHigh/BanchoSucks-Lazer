---
name: trademark-check
description: Detect osu!/ppy trademark and brand content that a sync from ppy/osu or ppy/osu-resources introduced into the g0v0 fork, report it as an inventory, and remove nothing before the human confirms. Use when syncing from upstream ppy/osu, after syncing/releasing the separate GooGuTeam/g0v0-resources repository, before cutting a v*-g0v0 release tag, when adding user-visible strings or image assets, or when auditing existing trademark leakage.
---

# Trademark change detection (g0v0 fork)

This fork of `ppy/osu` ships as **g0v0!** and must not present osu!/ppy branding as its own.
Attribution that licences require is *not* branding and must stay.

## Non-negotiable rule: confirm before removing

**Never delete, rename, or rewrite trademark content on your own initiative.** The project owner
requires explicit human confirmation first:

> 删除商标内容前和我确认

So the deliverable of this skill is always an **inventory + recommendation**, followed by a
question. Only after the human approves a scope (whole list, a subset, or none) do you change files.
Detection is read-only.

Also scope the work honestly: distinguish what *this change* introduced from what was already
there. Report pre-existing leakage separately and never present it as a new finding.

## What must be cleaned vs what must stay

| Clean (branding) | Keep (attribution / nominative / internal) |
|---|---|
| `osu!` in user-visible strings → `g0v0!` (ruleset display name: `osu!` → `standard`) | MIT file headers `Copyright (c) ppy Pty Ltd <contact@ppy.sh>` — required attribution |
| Mascots: pippi, pippidon, comboburst, fruit-catcher art | NuGet package ids `ppy.osu.Framework` (upstream identity, used as-is) and `g0v0.osu.Game.Resources` (fork identity, published separately) |
| osu! logo / wordmark, online promo art (`supporter-*`, `not-found`, `RankedPlay`) | Namespaces, type names, file names such as `osu.Game.*` — code identity, not branding |
| Intro tracks, intro backgrounds, seasonal intros, retro skin | `.osu` file format, `osu!stable`, `osu!direct`, `osu!supporter`, `osu! wiki`, game mode names — nominative references |
| Commercial fonts (Venera etc.) | Internal asset ids such as `assets/medals/*/osu-*.svg`; `LICENCE.md` / `README.md` attribution and the "not affiliated with ppy Pty Ltd" notice |
| Upstream contact/promo links (`github.com/ppy/osu`, `osu.ppy.sh`, `ppy.sh`) in code, CI, docs | Copyright / `authors` / `company` fields that credit ppy Pty Ltd |

Why headers stay: MIT requires the copyright notice to be preserved. A newly added `.cs` file
therefore legitimately contains `contact@ppy.sh`; that is **not** a finding. This is the single
biggest source of false positives — always exclude comment lines when scanning.

## Procedure

Run the bundled script for the mechanical part; it never writes:

```bash
.agents/skills/trademark-check/scripts/detect.sh                 # auto-pick the pre-change base
.agents/skills/trademark-check/scripts/detect.sh --base <ref>    # explicit base
.agents/skills/trademark-check/scripts/detect.sh --incoming      # also scan unmerged upstream/master
```

Then:

1. **Establish the base.** "Introduced by this change" means `<base>..HEAD`. Pass `--base <ref>`
   when you know it; otherwise the script falls back to `git merge-base HEAD upstream/master`.
   (The fork used to keep `backup/*` pre-sync tags as the preferred base; they were removed in
   2026-09, so **tag the tree yourself before starting a sync** if you want an exact base.) If no
   base can be determined, ask which commit the change started from instead of guessing.
2. **Scan before merging** with `--incoming`: `git diff HEAD...upstream/master` shows what upstream
   would bring in. Do this *before* resolving conflicts so branded strings can be dropped during the
   merge rather than re-added and then cleaned.
3. **Scan after merging.** Only *added* lines matter (`^+`, excluding `+++`), and comment-only lines
   are attribution noise. The script filters both.
4. **Inventory brand assets in the separate resources repo.** Assets live in a separate clone of
   `GooGuTeam/g0v0-resources` (with its own `upstream` remote `ppy/osu-resources`), not in this
   repository. Compare that tree against its `upstream/master`; `detect.sh` reads it from
   `../g0v0-resources` or a local `osu.Game.Resources` clone when present.
5. **Check for resurrections.** A sync can re-add files the fork deliberately deleted, or revert
   replaced art. `--diff-filter=A` over the change range, plus comparing the resources tree, catches
   this. Also verify `G0V0ResourcesVersion` actually points at the newly published resources package
   (a sync that forgets to bump it silently keeps old assets).
6. **Compare against the known-outstanding list** in `references/known-outstanding.md` so
   long-standing, already-triaged items are not reported as new.
7. **Report and ask.** Group findings as: *introduced by this change* / *pre-existing* /
   *not a finding (attribution or nominative)*. For each, name the file, the exact string or asset,
   and a recommended action. Then ask the human which of it to apply. Do not act before the answer.

## Report shape

```markdown
## 商标更改检测（base: <ref> → HEAD）

### 本次变更引入
| 文件 | 内容 | 建议 |
|---|---|---|
| path | `osu!` in user-visible string | 改为 g0v0! |
| path | added mascot asset | 替换为 g0v0 素材 / 删除 |

### 既有（非本次引入）
...

### 不算问题（署名 / nominative）
- N 个新增 .cs 的 MIT 版权头；NuGet 包名 ppy.osu.Framework / g0v0.osu.Game.Resources ...

需要我处理哪些？（全部 / 仅本次引入 / 指定条目 / 先不动）
```

Machine-verify with counts, not impressions: report how many hits each scan produced, and state
explicitly when a scan produced zero. "0 introduced" is a valid and useful result.

## Pitfalls

- `git rev-list --left-right --count A...B` prints `<left-only> <right-only>`; misreading it inverts
  which side is ahead. It counts commits, not files — pair it with `git log --oneline A..B`.
- Grepping the whole tree for `osu` is useless here: namespaces, assembly names and 7000+ localisation
  files match. Always scope to a diff range, and exclude `bin`, `obj`, `.git` and any local resources checkout.
- The separate `g0v0-resources` clone needs its own `upstream` remote
  (`https://github.com/ppy/osu-resources`) before it can be compared or synced.
- A release can silently ship old assets if `G0V0ResourcesVersion` is not bumped after publishing a
  new `g0v0.osu.Game.Resources` package. Confirm the main-repo value matches the package version.
- Brand-file names can be legitimate internal ids (`osu-combo-2000.svg`). Check content/purpose before
  proposing removal.
- Keyword scans over translated `.resx` files produce hundreds of legitimate hits (the game *is*
  about osu! beatmaps). Treat localisation text as low priority and never bulk-rewrite it.
