// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Sprites;
using osu.Game.Replays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Screens.Ranking.Statistics;
using osuTK;

namespace osu.Game.Tests.Visual.Ranking
{
    /// <summary>
    /// Banchosucks: the miss review, fed with the hit events osu! really produces for a hand-made replay.
    /// </summary>
    public partial class TestSceneBanchosucksMissReview : ScreenTestScene
    {
        private ReplayPlayer player = null!;
        private List<HitEvent> hitEvents = null!;
        private BanchosucksMissReview review = null!;

        // default difficulty (OD 5): hittable within +-149.5 ms, a press within +-400 ms still gives a judgement
        private static readonly Vector2 circle_aim = new Vector2(256, 192);
        private static readonly Vector2 circle_not_pressed = new Vector2(100, 100);
        private static readonly Vector2 circle_too_early = new Vector2(400, 300);
        private static readonly Vector2 circle_hit = new Vector2(150, 300);
        private static readonly Vector2 circle_far_too_early = new Vector2(300, 100);

        [Test]
        public void TestReasonsFromARealReplay()
        {
            AddStep("play replay", () =>
            {
                Beatmap.Value = CreateWorkingBeatmap(new Beatmap<OsuHitObject>
                {
                    HitObjects =
                    {
                        new HitCircle { StartTime = 1000, Position = circle_aim },
                        new HitCircle { StartTime = 2000, Position = circle_not_pressed },
                        new HitCircle { StartTime = 3000, Position = circle_too_early },
                        new HitCircle { StartTime = 4500, Position = circle_hit },
                        new HitCircle { StartTime = 5500, Position = circle_far_too_early },
                    },
                    BeatmapInfo =
                    {
                        Ruleset = new OsuRuleset().RulesetInfo
                    },
                });

                SelectedMods.Value = new Mod[] { new OsuModNoFail() };

                var frames = new List<ReplayFrame>
                {
                    new OsuReplayFrame(0, new Vector2(500, 380)),
                    // in time, but 120 px beside the first circle
                    new OsuReplayFrame(1000, circle_aim + new Vector2(120, 0), OsuAction.LeftButton),
                    new OsuReplayFrame(1050, circle_aim + new Vector2(120, 0)),
                    // nothing at all for the second circle
                    // 200 ms early on the third one: inside the miss window, before the hit window
                    new OsuReplayFrame(2800, circle_too_early, OsuAction.LeftButton),
                    new OsuReplayFrame(2850, circle_too_early),
                    // the fourth one is hit
                    new OsuReplayFrame(4500, circle_hit, OsuAction.LeftButton),
                    new OsuReplayFrame(4550, circle_hit),
                    // 450 ms early on the fifth one: before the miss window, so the click does not count, and no second try
                    new OsuReplayFrame(5050, circle_far_too_early, OsuAction.LeftButton),
                    new OsuReplayFrame(5100, circle_far_too_early),
                    new OsuReplayFrame(7000, circle_far_too_early),
                };

                LoadScreen(player = new ReplayPlayer(new Score { Replay = new Replay { Frames = frames } }, new PlayerConfiguration { ShowResults = false }));
            });

            AddUntilStep("replay played", () => player.IsCurrentScreen() && player.GameplayState.HasPassed);
            AddStep("take hit events", () => hitEvents = player.ChildrenOfType<ScoreProcessor>().Single().HitEvents.ToList());

            AddAssert("five judgements", () => hitEvents, () => Has.Count.EqualTo(5));
            AddAssert("fourth circle was hit", () => hitEvents.Single(e => e.HitObject.StartTime == 4500).Result.IsHit());

            AddAssert("reasons", () => BanchosucksMissClassifier.Analyse(hitEvents, new OsuRuleset()).Select(m => (m.Time, m.Reason)), () => Is.EqualTo(new[]
            {
                (1000.0, BanchosucksMissReason.Aim),
                (2000.0, BanchosucksMissReason.NotHitInTime),
                (3000.0, BanchosucksMissReason.TooEarly),
                (5500.0, BanchosucksMissReason.ClickIgnored),
            }));

            // the circle has a radius of 32 at the default circle size
            AddAssert("aim distance is measured from the edge of the circle", () => BanchosucksMissClassifier.Analyse(hitEvents, new OsuRuleset()).First().DistanceOutside,
                () => Is.EqualTo(120 - 32).Within(1.5));

            AddAssert("too early by 200 ms", () => BanchosucksMissClassifier.Analyse(hitEvents, new OsuRuleset()).Single(m => m.Reason == BanchosucksMissReason.TooEarly).TimeOffset,
                () => Is.EqualTo(-200).Within(1));

            AddStep("exit player", () => player.Exit());
            AddUntilStep("player gone", () => !player.IsCurrentScreen());

            AddStep("show review", () => showReview(hitEvents));

            AddUntilStep("review loaded", () => review.IsLoaded);
            AddAssert("four lines", () => review.ChildrenOfType<BanchosucksMissReview.MissLine>().Count(), () => Is.EqualTo(4));
            AddAssert("times shown as mm:ss", () => review.ChildrenOfType<BanchosucksMissReview.MissLine>().Select(l => l.ChildrenOfType<OsuSpriteText>().First().Text.ToString()),
                () => Is.EqualTo(new[] { "00:01", "00:02", "00:03", "00:05" }));
            AddAssert("reasons shown", () => review.ChildrenOfType<BanchosucksMissReview.MissLine>().Select(l => l.ChildrenOfType<TruncatingSpriteText>().Single().Text.ToString()).ToArray(), () =>
                Has.Exactly(1).StartsWith("Aim: clicked")
                   .And.Exactly(1).StartsWith("Not hit in time")
                   .And.Exactly(1).StartsWith("Too early (-200 ms)")
                   .And.Exactly(1).StartsWith("Click on the circle did not count"));

            // this score is not in the database and there is no game to present it
            AddAssert("no replay, lines not clickable", () => !review.ReplayAvailable && review.ChildrenOfType<BanchosucksMissReview.MissLine>().All(l => !l.Enabled.Value));
        }

        private void showReview(List<HitEvent> events) => Child = new Container
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Width = 900,
            AutoSizeAxes = Axes.Y,
            Child = review = new BanchosucksMissReview(new ScoreInfo
            {
                Ruleset = new OsuRuleset().RulesetInfo,
                HitEvents = events,
            }),
        };

        [Test]
        public void TestScoreWithoutMisses()
        {
            AddStep("show review", () => showReview(new List<HitEvent> { new HitEvent(3, 1, HitResult.Great, new HitCircle { StartTime = 1000 }, null, null) }));

            AddUntilStep("review loaded", () => review.IsLoaded);
            AddAssert("no lines", () => !review.ChildrenOfType<BanchosucksMissReview.MissLine>().Any());
            AddAssert("says so", () => review.ChildrenOfType<OsuSpriteText>().Single().Text.ToString(), () => Is.EqualTo("No misses and no combo breaks."));
        }

        [Test]
        public void TestLongListIsCapped()
        {
            AddStep("show review", () => showReview(Enumerable.Range(0, 250).Select(i => new HitEvent(0, 1, HitResult.Miss, new Spinner { StartTime = i * 1000 }, null, null)).ToList()));

            AddUntilStep("review loaded", () => review.IsLoaded);
            AddAssert("all misses known", () => review.Misses, () => Has.Count.EqualTo(250));
            AddAssert("only the first hundred shown", () => review.ChildrenOfType<BanchosucksMissReview.MissLine>().Count(), () => Is.EqualTo(BanchosucksMissReview.MAX_LINES));
            AddAssert("rest is counted", () => review.ChildrenOfType<OsuSpriteText>().Any(t => t.Text.ToString() == "... and 150 more"));
        }
    }
}
