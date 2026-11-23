// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Banchosucks.Share;
using osu.Game.Screens.Ranking;
using osu.Game.Tests.Resources;
using osuTK;
using osuTK.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using RectangleF = osu.Framework.Graphics.Primitives.RectangleF;

namespace osu.Game.Tests.Visual.Ranking
{
    /// <summary>
    /// Banchosucks: the score card as a drawable (does it build, does everything stay inside its 1200 x 630) and the
    /// way to the image file. The headless test host does not draw, so there the export can only show that it gives
    /// up cleanly; with a host that draws, the same test checks the saved picture.
    /// </summary>
    public partial class TestSceneBanchosucksScoreCard : OsuTestScene
    {
        protected override Container<Drawable> Content { get; } = new Container { RelativeSizeAxes = Axes.Both };

        private NotificationOverlay notificationOverlay = null!;
        private BanchosucksCardExporter exporter = null!;
        private Storage cardStorage = null!;

        private BanchosucksScoreCard card = null!;

        [BackgroundDependencyLoader]
        private void load(Storage storage)
        {
            cardStorage = storage.GetStorageForDirectory(@"screenshots").GetStorageForDirectory(@"cards");

            // cached before the exporter is created: it resolves the overlay when it loads
            notificationOverlay = new NotificationOverlay
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
            };
            Dependencies.CacheAs<INotificationOverlay>(notificationOverlay);

            exporter = new BanchosucksCardExporter { Timeout = 1500 };
            Dependencies.Cache(exporter);

            base.Content.AddRange(new Drawable[] { Content, exporter, notificationOverlay });
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddUntilStep("exporter idle", () => !exporter.Busy);
            AddStep("clear", () =>
            {
                notificationOverlay.ChildrenOfType<Notification>().ToArray().ForEach(n => n.Close(false));
                Clear();
            });
        }

        private static ScoreInfo createScore(bool longTexts = false)
        {
            var score = TestResources.CreateTestScoreInfo(new OsuRuleset().RulesetInfo);

            score.BeatmapInfo!.Metadata.Title = longTexts ? string.Join(' ', Enumerable.Repeat("A Very Long Song Title", 12)) : "Some Song";
            score.BeatmapInfo.Metadata.Artist = longTexts ? string.Join(' ', Enumerable.Repeat("Long Artist Name", 12)) : "Some Artist";
            score.BeatmapInfo.Metadata.Author.Username = longTexts ? "MapperWithALongName" : "Some Mapper";
            score.BeatmapInfo.DifficultyName = longTexts ? string.Join(' ', Enumerable.Repeat("Extra Extreme", 12)) : "Insane";
            score.BeatmapInfo.StarRating = 6.54;

            score.Mods = longTexts
                ? new Mod[]
                {
                    new OsuModDoubleTime { SpeedChange = { Value = 1.3 } }, new OsuModHidden(), new OsuModHardRock(), new OsuModFlashlight(), new OsuModClassic(),
                    new OsuModSuddenDeath(), new OsuModTraceable(), new OsuModBarrelRoll(), new OsuModApproachDifferent(), new OsuModMuted(), new OsuModNoScope(),
                }
                : new Mod[] { new OsuModHidden(), new OsuModHardRock() };

            score.Accuracy = 0.987654;
            score.MaxCombo = longTexts ? 12345 : 1234;
            score.TotalScore = longTexts ? 1234567890 : 987654;
            score.Rank = ScoreRank.SH;
            score.PP = longTexts ? 12345.6 : 432.1;
            score.Statistics = new Dictionary<HitResult, int>
            {
                [HitResult.Great] = longTexts ? 12345 : 1500,
                [HitResult.Ok] = longTexts ? 1234 : 12,
                [HitResult.Meh] = longTexts ? 1234 : 3,
                [HitResult.Miss] = longTexts ? 1234 : 1,
                [HitResult.LargeTickHit] = longTexts ? 1234 : 80,
                [HitResult.SliderTailHit] = longTexts ? 1234 : 300,
            };
            score.MaximumStatistics = new Dictionary<HitResult, int>
            {
                [HitResult.Great] = longTexts ? 16047 : 1516,
                [HitResult.LargeTickHit] = longTexts ? 1300 : 84,
                [HitResult.SliderTailHit] = longTexts ? 1300 : 310,
            };

            if (longTexts)
                score.User = new APIUser { Id = 2, Username = "WWWWWWWWWWWWWWW" };

            return score;
        }

        private void showCard(bool longTexts, bool german = false) => AddStep("show card", () => Child = new Container
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Size = new Vector2(BanchosucksCardExporter.CARD_WIDTH, BanchosucksCardExporter.CARD_HEIGHT),
            Scale = new Vector2(0.6f),
            Children = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.DarkGray },
                card = new BanchosucksScoreCard(BanchosucksScoreCardData.From(createScore(longTexts), german: german)),
            }
        });

        /// <summary>
        /// Whether every text on the card lies inside the card (with a little slack for font metrics).
        /// </summary>
        private bool textsInsideCard()
        {
            RectangleF bounds = card.ScreenSpaceDrawQuad.AABBFloat.Inflate(2);

            return card.ChildrenOfType<SpriteText>().All(t =>
            {
                RectangleF text = t.ScreenSpaceDrawQuad.AABBFloat;
                return text.Width == 0 || bounds.Contains(text.TopLeft) && bounds.Contains(text.BottomRight);
            });
        }

        private bool hasText(string text) => card.ChildrenOfType<SpriteText>().Any(t => t.Text.ToString() == text);

        [Test]
        public void TestCardBuilds()
        {
            showCard(false);

            AddUntilStep("card loaded", () => card.IsLoaded);
            AddAssert("fixed size", () => card.DrawSize, () => Is.EqualTo(new Vector2(1200, 630)));
            AddAssert("accuracy", () => hasText("98.76%"));
            AddAssert("combo", () => hasText("1,234x") && hasText(card.Data.MaxComboText) && card.Data.MaxComboText.StartsWith("/ ", StringComparison.Ordinal));
            AddAssert("pp", () => hasText("432"));
            AddAssert("score", () => hasText("987,654"));
            AddAssert("player", () => hasText("peppy"));
            AddAssert("difficulty", () => hasText("[Insane]"));
            AddAssert("stars", () => hasText("6.54"));
            AddAssert("mods", () => hasText("HD") && hasText("HR"));
            AddAssert("rank", () => hasText("S"));
            AddAssert("mapper", () => hasText("mapped by Some Mapper"));
            AddAssert("hit counts", () => hasText("1,500") && hasText("80/84") && hasText("300/310"));
            AddAssert("website mark", () => hasText("banchosucks.cc"));
            AddUntilStep("laid out", () => card.ChildrenOfType<SpriteText>().All(t => t.DrawWidth > 0 || string.IsNullOrEmpty(t.Text.ToString())));
            AddAssert("everything inside the card", textsInsideCard);
        }

        [Test]
        public void TestGermanCard()
        {
            showCard(false, german: true);

            AddUntilStep("card loaded", () => card.IsLoaded);
            AddAssert("accuracy", () => hasText("98,76%"));
            AddAssert("labels", () => hasText("GENAUIGKEIT") && hasText("gespielt von") && hasText("gemappt von Some Mapper"));
            AddAssert("everything inside the card", textsInsideCard);
        }

        [Test]
        public void TestLongTextsStayInside()
        {
            showCard(true);

            AddUntilStep("card loaded", () => card.IsLoaded);
            AddAssert("mods limited", () => card.Data.Mods.Count, () => Is.EqualTo(BanchosucksScoreCardData.MAX_MODS));
            AddAssert("rest of the mods counted", () => hasText("+4"));
            AddWaitStep("let it lay out", 3);
            AddAssert("everything inside the card", textsInsideCard);
        }

        [Test]
        public void TestCardWithoutPpAndStars()
        {
            AddStep("show card", () =>
            {
                var score = createScore();
                score.PP = null;
                score.BeatmapInfo!.StarRating = -1;
                score.Mods = Array.Empty<Mod>();
                score.MaximumStatistics = new Dictionary<HitResult, int>();

                Child = card = new BanchosucksScoreCard(BanchosucksScoreCardData.From(score)) { Scale = new Vector2(0.5f) };
            });

            AddUntilStep("card loaded", () => card.IsLoaded);
            AddAssert("no pp block", () => !hasText("PP"));
            AddAssert("accuracy still there", () => hasText("98.76%"));
        }

        [Test]
        public void TestBrokenBackgroundDoesNotBreakCard()
        {
            AddStep("show card", () => Child = card = new BanchosucksScoreCard(BanchosucksScoreCardData.From(createScore()), () => throw new InvalidOperationException("broken image"))
            {
                Scale = new Vector2(0.5f)
            });

            AddUntilStep("card loaded anyway", () => card.IsLoaded);
        }

        [Test]
        public void TestExport()
        {
            Task<string?> export = null!;

            AddStep("export", () => export = exporter.Export(new BanchosucksScoreCard(BanchosucksScoreCardData.From(createScore())), "test card", ("Score card", "Score-Karte")));
            AddAssert("busy", () => exporter.Busy);
            AddStep("a second export is refused meanwhile", () =>
            {
                var second = exporter.Export(new BanchosucksScoreCard(BanchosucksScoreCardData.From(createScore())), "second", ("Score card", "Score-Karte"));
                Assert.That(second.IsCompleted && second.GetResultSafely() == null);
            });

            AddUntilStep("export finished", () => export.IsCompleted);
            AddAssert("not busy any more", () => !exporter.Busy);
            AddUntilStep("the player is told", () => notificationOverlay.ChildrenOfType<SimpleNotification>().Any(n => n.Text.ToString().Contains("Score card")));

            AddAssert("picture is right, or the host does not draw", () =>
            {
                string? filename = export.GetResultSafely();

                if (filename == null)
                {
                    // headless: no draw thread output, so no pixels. the exporter must have said so instead of saving something.
                    return notificationOverlay.ChildrenOfType<SimpleNotification>().Any(n => n.Text.ToString().Contains("could not be created"))
                           && !cardStorage.GetFiles(string.Empty).Any(f => f.Contains("test card"));
                }

                using (var stream = cardStorage.GetStream(filename))
                using (var image = SixLabors.ImageSharp.Image.Load<Rgba32>(stream))
                {
                    return filename.StartsWith("test card_", StringComparison.Ordinal)
                           && filename.EndsWith(".png", StringComparison.Ordinal)
                           && image.Width == BanchosucksCardExporter.CARD_WIDTH
                           && image.Height == BanchosucksCardExporter.CARD_HEIGHT
                           && BanchosucksCardImage.HasContent(image);
                }
            });

            AddStep("export again after the first", () => export = exporter.Export(new BanchosucksScoreCard(BanchosucksScoreCardData.From(createScore())), "test card 2", ("Score card", "Score-Karte")));
            AddAssert("accepted", () => exporter.Busy || export.IsCompleted);
            AddUntilStep("finished", () => export.IsCompleted);
        }

        [Test]
        public void TestButton()
        {
            BanchosucksScoreCardButton button = null!;

            AddStep("add button", () => Child = button = new BanchosucksScoreCardButton
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Score = { Value = createScore() },
            });

            AddUntilStep("loaded", () => button.IsLoaded);
            AddAssert("offered for a score", () => button.Available);
            AddStep("no score", () => button.Score.Value = null);
            AddAssert("hidden without score", () => !button.Available);
            AddStep("score again", () => button.Score.Value = createScore());

            AddStep("click", () => button.TriggerClick());
            AddUntilStep("export started", () => exporter.Busy || notificationOverlay.ChildrenOfType<SimpleNotification>().Any());
            AddUntilStep("export over", () => !exporter.Busy);
            AddUntilStep("button usable again", () => button.Enabled.Value);
        }
    }
}
