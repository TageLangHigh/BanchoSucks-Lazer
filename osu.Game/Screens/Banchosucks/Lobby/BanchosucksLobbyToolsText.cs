// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Screens.Banchosucks.Clicker;

namespace osu.Game.Screens.Banchosucks.Lobby
{
    /// <summary>
    /// Banchosucks: what the lobby tools write into the room chat and show on their panel, English or German by the
    /// host's client language. The chat lines are ordinary messages from the host: player names, numbers and map names.
    /// </summary>
    public static class BanchosucksLobbyToolsText
    {
        /// <summary>
        /// A chat message is kept below this length; longer announcements are split into several messages.
        /// </summary>
        public const int MAX_MESSAGE_LENGTH = 400;

        public static string TeamName(BanchosucksLobbyTeam team)
            => team == BanchosucksLobbyTeam.Red ? ClickerStrings.Pick("Team Red", "Team Rot") : ClickerStrings.Pick("Team Blue", "Team Blau");

        /// <summary>
        /// The chat messages announcing a team draw.
        /// </summary>
        /// <param name="result">The draw.</param>
        /// <param name="playersSwitchThemselves">Whether the room is in team versus mode, where everybody has to click into their team (only the host's own team can be set by the host's client).</param>
        public static List<string> TeamDraw(BanchosucksTeamDrawResult result, bool playersSwitchThemselves)
        {
            int count = result.Red.Count + result.Blue.Count;

            var parts = new List<string>
            {
                ClickerStrings.Pick($"Team draw, {count} players, seed {result.Seed}:", $"Team-Auslosung, {count} Spieler, Seed {result.Seed}:")
            };

            foreach (var team in new[] { BanchosucksLobbyTeam.Red, BanchosucksLobbyTeam.Blue })
                parts.AddRange(namesWithLabel($"{TeamName(team)}:", result.Members(team).Select(p => p.Name)));

            if (playersSwitchThemselves)
                parts.Add(ClickerStrings.Pick("Please switch to your team yourselves.", "Bitte wechselt selbst in euer Team."));

            return Pack(parts);
        }

        /// <summary>
        /// The chat messages announcing a roll the host made for some players.
        /// </summary>
        public static List<string> Roll(IReadOnlyList<(BanchosucksLobbyPlayer Player, int Roll)> rolls)
        {
            var parts = new List<string>
            {
                ClickerStrings.Pick($"Roll {BanchosucksLobbyDraw.ROLL_MIN}-{BanchosucksLobbyDraw.ROLL_MAX}:", $"Würfeln {BanchosucksLobbyDraw.ROLL_MIN}-{BanchosucksLobbyDraw.ROLL_MAX}:")
            };

            parts.AddRange(rolls.Select((r, i) => $"{r.Player.Name} {r.Roll}{(i < rolls.Count - 1 ? "," : ".")}"));

            var highest = BanchosucksLobbyDraw.Highest(rolls);

            if (rolls.Count > 1)
            {
                parts.Add(highest.Count == 1
                    ? ClickerStrings.Pick($"Highest: {highest[0].Name}.", $"Höchster Wurf: {highest[0].Name}.")
                    : ClickerStrings.Pick($"Tie between {string.Join(", ", highest.Select(p => p.Name))}.", $"Gleichstand zwischen {string.Join(", ", highest.Select(p => p.Name))}."));
            }

            return Pack(parts);
        }

        /// <summary>
        /// The chat message for one ban or pick.
        /// </summary>
        public static string BanPick(BanchosucksBanPickBoard board, BanchosucksBanPickEntry entry)
            => entry.Action == BanchosucksBanPickAction.Ban
                ? clip(ClickerStrings.Pick($"{TeamName(entry.Team)} bans: {board.Maps[entry.MapIndex]}", $"{TeamName(entry.Team)} bannt: {board.Maps[entry.MapIndex]}"))
                : clip(ClickerStrings.Pick($"{TeamName(entry.Team)} picks: {board.Maps[entry.MapIndex]}", $"{TeamName(entry.Team)} pickt: {board.Maps[entry.MapIndex]}"));

        /// <summary>
        /// The chat message for a ban or pick that was taken back.
        /// </summary>
        public static string Undone(BanchosucksBanPickBoard board, BanchosucksBanPickEntry entry)
            => entry.Action == BanchosucksBanPickAction.Ban
                ? clip(ClickerStrings.Pick($"Taken back: {TeamName(entry.Team)}'s ban of {board.Maps[entry.MapIndex]}", $"Zurückgenommen: Bann von {TeamName(entry.Team)} für {board.Maps[entry.MapIndex]}"))
                : clip(ClickerStrings.Pick($"Taken back: {TeamName(entry.Team)}'s pick of {board.Maps[entry.MapIndex]}", $"Zurückgenommen: Pick von {TeamName(entry.Team)} für {board.Maps[entry.MapIndex]}"));

        /// <summary>
        /// The chat message naming the map that is left over, or null while the board is not finished.
        /// </summary>
        public static string? Decider(BanchosucksBanPickBoard board)
            => board.Decider is int index ? clip(ClickerStrings.Pick($"Decider: {board.Maps[index]}", $"Entscheidungs-Map: {board.Maps[index]}")) : null;

        /// <summary>
        /// The line on the panel saying whose turn it is.
        /// </summary>
        public static string NextTurn(BanchosucksBanPickBoard board)
        {
            if (board.NextTurn is var (team, action))
            {
                return action == BanchosucksBanPickAction.Ban
                    ? ClickerStrings.Pick($"Next: {TeamName(team)} bans", $"Als Nächstes: {TeamName(team)} bannt")
                    : ClickerStrings.Pick($"Next: {TeamName(team)} picks", $"Als Nächstes: {TeamName(team)} pickt");
            }

            return Decider(board) ?? ClickerStrings.Pick("The board is finished.", "Das Board ist fertig.");
        }

        /// <summary>
        /// What the board shows next to a map.
        /// </summary>
        public static string State(BanchosucksBanPickBoard board, int mapIndex)
        {
            if (board.EntryOf(mapIndex) is BanchosucksBanPickEntry entry)
            {
                if (entry.Action == BanchosucksBanPickAction.Ban)
                    return ClickerStrings.Pick($"banned by {TeamName(entry.Team)}", $"gebannt von {TeamName(entry.Team)}");

                int order = board.PicksOf(entry.Team).ToList().IndexOf(mapIndex) + 1;
                return ClickerStrings.Pick($"pick {order} of {TeamName(entry.Team)}", $"Pick {order} von {TeamName(entry.Team)}");
            }

            return board.Decider == mapIndex ? ClickerStrings.Pick("decider", "Entscheidungs-Map") : string.Empty;
        }

        /// <summary>
        /// Joins parts with spaces into as few messages as possible, none longer than <see cref="MAX_MESSAGE_LENGTH"/>.
        /// A part is never torn apart unless it is too long on its own.
        /// </summary>
        public static List<string> Pack(IEnumerable<string> parts)
        {
            var messages = new List<string>();
            string current = string.Empty;

            foreach (string raw in parts)
            {
                string part = clip(raw);

                if (current.Length > 0 && current.Length + 1 + part.Length > MAX_MESSAGE_LENGTH)
                {
                    messages.Add(current);
                    current = string.Empty;
                }

                current = current.Length == 0 ? part : $"{current} {part}";
            }

            if (current.Length > 0)
                messages.Add(current);

            return messages;
        }

        private static IEnumerable<string> namesWithLabel(string label, IEnumerable<string> names)
        {
            var list = names.ToList();

            if (list.Count == 0)
            {
                yield return $"{label} -";

                yield break;
            }

            for (int i = 0; i < list.Count; i++)
            {
                string name = i < list.Count - 1 ? $"{list[i]}," : $"{list[i]}.";
                yield return i == 0 ? $"{label} {name}" : name;
            }
        }

        private static string clip(string text) => text.Length > MAX_MESSAGE_LENGTH ? string.Concat(text.AsSpan(0, MAX_MESSAGE_LENGTH - 1), "…") : text;
    }
}
