// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Taiko;
using osu.Game.Scoring;
using osu.Game.Screens.Select;
using osu.Game.Screens.Select.Filter;
using osu.Game.Tests.Database;
using osu.Game.Tests.Resources;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>
    /// Banchosucks: the song select filter "no score yet" (<c>score=no</c> / <c>score=yes</c>).
    /// </summary>
    [TestFixture]
    public class BanchosucksLocalScoreFilterTest : RealmTest
    {
        [TestCase("score=no", false)]
        [TestCase("score=nein", false)]
        [TestCase("score:no", false)]
        [TestCase("score=0", false)]
        [TestCase("score=false", false)]
        [TestCase("score=none", false)]
        [TestCase("Score=NO", false)]
        [TestCase("scores=no", false)]
        [TestCase("score!=yes", false)]
        [TestCase("score=yes", true)]
        [TestCase("score=ja", true)]
        [TestCase("score=1", true)]
        [TestCase("score!=no", true)]
        [TestCase("score!:nein", true)]
        public void TestKeywordIsParsed(string query, bool expected)
        {
            var criteria = new FilterCriteria();
            FilterQueryParser.ApplyQueries(criteria, query);

            Assert.That(criteria.BanchosucksHasLocalScore, Is.EqualTo(expected));
            Assert.That(criteria.SearchText.Trim(), Is.Empty, "the keyword must not be searched as text");
            Assert.That(BanchosucksLocalScoreQuery.GetState(query), Is.EqualTo(expected));
        }

        [TestCase("score=maybe")]
        [TestCase("score>no")]
        [TestCase("score<=1")]
        [TestCase("score=")]
        [TestCase("highscore")]
        [TestCase("no score")]
        public void TestOtherTextStaysASearch(string query)
        {
            var criteria = new FilterCriteria();
            FilterQueryParser.ApplyQueries(criteria, query);

            Assert.That(criteria.BanchosucksHasLocalScore, Is.Null);
            Assert.That(criteria.SearchText, Is.EqualTo(query));
            Assert.That(BanchosucksLocalScoreQuery.GetState(query), Is.Null);
        }

        [Test]
        public void TestKeywordCombinesWithOtherFilters()
        {
            var criteria = new FilterCriteria();
            FilterQueryParser.ApplyQueries(criteria, "camellia stars>5 score=no");

            Assert.That(criteria.BanchosucksHasLocalScore, Is.False);
            Assert.That(criteria.StarDifficulty.Min, Is.EqualTo(5));
            Assert.That(criteria.SearchTerms.Select(t => t.SearchTerm), Is.EqualTo(new[] { "camellia" }));
        }

        [Test]
        public void TestLastKeywordWins()
        {
            var criteria = new FilterCriteria();
            FilterQueryParser.ApplyQueries(criteria, "score=yes score=no");

            Assert.That(criteria.BanchosucksHasLocalScore, Is.False);
            Assert.That(BanchosucksLocalScoreQuery.GetState("score=yes score=no"), Is.False);
        }

        [TestCase("", true, "score=no")]
        [TestCase("camellia", true, "camellia score=no")]
        [TestCase("camellia  stars>5 ", true, "camellia stars>5 score=no")]
        [TestCase("score=yes camellia", true, "camellia score=no")]
        [TestCase("score=no", true, "score=no")]
        [TestCase("score=no", false, "")]
        [TestCase("camellia score=nein stars>5", false, "camellia stars>5")]
        [TestCase("camellia score=maybe", false, "camellia score=maybe")]
        [TestCase("camellia", false, "camellia")]
        public void TestToggleEditsOnlyItsKeyword(string query, bool enabled, string expected)
        {
            string result = BanchosucksLocalScoreQuery.WithNoScore(query, enabled);

            Assert.That(result, Is.EqualTo(expected));
            Assert.That(BanchosucksLocalScoreQuery.GetState(result) == false, Is.EqualTo(enabled));
        }

        [Test]
        public void TestIdsAreLoadedOnceForAWholeLibrary()
        {
            var beatmaps = Enumerable.Range(0, 50000).Select(_ => new BeatmapInfo { Ruleset = new OsuRuleset().RulesetInfo }).ToList();
            var withScore = beatmaps.Where((_, i) => i % 7 == 0).Select(b => b.ID).ToHashSet();

            int loads = 0;
            var scored = new BanchosucksScoredBeatmaps(() =>
            {
                loads++;
                return withScore;
            });

            Assert.That(scored.IsLoaded, Is.False, "nothing is read before the filter needs it");

            var noScore = new FilterCriteria { BanchosucksHasLocalScore = false, BanchosucksScoredBeatmaps = scored };
            var hasScore = new FilterCriteria { BanchosucksHasLocalScore = true, BanchosucksScoredBeatmaps = scored };

            // three filter passes over the same library, as when the player keeps typing.
            for (int pass = 0; pass < 3; pass++)
            {
                Assert.That(beatmaps.Count(b => BeatmapCarouselFilterMatching.CheckCriteriaMatch(b, noScore)), Is.EqualTo(beatmaps.Count - withScore.Count));
                Assert.That(beatmaps.Count(b => BeatmapCarouselFilterMatching.CheckCriteriaMatch(b, hasScore)), Is.EqualTo(withScore.Count));
            }

            Assert.That(loads, Is.EqualTo(1));
            Assert.That(scored.Count, Is.EqualTo(withScore.Count));
        }

        [Test]
        public void TestFilterMatchesExactlyTheScoredDifficulties()
        {
            var played = new BeatmapInfo { Ruleset = new OsuRuleset().RulesetInfo };
            var unplayed = new BeatmapInfo { Ruleset = new OsuRuleset().RulesetInfo };
            var scored = new BanchosucksScoredBeatmaps(() => new[] { played.ID });

            var noScore = new FilterCriteria { BanchosucksHasLocalScore = false, BanchosucksScoredBeatmaps = scored };
            Assert.That(BeatmapCarouselFilterMatching.CheckCriteriaMatch(played, noScore), Is.False);
            Assert.That(BeatmapCarouselFilterMatching.CheckCriteriaMatch(unplayed, noScore), Is.True);

            var hasScore = new FilterCriteria { BanchosucksHasLocalScore = true, BanchosucksScoredBeatmaps = scored };
            Assert.That(BeatmapCarouselFilterMatching.CheckCriteriaMatch(played, hasScore), Is.True);
            Assert.That(BeatmapCarouselFilterMatching.CheckCriteriaMatch(unplayed, hasScore), Is.False);

            // no keyword, or a keyword without ids (a caller that does not provide them): nothing is filtered.
            Assert.That(BeatmapCarouselFilterMatching.CheckCriteriaMatch(played, new FilterCriteria { BanchosucksScoredBeatmaps = scored }), Is.True);
            Assert.That(BeatmapCarouselFilterMatching.CheckCriteriaMatch(played, new FilterCriteria { BanchosucksHasLocalScore = false }), Is.True);
        }

        [Test]
        public void TestScoresAreReadPerUserAndRuleset()
        {
            RunTestWithRealm((realm, _) =>
            {
                var set = TestResources.CreateTestBeatmapSetInfo(6, new[] { new OsuRuleset().RulesetInfo });
                realm.Write(r => r.Add(set));

                Guid[] ids = set.Beatmaps.Select(b => b.ID).ToArray();

                realm.Write(r =>
                {
                    var beatmaps = ids.Select(id => r.Find<BeatmapInfo>(id)!).ToArray();
                    var taiko = r.Find<RulesetInfo>(new TaikoRuleset().RulesetInfo.ShortName) ?? r.Add(new TaikoRuleset().RulesetInfo);

                    // 0: own score. 1: guest score (counts as own, like the rank badge). 2: somebody else's score.
                    // 3: own score that was deleted. 4: own score on an older version of the difficulty. 5: own score in another ruleset.
                    r.Add(createScore(beatmaps[0], 2));
                    r.Add(createScore(beatmaps[1], 1));
                    r.Add(createScore(beatmaps[2], 3));

                    var deleted = createScore(beatmaps[3], 2);
                    deleted.DeletePending = true;
                    r.Add(deleted);

                    var outdated = createScore(beatmaps[4], 2);
                    outdated.BeatmapHash = "an older version";
                    r.Add(outdated);

                    var otherRuleset = createScore(beatmaps[5], 2);
                    otherRuleset.Ruleset = taiko;
                    r.Add(otherRuleset);
                });

                Assert.That(BanchosucksLocalScoreTracker.Query(realm, 2, "osu"), Is.EquivalentTo(new[] { ids[0], ids[1] }));
                Assert.That(BanchosucksLocalScoreTracker.Query(realm, 3, "osu"), Is.EquivalentTo(new[] { ids[1], ids[2] }));
                Assert.That(BanchosucksLocalScoreTracker.Query(realm, null, "osu"), Is.EquivalentTo(new[] { ids[1] }), "a guest only owns guest scores");
                Assert.That(BanchosucksLocalScoreTracker.Query(realm, 2, "taiko"), Is.EquivalentTo(new[] { ids[5] }));
            });
        }

        [Test]
        public void TestIdsAreRefreshedWhenScoresChange()
        {
            RunTestWithRealm((realm, _) =>
            {
                var set = TestResources.CreateTestBeatmapSetInfo(2, new[] { new OsuRuleset().RulesetInfo });
                realm.Write(r => r.Add(set));

                Guid[] ids = set.Beatmaps.Select(b => b.ID).ToArray();

                int changes = 0;

                using (var tracker = new BanchosucksLocalScoreTracker(realm))
                {
                    tracker.ScoresChanged += () => changes++;

                    var before = tracker.Get(2, "osu");
                    realm.Run(r => r.Refresh());

                    Assert.That(before.Contains(ids[0]), Is.False);
                    Assert.That(tracker.Get(2, "osu"), Is.SameAs(before), "nothing changed, so the same ids are used again");
                    Assert.That(changes, Is.EqualTo(0));

                    // a change to a beatmap (as when a difficulty is played or its star rating is recalculated) is not a score change.
                    realm.Write(r => r.Find<BeatmapInfo>(ids[1])!.LastPlayed = DateTimeOffset.Now);
                    realm.Run(r => r.Refresh());

                    Assert.That(changes, Is.EqualTo(0));
                    Assert.That(tracker.Get(2, "osu"), Is.SameAs(before));

                    Guid scoreId = Guid.Empty;
                    realm.Write(r => scoreId = r.Add(createScore(r.Find<BeatmapInfo>(ids[0])!, 2)).ID);
                    realm.Run(r => r.Refresh());

                    Assert.That(changes, Is.EqualTo(1));

                    var afterPlay = tracker.Get(2, "osu");
                    Assert.That(afterPlay, Is.Not.SameAs(before));
                    Assert.That(afterPlay.Contains(ids[0]), Is.True);
                    Assert.That(afterPlay.Contains(ids[1]), Is.False);

                    // deleting the score (the game's soft delete) brings the difficulty back into "no score yet".
                    realm.Write(r => r.Find<ScoreInfo>(scoreId)!.DeletePending = true);
                    realm.Run(r => r.Refresh());

                    Assert.That(changes, Is.EqualTo(2));
                    Assert.That(tracker.Get(2, "osu").Contains(ids[0]), Is.False);

                    Assert.That(tracker.Get(2, "taiko"), Is.Not.SameAs(tracker.Get(2, "osu")), "another ruleset has its own ids");
                }
            });
        }

        private static ScoreInfo createScore(BeatmapInfo beatmap, int userId)
        {
            var score = TestResources.CreateTestScoreInfo(beatmap);
            score.User = new APIUser { Id = userId, Username = $"user {userId}" };
            return score;
        }

        private static IEnumerable<Operator> allOperators => Enum.GetValues<Operator>();

        [Test]
        public void TestOnlyEqualityOperatorsAreAccepted()
        {
            foreach (var op in allOperators)
            {
                bool expected = op == Operator.Equal || op == Operator.NotEqual;
                Assert.That(BanchosucksLocalScoreQuery.TryParse(op, "no", out _), Is.EqualTo(expected), op.ToString());
            }
        }
    }
}
