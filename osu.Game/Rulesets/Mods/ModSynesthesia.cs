// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Localisation;

namespace osu.Game.Rulesets.Mods
{
    /// <summary>
    /// Mod that colours hitobjects based on the musical division they are on
    /// </summary>
    public class ModSynesthesia : Mod
    {
        public override string Name => "Synesthesia";
        public override string Acronym => "SY";
        public override LocalisableString Description => CommonModsStrings.SynesthesiaDescription;
        public override IconUsage? Icon => OsuIcon.ModSynesthesia;
        public override ModType Type => ModType.Fun;
    }
}
