// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets.Mania.Difficulty;
using osu.Game.Rulesets.Mania.Difficulty.Banchosucks;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Mania.Tests
{
    /// <summary>
    /// Banchosucks: the client's mania rating and pp must match the server's Python package to the digit. The expected
    /// values were produced with bancho.py <c>app/maniadiff</c> (algorithm version 1, pp version 1) for five real maps
    /// (4K rice, 4K LN, 4K chordjack, 7K LN, 9K) at normal speed, DT and HT.
    /// </summary>
    [TestFixture]
    public class BanchosucksManiaRatingTest
    {
        private const double star_precision = 0.0005;
        private const double pp_precision = 0.05;

        [TestCase("banchosucks-2325444", 1.0, 5.02150755, 1840, 844)]
        [TestCase("banchosucks-2325444", 1.5, 7.0596503, 1840, 844)]
        [TestCase("banchosucks-2325444", 0.75, 4.23244257, 1840, 844)]
        [TestCase("banchosucks-5170433", 1.0, 4.82522385, 3226, 0)]
        [TestCase("banchosucks-5170433", 1.5, 7.80803164, 3226, 0)]
        [TestCase("banchosucks-5170433", 0.75, 3.56293881, 3226, 0)]
        [TestCase("banchosucks-5425091", 1.0, 6.2561746, 1218, 59)]
        [TestCase("banchosucks-5425091", 1.5, 9.57124951, 1218, 59)]
        [TestCase("banchosucks-5425091", 0.75, 4.3576788, 1218, 59)]
        [TestCase("banchosucks-2283300", 1.0, 9.68795167, 5775, 4027)]
        [TestCase("banchosucks-2283300", 1.5, 11.18672528, 5775, 4027)]
        [TestCase("banchosucks-2283300", 0.75, 8.49350269, 5775, 4027)]
        [TestCase("banchosucks-4089098", 1.0, 6.93797053, 3901, 1507)]
        [TestCase("banchosucks-4089098", 1.5, 9.16516918, 3901, 1507)]
        [TestCase("banchosucks-4089098", 0.75, 5.9059517, 3901, 1507)]
        public void TestStarRatingMatchesServer(string name, double rate, double expectedStarRating, int expectedNotes, int expectedLongNotes)
        {
            var working = getBeatmap(name);
            var attributes = (ManiaDifficultyAttributes)new ManiaDifficultyCalculator(new ManiaRuleset().RulesetInfo, working).Calculate(modsForRate(rate));

            Assert.That(attributes.StarRating, Is.EqualTo(expectedStarRating).Within(star_precision));
            Assert.That(attributes.NoteCount, Is.EqualTo(expectedNotes));
            Assert.That(expectedLongNotes, Is.GreaterThanOrEqualTo(0));
        }

        /// <summary>
        /// A map whose first notes lie before 0 ms. The server rates such maps; the client threw an
        /// <see cref="IndexOutOfRangeException"/> on them until 2026.913.16. Expected values from bancho.py <c>app/maniadiff</c>
        /// for the same notes: 240 notes, 90 ms apart, columns in turn, every fifth one a 200 ms long note.
        /// </summary>
        [TestCase(-9, 1.0, 3.44962297, 48)]
        [TestCase(-9, 1.5, 4.32007671, 48)]
        [TestCase(0, 1.0, 3.44966718, 48)]
        [TestCase(-250, 1.0, 3.44455310, 47)]
        [TestCase(-250, 1.5, 4.31609142, 47)]
        public void TestNotesBeforeZeroMatchServer(int firstHead, double rate, double expectedStarRating, int expectedLongNotes)
        {
            var notes = new System.Collections.Generic.List<ManiaRating.Note>();

            for (int i = 0; i < 240; i++)
            {
                int head = firstHead + i * 90;
                notes.Add(new ManiaRating.Note(i % 4, head, i % 5 == 0 ? head + 200 : -1));
            }

            var result = ManiaRating.Calculate(notes, 4, 8.0, rate);

            Assert.That(result.StarRating, Is.EqualTo(expectedStarRating).Within(star_precision));
            Assert.That(result.NoteCount, Is.EqualTo(240));
            Assert.That(result.LongNoteCount, Is.EqualTo(expectedLongNotes));
        }

        [TestCase("banchosucks-2325444", 1.0, 311.008)]
        [TestCase("banchosucks-2325444", 1.5, 670.99)]
        [TestCase("banchosucks-2325444", 0.75, 210.832)]
        [TestCase("banchosucks-5170433", 1.0, 284.104)]
        [TestCase("banchosucks-2283300", 1.0, 1363.684)]
        [TestCase("banchosucks-4089098", 1.5, 1204.634)]
        public void TestPerformanceMatchesServer(string name, double rate, double expectedPerfectPp)
        {
            var ruleset = new ManiaRuleset();
            var working = getBeatmap(name);
            var mods = modsForRate(rate);
            var attributes = (ManiaDifficultyAttributes)new ManiaDifficultyCalculator(ruleset.RulesetInfo, working).Calculate(mods);
            var calculator = ruleset.CreatePerformanceCalculator().AsNonNull();

            Assert.That(calculator.Calculate(scoreWithAccuracy(ruleset, working, mods, attributes.NoteCount, 1.0), attributes).Total, Is.EqualTo(expectedPerfectPp).Within(pp_precision));

            // every judgement a 300 instead of a 320: accuracy exactly 300/320 = 93.75 %, accuracy term (5 * 0.9375 - 4)^2
            double allGreatPp = calculator.Calculate(scoreWithAccuracy(ruleset, working, mods, attributes.NoteCount, 300.0 / 320), attributes).Total;
            Assert.That(allGreatPp, Is.EqualTo(expectedPerfectPp * 0.47265625).Within(pp_precision));
        }

        [Test]
        public void TestModRules()
        {
            var ruleset = new ManiaRuleset();
            var working = getBeatmap("banchosucks-5170433");
            var calculator = ruleset.CreatePerformanceCalculator().AsNonNull();

            var plain = (ManiaDifficultyAttributes)new ManiaDifficultyCalculator(ruleset.RulesetInfo, working).Calculate(Array.Empty<Mod>());
            double basePp = calculator.Calculate(scoreWithAccuracy(ruleset, working, Array.Empty<Mod>(), plain.NoteCount, 1.0), plain).Total;

            var noFail = new Mod[] { new ManiaModNoFail() };
            Assert.That(calculator.Calculate(scoreWithAccuracy(ruleset, working, noFail, plain.NoteCount, 1.0), plain).Total, Is.EqualTo(basePp * 0.75).Within(pp_precision));

            var mirror = new Mod[] { new ManiaModMirror() };
            Assert.That(calculator.Calculate(scoreWithAccuracy(ruleset, working, mirror, plain.NoteCount, 1.0), plain).Total, Is.EqualTo(basePp).Within(pp_precision));

            var random = new Mod[] { new ManiaModRandom() };
            Assert.That(calculator.Calculate(scoreWithAccuracy(ruleset, working, random, plain.NoteCount, 1.0), plain).Total, Is.EqualTo(0));
        }

        [Test]
        public void TestTimedAttributesFollowTheMap()
        {
            var working = getBeatmap("banchosucks-2325444");
            var calc = new ManiaDifficultyCalculator(new ManiaRuleset().RulesetInfo, working);
            var full = calc.Calculate(Array.Empty<Mod>());
            var timed = calc.CalculateTimed(Array.Empty<Mod>());

            Assert.That(timed.Count, Is.EqualTo(working.Beatmap.HitObjects.Count));
            Assert.That(timed.Last().Attributes.StarRating, Is.EqualTo(full.StarRating).Within(star_precision));
            Assert.That(timed[timed.Count / 2].Attributes.StarRating, Is.GreaterThan(0));
            Assert.That(timed[timed.Count / 2].Attributes.StarRating, Is.LessThan(full.StarRating * 1.5));
        }

        [Test]
        public void TestUnsupportedKeyCountIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ManiaRating.Calculate(Array.Empty<ManiaRating.Note>(), 18, 8, 1));
        }

        private static Mod[] modsForRate(double rate)
        {
            if (rate == 1.5) return new Mod[] { new ManiaModDoubleTime() };
            if (rate == 0.75) return new Mod[] { new ManiaModHalfTime() };
            return Array.Empty<Mod>();
        }

        private static ScoreInfo scoreWithAccuracy(Ruleset ruleset, IWorkingBeatmap working, Mod[] mods, int notes, double accuracy)
        {
            // the server's accuracy weights 320 as the maximum; 97 % = enough 300s instead of 320s
            int greats = (int)Math.Round(notes * (1 - accuracy) / (1 - 300.0 / 320.0));
            greats = Math.Clamp(greats, 0, notes);

            var score = new ScoreInfo
            {
                Ruleset = ruleset.RulesetInfo,
                BeatmapInfo = (BeatmapInfo)working.BeatmapInfo,
                Mods = mods,
            };
            score.Statistics[HitResult.Perfect] = notes - greats;
            score.Statistics[HitResult.Great] = greats;
            return score;
        }

        private static IWorkingBeatmap getBeatmap(string name)
        {
            using (var resStream = openResource($"Testing.Beatmaps.{name}.osu"))
            using (var stream = new LineBufferedReader(resStream))
            {
                var decoder = Decoder.GetDecoder<Beatmap>(stream);
                ((LegacyBeatmapDecoder)decoder).ApplyOffsets = false;
                return new TestWorkingBeatmap(decoder.Decode(stream))
                {
                    BeatmapInfo =
                    {
                        Ruleset = new ManiaRuleset().RulesetInfo
                    }
                };
            }
        }

        private static Stream openResource(string name)
        {
            string localPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location).AsNonNull();
            return Assembly.LoadFrom(Path.Combine(localPath, "osu.Game.Rulesets.Mania.Tests.dll")).GetManifestResourceStream($@"osu.Game.Rulesets.Mania.Tests.Resources.{name}").AsNonNull();
        }
    }
}
