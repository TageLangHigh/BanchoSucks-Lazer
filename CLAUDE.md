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
- Commits tragen nur Lidl oder Julian als Autor. Keine `Co-Authored-By`-Zeilen von KI-Assistenten,
  in keinem unserer Repos (Regel von Julian, 27.09.2026).
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
- Minispiele und osu! Clicker: `osu.Game/Screens/Banchosucks/`. Das Spiel selbst ist
  `Clicker/ClickerEngine.cs` (lebt in `OsuGame`, produziert in jedem Bildschirm, speichert,
  sendet an den Server); der Bildschirm `OsuClickerScreen.cs` zeigt nur an. Alle Zahlen stehen in
  `Clicker/clicker-balance.json` (eingebettet), nie im Code; Rechnungen in
  `Clicker/ClickerEconomy.cs`, Spielstand in `Clicker/ClickerState.cs`, Texte Deutsch und
  Englisch in `Clicker/ClickerStrings.cs`, API-Modelle in `Clicker/ClickerRequests.cs`, Grafik in
  `ClickerDrawables.cs` und `ClickerPanels.cs`, Rangliste in `ClickerLeaderboard.cs`, Tipp-BPM in
  `TapBpmMeter.cs`. Tests: `osu.Game.Tests/Visual/Navigation/TestSceneBanchosucksMinigames.cs`,
  `osu.Game.Tests/NonVisual/BanchosucksTapBpmMeterTest.cs` und
  `osu.Game.Tests/NonVisual/BanchosucksClickerEconomyTest.cs` (gemeinsame Fixture mit dem Plugin).
- Die Server-Regeln zum Clicker leben im Website-Repo in
  `server/lazer/plugins/banchosucks_clicker/`: `clicker-balance.json` (byteweise gleiche Kopie
  der Client-Datei), `economy.py` (Spiegel von `ClickerEconomy.cs`), `__init__.py` (API, kappt
  unplausibles Wachstum, Saison-Feld, Cloud-Save). Zahlen nur in der JSON ändern, dann
  `make_fixture.py` laufen lassen und die Fixture in beide Repos kopieren; Client und Plugin
  immer zusammen ausrollen. Eine neue Belohnungsquelle muss in `growth_allowance` (economy.py)
  eingerechnet werden, sonst werden ehrliche Spieler gekappt. Ein BPM-Rekord zählt nur mit
  `bpm_epoch` >= Wert der Bilanzdatei; ein Reset läuft über `season` (das Plugin benennt die
  alten Tabellen beim ersten Aufruf um). Balance-Änderungen vorher mit der Simulation im
  Plugin-Ordner (`sim.py`, aggressives Profil = Autoclicker am 500-BPM-Limit rund um die Uhr)
  prüfen: der ganze Baum soll auch für dieses Profil Monate dauern.
- Discord Rich Presence: `osu.Desktop/DiscordRichPresence.cs`, App-ID und Bilder kommen vom
  Lazer-Plugin `banchosucks_client` im Website-Repo.
- Replay-Renderer (danser-go): `osu.Game/Scoring/BanchosucksReplayRenderer.cs`.
- Eingabe- und Audio-Rate: `osu.Game/Graphics/BanchosucksThreadRateManager.cs`.
- Server-Adressen: `osu.Game/Online/ProductionEndpointConfiguration.cs`.

## 4. Release bauen

Ein Release wird nur aus einem gepushten Commit gebaut, damit zu jeder Datei der passende Quelltext
gehört. Der Client aktualisiert sich selbst über Velopack aus unseren GitHub-Releases
(`osu.Desktop/Updater/VelopackUpdateManager.cs`, packId `BanchoSucksLazer`, Kanal `win`); deshalb
müssen die Velopack-Dateien (Setup.exe, *.nupkg, releases.win.json, Portable.zip) am Release hängen,
eine nackte Zip reicht nicht mehr. Reihenfolge:

1. Committen, `git push server main`, `git push origin main`.
2. Tests: `dotnet test osu.Game.Tests -c Release --filter "FullyQualifiedName~Banchosucks"`.
3. Tag `vJJJJ.MMTT.N-banchosucks` auf genau diesen Commit, zu `server` und `origin` pushen
   (erst den Tag pushen, dann das Release anlegen, sonst erzeugt GitHub einen falschen Tag).
4. Auf dem Server (Linux, .NET 10 SDK, `dotnet tool install -g vpk --version 1.2.0`):
   `./build-release.sh JJJJ.MMTT.N` baut win-x64, holt die vorigen Velopack-Pakete für Delta-Updates,
   packt mit vpk und lädt alles ans GitHub-Release (Token in `~/.config/github-token`, nie ausgeben).
   Release-Notizen vorher nach `../artifacts/notes-JJJJ.MMTT.N.md` legen.
5. Den MD5 von `osu.Game.dll` registrieren (das Skript druckt den Befehl), dann die Lazer-App mit
   Ankündigung neu starten (`maintenance.py --minutes 1 --restart lazer`).
6. Auf Windows bauen geht weiterhin mit dem `dotnet publish` aus dem Skript plus `vpk [win] pack`
   mit denselben Parametern; ohne Velopack-Pakete bekommen die Spieler kein Update angeboten.

Lizenz: `LICENCE`, `LICENCE-OSU` und `BANCHOSUCKS.md` bleiben im Repo. Den Client nie als
offiziellen osu!-Client ausgeben.
