// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Diagnostics;
using Newtonsoft.Json;
using osu.Framework.IO.Network;
using osu.Game.Online.API;

namespace osu.Game.Online.Banchosucks
{
    /// <summary>
    /// Banchosucks: answer of <c>GET /api/plugins/banchosucks_status/summary</c> (lazer-side numbers, also read by the website).
    /// </summary>
    public class BanchosucksStatusSummary
    {
        /// <summary>Players online on the lazer server (BanchoBot not counted).</summary>
        [JsonProperty("lazer_online")]
        public int LazerOnline { get; set; }

        /// <summary>Open multiplayer rooms without a password.</summary>
        [JsonProperty("open_rooms")]
        public int OpenRooms { get; set; }

        /// <summary>"Artist - Title [Difficulty]" of today's daily challenge, or null.</summary>
        [JsonProperty("daily_challenge")]
        public string? DailyChallenge { get; set; }

        /// <summary>
        /// PROPOSED, not sent by the plugin today (2026-11): start of a planned restart or maintenance, UTC, ISO 8601.
        /// The countdown in the toolbar appears as soon as the plugin sends it; until then this stays null.
        /// </summary>
        [JsonProperty("maintenance_at")]
        public DateTimeOffset? MaintenanceAt { get; set; }

        /// <summary>PROPOSED together with <see cref="MaintenanceAt"/>: a short notice for the tooltip.</summary>
        [JsonProperty("maintenance_message")]
        public string? MaintenanceMessage { get; set; }
    }

    /// <summary>
    /// Banchosucks: the summary request. It also takes the time from sending to the complete answer (<see cref="RoundTrip"/>),
    /// which is the "ping" shown in the toolbar: a cheap request the client makes anyway, so the measurement costs nothing extra.
    /// </summary>
    public class GetBanchosucksStatusSummaryRequest : APIRequest<BanchosucksStatusSummary>
    {
        private Stopwatch? stopwatch;

        /// <summary>
        /// Time between the start of the HTTP request and its end, or null if it was not measured (never sent, or a test double answered).
        /// The first request of a session includes the connection setup.
        /// </summary>
        public TimeSpan? RoundTrip { get; set; }

        protected override string Route => "plugins";

        protected override string Target => @"banchosucks_status/summary";

        protected override WebRequest CreateWebRequest()
        {
            var request = base.CreateWebRequest();

            request.Started += () => stopwatch = Stopwatch.StartNew();
            request.Finished += () => RoundTrip = stopwatch?.Elapsed;

            return request;
        }
    }

    public enum BanchosucksConnectionState
    {
        /// <summary>Logged out, or no connection and not trying.</summary>
        Offline,

        /// <summary>Logging in or waiting for the second factor.</summary>
        Connecting,

        /// <summary>The api is reachable and the last status request (if any) was answered.</summary>
        Online,

        /// <summary>The api reports failing requests, or it says online but the status request failed.</summary>
        Trouble,
    }

    /// <summary>
    /// Banchosucks: what the toolbar's server status shows, worked out from the api state and the last summary. Pure.
    /// </summary>
    public static class BanchosucksServerStatus
    {
        /// <summary>A countdown is shown from this long before a planned maintenance (and until it has started).</summary>
        public static readonly TimeSpan COUNTDOWN_WINDOW = TimeSpan.FromHours(24);

        /// <summary>After the planned start the notice stays this long ("now"), then it is treated as over.</summary>
        public static readonly TimeSpan NOTICE_AFTER_START = TimeSpan.FromMinutes(15);

        public static BanchosucksConnectionState Connection(APIState api, bool statusRequestFailed)
        {
            switch (api)
            {
                case APIState.Online:
                    return statusRequestFailed ? BanchosucksConnectionState.Trouble : BanchosucksConnectionState.Online;

                case APIState.Failing:
                    return BanchosucksConnectionState.Trouble;

                case APIState.Connecting:
                case APIState.RequiresSecondFactorAuth:
                    return BanchosucksConnectionState.Connecting;

                default:
                    return BanchosucksConnectionState.Offline;
            }
        }

        /// <summary>
        /// Time left until a planned maintenance, zero while it is starting, or null when none is planned, it is further away
        /// than <see cref="COUNTDOWN_WINDOW"/>, or it is over.
        /// </summary>
        public static TimeSpan? MaintenanceIn(BanchosucksStatusSummary? summary, DateTimeOffset now)
        {
            if (summary?.MaintenanceAt is not DateTimeOffset at)
                return null;

            var left = at - now;

            if (left > COUNTDOWN_WINDOW || left < -NOTICE_AFTER_START)
                return null;

            return left < TimeSpan.Zero ? TimeSpan.Zero : left;
        }

        /// <summary>
        /// "4:05" below an hour, "2:15 h" from there on.
        /// </summary>
        public static string FormatCountdown(TimeSpan left)
        {
            if (left < TimeSpan.Zero)
                left = TimeSpan.Zero;

            // show the second that is running, so the countdown ends on 0:00 and not a second early
            left = TimeSpan.FromSeconds(Math.Ceiling(left.TotalSeconds));

            return left.TotalHours >= 1
                ? $"{(int)left.TotalHours}:{left.Minutes:00} h"
                : $"{(int)left.TotalMinutes}:{left.Seconds:00}";
        }

        /// <summary>
        /// The round trip as whole milliseconds for display, or null if there is none worth showing.
        /// </summary>
        public static int? Milliseconds(TimeSpan? roundTrip)
        {
            if (roundTrip is not TimeSpan time || time < TimeSpan.Zero)
                return null;

            return (int)Math.Min(99_999, Math.Round(time.TotalMilliseconds));
        }
    }
}
