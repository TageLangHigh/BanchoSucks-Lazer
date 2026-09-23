// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Medals.Awarders
{
    /// <summary>
    /// g0v0's "1st Anniversary!" medal awarder (ID: 100001)
    /// Awarded on 2026-08-10.
    /// </summary>
    public class G0V0FirstAnniversaryAwarder : IMedalAwarder
    {
        public int MedalId => IMedalAwarder.G0V0_ACHIEVEMENTS_ID_START + 1;

        private bool enabled;
        private DateTimeOffset? firstCheckTime;

        public bool Enabled
        {
            get
            {
                var today = DateTime.Now.Date;
                if (today.Year != 2026 || today.Month != 8 || today.Day != 10)
                    return false;

                return enabled;
            }
            set => enabled = value;
        }

        public bool CheckMedalCriteria(OsuGameBase game)
        {
            firstCheckTime ??= DateTimeOffset.Now;
            return DateTimeOffset.Now - firstCheckTime.Value >= TimeSpan.FromSeconds(10);
        }
    }
}
