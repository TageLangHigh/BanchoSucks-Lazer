#!/usr/bin/env bash
#
# Trademark change detection for the g0v0 fork (see ../SKILL.md).
#
# READ-ONLY: this script never deletes, renames or rewrites anything. It prints an
# inventory for a human to confirm before any removal happens.
#
# Usage:
#   detect.sh                     auto-pick the pre-change base
#   detect.sh --base <ref>        explicit pre-change base
#   detect.sh --incoming          also scan upstream/master that is not merged yet
#   detect.sh --base <ref> --incoming
#   G0V0_RESOURCES_DIR=<path> detect.sh   # asset inventory for a separate g0v0-resources clone
#
set -uo pipefail

# Media/asset extensions. Brand *text* is handled separately, so path scans only
# care about art/audio/font files and not about, say, a .csproj that merely happens
# to live in a folder named after a mascot.
BRAND_TEXT_RE='osu!|ppy[.]sh|ppy[.]com|contact@ppy|osu[.]ppy|pippi|pippidon|mascot|trademark'
BRAND_PATH_RE='pippi|mascot|logo|comboburst|fruit-catcher|supporter-|not-found|rankedplay|skins/retro|textures/intro|tracks/.*[.]osz|fonts/(venera|vantar)'
ASSET_RE='[.](png|jpg|jpeg|gif|webp|svg|ico|icns|car|wav|mp3|ogg|osz|ttf|otf|bin)$'

BASE=""
INCOMING=0
while [ $# -gt 0 ]; do
  case "$1" in
    --base) BASE="${2:-}"; shift 2 ;;
    --incoming) INCOMING=1; shift ;;
    -h|--help) sed -n '2,15p' "$0"; exit 0 ;;
    *) echo "unknown argument: $1" >&2; exit 2 ;;
  esac
done

ROOT="$(git rev-parse --show-toplevel 2>/dev/null)" || { echo "not inside a git repository" >&2; exit 1; }
cd "$ROOT" || exit 1

hr() { printf '\n===== %s =====\n' "$1"; }

# Added lines whose content mentions a brand term, skipping comment-only lines and
# copyright headers (MIT attribution legitimately contains contact@ppy.sh).
scan_added_text() {
  git diff --unified=0 --no-color "$1" 2>/dev/null | awk -v pat="$BRAND_TEXT_RE" '
    /^\+\+\+ / { file = $2; sub(/^b\//, "", file); next }
    /^\+/ {
      line = substr($0, 2)
      t = line
      sub(/^[ \t]+/, "", t)
      if (t ~ /^\/\// || t ~ /^\*/ || t ~ /^\/\*/) next
      if (line ~ /Copyright \(c\)/) next
      if (tolower(line) ~ tolower(pat)) print file "\t" t
    }'
}

# Brand-looking *asset* paths touched by a diff, and those newly added (resurrections).
scan_changed_paths() {
  git diff --name-status -M --no-color "$1" 2>/dev/null | grep -iE "$BRAND_PATH_RE" | grep -iE "$ASSET_RE" || true
}
scan_added_paths() {
  git diff --name-status -M --diff-filter=A --no-color "$1" 2>/dev/null | grep -iE "$BRAND_PATH_RE" | grep -iE "$ASSET_RE" || true
}

count_lines() { [ -z "${1:-}" ] && echo 0 || printf '%s\n' "$1" | grep -c . ; }

hr "0. topology"
echo "repo:        $ROOT"
echo "branch:      $(git rev-parse --abbrev-ref HEAD) @ $(git rev-parse --short HEAD)"
for r in upstream/master origin/v2; do
  if git rev-parse --verify -q "$r" >/dev/null; then
    echo "ref present: $r -> $(git rev-parse --short "$r")"
  else
    echo "ref MISSING: $r"
  fi
done

# Base selection: the upstream merge base by default; pass --base <ref> for an exact base.
# NB: the fork's old backup/* pre-sync tags were removed in 2026-09, so no baseline tag is
# auto-discovered any more — tag the tree yourself before a sync if you want one.
if [ -z "$BASE" ]; then
  if git rev-parse --verify -q upstream/master >/dev/null; then
    BASE="$(git merge-base HEAD upstream/master)"
  fi
fi

if [ -z "$BASE" ]; then
  hr "1. change range"
  echo "no base could be auto-detected; re-run with --base <ref>"
else
  hr "1. change range ($BASE..HEAD)"
  echo "commits: $(git rev-list --count "$BASE..HEAD" 2>/dev/null || echo '?')   files: $(git diff --name-only "$BASE..HEAD" 2>/dev/null | wc -l)"
  echo "note: 'git rev-list --left-right --count A...B' prints <left-only> <right-only>."

  text_hits="$(scan_added_text "$BASE..HEAD")"
  echo
  echo "-- 1a. brand text added by this change (comments/headers excluded): $(count_lines "$text_hits")"
  [ -n "$text_hits" ] && printf '%s\n' "$text_hits" | head -40
  [ "$(count_lines "$text_hits")" -gt 40 ] && echo "... (truncated)"

  path_hits="$(scan_changed_paths "$BASE..HEAD")"
  echo
  echo "-- 1b. brand-looking paths touched by this change: $(count_lines "$path_hits")"
  [ -n "$path_hits" ] && printf '%s\n' "$path_hits" | head -40

  new_paths="$(scan_added_paths "$BASE..HEAD")"
  echo
  echo "-- 1c. brand-looking paths ADDED by this change (possible resurrections): $(count_lines "$new_paths")"
  [ -n "$new_paths" ] && printf '%s\n' "$new_paths" | head -40
fi

if [ "$INCOMING" -eq 1 ]; then
  hr "2. upstream/master not merged yet (HEAD...upstream/master)"
  if git rev-parse --verify -q upstream/master >/dev/null; then
    inc_text="$(scan_added_text 'HEAD...upstream/master')"
    echo "-- 2a. brand text upstream would add: $(count_lines "$inc_text")"
    [ -n "$inc_text" ] && printf '%s\n' "$inc_text" | head -40
    inc_paths="$(scan_changed_paths 'HEAD...upstream/master')"
    echo
    echo "-- 2b. brand-looking paths upstream would touch: $(count_lines "$inc_paths")"
    [ -n "$inc_paths" ] && printf '%s\n' "$inc_paths" | head -40
  else
    echo "skipped: no upstream/master ref (git remote add upstream https://github.com/ppy/osu.git && git fetch upstream)"
  fi
fi

hr "3. g0v0-resources asset brand inventory (separate repository)"

RES_DIR="${G0V0_RESOURCES_DIR:-}"
if [ -z "$RES_DIR" ]; then
  for candidate in "$ROOT/../g0v0-resources" "$ROOT/osu.Game.Resources"; do
    if [ -d "$candidate/.git" ] || [ -f "$candidate/.git" ]; then
      RES_DIR="$candidate"
      break
    fi
  done
fi

if [ -n "$RES_DIR" ] && git -C "$RES_DIR" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  RES_HEAD="$(git -C "$RES_DIR" rev-parse --short HEAD 2>/dev/null || echo '?')"
  echo "resources repo:              $RES_DIR @ $RES_HEAD"
  echo "main-repo resources version: $(grep -o '<G0V0ResourcesVersion[^>]*>[^<]*' "$ROOT/osu.Game/osu.Game.csproj" 2>/dev/null | sed 's/.*>//')"

  if git -C "$RES_DIR" rev-parse --verify -q upstream/master >/dev/null; then
    printf '%-30s %-6s %-9s %-8s %-8s %s\n' "path group" "same" "modified" "deleted" "renamed" "fork-added"
    # NB: git pathspecs match literally unless they contain a glob, so a group that is
    # a filename prefix needs an explicit '*'.
    for g in "Skins/Legacy/pippi*" "Skins/Retro" "Textures/Online" "Textures/Menu" "Textures/Icons" \
             "Textures/Intro" "Textures/Backgrounds" "Tracks" "Fonts" "Samples/Intro"; do
      out="$(git -C "$RES_DIR" diff --name-status -M --no-color upstream/master HEAD -- "osu.Game.Resources/$g" 2>/dev/null)"
      a="$(printf '%s\n' "$out" | grep -c '^A' || true)"
      m="$(printf '%s\n' "$out" | grep -c '^M' || true)"
      d="$(printf '%s\n' "$out" | grep -c '^D' || true)"
      r="$(printf '%s\n' "$out" | grep -c '^R' || true)"
      # Files whose content is byte-identical on both sides: upstream art the fork
      # has not touched at all, i.e. the real candidate list for review.
      same="$(comm -12 \
        <(git -C "$RES_DIR" ls-tree -r upstream/master -- "osu.Game.Resources/$g" 2>/dev/null | sort) \
        <(git -C "$RES_DIR" ls-tree -r HEAD -- "osu.Game.Resources/$g" 2>/dev/null | sort) | wc -l)"
      printf '%-30s %-6s %-9s %-8s %-8s %s\n' "$g" "$same" "$m" "$d" "$r" "$a"
    done
    echo
    echo "how to read this (diff is upstream/master -> fork HEAD):"
    echo "  same       = identical to upstream: upstream art still shipped untouched -> REVIEW THIS"
    echo "  modified   = the fork replaced it (already handled)"
    echo "  deleted    = the fork removed it (already handled)"
    echo "  renamed    = the fork renamed/consolidated it (already handled)"
    echo "  fork-added = the fork's own replacement art (not upstream leakage)"
    echo "Cross-check anything in 'same' against references/known-outstanding.md."
    echo "Also confirm the published g0v0.osu.Game.Resources version matches" \
         "G0V0ResourcesVersion in the main repo."
  else
    echo "skipped: g0v0-resources clone has no upstream/master ref"
    echo "  git -C \"$RES_DIR\" remote add upstream https://github.com/ppy/osu-resources.git && git -C \"$RES_DIR\" fetch upstream"
  fi

  echo
  echo "-- pippi-branded files still in the tree:"
  git -C "$RES_DIR" ls-files | grep -i pippi | sed 's/^/   /' || echo "   (none)"
else
  echo "skipped: no g0v0-resources checkout found"
  echo "  clone it next to this repo (../g0v0-resources), keep a local osu.Game.Resources clone,"
  echo "  or set G0V0_RESOURCES_DIR=<path>; asset branding lives in the separate resources repository."
fi

hr "4. already-triaged items (do not report as new)"
echo "see references/known-outstanding.md next to this skill"
echo
echo "REMINDER: report findings and wait for human confirmation before removing anything."
