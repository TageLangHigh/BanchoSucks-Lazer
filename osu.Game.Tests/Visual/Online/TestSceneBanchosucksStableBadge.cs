// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Testing;
using osu.Game.Online.API;
using osu.Game.Online.Banchosucks;
using osu.Game.Overlays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Scoring;
using osu.Game.Tests.Resources;
using osuTK;

namespace osu.Game.Tests.Visual.Online
{
    /// <summary>
    /// The "Stable" badge with a fake plugin: scores 100 and 101 were imported from stable, everything else was made in lazer.
    /// </summary>
    public partial class TestSceneBanchosucksStableBadge : OsuTestScene
    {
        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Aquamarine);

        private readonly BanchosucksScoreOrigins origins = new BanchosucksScoreOrigins();

        private readonly List<long> requested = new List<long>();

        private FillFlowContainer flow = null!;

        private bool failRequests;

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.Cache(origins);
            return dependencies;
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("fake plugin", () =>
            {
                requested.Clear();
                failRequests = false;

                ((DummyAPIAccess)API).HandleRequest = request =>
                {
                    if (request is not GetBanchosucksScoreOriginRequest origin)
                        return false;

                    requested.Add(origin.ScoreId);

                    if (failRequests)
                        return false;

                    origin.TriggerSuccess(new BanchosucksScoreOriginResponse { StableScoreId = origin.ScoreId is 100 or 101 ? origin.ScoreId + 9000 : null });
                    return true;
                };
            });

            AddStep("create content", () => Children = new Drawable[]
            {
                origins,
                flow = new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Width = 600,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 5),
                },
            });
        }

        [TearDownSteps]
        public void TearDownSteps()
        {
            AddStep("remove content", () =>
            {
                // the component is reused by the next test, so it must not be disposed with the content
                Remove(origins, false);
                Clear();
                ((DummyAPIAccess)API).HandleRequest = null;
            });
        }

        private static ScoreInfo score(long onlineId, params Mod[] mods)
        {
            var score = TestResources.CreateTestScoreInfo(new OsuRuleset().RulesetInfo);
            score.OnlineID = onlineId;
            score.Mods = mods;
            return score;
        }

        private readonly Dictionary<BanchosucksStableBadge, long> ids = new Dictionary<BanchosucksStableBadge, long>();

        // a badge in a row of its own, as it sits in a leaderboard row
        private Drawable row(ScoreInfo score)
        {
            var badge = new BanchosucksStableBadge(score) { Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft };
            ids[badge] = score.OnlineID;

            return new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = 30,
                Child = badge,
            };
        }

        private BanchosucksStableBadge badgeOf(long onlineId) => flow.ChildrenOfType<BanchosucksStableBadge>().Single(b => ids[b] == onlineId);

        [Test]
        public void TestBadgeOnlyForImportedScores()
        {
            AddStep("add rows", () => flow.AddRange(new Drawable[]
            {
                row(score(100, new OsuModClassic(), new OsuModHidden())),
                row(score(200, new OsuModClassic())),
                row(score(300, new OsuModHidden())),
                row(score(400, new OsuModClassic(), new OsuModDoubleTime { SpeedChange = { Value = 1.2 } })),
                row(score(0, new OsuModClassic())),
            }));

            AddUntilStep("imported score has the badge", () => badgeOf(100).Shown);
            AddUntilStep("classic score made in lazer answered", () => origins.Cache.Lookup(200), () => Is.False);
            AddAssert("no badge for it", () => badgeOf(200).Shown, () => Is.False);
            AddAssert("no badge without classic", () => badgeOf(300).Shown, () => Is.False);

            // only the two scores that could be imports were asked about
            AddAssert("two requests", () => requested, () => Is.EquivalentTo(new long[] { 100, 200 }));
            AddAssert("tooltip names the stable score", () => badgeOf(100).TooltipText.ToString(), () => Does.Contain("9100"));
            AddAssert("badge takes room only when shown", () => badgeOf(100).DrawWidth > 0 && badgeOf(200).DrawWidth == 0);
        }

        [Test]
        public void TestAnswersAreReusedForTheSession()
        {
            AddStep("add a row", () => flow.Add(row(score(101, new OsuModClassic()))));
            AddUntilStep("badge shown", () => badgeOf(101).Shown);
            AddAssert("one request", () => requested, () => Is.EquivalentTo(new long[] { 101 }));

            AddStep("show the same score again", () =>
            {
                flow.Clear();
                flow.Add(row(score(101, new OsuModClassic())));
            });
            AddUntilStep("badge shown", () => badgeOf(101).Shown);
            AddAssert("still one request", () => requested, () => Has.Count.EqualTo(1));
        }

        [Test]
        public void TestRowsOutOfViewAreNotAskedAbout()
        {
            BasicScrollContainer scroll = null!;

            AddStep("a short list with twenty rows", () =>
            {
                Remove(flow, false);
                flow.Anchor = flow.Origin = Anchor.TopLeft;
                flow.RelativeSizeAxes = Axes.X;

                Add(scroll = new BasicScrollContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(600, 100),
                    Child = flow,
                });

                for (int i = 0; i < 20; i++)
                    flow.Add(row(score(1000 + i, new OsuModClassic())));
            });

            AddUntilStep("first rows asked", () => requested, () => Does.Contain(1000L).And.Contain(1001L));
            AddWaitStep("wait", 10);
            AddAssert("rows further down not asked", () => requested, () => Has.Count.LessThan(6).And.Not.Contain(1019L));

            AddStep("scroll to the end", () => scroll.ScrollToEnd(false));
            AddUntilStep("last row asked", () => requested, () => Does.Contain(1019L));
            AddAssert("rows in between never asked", () => requested, () => Does.Not.Contain(1010L));
        }

        [Test]
        public void TestAFailedLookupShowsNothing()
        {
            AddStep("plugin fails", () => failRequests = true);
            AddStep("add a row", () => flow.Add(row(score(555, new OsuModClassic()))));

            AddUntilStep("asked once", () => requested, () => Is.EquivalentTo(new long[] { 555 }));
            AddWaitStep("wait", 5);
            AddAssert("no badge", () => badgeOf(555).Shown, () => Is.False);
            AddAssert("nothing recorded", () => origins.Cache.Lookup(555), () => Is.Null);
            AddAssert("not asked again", () => requested, () => Has.Count.EqualTo(1));
        }
    }
}
