// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Localisation.Mania;
using osu.Game.Localisation;
using osu.Game.Rulesets.Mania.UI;

namespace osu.Game.Rulesets.Mania.Mods
{
    public class ManiaModCover : ManiaModWithPlayfieldCover
    {
        public override string Name => "Cover";
        public override string Acronym => "CO";
        public override IconUsage? Icon => OsuIcon.ModCover;

        public override LocalisableString Description => ModsStrings.CoverDescription;

        protected override CoverExpandDirection ExpandDirection => Direction.Value;

        public override Type[] IncompatibleMods => base.IncompatibleMods.Concat(new[]
        {
            typeof(ManiaModHidden),
            typeof(ManiaModFadeIn)
        }).ToArray();

        public override bool Ranked => true;

        public override bool ValidForFreestyleAsRequiredMod => false;

        [SettingSource(typeof(ModsStrings), nameof(ModsStrings.CoverCoverageLabel), nameof(ModsStrings.CoverCoverageDescription))]
        public override BindableNumber<float> Coverage { get; } = new BindableFloat(0.5f)
        {
            Precision = 0.1f,
            MinValue = 0.2f,
            MaxValue = 0.8f,
            Default = 0.5f,
        };

        [SettingSource(typeof(CommonModsStrings), nameof(CommonModsStrings.DirectionLabel), nameof(ModsStrings.CoverDirectionDescription))]
        public Bindable<CoverExpandDirection> Direction { get; } = new Bindable<CoverExpandDirection>();
    }
}
