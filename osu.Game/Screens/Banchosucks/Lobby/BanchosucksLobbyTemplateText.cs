// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Rooms;
using osu.Game.Screens.Banchosucks.Clicker;

namespace osu.Game.Screens.Banchosucks.Lobby
{
    /// <summary>
    /// Banchosucks: the texts around lobby templates, English and German by client language: what loading will do
    /// (shown before the host confirms) and what it did.
    /// </summary>
    public static class BanchosucksLobbyTemplateText
    {
        /// <summary>
        /// Map names listed per group before the rest is summed up as "and N more".
        /// </summary>
        public const int MAX_LISTED_MAPS = 8;

        /// <summary>
        /// One line of the preview or the result; <see cref="Warning"/> lines are shown in red.
        /// </summary>
        public readonly record struct Line(string Text, bool Warning = false);

        public static string Name(MatchType type)
        {
            switch (type)
            {
                case MatchType.TeamVersus:
                    return ClickerStrings.Pick("Team versus", "Team gegen Team");

                case MatchType.HeadToHead:
                    return ClickerStrings.Pick("Head to head", "Jeder gegen jeden");

                default:
                    return type.ToString();
            }
        }

        public static string Name(QueueMode mode)
        {
            switch (mode)
            {
                case QueueMode.HostOnly:
                    return ClickerStrings.Pick("Host only", "Nur der Host");

                case QueueMode.AllPlayers:
                    return ClickerStrings.Pick("All players", "Alle Spieler");

                case QueueMode.AllPlayersRoundRobin:
                    return ClickerStrings.Pick("All players (round robin)", "Alle Spieler (reihum)");

                default:
                    return mode.ToString();
            }
        }

        public static string AutoStart(int seconds) => seconds <= 0 ? ClickerStrings.Pick("off", "aus") : $"{seconds} s";

        private static string onOff(bool on) => on ? ClickerStrings.Pick("on", "an") : ClickerStrings.Pick("off", "aus");

        /// <summary>
        /// A one-line summary of a template for the list.
        /// </summary>
        public static string Summary(BanchosucksLobbyTemplate template)
            => ClickerStrings.Pick(
                $"{Name(template.MatchType)}, {Name(template.QueueMode)}, {template.Playlist.Count} {(template.Playlist.Count == 1 ? "map" : "maps")}",
                $"{Name(template.MatchType)}, {Name(template.QueueMode)}, {template.Playlist.Count} {(template.Playlist.Count == 1 ? "Map" : "Maps")}");

        /// <summary>
        /// What loading the template will do, for the question asked before it is applied.
        /// </summary>
        /// <param name="plan">The plan.</param>
        /// <param name="creating">Whether the room does not exist yet (the settings go into the form, the first map into the playlist).</param>
        public static List<Line> Describe(BanchosucksLobbyTemplatePlan plan, bool creating)
        {
            var lines = new List<Line>();

            if (plan.Changes.Count == 0)
                lines.Add(new Line(ClickerStrings.Pick("The room settings stay as they are.", "Die Raumeinstellungen bleiben, wie sie sind.")));

            foreach (var change in plan.Changes)
            {
                switch (change.Field)
                {
                    case BanchosucksLobbyTemplateField.RoomName:
                        lines.Add(new Line(ClickerStrings.Pick($"Room name: \"{change.From}\" → \"{change.To}\"", $"Raumname: „{change.From}“ → „{change.To}“")));
                        break;

                    case BanchosucksLobbyTemplateField.MatchType:
                        lines.Add(new Line(ClickerStrings.Pick($"Game type: → {Name(plan.Settings.Type)}", $"Spielart: → {Name(plan.Settings.Type)}")));
                        break;

                    case BanchosucksLobbyTemplateField.QueueMode:
                        lines.Add(new Line(ClickerStrings.Pick($"Queue mode: → {Name(plan.Settings.QueueMode)}", $"Warteschlange: → {Name(plan.Settings.QueueMode)}")));
                        break;

                    case BanchosucksLobbyTemplateField.AutoStart:
                        lines.Add(new Line(ClickerStrings.Pick($"Auto start: → {AutoStart(plan.Settings.AutoStartSeconds)}", $"Automatischer Start: → {AutoStart(plan.Settings.AutoStartSeconds)}")));
                        break;

                    case BanchosucksLobbyTemplateField.AutoSkip:
                        lines.Add(new Line(ClickerStrings.Pick($"Skip the beatmap intro automatically: → {onOff(plan.Settings.AutoSkip)}", $"Intro automatisch überspringen: → {onOff(plan.Settings.AutoSkip)}")));
                        break;
                }
            }

            if (plan.ToAdd.Count > 0)
            {
                lines.Add(new Line(creating
                    ? ClickerStrings.Pick($"Maps of the room ({plan.ToAdd.Count}): {list(plan.ToAdd)}", $"Maps des Raums ({plan.ToAdd.Count}): {list(plan.ToAdd)}")
                    : ClickerStrings.Pick($"Added to the queue ({plan.ToAdd.Count}): {list(plan.ToAdd)}", $"Kommt in die Warteschlange ({plan.ToAdd.Count}): {list(plan.ToAdd)}")));

                if (creating && plan.ToAdd.Count > 1)
                {
                    lines.Add(new Line(ClickerStrings.Pick(
                        $"The first map is set right away, the other {plan.ToAdd.Count - 1} are added once the room is created.",
                        $"Die erste Map wird sofort gesetzt, die anderen {plan.ToAdd.Count - 1} kommen dazu, sobald der Raum erstellt ist.")));
                }
            }
            else if (plan.Template.Playlist.Count == 0)
                lines.Add(new Line(ClickerStrings.Pick("The template has no maps.", "Die Vorlage enthält keine Maps.")));
            else
                lines.Add(new Line(ClickerStrings.Pick("No map is added.", "Es wird keine Map hinzugefügt.")));

            if (plan.AlreadyQueued.Count > 0)
            {
                lines.Add(new Line(ClickerStrings.Pick(
                    $"Already in the queue, skipped ({plan.AlreadyQueued.Count}): {list(plan.AlreadyQueued)}",
                    $"Schon in der Warteschlange, übersprungen ({plan.AlreadyQueued.Count}): {list(plan.AlreadyQueued)}")));
            }

            if (plan.Unavailable.Count > 0)
                lines.Add(unavailable(plan));

            lines.Add(new Line(ClickerStrings.Pick("The password is not part of a template and stays as it is.", "Das Passwort gehört nicht zur Vorlage und bleibt, wie es ist.")));

            return lines;
        }

        /// <summary>
        /// What loading the template did.
        /// </summary>
        /// <param name="plan">The plan that was applied.</param>
        /// <param name="added">Maps the server accepted.</param>
        /// <param name="failed">Maps the server refused, with its reason.</param>
        /// <param name="settingsError">Why the settings could not be changed, if they could not.</param>
        public static List<Line> Report(BanchosucksLobbyTemplatePlan plan, int added, IReadOnlyList<(BanchosucksLobbyTemplateItem Item, string Reason)> failed, string? settingsError = null)
        {
            var lines = new List<Line>();

            if (settingsError != null)
            {
                lines.Add(new Line(ClickerStrings.Pick(
                    $"Template \"{plan.Template.Name}\": the room settings could not be changed ({settingsError}). No map was added.",
                    $"Vorlage „{plan.Template.Name}“: Die Raumeinstellungen konnten nicht geändert werden ({settingsError}). Es wurde keine Map hinzugefügt."), true));
                return lines;
            }

            lines.Add(new Line(ClickerStrings.Pick(
                $"Template \"{plan.Template.Name}\" loaded: {added} {(added == 1 ? "map" : "maps")} added.",
                $"Vorlage „{plan.Template.Name}“ geladen: {added} {(added == 1 ? "Map" : "Maps")} hinzugefügt.")));

            if (plan.AlreadyQueued.Count > 0)
            {
                lines.Add(new Line(ClickerStrings.Pick(
                    $"Already in the queue, skipped: {plan.AlreadyQueued.Count}.",
                    $"Schon in der Warteschlange, übersprungen: {plan.AlreadyQueued.Count}.")));
            }

            if (plan.Unavailable.Count > 0)
                lines.Add(unavailable(plan));

            foreach (var (item, reason) in failed)
            {
                lines.Add(new Line(ClickerStrings.Pick(
                    $"Not added: {item.DisplayTitle} ({reason})",
                    $"Nicht hinzugefügt: {item.DisplayTitle} ({reason})"), true));
            }

            return lines;
        }

        private static Line unavailable(BanchosucksLobbyTemplatePlan plan)
            => new Line(ClickerStrings.Pick(
                $"Not found on the server, cannot be added ({plan.Unavailable.Count}): {list(plan.Unavailable)}",
                $"Auf dem Server nicht gefunden, kann nicht hinzugefügt werden ({plan.Unavailable.Count}): {list(plan.Unavailable)}"), true);

        private static string list(IReadOnlyList<BanchosucksLobbyTemplateItem> items)
        {
            string names = string.Join("; ", items.Take(MAX_LISTED_MAPS).Select(i => i.DisplayTitle));

            if (items.Count > MAX_LISTED_MAPS)
                names += ClickerStrings.Pick($"; and {items.Count - MAX_LISTED_MAPS} more", $"; und {items.Count - MAX_LISTED_MAPS} weitere");

            return names;
        }
    }
}
