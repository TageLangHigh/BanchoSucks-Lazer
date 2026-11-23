// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;

namespace osu.Game.Online.Banchosucks
{
    /// <summary>
    /// Banchosucks: decides when a piece of server data may be requested again, so that no display can ask the server more often
    /// than it should. Pure: time comes in as milliseconds, nothing is requested here.
    /// </summary>
    /// <remarks>
    /// Three paces: <see cref="Interval"/> for the regular refresh, <see cref="MinimumInterval"/> as the floor that also
    /// holds when the player asks for a refresh (opens the panel, clicks), and the same floor after a failure.
    /// Never more than one request at a time.
    /// </remarks>
    public class BanchosucksRefreshGate
    {
        /// <summary>The server must not be asked for status-like data more often than this, whatever the trigger.</summary>
        public const double ONE_MINUTE = 60_000;

        public double Interval { get; }

        public double MinimumInterval { get; }

        private double? lastStart;

        public bool InFlight { get; private set; }

        /// <summary>Whether the last finished request failed.</summary>
        public bool LastFailed { get; private set; }

        /// <summary>Time of the last successful answer, or null if there is none.</summary>
        public double? LastSuccess { get; private set; }

        /// <param name="interval">Milliseconds between two regular refreshes.</param>
        /// <param name="minimumInterval">Milliseconds that always pass between two requests; at least one minute.</param>
        public BanchosucksRefreshGate(double interval, double minimumInterval = ONE_MINUTE)
        {
            MinimumInterval = Math.Max(ONE_MINUTE, minimumInterval);
            Interval = Math.Max(MinimumInterval, interval);
        }

        /// <summary>
        /// Whether a request may be sent now.
        /// </summary>
        /// <param name="now">Current time in milliseconds.</param>
        /// <param name="wanted">The player asked for fresh data (opened the panel, clicked): the regular interval is skipped, the floor is not.</param>
        public bool ShouldRefresh(double now, bool wanted = false)
        {
            if (InFlight)
                return false;

            if (lastStart == null)
                return true;

            double since = now - lastStart.Value;

            // a clock that went backwards (new session clock, test): do not wait for it to catch up
            if (since < 0)
                return true;

            return since >= (wanted || LastFailed ? MinimumInterval : Interval);
        }

        public void Started(double now)
        {
            InFlight = true;
            lastStart = now;
        }

        public void Succeeded(double now)
        {
            InFlight = false;
            LastFailed = false;
            LastSuccess = now;
        }

        public void Failed()
        {
            InFlight = false;
            LastFailed = true;
        }

        /// <summary>
        /// Milliseconds since the last successful answer, or null without one.
        /// </summary>
        public double? Age(double now) => LastSuccess == null ? null : Math.Max(0, now - LastSuccess.Value);
    }
}
