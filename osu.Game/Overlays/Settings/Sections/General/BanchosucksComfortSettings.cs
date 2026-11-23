// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.IPC;

namespace osu.Game.Overlays.Settings.Sections.General
{
    /// <summary>
    /// Banchosucks: switches for the comfort and streaming features.
    /// </summary>
    public partial class BanchosucksComfortSettings : SettingsSubsection
    {
        protected override LocalisableString Header => "Komfort und Streaming / Comfort and streaming";

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, Storage storage)
        {
            AddRange(new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = "Nicht stören während einer Map / Do not disturb during a map",
                    HintText = "Meldungen (Chat, Freunde, fertige Downloads) warten, bis die Map vorbei ist, und erscheinen dann als eine Zusammenfassung. Fehler und wichtige Meldungen kommen wie bisher. / Notifications (chat, friends, finished downloads) wait until the map is over and then show as one summary. Errors and critical notifications behave as before.",
                    Current = config.GetBindable<bool>(OsuSetting.BanchosucksDoNotDisturb),
                })
                {
                    Keywords = new[] { @"notifications", @"meldungen", @"benachrichtigungen", @"dnd", @"nicht stören", @"toast" },
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = "Streamer-Modus / Streamer mode",
                    HintText = "Der Chat lässt sich nicht öffnen; Meldungen zu Privatnachrichten, Erwähnungen und Freunden werden gesammelt und erst beim Ausschalten gezeigt; Einladungen zeigen keinen Spielernamen; das Kontofeld im Login zeigt Punkte. Ankündigungen des Servers erscheinen weiter. Chat in Mehrspieler-Räumen und Namen auf Ranglisten bleiben sichtbar. Eine Taste dafür lässt sich unter Eingabe festlegen. / The chat cannot be opened; private message, mention and friend notifications are collected and shown when the mode is switched off; invitations show no player name; the account field of the login form shows dots. Server announcements still appear. Chat in multiplayer rooms and names on leaderboards stay visible. A key can be bound under Input.",
                    Current = config.GetBindable<bool>(OsuSetting.BanchosucksStreamerMode),
                })
                {
                    Keywords = new[] { @"stream", @"streamer", @"obs", @"twitch", @"privacy", @"privat", @"chat" },
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = "Ruckler-Bericht nach der Map / Stutter report after a map",
                    HintText = "Misst beim Spielen die Frame-Zeiten. Gab es spürbare Ruckler (Frames über 25 ms und länger als das Dreifache des Üblichen), erscheint auf dem Ergebnisbildschirm eine Meldung mit Anzahl, längstem Frame und einem Tipp zu den Einstellungen. / Measures frame times while you play. After noticeable stutters (frames above 25 ms and longer than three times the usual) a notification on the results screen shows how many, the longest frame and a hint about your settings.",
                    Current = config.GetBindable<bool>(OsuSetting.BanchosucksStutterReport),
                })
                {
                    Keywords = new[] { @"stutter", @"ruckler", @"lag", @"fps", @"frame", @"performance" },
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = "Overlay-Datenquelle (tosu-kompatibel) / Overlay data source (tosu compatible)",
                    HintText = "Für Stream-Overlays, die für tosu geschrieben sind (pp-Zähler, Now-Playing): Der Client beantwortet ws://127.0.0.1:24050/websocket/v2 und /json/v2 selbst, nur auf diesem PC. Gesendet werden Spielzustand, Beatmap, Mods, Punkte, Treffer, pp, dein Spielername und dein Rang; nie Passwort, E-Mail, Chat oder Dateipfade. tosu darf nicht gleichzeitig laufen (gleicher Port). Overlays kommen in den Overlay-Ordner und sind dann unter http://127.0.0.1:24050/<Ordnername>/ erreichbar. / For stream overlays written for tosu (pp counters, now playing): the client itself answers ws://127.0.0.1:24050/websocket/v2 and /json/v2, on this PC only. It sends game state, beatmap, mods, score, hits, pp, your player name and rank; never password, e-mail, chat or file paths. tosu must not run at the same time (same port). Put overlays into the overlay folder; they are then available at http://127.0.0.1:24050/<folder name>/.",
                    Current = config.GetBindable<bool>(OsuSetting.BanchosucksOverlayDataSource),
                })
                {
                    Keywords = new[] { @"tosu", @"overlay", @"stream", @"obs", @"websocket", @"pp counter", @"gosumemory", @"streamcompanion" },
                },
                new SettingsButtonV2
                {
                    Text = "Overlay-Ordner öffnen / Open overlay folder",
                    TooltipText = "Jeder Unterordner hier ist ein Overlay (wie tosus static-Ordner). / Every subfolder here is one overlay (like tosu's static folder).",
                    Keywords = new[] { @"tosu", @"overlay", @"stream", @"obs" },
                    Action = () => storage.GetStorageForDirectory(BanchosucksOverlayDataSource.OVERLAY_FOLDER).PresentExternally(),
                },
            });
        }
    }
}
