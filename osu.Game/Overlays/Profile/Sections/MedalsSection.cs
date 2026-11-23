// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.Resources.Localisation.Web;

namespace osu.Game.Overlays.Profile.Sections
{
    public partial class MedalsSection : ProfileSection
    {
        public override LocalisableString Title => UsersStrings.ShowExtraMedalsTitle;

        public override string Identifier => @"medals";

        // Banchosucks: upstream leaves this section empty (and out of the profile). Here it holds the medal overview:
        // the server's catalogue grouped by origin, the player's medals highlighted.
        public MedalsSection()
        {
            Child = new BanchosucksMedalList(User);
        }
    }
}
