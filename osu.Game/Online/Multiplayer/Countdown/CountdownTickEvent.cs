// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using MessagePack;
using Newtonsoft.Json;

namespace osu.Game.Online.Multiplayer.Countdown
{
    /// <summary>
    /// Indicates a countdown tick (periodic reminder) in a <see cref="MultiplayerRoom"/>.
    /// </summary>
    [MessagePackObject]
    public class CountdownTickEvent : MatchServerEvent
    {
        /// <summary>
        /// The identifier of the countdown this tick pertains to.
        /// </summary>
        [Key(0)]
        public readonly int CountdownId;

        /// <summary>
        /// Number of seconds (fractional) until the completion of the countdown.
        /// </summary>
        [Key(1)]
        public readonly double Seconds;

        [JsonConstructor]
        [SerializationConstructor]
        public CountdownTickEvent(int countdownId, double seconds)
        {
            CountdownId = countdownId;
            Seconds = seconds;
        }
    }
}
