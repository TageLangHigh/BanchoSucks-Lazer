// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Models;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.Settings.Sections.Maintenance;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using osu.Game.Tests.Resources;

namespace osu.Game.Tests.Visual.Settings
{
    /// <summary>
    /// Banchosucks: "clean up library" from the button to the removal, against a real database and file store.
    /// The point of most assertions: nothing is removed before the held confirmation, and what is removed is only soft-deleted.
    /// </summary>
    public partial class TestSceneBanchosucksLibraryCleanup : OsuManualInputManagerTestScene
    {
        protected override Container<Drawable> Content { get; } = new Container { RelativeSizeAxes = Axes.Both };

        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Purple);

        private DialogOverlay dialogOverlay = null!;
        private NotificationOverlay notificationOverlay = null!;
        private BeatmapManager beatmapManager = null!;
        private RulesetStore rulesets = null!;

        private BanchosucksLibraryCleanupButton button = null!;

        private readonly Dictionary<string, Guid> ids = new Dictionary<string, Guid>();

        [BackgroundDependencyLoader]
        private void load(GameHost host)
        {
            Dependencies.Cache(rulesets = new RealmRulesetStore(Realm));
            Dependencies.Cache(beatmapManager = new BeatmapManager(LocalStorage, Realm, null, Audio, Resources, host, Beatmap.Default));
            Dependencies.Cache(Realm);
            Dependencies.CacheAs(LocalStorage);

            base.Content.AddRange(new Drawable[]
            {
                Content,
                dialogOverlay = new DialogOverlay(),
                notificationOverlay = new NotificationOverlay
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                },
            });

            Dependencies.CacheAs<IDialogOverlay>(dialogOverlay);
            Dependencies.CacheAs<INotificationOverlay>(notificationOverlay);
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("close dialogs", () => dialogOverlay.CurrentDialog?.Hide());
            AddStep("clear notifications", () => notificationOverlay.ChildrenOfType<Notification>().ToArray().ForEach(n => n.Close(false)));

            AddStep("create library", () =>
            {
                ids.Clear();

                var fileStore = new RealmFileStore(Realm, LocalStorage);

                Realm.Write(r =>
                {
                    r.RemoveAll<ScoreInfo>();
                    r.RemoveAll<BeatmapInfo>();
                    r.RemoveAll<BeatmapMetadata>();
                    r.RemoveAll<BeatmapSetInfo>();

                    var ruleset = r.All<RulesetInfo>().First(i => i.ShortName == "osu");

                    RealmFile stored(string content) => fileStore.Add(new MemoryStream(Encoding.UTF8.GetBytes(content)), r);
                    RealmFile lost(string content) => r.Find<RealmFile>(hash(content)) ?? r.Add(new RealmFile { Hash = hash(content) });

                    BeatmapSetInfo add(string name, int onlineId, int addedOnDay, params RealmFile[] osuFiles)
                    {
                        var audio = stored($"audio of {name}");
                        var beatmapSet = new BeatmapSetInfo { OnlineID = onlineId, DateAdded = new DateTimeOffset(2026, 11, addedOnDay, 0, 0, 0, TimeSpan.Zero) };

                        foreach (var osuFile in osuFiles)
                        {
                            beatmapSet.Beatmaps.Add(new BeatmapInfo(ruleset, new BeatmapDifficulty(), new BeatmapMetadata { Title = name, Artist = "artist", AudioFile = "audio.mp3" })
                            {
                                Hash = osuFile.Hash,
                                MD5Hash = osuFile.Hash,
                                DifficultyName = $"difficulty {beatmapSet.Beatmaps.Count}",
                                BeatmapSet = beatmapSet,
                            });
                            beatmapSet.Files.Add(new RealmNamedFileUsage(osuFile, $"{beatmapSet.Beatmaps.Count}.osu"));
                        }

                        beatmapSet.Files.Add(new RealmNamedFileUsage(audio, "audio.mp3"));

                        var managed = r.Add(beatmapSet);
                        ids[name] = managed.ID;
                        return managed;
                    }

                    // imported twice with the same online id. The older copy carries the player's score, so the newer one is the surplus copy.
                    var played = add("played copy", 100, 1, stored("map a"));
                    add("unplayed copy", 100, 5, stored("map a"));
                    r.Add(TestResources.CreateTestScoreInfo(played.Beatmaps.First()));

                    // the same difficulties from two archives, no online id, no scores: the newer import stays.
                    add("older import", -1, 2, stored("map b easy"), stored("map b hard"));
                    add("newer import", -1, 6, stored("map b easy"), stored("map b hard"));

                    // its .osu file is not in the file store.
                    add("broken", 300, 3, lost("map c"));

                    // one of two difficulties is unplayable: reported, never removed.
                    add("partly damaged", 400, 4, stored("map d easy"), lost("map d hard"));

                    add("unique", 500, 7, stored("map e"));
                });
            });

            AddStep("create button", () => Child = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Width = 400,
                AutoSizeAxes = Axes.Y,
                Child = button = new BanchosucksLibraryCleanupButton(),
            });
        }

        [Test]
        public void TestNothingIsRemovedWithoutConfirmation()
        {
            scan();

            AddAssert("two duplicates found", () => button.LastResult!.DuplicatesToRemove.Select(s => s.ID), () => Is.EquivalentTo(new[] { ids["unplayed copy"], ids["older import"] }));
            AddAssert("one broken set found", () => button.LastResult!.BrokenSets.Select(b => b.Set.ID), () => Is.EqualTo(new[] { ids["broken"] }));
            AddAssert("one partly damaged set reported", () => button.LastResult!.PartlyDamagedSets.Select(b => b.Set.ID), () => Is.EqualTo(new[] { ids["partly damaged"] }));
            AddAssert("seven sets checked", () => button.LastResult!.ScannedSets, () => Is.EqualTo(7));

            AddAssert("dialog shows the counts", () => resultDialog.BodyText.ToString(), () => Does.Contain("checked: 7").And.Contain("Duplicates: 2").And.Contain("every difficulty): 1"));
            AddAssert("three ways to remove, one to leave", () => resultDialog.Buttons.Count(), () => Is.EqualTo(4));
            assertNothingRemoved("after the scan");

            // leaving the result dialog changes nothing.
            AddStep("change nothing", () => resultDialog.PerformAction<PopupDialogCancelButton>());
            AddUntilStep("dialog closed", () => dialogOverlay.CurrentDialog, () => Is.Null);
            assertNothingRemoved("after closing the result");

            // choosing what to remove only asks again, with the number of sets.
            scan();
            AddStep("choose both", () => resultDialog.Buttons.OfType<PopupDialogOkButton>().Last().TriggerClick());
            AddUntilStep("confirmation shown", () => dialogOverlay.CurrentDialog, () => Is.TypeOf<BanchosucksLibraryCleanupConfirmationDialog>());
            AddAssert("confirmation names three sets", () => confirmationDialog.SetCount, () => Is.EqualTo(3));
            AddAssert("confirmation text names the numbers", () => confirmationDialog.BodyText.ToString(), () => Does.StartWith("Remove 3 beatmap set(s)").And.Contain("2 duplicate(s), 1 broken"));
            AddWaitStep("wait", 5);
            assertNothingRemoved("while the confirmation is open");

            AddStep("cancel", () => confirmationDialog.PerformAction<PopupDialogCancelButton>());
            AddUntilStep("dialog closed", () => dialogOverlay.CurrentDialog, () => Is.Null);
            AddWaitStep("wait", 5);
            assertNothingRemoved("after cancelling the confirmation");
        }

        [Test]
        public void TestConfirmedRemovalIsASoftDelete()
        {
            scan();

            AddStep("choose both", () => resultDialog.Buttons.OfType<PopupDialogOkButton>().Last().TriggerClick());
            AddUntilStep("confirmation shown", () => dialogOverlay.CurrentDialog, () => Is.TypeOf<BanchosucksLibraryCleanupConfirmationDialog>());
            assertNothingRemoved("before confirming");

            AddStep("hold to confirm", () => confirmationDialog.PerformAction<PopupDialogDangerousButton>());
            AddUntilStep("removal finished", () => !button.Busy && deletePending().Count > 0);

            AddAssert("exactly the three proposed sets are marked deleted", deletePending, () => Is.EquivalentTo(new[] { ids["unplayed copy"], ids["older import"], ids["broken"] }));
            AddAssert("nothing left the database", () => Realm.Run(r => r.All<BeatmapSetInfo>().Count()), () => Is.EqualTo(7));
            AddAssert("the score is still there", () => Realm.Run(r => r.All<ScoreInfo>().Count(s => !s.DeletePending)), () => Is.EqualTo(1));
            AddAssert("the played copy still has its score", () => Realm.Run(r => r.Find<BeatmapSetInfo>(ids["played copy"])!.Beatmaps.Single().Scores.Count()), () => Is.EqualTo(1));

            // the maintenance button "restore all recently deleted beatmaps" does exactly this.
            AddStep("restore recently deleted", () => beatmapManager.UndeleteAll());
            assertNothingRemoved("after restoring");
        }

        [Test]
        public void TestOnlyDuplicatesCanBeRemoved()
        {
            scan();

            AddStep("choose duplicates", () => resultDialog.Buttons.OfType<PopupDialogOkButton>().First().TriggerClick());
            AddUntilStep("confirmation shown", () => dialogOverlay.CurrentDialog, () => Is.TypeOf<BanchosucksLibraryCleanupConfirmationDialog>());
            AddAssert("confirmation names two sets", () => confirmationDialog.SetCount, () => Is.EqualTo(2));

            AddStep("hold to confirm", () => confirmationDialog.PerformAction<PopupDialogDangerousButton>());
            AddUntilStep("removal finished", () => !button.Busy && deletePending().Count > 0);

            AddAssert("only the duplicates are marked deleted", deletePending, () => Is.EquivalentTo(new[] { ids["unplayed copy"], ids["older import"] }));
        }

        [Test]
        public void TestDuplicateThatGotAScoreAfterTheScanStays()
        {
            scan();

            AddStep("choose duplicates", () => resultDialog.Buttons.OfType<PopupDialogOkButton>().First().TriggerClick());
            AddUntilStep("confirmation shown", () => dialogOverlay.CurrentDialog, () => Is.TypeOf<BanchosucksLibraryCleanupConfirmationDialog>());

            AddStep("a score arrives on a proposed copy", () => Realm.Write(r => r.Add(TestResources.CreateTestScoreInfo(r.Find<BeatmapSetInfo>(ids["older import"])!.Beatmaps.First()))));

            AddStep("hold to confirm", () => confirmationDialog.PerformAction<PopupDialogDangerousButton>());
            AddUntilStep("removal finished", () => !button.Busy && deletePending().Count > 0);

            AddAssert("the copy with the new score stays", deletePending, () => Is.EquivalentTo(new[] { ids["unplayed copy"] }));
        }

        [Test]
        public void TestCleanLibraryOffersNothingToRemove()
        {
            AddStep("remove the problem sets", () => Realm.Write(r =>
            {
                foreach (string name in new[] { "unplayed copy", "older import", "broken", "partly damaged" })
                    r.Find<BeatmapSetInfo>(ids[name])!.DeletePending = true;
            }));

            scan();

            AddAssert("nothing to remove", () => button.LastResult!.AnythingToRemove, () => Is.False);
            AddAssert("three sets checked", () => button.LastResult!.ScannedSets, () => Is.EqualTo(3));
            AddAssert("only a close button", () => resultDialog.Buttons.Single(), () => Is.TypeOf<PopupDialogCancelButton>());
            AddAssert("dialog says so", () => resultDialog.BodyText.ToString(), () => Does.Contain("Nothing to clean up"));
        }

        [Test]
        public void TestResultWaitsBehindTheNotificationWhenSettingsWereClosed()
        {
            // a hidden button behaves like the button in closed settings: it is not updated.
            AddStep("hide the button", () => button.Parent!.Hide());
            AddStep("click clean up", () => button.TriggerClick());

            AddUntilStep("completion notification posted", () => completionNotification, () => Is.Not.Null);
            AddAssert("notification names the counts", () => completionNotification!.Text.ToString(), () => Does.Contain("2 duplicate(s), 1 broken set(s)"));
            AddWaitStep("wait", 5);
            AddAssert("no dialog opened by itself", () => dialogOverlay.CurrentDialog, () => Is.Null);
            assertNothingRemoved("after the scan");

            AddStep("click the notification", () => completionNotification!.TriggerClick());
            AddUntilStep("result shown", () => dialogOverlay.CurrentDialog, () => Is.TypeOf<BanchosucksLibraryCleanupResultDialog>());
            AddStep("change nothing", () => resultDialog.PerformAction<PopupDialogCancelButton>());
            AddUntilStep("dialog closed", () => dialogOverlay.CurrentDialog, () => Is.Null);

            AddStep("show the button again", () => button.Parent!.Show());
            AddUntilStep("button ready again", () => !button.Busy);
            AddWaitStep("wait", 5);
            AddAssert("the result does not open a second time", () => dialogOverlay.CurrentDialog, () => Is.Null);
            assertNothingRemoved("at the end");
        }

        private ProgressCompletionNotification? completionNotification => notificationOverlay.ChildrenOfType<ProgressCompletionNotification>().FirstOrDefault();

        private BanchosucksLibraryCleanupResultDialog resultDialog => (BanchosucksLibraryCleanupResultDialog)dialogOverlay.CurrentDialog!;
        private BanchosucksLibraryCleanupConfirmationDialog confirmationDialog => (BanchosucksLibraryCleanupConfirmationDialog)dialogOverlay.CurrentDialog!;

        private void scan()
        {
            AddStep("click clean up", () => button.TriggerClick());
            AddUntilStep("result shown", () => dialogOverlay.CurrentDialog, () => Is.TypeOf<BanchosucksLibraryCleanupResultDialog>());
            AddUntilStep("button ready again", () => !button.Busy);
        }

        private List<Guid> deletePending() => Realm.Run(r => r.All<BeatmapSetInfo>().Where(s => s.DeletePending).ToList().Select(s => s.ID).ToList());

        private void assertNothingRemoved(string when)
        {
            AddAssert($"no set marked deleted {when}", deletePending, () => Is.Empty);
            AddAssert($"all seven sets present {when}", () => Realm.Run(r => r.All<BeatmapSetInfo>().Count()), () => Is.EqualTo(7));
        }

        private static string hash(string content) => new MemoryStream(Encoding.UTF8.GetBytes(content)).ComputeSHA2Hash();

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
            rulesets?.Dispose();
        }
    }
}
