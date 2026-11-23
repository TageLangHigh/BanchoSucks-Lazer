// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Configuration
{
    public enum ResultsScreenStyle
    {
        [LocalisableDescription(typeof(UserInterfaceStrings), nameof(UserInterfaceStrings.DefaultResultsStyle))]
        Default,

        // Banchosucks: g0v0's "V2" style is not part of this client, only the default and the stable-style layout.
        [LocalisableDescription(typeof(UserInterfaceStrings), nameof(UserInterfaceStrings.LegacyResultsStyle))]
        Legacy,
    }
}
