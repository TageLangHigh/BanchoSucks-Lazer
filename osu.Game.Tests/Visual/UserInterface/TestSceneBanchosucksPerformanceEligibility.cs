// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Online.Banchosucks;
using osu.Game.Overlays;
using osu.Game.Overlays.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Screens.Select;

namespace osu.Game.Tests.Visual.UserInterface
{
    /// <summary>
    /// The "gives pp" label of the mod select overlay and the badge on the song select mods button.
    /// </summary>
    public partial class TestSceneBanchosucksPerformanceEligibility : OsuTestScene
    {
        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Aquamarine);

        private RankingInformationDisplay display = null!;
        private FooterButtonMods footerButtonMods = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create content", () => Children = new Drawable[]
            {
                display = new RankingInformationDisplay
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Y = -100,
                },
                footerButtonMods = new FooterButtonMods(new TestModSelectOverlay())
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.CentreLeft,
                    Action = () => { },
                    X = -100,
                    Y = 100,
                    Ruleset = { Value = new OsuRuleset().RulesetInfo },
                },
            });
        }

        private string tooltip => ((IHasTooltip)display.ChildrenOfType<SpriteText>().First(t => t is IHasTooltip)).TooltipText.ToString();

        private string label => display.ChildrenOfType<SpriteText>().First(t => t is IHasTooltip).Text.ToString();

        private void select(params Mod[] mods)
        {
            var verdict = BanchosucksPerformanceEligibility.Check(new OsuRuleset().RulesetInfo, null, mods);

            display.BanchosucksVerdict.Value = verdict;
            display.Ranked.Value = verdict.GivesPerformance;
            footerButtonMods.Mods.Value = mods;
        }

        private float badgeAlpha => footerButtonMods.ChildrenOfType<FooterButtonMods.UnrankedBadge>().Single().Alpha;

        [Test]
        public void TestUpstreamTextWithoutVerdict()
        {
            AddStep("unranked without a verdict", () => display.Ranked.Value = false);
            AddAssert("upstream text", () => label, () => Is.EqualTo("Unranked"));
        }

        [Test]
        public void TestLabelAndBadge()
        {
            AddStep("hidden + hard rock", () => select(new OsuModHidden(), new OsuModHardRock()));
            AddAssert("gives pp", () => label, () => Is.EqualTo("gives pp"));
            AddUntilStep("no badge", () => badgeAlpha, () => Is.EqualTo(0));

            AddStep("classic", () => select(new OsuModClassic()));
            AddAssert("classic gives pp", () => label, () => Is.EqualTo("gives pp"));
            AddUntilStep("no badge", () => badgeAlpha, () => Is.EqualTo(0));

            AddStep("double time 1.2x", () => select(new OsuModDoubleTime { SpeedChange = { Value = 1.2 } }));
            AddAssert("gives pp", () => label, () => Is.EqualTo("gives pp"));

            AddStep("difficulty adjust CS 8", () => select(new OsuModDifficultyAdjust { CircleSize = { Value = 8 } }));
            AddAssert("no pp", () => label, () => Is.EqualTo("no pp"));
            AddAssert("tooltip names the reason", () => tooltip, () => Does.StartWith("no pp on Banchosucks: DA circle size only from 0 to 7"));
            AddUntilStep("badge shown", () => badgeAlpha, () => Is.EqualTo(1));
            AddAssert("badge tooltip names the reason",
                () => footerButtonMods.ChildrenOfType<FooterButtonMods.UnrankedBadge>().Single().TooltipText.ToString(), () => Does.Contain("DA circle size only from 0 to 7"));

            AddStep("difficulty adjust CS 7", () => select(new OsuModDifficultyAdjust { CircleSize = { Value = 7 } }));
            AddAssert("gives pp", () => label, () => Is.EqualTo("gives pp"));
            AddUntilStep("no badge", () => badgeAlpha, () => Is.EqualTo(0));

            AddStep("relax", () => select(new OsuModRelax()));
            AddAssert("no pp", () => label, () => Is.EqualTo("no pp"));
            AddAssert("tooltip names the mod", () => tooltip, () => Does.StartWith("no pp on Banchosucks: mod RX"));
            AddUntilStep("badge shown", () => badgeAlpha, () => Is.EqualTo(1));

            AddStep("half time 0.8x", () => select(new OsuModHalfTime { SpeedChange = { Value = 0.8 } }));
            AddAssert("tooltip names the setting rule", () => tooltip, () => Does.StartWith("no pp on Banchosucks: HT only with default settings"));

            AddStep("no mods", () => select(Array.Empty<Mod>()));
            AddAssert("gives pp", () => label, () => Is.EqualTo("gives pp"));
        }

        private partial class TestModSelectOverlay : UserModSelectOverlay
        {
            public TestModSelectOverlay()
                : base(OverlayColourScheme.Aquamarine)
            {
                ShowPresets = true;
            }
        }
    }
}
