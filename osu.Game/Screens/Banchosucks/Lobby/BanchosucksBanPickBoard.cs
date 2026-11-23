// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Screens.Banchosucks.Lobby
{
    public enum BanchosucksBanPickAction
    {
        Ban,
        Pick,
    }

    /// <summary>
    /// Banchosucks: one step on the ban/pick board.
    /// </summary>
    /// <param name="MapIndex">Index of the map in <see cref="BanchosucksBanPickBoard.Maps"/>.</param>
    /// <param name="Team">The team that acted.</param>
    /// <param name="Action">Ban or pick.</param>
    public readonly record struct BanchosucksBanPickEntry(int MapIndex, BanchosucksLobbyTeam Team, BanchosucksBanPickAction Action);

    /// <summary>
    /// Banchosucks: the ban/pick board of the lobby tools: which team bans or picks next and what happened so far.
    /// It is the host's notepad for a small tournament match; it never changes the room, a score or a result.
    /// </summary>
    /// <remarks>
    /// The order: first the bans, the teams taking turns, starting with <see cref="FirstTeam"/>; then the picks, the
    /// teams taking turns again, starting with the team that banned second (with no bans, with <see cref="FirstTeam"/>).
    /// Picking goes on until one map is left; that map is the decider and belongs to nobody.
    /// </remarks>
    public class BanchosucksBanPickBoard
    {
        public const int MAX_BANS_PER_TEAM = 4;

        /// <summary>
        /// The names of the maps on the board.
        /// </summary>
        public IReadOnlyList<string> Maps { get; }

        /// <summary>
        /// Bans every team has. Lowered when the board has too few maps to leave a decider.
        /// </summary>
        public int BansPerTeam { get; }

        /// <summary>
        /// The team that bans first.
        /// </summary>
        public BanchosucksLobbyTeam FirstTeam { get; }

        private readonly List<BanchosucksBanPickEntry> history = new List<BanchosucksBanPickEntry>();

        /// <summary>
        /// Everything that was banned and picked, in order.
        /// </summary>
        public IReadOnlyList<BanchosucksBanPickEntry> History => history;

        public BanchosucksBanPickBoard(IReadOnlyList<string> maps, int bansPerTeam, BanchosucksLobbyTeam firstTeam)
        {
            Maps = maps.ToArray();
            FirstTeam = firstTeam;

            // both teams ban equally often and at least one map stays
            BansPerTeam = Math.Clamp(bansPerTeam, 0, Math.Min(MAX_BANS_PER_TEAM, Math.Max(0, (Maps.Count - 1) / 2)));
        }

        /// <summary>
        /// Maps that are neither banned nor picked.
        /// </summary>
        public IEnumerable<int> FreeMaps => Enumerable.Range(0, Maps.Count).Where(i => EntryOf(i) == null);

        /// <summary>
        /// What happened to a map, or null when it is still free.
        /// </summary>
        public BanchosucksBanPickEntry? EntryOf(int mapIndex)
        {
            foreach (var entry in history)
            {
                if (entry.MapIndex == mapIndex)
                    return entry;
            }

            return null;
        }

        /// <summary>
        /// Whose turn it is and what for, or null when the board is finished (or has no map to act on).
        /// </summary>
        public (BanchosucksLobbyTeam Team, BanchosucksBanPickAction Action)? NextTurn
        {
            get
            {
                int free = FreeMaps.Count();
                int step = history.Count;
                int bans = BansPerTeam * 2;

                if (step < bans)
                    return (step % 2 == 0 ? FirstTeam : FirstTeam.Other(), BanchosucksBanPickAction.Ban);

                // the last map is the decider
                if (free <= 1)
                    return null;

                var firstPick = BansPerTeam > 0 ? FirstTeam.Other() : FirstTeam;
                return ((step - bans) % 2 == 0 ? firstPick : firstPick.Other(), BanchosucksBanPickAction.Pick);
            }
        }

        /// <summary>
        /// The map left over once everything else is banned or picked, or null while the board is still being worked on.
        /// </summary>
        public int? Decider
        {
            get
            {
                if (NextTurn != null || Maps.Count < 2)
                    return null;

                var free = FreeMaps.ToList();
                return free.Count == 1 ? free[0] : null;
            }
        }

        /// <summary>
        /// Lets the team whose turn it is ban or pick a map. Returns false when the map is taken, does not exist or the board is finished.
        /// </summary>
        public bool TryApply(int mapIndex, out BanchosucksBanPickEntry entry)
        {
            entry = default;

            if (mapIndex < 0 || mapIndex >= Maps.Count || EntryOf(mapIndex) != null)
                return false;

            if (NextTurn is not var (team, action))
                return false;

            entry = new BanchosucksBanPickEntry(mapIndex, team, action);
            history.Add(entry);
            return true;
        }

        /// <summary>
        /// Takes back the last step and returns it, or null when nothing has happened yet.
        /// </summary>
        public BanchosucksBanPickEntry? Undo()
        {
            if (history.Count == 0)
                return null;

            var last = history[^1];
            history.RemoveAt(history.Count - 1);
            return last;
        }

        /// <summary>
        /// The maps a team picked, in the order it picked them.
        /// </summary>
        public IEnumerable<int> PicksOf(BanchosucksLobbyTeam team) => history.Where(e => e.Team == team && e.Action == BanchosucksBanPickAction.Pick).Select(e => e.MapIndex);
    }
}
