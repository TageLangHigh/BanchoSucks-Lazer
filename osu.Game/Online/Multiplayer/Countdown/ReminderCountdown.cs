// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using MessagePack;

namespace osu.Game.Online.Multiplayer.Countdown
{
    /// <summary>
    /// A countdown for match notifications/reminders, does not start the match.
    /// </summary>
    [MessagePackObject]
    public class ReminderCountdown : MultiplayerCountdown
    {
        public override bool IsExclusive => true;
    }
}
