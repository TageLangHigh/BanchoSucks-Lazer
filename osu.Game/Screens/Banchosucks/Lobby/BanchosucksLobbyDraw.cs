// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace osu.Game.Screens.Banchosucks.Lobby
{
    /// <summary>
    /// Banchosucks: a player in the room, as far as the lobby tools need one.
    /// </summary>
    public readonly record struct BanchosucksLobbyPlayer(int UserId, string Name);

    public enum BanchosucksLobbyTeam
    {
        /// <summary>
        /// Team 0 of a team versus room.
        /// </summary>
        Red = 0,

        /// <summary>
        /// Team 1 of a team versus room.
        /// </summary>
        Blue = 1,
    }

    public static class BanchosucksLobbyTeamExtensions
    {
        public static BanchosucksLobbyTeam Other(this BanchosucksLobbyTeam team) => team == BanchosucksLobbyTeam.Red ? BanchosucksLobbyTeam.Blue : BanchosucksLobbyTeam.Red;
    }

    /// <summary>
    /// Banchosucks: a small random number generator that gives the same numbers for the same seed text on every PC and
    /// every client version (FNV-1a of the seed's UTF-8 bytes feeds SplitMix64). It exists so a host can name the seed
    /// of a team draw and anyone can repeat the draw; it is not meant for anything secret.
    /// </summary>
    public sealed class BanchosucksLobbyRandom
    {
        private ulong state;

        public BanchosucksLobbyRandom(string seed)
        {
            ulong hash = 14695981039346656037;

            foreach (byte b in Encoding.UTF8.GetBytes(seed))
            {
                hash ^= b;
                hash *= 1099511628211;
            }

            state = hash;
        }

        public ulong NextUInt64()
        {
            unchecked
            {
                ulong z = state += 0x9E3779B97F4A7C15;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EB;
                return z ^ (z >> 31);
            }
        }

        /// <summary>
        /// A number from 0 to <paramref name="maxExclusive"/> - 1, every one equally likely.
        /// </summary>
        public int Next(int maxExclusive)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxExclusive);

            ulong range = (ulong)maxExclusive;
            // numbers at or above this limit would favour the small results and are drawn again
            ulong limit = ulong.MaxValue - ulong.MaxValue % range;

            ulong value;

            do
            {
                value = NextUInt64();
            } while (value >= limit);

            return (int)(value % range);
        }

        /// <summary>
        /// A fresh six-digit seed for a draw the host did not name one for.
        /// </summary>
        public static string NewSeed() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("000000", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Banchosucks: two teams drawn from the players in the room.
    /// </summary>
    public class BanchosucksTeamDrawResult
    {
        public string Seed { get; }

        public IReadOnlyList<BanchosucksLobbyPlayer> Red { get; }

        public IReadOnlyList<BanchosucksLobbyPlayer> Blue { get; }

        public BanchosucksTeamDrawResult(string seed, IReadOnlyList<BanchosucksLobbyPlayer> red, IReadOnlyList<BanchosucksLobbyPlayer> blue)
        {
            Seed = seed;
            Red = red;
            Blue = blue;
        }

        public IReadOnlyList<BanchosucksLobbyPlayer> Members(BanchosucksLobbyTeam team) => team == BanchosucksLobbyTeam.Red ? Red : Blue;

        /// <summary>
        /// The team a player was drawn into, or null for somebody who was not part of the draw.
        /// </summary>
        public BanchosucksLobbyTeam? TeamOf(int userId)
        {
            if (Red.Any(p => p.UserId == userId))
                return BanchosucksLobbyTeam.Red;

            if (Blue.Any(p => p.UserId == userId))
                return BanchosucksLobbyTeam.Blue;

            return null;
        }
    }

    /// <summary>
    /// Banchosucks: draws teams and rolls numbers for the lobby tools. Pure bookkeeping; nothing here touches the room.
    /// </summary>
    public static class BanchosucksLobbyDraw
    {
        public const int ROLL_MIN = 1;
        public const int ROLL_MAX = 100;

        /// <summary>
        /// Splits the players into two teams whose sizes differ by one at most.
        /// </summary>
        /// <remarks>
        /// The players are first put in the order of their user ids, so the result depends only on who takes part and on
        /// the seed, not on the order they joined in. They are then shuffled (Fisher-Yates) and dealt out in turns,
        /// starting with a team the seed decides as well, so neither colour is favoured when the count is odd.
        /// </remarks>
        /// <param name="players">Who takes part. A user id that appears twice is taken once.</param>
        /// <param name="seed">The seed; the same seed and the same players always give the same teams.</param>
        public static BanchosucksTeamDrawResult DrawTeams(IEnumerable<BanchosucksLobbyPlayer> players, string seed)
        {
            var ordered = players.GroupBy(p => p.UserId).Select(g => g.First()).OrderBy(p => p.UserId).ToList();
            var random = new BanchosucksLobbyRandom(seed);

            for (int i = ordered.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (ordered[i], ordered[j]) = (ordered[j], ordered[i]);
            }

            var first = random.Next(2) == 0 ? BanchosucksLobbyTeam.Red : BanchosucksLobbyTeam.Blue;

            var red = new List<BanchosucksLobbyPlayer>();
            var blue = new List<BanchosucksLobbyPlayer>();

            for (int i = 0; i < ordered.Count; i++)
            {
                var team = i % 2 == 0 ? first : first.Other();
                (team == BanchosucksLobbyTeam.Red ? red : blue).Add(ordered[i]);
            }

            return new BanchosucksTeamDrawResult(seed, red, blue);
        }

        /// <summary>
        /// Rolls one number from <see cref="ROLL_MIN"/> to <see cref="ROLL_MAX"/> for every player, in the given order.
        /// </summary>
        /// <param name="players">Who rolls.</param>
        /// <param name="next">Returns a number from the first to the second argument, both included.</param>
        public static IReadOnlyList<(BanchosucksLobbyPlayer Player, int Roll)> Roll(IEnumerable<BanchosucksLobbyPlayer> players, Func<int, int, int> next)
            => players.Select(p => (p, Math.Clamp(next(ROLL_MIN, ROLL_MAX), ROLL_MIN, ROLL_MAX))).ToList();

        /// <summary>
        /// The players with the highest roll: one winner, or several when they tie.
        /// </summary>
        public static IReadOnlyList<BanchosucksLobbyPlayer> Highest(IReadOnlyList<(BanchosucksLobbyPlayer Player, int Roll)> rolls)
        {
            if (rolls.Count == 0)
                return Array.Empty<BanchosucksLobbyPlayer>();

            int best = rolls.Max(r => r.Roll);
            return rolls.Where(r => r.Roll == best).Select(r => r.Player).ToList();
        }
    }
}
