#!/usr/bin/env bash
# Builds and publishes a BanchoSucks Lazer release from the server (Linux, .NET 10 SDK, vpk 1.2.0).
#
#   ./build-release.sh 2026.913.10            build + Velopack package + GitHub release
#   ./build-release.sh 2026.913.10 --no-upload   build + package only
#   ./build-release.sh 2026.913.10 --skip-publish  reuse ../artifacts/BanchoSucks-Lazer-win-x64-VERSION
#
# Needs: a clean checkout of the commit to release (tag vVERSION-banchosucks on it), the GitHub token in
# ~/.config/github-token (Julian's account, push rights), and for the lazer server the md5 registration
# afterwards (printed at the end). Never echo the token.
set -euo pipefail
V=${1:?version like 2026.913.10}; shift || true
UPLOAD=1; PUBLISH=1
for flag in "$@"; do
  case $flag in --no-upload) UPLOAD=0 ;; --skip-publish) PUBLISH=0 ;; *) echo "unknown flag $flag"; exit 1 ;; esac
done
REPO=TageLangHigh/BanchoSucks-Lazer
ROOT=$(cd "$(dirname "$0")" && pwd)
ART=$ROOT/../artifacts; PUB=$ART/BanchoSucks-Lazer-win-x64-$V; OUT=$ART/velopack-$V
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 PATH="$PATH:$HOME/.dotnet/tools"

cd "$ROOT"
[[ -z "$(git status --porcelain)" ]] || { echo "working tree not clean, commit first"; exit 1; }
git rev-parse -q --verify "refs/tags/v$V-banchosucks" >/dev/null || { echo "tag v$V-banchosucks missing on this commit"; exit 1; }

if [[ $PUBLISH -eq 1 ]]; then
  echo "==> publish win-x64 $V"
  rm -rf "$PUB"
  # low priority and at most two build processes: the game servers share these four cores,
  # a full-speed build made the website crawl for players (2026-09-26)
  nice -n 19 ionice -c 3 dotnet publish osu.Desktop/osu.Desktop.csproj -c Release -r win-x64 --self-contained true -o "$PUB" \
    -m:2 -p:Version=$V -p:AssemblyVersion=$V -p:FileVersion=$V -p:InformationalVersion=$V-banchosucks >"$ART/publish-$V.log"
else
  [[ -f "$PUB/BanchoSucks-Lazer.exe" ]] || { echo "no publish output in $PUB"; exit 1; }
fi

echo "==> previous Velopack releases (for delta packages)"
rm -rf "$OUT"; mkdir -p "$OUT"
vpk download github --repoUrl "https://github.com/$REPO" -o "$OUT" -c win >/dev/null 2>&1 || echo "    none yet (first Velopack release)"

echo "==> vpk pack"
nice -n 19 ionice -c 3 vpk '[win]' pack -u BanchoSucksLazer -v "$V" -p "$PUB" -e BanchoSucks-Lazer.exe -o "$OUT" -c win \
  --packTitle "BanchoSucks Lazer" --packAuthors "banchosucks.cc" -i osu.Desktop/lazer.ico -s assets/lazer-nuget.png
ls -la "$OUT" | awk 'NR>1{printf "    %8.0f KB  %s\n", $5/1024, $9}'

MD5=$(md5sum "$PUB/osu.Game.dll" | cut -d' ' -f1)
echo "==> osu.Game.dll md5: $MD5"

# the download mirror (dl.banchosucks.cc on Julian's server) pulls /srv/bancho-web/downloads every ten
# minutes; the fixed names are what the website links, the versioned ones stay for reference
if [[ $UPLOAD -eq 1 && -d /srv/bancho-web/downloads ]]; then
  echo "==> copy installer and portable package to /srv/bancho-web/downloads"
  for f in BanchoSucksLazer-win-Setup.exe BanchoSucksLazer-win-Portable.zip; do
    sudo -n cp "$OUT/$f" "/srv/bancho-web/downloads/$f" && sudo -n cp "$OUT/$f" "/srv/bancho-web/downloads/${f/win-/$V-}" || echo "    copy of $f failed"
  done
  sudo -n chmod 644 /srv/bancho-web/downloads/BanchoSucksLazer-* || true
fi

if [[ $UPLOAD -eq 1 ]]; then
  T=$(tr -d '\r\n' < ~/.config/github-token)
  echo "==> GitHub release v$V-banchosucks"
  python3 - "$T" "$V" "$OUT" "$REPO" <<'PY'
import json, sys, os, urllib.request, urllib.error
token, v, out, repo = sys.argv[1:5]
tag = f"v{v}-banchosucks"
def api(method, url, data=None, ctype="application/json"):
    req = urllib.request.Request(url, data=data, method=method, headers={"Authorization": f"Bearer {token}", "Accept": "application/vnd.github+json", "Content-Type": ctype, "User-Agent": "banchosucks-release"})
    with urllib.request.urlopen(req, timeout=1800) as r: return json.load(r)
try:
    rel = api("GET", f"https://api.github.com/repos/{repo}/releases/tags/{tag}")
except urllib.error.HTTPError:
    notes = open(os.path.join(out, "..", f"notes-{v}.md")).read() if os.path.exists(os.path.join(out, "..", f"notes-{v}.md")) else f"BanchoSucks Lazer {v}"
    rel = api("POST", f"https://api.github.com/repos/{repo}/releases", json.dumps({"tag_name": tag, "name": f"BanchoSucks Lazer {v}", "body": notes}).encode())
have = {a["name"] for a in rel.get("assets", [])}
# only what this pack produced (assets.win.json); the previous full package downloaded for the delta stays local
produced = {e["RelativeFileName"] for e in json.load(open(os.path.join(out, "assets.win.json")))} if os.path.exists(os.path.join(out, "assets.win.json")) else None
for name in sorted(os.listdir(out)):
    if name in have or name.endswith(".log"): continue
    if produced is not None and name not in produced and not name.endswith(".json") and name != "RELEASES": continue
    path = os.path.join(out, name); data = open(path, "rb").read()
    api("POST", f"https://uploads.github.com/repos/{repo}/releases/{rel['id']}/assets?name={name}", data, "application/octet-stream")
    print(f"    uploaded {name} ({len(data)//1048576} MB)")
print("    release:", rel["html_url"])
PY
fi

cat <<MSG
==> next, on the server (as lidl):
    python3 /home/lidl/g0v0-server/register-client-build.py $MD5 $V-banchosucks Windows
    python3 /home/lidl/ops/maintenance.py --minutes 1 --restart lazer --reason "Client $V"
MSG
