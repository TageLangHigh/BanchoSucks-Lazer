// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;

namespace osu.Game.Online.Multiplayer
{
    public enum WinCondition
    {
        [Description("Score")]
        Score,

        [Description("Accuracy")]
        Accuracy,

        [Description("Combo")]
        Combo,

        [Description("PP")]
        Pp
    }
}
