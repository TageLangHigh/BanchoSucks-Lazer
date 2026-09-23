// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Localisation.Mania;

namespace osu.Game.Rulesets.Mania.Mods
{
    public class ManiaModKey2 : ManiaKeyMod
    {
        public override int KeyCount => 2;
        public override string Name => "Two Keys";
        public override string Acronym => "2K";
        public override IconUsage? Icon => OsuIcon.ModTwoKeys;
        public override LocalisableString Description => ModsStrings.KeyModDescription(2);
        public override bool Ranked => false;
    }
}
