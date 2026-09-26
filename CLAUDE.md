# BanchoSucks Lazer – Hinweise für KI-Assistenten (Claude Code, Codex, …)

Dieses Repository ist der Lazer-Client des osu!-Privatservers banchosucks.cc (Betreiber Julian und
Lidl), ein Fork von GooGuTeam/g0v0 (osu!lazer). Diese Datei wird beim Start jeder Sitzung gelesen.
Sie ist bewusst kurz; die Regeln sind verbindlich.

## 1. Datenschutz: Was den Server verlassen darf

Discord, der Butzebot-Server und jeder andere externe Dienst sind Dritte. Unsere
Datenschutzerklärung (im Website-Repo server/legal/datenschutz.html, §6 und §10) verspricht den
Spielern, dass IP-Adressen und Gerätekennungen nicht weitergegeben werden. Deshalb gilt für JEDE
Nachricht, die per Webhook, Bot, E-Mail oder Log-Weiterleitung nach außen geht:

Erlaubt: Spielername und Konto-ID, Links auf banchosucks.cc, Beatmap-Namen, Score-Werte,
Zahlen (Anzahl, Dauer), Moderationsaktionen mit dem vom Team eingegebenen Grund.

Verboten, auch verkürzt, maskiert oder gehasht:
- IP-Adressen (auch „185.132.x.x“ oder „2a02:810c:…“)
- Gerätekennungen und Hash-Fragmente (Adapter, Uninstall-ID, Disk-Serial, osupath, „81b68515…“)
- E-Mail-Adressen, Passwort-Hashes, Sitzungs-Tokens
- Login-Standorte (Land, Stadt, Koordinaten), User-Agents, Zeitzonen
- Inhalte privater Nachrichten

Für den Client heißt das zusätzlich:
- Der Client spricht nur mit unseren Servern (lazer-api.banchosucks.cc, banchosucks.cc).
  Ausnahmen sind Discord Rich Presence (lokal über die Discord-App, nur Spielername, Rang und
  Aktivität, im Modus „Limited“ ohne Namen) und der danser-Download von GitHub, der erst nach
  Zustimmung des Spielers startet.
- Jede neue Verbindung zu einem Dritten braucht die Zustimmung des Spielers im Client und einen
  Absatz in server/legal/datenschutz.html im Website-Repo, Deutsch und Englisch.
- Nichts aus der Liste oben in Rich Presence, Fehlermeldungen oder Daten für Server-Plugins.

## 2. Git-Regeln

- Remotes: `server` ist der Hub `lidl@banchosucks:/home/lidl/git/banchosucks-lazer.git` und die
  gemeinsame Quelle. `origin` ist GitHub (TageLangHigh/BanchoSucks-Lazer) und nur für Releases.
  `upstream` ist GooGuTeam/g0v0 und wird nur gelesen.
- Vor der Arbeit: `git pull server main`. Julians Claude kann Änderungen auf den Hub gepusht haben.
- Kleine Commits auf Deutsch, mit dem Warum. Zügig pushen, erst `git push server main`, dann
  `git push origin main`. Beide müssen danach auf denselben Commit zeigen.
- Nie force-pushen, nie die Historie umschreiben (kein Rebase oder Amend von gepushten Commits).
- Die Historie hier ist bewusst von g0v0 getrennt (eigener Root-Commit). g0v0 nie mergen.
  Upstream-Änderungen gezielt als normale Commits übernehmen und vorher mit Julian absprechen.
- Zeilenenden: `.gitattributes` und `.editorconfig` gelten. Bestehende Dateien behalten ihre
  Zeilenenden, keine Massenumstellung.
- Nie committen: Zugangsdaten, Signierschlüssel, lokale Pfade, Build-Ausgaben (`bin/`, `obj/`,
  `*.zip`, `*.tar.gz`).

## 3. Wo was liegt

- `BANCHOSUCKS.md`: alle Banchosucks-Änderungen am Client, Branding, Lizenz-Hinweise.
- Minispiele und osu! Clicker: `osu.Game/Screens/Banchosucks/`. Zahlen und Spielstand-Format in
  `ClickerBalance.cs`, Grafik in `ClickerDrawables.cs`, Rangliste und API-Anfragen in
  `ClickerLeaderboard.cs`, Tipp-BPM in `TapBpmMeter.cs`, der Screen in `OsuClickerScreen.cs`.
  Tests: `osu.Game.Tests/Visual/Navigation/TestSceneBanchosucksMinigames.cs` und
  `osu.Game.Tests/NonVisual/BanchosucksTapBpmMeterTest.cs`.
- Die Server-Regeln zum Clicker leben im Website-Repo in
  `server/lazer/plugins/banchosucks_clicker/__init__.py`: dieselben Zahlen wie `ClickerBalance.cs`,
  die Plausibilitätsprüfung und `BPM_EPOCH`. Client und Plugin immer zusammen ändern, sonst lehnt
  der Server ehrliche Spieler ab. Ein BPM-Rekord zählt nur, wenn der Client `bpm_epoch` mitschickt
  und der Wert mindestens `BPM_EPOCH` ist; wer die Tipp-Erkennung ändert, erhöht den Wert in beiden.
- Discord Rich Presence: `osu.Desktop/DiscordRichPresence.cs`, App-ID und Bilder kommen vom
  Lazer-Plugin `banchosucks_client` im Website-Repo.
- Replay-Renderer (danser-go): `osu.Game/Scoring/BanchosucksReplayRenderer.cs`.
- Eingabe- und Audio-Rate: `osu.Game/Graphics/BanchosucksThreadRateManager.cs`.
- Server-Adressen: `osu.Game/Online/ProductionEndpointConfiguration.cs`.

## 4. Release bauen

Ein Release wird nur aus einem gepushten Commit gebaut, damit zu jeder Zip der passende Quelltext
gehört. Reihenfolge:

1. Committen, `git push server main`, `git push origin main`.
2. Tests: `dotnet test osu.Game.Tests -c Release --filter "FullyQualifiedName~Banchosucks"`.
3. Tag `vJJJJ.MMTT.N-banchosucks` auf genau diesen Commit setzen und zu `server` und `origin`
   pushen. Derzeit heißt die Version 2026.913.N (913 stammt von der g0v0-Basis), N zählt hoch.
4. Bauen, mit `V` als Version ohne `v` und ohne Endung:
   `dotnet publish osu.Desktop/osu.Desktop.csproj -c Release -r win-x64 --self-contained true -o ../artifacts/BanchoSucks-Lazer-win-x64-$V -p:Version=$V -p:AssemblyVersion=$V -p:FileVersion=$V -p:InformationalVersion=$V-banchosucks`
   und dasselbe mit `-r linux-x64` und zusätzlich `-p:PublishSingleFile=false -p:PublishTrimmed=false`.
5. Den MD5 von `osu.Game.dll` auf dem Server registrieren, sonst lehnt der Server den Client ab:
   `python3 /home/lidl/g0v0-server/register-client-build.py <md5> $V-banchosucks Windows`
   (bei einem anderen Linux-Hash zusätzlich mit `Linux`), danach im Ordner
   `/home/lidl/g0v0-server` den Befehl `docker compose -p g0v0 -f compose.lazer.yml restart app`.
6. Packen: Windows als Zip, Linux als tar.gz mit `--mode='u+rwx,go+rx' --owner=0 --group=0`,
   damit der Starter ausführbar bleibt.
7. Das Release auf GitHub im Browser aus dem gepushten Tag anlegen und beide Dateien hochladen.

Lizenz: `LICENCE`, `LICENCE-OSU` und `BANCHOSUCKS.md` bleiben im Repo. Den Client nie als
offiziellen osu!-Client ausgeben.
