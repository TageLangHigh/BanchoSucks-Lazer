// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Rulesets.Mania.Difficulty;
using osu.Game.Screens.Play;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Mania.Tests
{
    /// <summary>
    /// Banchosucks: song select sorts and groups by the star rating stored in the local database
    /// (<see cref="BeatmapInfo.StarRating"/>), not by the one shown on the panels. Both must be our rating: after an
    /// import, after the reprocessing that a new <see cref="ManiaDifficultyCalculator.Version"/> triggers, and after the
    /// one-off check that repairs stale values in place (<see cref="BackgroundDataStoreProcessor.BANCHOSUCKS_STAR_RATING_CHECK"/>).
    /// </summary>
    [HeadlessTest]
    public partial class BanchosucksStoredStarRatingTest : OsuTestScene, ILocalUserPlayInfo
    {
        // the server's value for this map (bancho.py app/maniadiff, see BanchosucksManiaRatingTest)
        private const string map_name = "banchosucks-2325444";
        private const double expected_star_rating = 5.02150755;
        private const double precision = 0.0005;

        // what the official calculation gave before 2026.913.16; any value far from ours will do
        private const double stale_star_rating = 3.33;

        public IBindable<LocalUserPlayingState> PlayingState => isPlaying;

        private readonly Bindable<LocalUserPlayingState> isPlaying = new Bindable<LocalUserPlayingState>();

        private Guid importedSetId;

        private double? ratingAfterImport;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(OsuGameBase osu)
        {
            string archive = Path.Combine(Path.GetTempPath(), $"{map_name}-{Guid.NewGuid():N}.osz");

            using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
            using (var entry = zip.CreateEntry($"{map_name}.osu").Open())
            using (var resource = openResource($"Testing.Beatmaps.{map_name}.osu"))
                resource.CopyTo(entry);

            var imported = osu.Dependencies.Get<BeatmapManager>().Import(new ImportTask(archive)).GetResultSafely();
            importedSetId = imported.AsNonNull().ID;
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("not playing, check not done yet", () =>
            {
                isPlaying.Value = LocalUserPlayingState.NotPlaying;
                config.SetValue(OsuSetting.BanchosucksStarRatingCheck, 0);
                config.SetValue(OsuSetting.BanchosucksStarRatingCheckPosition, string.Empty);
            });
            // the import rates the map in the background; the first rating that shows up is the import's
            AddUntilStep("import finished", () => (ratingAfterImport ??= storedRating() >= 0 ? storedRating() : null) != null);
            // the cases share one imported map and some leave a stale rating behind
            AddStep("start from our rating", () => storeRating(expected_star_rating));
        }

        [Test]
        public void TestImportStoresOurRating()
        {
            AddAssert("rating stored by the import is ours", () => ratingAfterImport, () => Is.EqualTo(expected_star_rating).Within(precision));
        }

        /// <summary>
        /// The upstream path: an older difficulty version on record resets every rating and recalculates it.
        /// </summary>
        [Test]
        public void TestNewVersionReplacesStaleRating()
        {
            AddStep("store a stale rating", () => storeRating(stale_star_rating));
            AddStep("difficulty version on record is the official one", () => setRecordedVersion(20241007));
            AddStep("check already done", () => config.SetValue(OsuSetting.BanchosucksStarRatingCheck, BackgroundDataStoreProcessor.BANCHOSUCKS_STAR_RATING_CHECK));

            runProcessor();

            AddAssert("stored rating is ours again", storedRating, () => Is.EqualTo(expected_star_rating).Within(precision));
            AddAssert("version recorded", () => Realm.Run(r => r.Find<RulesetInfo>("mania")!.LastAppliedDifficultyVersion), () => Is.EqualTo(currentVersion));
        }

        /// <summary>
        /// What players reported after 2026.913.16: the version on record is current, yet the stored rating is the old one.
        /// </summary>
        [Test]
        public void TestCheckRepairsStaleRatingDespiteCurrentVersion()
        {
            AddStep("store a stale rating", () => storeRating(stale_star_rating));
            AddStep("difficulty version on record is current", () => setRecordedVersion(currentVersion));

            runProcessor();

            AddAssert("stored rating is ours again", storedRating, () => Is.EqualTo(expected_star_rating).Within(precision));
            AddAssert("check recorded as done", () => config.Get<int>(OsuSetting.BanchosucksStarRatingCheck), () => Is.EqualTo(BackgroundDataStoreProcessor.BANCHOSUCKS_STAR_RATING_CHECK));
            AddAssert("no position left behind", () => config.Get<string>(OsuSetting.BanchosucksStarRatingCheckPosition), () => Is.Empty);
        }

        [Test]
        public void TestCheckRunsOnlyOnce()
        {
            AddStep("difficulty version on record is current", () => setRecordedVersion(currentVersion));
            AddStep("check already done", () => config.SetValue(OsuSetting.BanchosucksStarRatingCheck, BackgroundDataStoreProcessor.BANCHOSUCKS_STAR_RATING_CHECK));
            AddStep("store a stale rating", () => storeRating(stale_star_rating));

            runProcessor();

            AddAssert("stored rating untouched", storedRating, () => Is.EqualTo(stale_star_rating));
        }

        [Test]
        public void TestCheckWaitsWhilePlaying()
        {
            AddStep("store a stale rating", () => storeRating(stale_star_rating));
            AddStep("difficulty version on record is current", () => setRecordedVersion(currentVersion));
            AddStep("playing", () => isPlaying.Value = LocalUserPlayingState.Playing);

            TestProcessor processor = null!;
            AddStep("run background processor", () => Add(processor = new TestProcessor()));
            AddWaitStep("wait some", 20);
            AddAssert("stored rating untouched", storedRating, () => Is.EqualTo(stale_star_rating));

            AddStep("not playing", () => isPlaying.Value = LocalUserPlayingState.NotPlaying);
            AddUntilStep("wait for completion", () => processor.Completed);
            AddAssert("stored rating is ours again", storedRating, () => Is.EqualTo(expected_star_rating).Within(precision));
        }

        private int currentVersion => new ManiaRuleset().CreateDifficultyCalculator(Beatmap.Value).Version;

        private void runProcessor()
        {
            TestProcessor processor = null!;
            AddStep("run background processor", () => Add(processor = new TestProcessor()));
            AddUntilStep("wait for completion", () => processor.Completed);
        }

        private void storeRating(double starRating) => Realm.Write(r =>
        {
            foreach (var b in r.Find<BeatmapSetInfo>(importedSetId)!.Beatmaps)
            {
                b.StarRating = starRating;

                // a map without online data is processed again at every start, which recalculates its rating as well;
                // the cases here are about maps that are otherwise complete
                b.LastOnlineUpdate = DateTimeOffset.Now;
            }
        });

        /// <summary>
        /// The processor reads the version from the ruleset store's detached copies, which are made at startup.
        /// </summary>
        private void setRecordedVersion(int version)
        {
            Realm.Write(r => r.Find<RulesetInfo>("mania")!.LastAppliedDifficultyVersion = version);
            rulesets.AvailableRulesets.Single(r => r.ShortName == "mania").LastAppliedDifficultyVersion = version;
        }

        private double storedRating() => Realm.Run(r => r.Find<BeatmapSetInfo>(importedSetId)?.Beatmaps.Single().StarRating ?? double.NaN);

        private static Stream openResource(string name)
        {
            string localPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location).AsNonNull();
            return Assembly.LoadFrom(Path.Combine(localPath, "osu.Game.Rulesets.Mania.Tests.dll")).GetManifestResourceStream($@"osu.Game.Rulesets.Mania.Tests.Resources.{name}").AsNonNull();
        }

        private partial class TestProcessor : BackgroundDataStoreProcessor
        {
            protected override int TimeToSleepDuringGameplay => 10;

            protected override bool SkipProcessing => false;

            public bool Completed => ProcessingTask.IsCompleted;
        }
    }
}
