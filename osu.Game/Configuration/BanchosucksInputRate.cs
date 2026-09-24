// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.ComponentModel;

namespace osu.Game.Configuration
{
    /// <summary>
    /// Banchosucks: how often per second input is read from the operating system
    /// and processed by the game. osu!framework uses 1000 Hz for both; higher
    /// values can shave a little latency off fast mice and tablets at the cost of CPU time.
    /// </summary>
    public enum BanchosucksInputRate
    {
        [Description("1000 Hz (Standard)")]
        Default = 1000,

        [Description("2000 Hz")]
        Hz2000 = 2000,

        [Description("4000 Hz")]
        Hz4000 = 4000,

        [Description("8000 Hz")]
        Hz8000 = 8000,
    }
}
