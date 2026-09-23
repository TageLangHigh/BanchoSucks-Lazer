// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Medals.Awarders
{
    /// <summary>
    /// Interface for awarding medals to users
    /// </summary>
    public interface IMedalAwarder
    {
        // ReSharper disable once InconsistentNaming
        const int G0V0_ACHIEVEMENTS_ID_START = 100_000;

        /// <summary>
        /// The ID of the medal to be awarded.
        /// </summary>
        int MedalId { get; }

        /// <summary>
        /// Whether the medal detection is enabled (disabled if the user already owns this medal).
        /// </summary>
        bool Enabled { get; set; }

        /// <summary>
        /// Checks if the criteria for awarding the medal are met
        /// </summary>
        bool CheckMedalCriteria(OsuGameBase game);
    }
}
