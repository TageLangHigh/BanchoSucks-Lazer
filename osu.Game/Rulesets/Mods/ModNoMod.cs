// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Localisation;

namespace osu.Game.Rulesets.Mods
{
    /// <summary>
    /// Indicates a type of mod that doesn't do anything.
    /// </summary>
    public sealed class ModNoMod : Mod
    {
        public override string Name => "No Mod";
        public override string Acronym => "NM";
        public override LocalisableString Description => CommonModsStrings.NoModDescription;
        public override IconUsage? Icon => OsuIcon.ModNoMod;
        public override ModType Type => ModType.System;
    }
}
