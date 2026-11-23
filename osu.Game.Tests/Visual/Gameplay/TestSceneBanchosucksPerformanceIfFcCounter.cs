// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Judgements;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD;
using osu.Game.Skinning.Triangles;
using osu.Game.Tests.Gameplay;
using osuTK;

namespace osu.Game.Tests.Visual.Gameplay
{
    /// <summary>
    /// The "pp if FC" counter next to a stock pp counter, both fed by the same score processor.
    /// </summary>
    public partial class TestSceneBanchosucksPerformanceIfFcCounter : OsuTestScene
    {
        [Cached(typeof(ScoreProcessor))]
        private readonly ScoreProcessor scoreProcessor = new OsuScoreProcessor();

        [Cached]
        private readonly GameplayState gameplayState = TestGameplayState.Create(new OsuRuleset());

        private TrianglesPerformancePointsCounter stock = null!;
        private BanchosucksPerformanceIfFcCounter ifFc = null!;

        private int iteration;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("reset", () =>
            {
                var ruleset = new OsuRuleset();
                var beatmap = CreateWorkingBeatmap(ruleset.RulesetInfo).GetPlayableBeatmap(ruleset.RulesetInfo);

                iteration = 0;
                scoreProcessor.ApplyBeatmap(beatmap);
            });

            AddStep("create counters", () => Child = new FillFlowContainer
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 20),
                Scale = new Vector2(3),
                Children = new Drawable[]
                {
                    stock = new TrianglesPerformancePointsCounter { Anchor = Anchor.TopCentre, Origin = Anchor.TopCentre },
                    ifFc = new BanchosucksPerformanceIfFcCounter { Anchor = Anchor.TopCentre, Origin = Anchor.TopCentre },
                },
            });
        }

        [Test]
        public void TestSameAsStockWhileFullCombo()
        {
            AddRepeatStep("hit", () => applyJudgement(HitResult.Great), 10);
            AddStep("one 100", () => applyJudgement(HitResult.Ok));

            AddUntilStep("both valid", () => stock.IsValid && ifFc.IsValid);
            AddUntilStep("stock counter above zero", () => stock.Current.Value, () => Is.GreaterThan(0));
            AddAssert("same value", () => ifFc.Current.Value, () => Is.EqualTo(stock.Current.Value));
            AddAssert("nothing replaced", () => ifFc.LastProjection?.Replaced, () => Is.EqualTo(0));
        }

        [Test]
        public void TestHigherAfterAMiss()
        {
            AddRepeatStep("hit", () => applyJudgement(HitResult.Great), 10);
            AddStep("miss", () => applyJudgement(HitResult.Miss));
            AddRepeatStep("hit", () => applyJudgement(HitResult.Great), 5);

            AddUntilStep("both valid", () => stock.IsValid && ifFc.IsValid);
            AddUntilStep("miss replaced", () => ifFc.LastProjection?.Replaced, () => Is.EqualTo(1));
            AddAssert("projected as full combo", () => ifFc.LastProjection?.MaxCombo, () => Is.EqualTo(16));
            AddAssert("projected accuracy is 100 %", () => ifFc.LastProjection?.Accuracy, () => Is.EqualTo(1));
            AddAssert("worth more than the real run", () => ifFc.Current.Value, () => Is.GreaterThan(stock.Current.Value));

            AddStep("revert the last hit", () => scoreProcessor.RevertResult(lastResult));
            AddAssert("follows reverts like the stock counter", () => ifFc.LastProjection?.MaxCombo, () => Is.EqualTo(15));
        }

        [Test]
        public void TestLabelOption()
        {
            AddStep("hide label", () => ifFc.ShowLabel.Value = false);
            AddStep("show label", () => ifFc.ShowLabel.Value = true);
        }

        private JudgementResult lastResult = null!;

        private void applyJudgement(HitResult type)
        {
            lastResult = new OsuJudgementResult(new HitObject { StartTime = iteration * 2000 }, new OsuJudgement()) { Type = type };
            scoreProcessor.ApplyResult(lastResult);
            iteration++;
        }
    }
}
