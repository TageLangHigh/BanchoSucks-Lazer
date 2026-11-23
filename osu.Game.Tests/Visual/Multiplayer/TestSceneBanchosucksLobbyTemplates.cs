// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Chat;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Rooms;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Screens.Menu;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Screens.Banchosucks.Lobby;
using osu.Game.Screens.OnlinePlay.Multiplayer;
using osu.Game.Screens.OnlinePlay.Multiplayer.Match;
using osu.Game.Tests.Beatmaps;
using osu.Game.Tests.Resources;
using MatchType = osu.Game.Online.Rooms.MatchType;

namespace osu.Game.Tests.Visual.Multiplayer
{
    /// <summary>
    /// Banchosucks: lobby templates in the multiplayer room settings: saving, loading while creating a room and
    /// loading into a hosted room, against the test multiplayer server.
    /// </summary>
    public partial class TestSceneBanchosucksLobbyTemplates : MultiplayerTestScene
    {
        private const string password = "secret-pw";

        private MultiplayerMatchSubScreen screen = null!;
        private Room room = null!;
        private Storage templateStorage = null!;

        private BanchosucksLobbyTemplateManager manager => screen.ChildrenOfType<BanchosucksLobbyTemplateManager>().Single();
        private BanchosucksLobbyTemplateControls controls => screen.ChildrenOfType<BanchosucksLobbyTemplateControls>().Single();
        private MultiplayerMatchSettingsOverlay settingsOverlay => screen.ChildrenOfType<MultiplayerMatchSettingsOverlay>().Single();
        private IBanchosucksLobbyTemplateForm form => settingsOverlay.ChildrenOfType<CompositeDrawable>().OfType<IBanchosucksLobbyTemplateForm>().Single();

        private BanchosucksLobbyTemplateControls.SavePopover? savePopover => screen.ChildrenOfType<BanchosucksLobbyTemplateControls.SavePopover>().SingleOrDefault();
        private BanchosucksLobbyTemplateControls.LoadPopover? loadPopover => screen.ChildrenOfType<BanchosucksLobbyTemplateControls.LoadPopover>().SingleOrDefault();

        private string templatePath => templateStorage.GetStorageForDirectory("banchosucks").GetFullPath(BanchosucksLobbyTemplateStore.FILE);

        [BackgroundDependencyLoader]
        private void load(GameHost host, AudioManager audio)
        {
            BeatmapManager beatmaps;

            Dependencies.Cache(new RealmRulesetStore(Realm));
            Dependencies.Cache(beatmaps = new BeatmapManager(LocalStorage, Realm, null, audio, Resources, host, Beatmap.Default));
            Dependencies.Cache(Realm);
            Dependencies.CacheAs<BeatmapStore>(new RealmDetachedBeatmapStore());
            Dependencies.Cache(new ChannelManager(API));

            // the templates of this scene go to its own temporary folder, not into the game's data
            Dependencies.CacheAs(templateStorage = LocalStorage);

            beatmaps.Import(TestResources.GetQuickTestBeatmapForImport()).WaitSafely();

            Realm.Write(r =>
            {
                foreach (var beatmapInfo in r.All<BeatmapInfo>())
                    beatmapInfo.OnlineMD5Hash = beatmapInfo.MD5Hash;
            });
        }

        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("no templates on disk", () =>
            {
                var directory = templateStorage.GetStorageForDirectory("banchosucks");

                directory.Delete(BanchosucksLobbyTemplateStore.FILE);
                directory.Delete(BanchosucksLobbyTemplateStore.BACKUP_FILE);
            });
        }

        private void loadScreen(Room newRoom)
        {
            AddStep("load match", () => LoadScreen(screen = new TestMultiplayerMatchSubScreen(room = newRoom)));
            AddUntilStep("wait for load", () => screen.IsCurrentScreen() && screen.ChildrenOfType<BanchosucksLobbyTemplateControls>().Any(c => c.IsLoaded));
        }

        private static PlaylistItem playlistItem() => new PlaylistItem(new TestBeatmap(new OsuRuleset().RulesetInfo).BeatmapInfo)
        {
            RulesetID = new OsuRuleset().RulesetInfo.OnlineID
        };

        private static BanchosucksLobbyTemplate cupTemplate(params int[] beatmapIds) => new BanchosucksLobbyTemplate
        {
            Name = "Cup",
            RoomName = "{user}'s cup",
            MatchType = MatchType.TeamVersus,
            QueueMode = QueueMode.AllPlayers,
            AutoStartSeconds = 30,
            AutoSkip = true,
            Playlist = beatmapIds.Select(id => new BanchosucksLobbyTemplateItem { BeatmapId = id, RulesetId = 0, Title = $"Saved title {id}" }).ToList(),
        };

        private void previewAndApply(string name)
        {
            AddUntilStep("load button enabled", () => controls.LoadButton.Enabled.Value);
            AddStep("open the list", () => controls.LoadButton.TriggerClick());
            AddUntilStep("list shown", () => loadPopover?.Rows.Any(r => r.IsLoaded) == true);
            AddStep($"pick \"{name}\"", () => loadPopover!.Rows.Single(r => r.Template.Name == name).LoadButton.TriggerClick());
            AddUntilStep("preview shown", () => loadPopover?.Plan != null && loadPopover.ApplyButton?.IsLoaded == true);
        }

        [Test]
        public void TestSaveWhileCreatingNeverStoresThePassword()
        {
            loadScreen(new Room { Name = "Test Room" });

            AddStep("fill the form", () =>
            {
                room.Playlist = [playlistItem()];
                form.BanchosucksWriteSettings(new BanchosucksLobbyTemplateSettings("Friday cup", MatchType.TeamVersus, QueueMode.AllPlayersRoundRobin, 60, true));
                settingsOverlay.ChildrenOfType<OsuPasswordTextBox>().Single().Text = password;
            });

            AddAssert("load disabled without templates", () => !controls.LoadButton.Enabled.Value);

            AddStep("click save", () => controls.SaveButton.TriggerClick());
            AddUntilStep("popover shown", () => savePopover?.IsLoaded == true);
            AddAssert("room name suggested", () => savePopover!.RoomNameBox.Text, () => Is.EqualTo("Friday cup"));
            AddAssert("cannot save without a name", () => !savePopover!.ConfirmButton.Enabled.Value);

            AddStep("name it", () =>
            {
                savePopover!.NameBox.Text = "Friday";
                savePopover.RoomNameBox.Text = "{user}'s Friday cup";
            });
            AddUntilStep("can save", () => savePopover!.ConfirmButton.Enabled.Value);
            AddStep("confirm", () => savePopover!.ConfirmButton.TriggerClick());

            AddUntilStep("template stored", () => manager.Store.Find("Friday") != null);
            AddAssert("settings stored", () =>
            {
                var template = manager.Store.Find("Friday")!;
                return template.RoomName == "{user}'s Friday cup" && template.MatchType == MatchType.TeamVersus && template.QueueMode == QueueMode.AllPlayersRoundRobin
                       && template.AutoStartSeconds == 60 && template.AutoSkip;
            });
            AddAssert("map stored with its name", () =>
            {
                var item = manager.Store.Find("Friday")!.Playlist.Single();
                return item.BeatmapId == room.Playlist.Single().Beatmap.OnlineID && item.RulesetId == 0 && item.Title.Contains('[');
            });

            AddAssert("file written", () => File.Exists(templatePath));
            AddAssert("no password in the file", () => File.ReadAllText(templatePath), () => Does.Not.Contain(password).And.Not.Contain("password"));
            AddAssert("room not created by saving", () => !RoomJoined);
            AddAssert("client still connected", () => MultiplayerClient.IsConnected.Value);
            AddUntilStep("load enabled now", () => controls.LoadButton.Enabled.Value);
        }

        [Test]
        public void TestLoadWhileCreatingThenCreate()
        {
            AddStep("template on disk", () => new BanchosucksLobbyTemplateStore(templateStorage).Save(cupTemplate(9001, 9002, 9003)));

            loadScreen(new Room { Name = "Test Room" });

            AddStep("type a password", () => settingsOverlay.ChildrenOfType<OsuPasswordTextBox>().Single().Text = password);

            previewAndApply("Cup");

            AddAssert("all three maps will be set", () => loadPopover!.Plan!.ToAdd.Select(i => i.BeatmapId), () => Is.EqualTo(new[] { 9001, 9002, 9003 }));
            AddAssert("nothing changed before the confirmation", () => form.BanchosucksReadSettings().Name == "Test Room" && room.Playlist.Count == 0);

            AddStep("confirm", () => loadPopover!.ApplyButton!.TriggerClick());

            AddAssert("form filled", () => form.BanchosucksReadSettings(),
                () => Is.EqualTo(new BanchosucksLobbyTemplateSettings($"{API.LocalUser.Value.Username}'s cup", MatchType.TeamVersus, QueueMode.AllPlayers, 30, true)));
            AddAssert("password untouched", () => settingsOverlay.ChildrenOfType<OsuPasswordTextBox>().Single().Text, () => Is.EqualTo(password));
            AddAssert("first map picked", () => room.Playlist.Single().Beatmap.OnlineID, () => Is.EqualTo(9001));
            AddAssert("two maps wait for the room", () => manager.PendingCount, () => Is.EqualTo(2));
            AddAssert("room not created by loading", () => !RoomJoined);

            ClickButtonWhenEnabled<MultiplayerMatchSettingsOverlay.CreateOrUpdateButton>();
            AddUntilStep("wait for join", () => RoomJoined);

            AddUntilStep("the other maps were added", () => !manager.Busy && manager.PendingCount == 0 && MultiplayerClient.ServerRoom!.Playlist.Count == 3);
            AddAssert("in template order", () => MultiplayerClient.ServerRoom!.Playlist.OrderBy(i => i.ID).Select(i => i.BeatmapID), () => Is.EqualTo(new[] { 9001, 9002, 9003 }));
            AddAssert("game type from the template", () => MultiplayerClient.ServerRoom!.Settings.MatchType, () => Is.EqualTo(MatchType.TeamVersus));
            AddAssert("queue mode from the template", () => MultiplayerClient.ServerRoom!.Settings.QueueMode, () => Is.EqualTo(QueueMode.AllPlayers));
            AddAssert("auto start from the template", () => MultiplayerClient.ServerRoom!.Settings.AutoStartDuration, () => Is.EqualTo(TimeSpan.FromSeconds(30)));
            // auto skip is not checked here: the test server does not take it over from a created room (the form above has it)
            AddAssert("password as typed", () => MultiplayerClient.ServerRoom!.Settings.Password, () => Is.EqualTo(password));
            AddAssert("result reported", () => manager.Status.First().Text, () => Does.Contain("3 maps added"));
            AddAssert("client still connected", () => MultiplayerClient.IsConnected.Value);
        }

        [Test]
        public void TestLoadIntoHostedRoomSkipsQueuedAndReportsMissing()
        {
            int queuedId = 0;

            AddStep("create room with a password", () =>
            {
                var hosted = CreateDefaultRoom();
                hosted.Password = password;
                room = hosted;
                LoadScreen(screen = new TestMultiplayerMatchSubScreen(hosted));
            });
            AddUntilStep("wait for load", () => screen.IsCurrentScreen() && screen.ChildrenOfType<BanchosucksLobbyTemplateControls>().Any(c => c.IsLoaded));
            ClickButtonWhenEnabled<MultiplayerMatchSettingsOverlay.CreateOrUpdateButton>();
            AddUntilStep("wait for join", () => RoomJoined);

            AddStep("template with the queued map, a new one and one the server does not have", () =>
            {
                queuedId = MultiplayerClient.ServerRoom!.Playlist.Single().BeatmapID;
                manager.Store.Save(cupTemplate(queuedId, 9002, 9404));

                // the test server answers for every id; this one it does not know
                manager.LookupOverride = ids => Task.FromResult(ids.Select(id => id == 9404 ? null : new APIBeatmap
                {
                    OnlineID = id,
                    Checksum = $"checksum-{id}",
                    DifficultyName = "Hard",
                    BeatmapSet = new APIBeatmapSet { Title = $"Song {id}", Artist = "Artist" },
                }).ToArray());
            });

            AddStep("open settings", () => settingsOverlay.Show());
            previewAndApply("Cup");

            AddAssert("plan sorts the maps", () =>
            {
                var plan = loadPopover!.Plan!;
                return plan.AlreadyQueued.Single().BeatmapId == queuedId && plan.ToAdd.Single().BeatmapId == 9002 && plan.Unavailable.Single().BeatmapId == 9404;
            });
            AddAssert("nothing changed before the confirmation", () => MultiplayerClient.ServerRoom!.Settings.MatchType == MatchType.HeadToHead && MultiplayerClient.ServerRoom.Playlist.Count == 1);

            AddStep("confirm", () => loadPopover!.ApplyButton!.TriggerClick());
            AddUntilStep("applied", () => !manager.Busy && MultiplayerClient.ServerRoom!.Playlist.Count == 2);

            AddAssert("settings changed through the client", () =>
            {
                var settings = MultiplayerClient.ServerRoom!.Settings;
                return settings.Name == $"{API.LocalUser.Value.Username}'s cup" && settings.MatchType == MatchType.TeamVersus && settings.QueueMode == QueueMode.AllPlayers
                       && settings.AutoStartDuration == TimeSpan.FromSeconds(30) && settings.AutoSkip;
            });
            AddAssert("password kept", () => MultiplayerClient.ServerRoom!.Settings.Password, () => Is.EqualTo(password));
            AddAssert("only the new map was added", () => MultiplayerClient.ServerRoom!.Playlist.Select(i => i.BeatmapID), () => Is.EquivalentTo(new[] { queuedId, 9002 }));
            AddAssert("with the server's checksum", () => MultiplayerClient.ServerRoom!.Playlist.Single(i => i.BeatmapID == 9002).BeatmapChecksum, () => Is.EqualTo("checksum-9002"));

            AddAssert("missing map reported by name", () => manager.Status.Any(l => l.Warning && l.Text.Contains("Saved title 9404")));
            AddAssert("skipped map reported", () => manager.Status.Any(l => l.Text.Contains("skipped: 1")));
            AddAssert("one map added", () => manager.Status.First().Text, () => Does.Contain("1 map added"));
            AddUntilStep("form follows the room", () => form.BanchosucksReadSettings().Type == MatchType.TeamVersus);
            AddAssert("no operation left running", () => !OnlinePlayDependencies.OngoingOperationTracker.InProgress.Value);
            AddAssert("client still connected", () => MultiplayerClient.IsConnected.Value);

            // loading the same template again finds everything in place
            previewAndApply("Cup");
            AddAssert("nothing left to do", () => !loadPopover!.Plan!.ChangesAnything && !loadPopover.ApplyButton!.Enabled.Value);
        }

        [Test]
        public void TestSaveFromHostedRoomAndDelete()
        {
            AddStep("create room", () => LoadScreen(screen = new TestMultiplayerMatchSubScreen(room = CreateDefaultRoom())));
            AddUntilStep("wait for load", () => screen.IsCurrentScreen() && screen.ChildrenOfType<BanchosucksLobbyTemplateControls>().Any(c => c.IsLoaded));
            ClickButtonWhenEnabled<MultiplayerMatchSettingsOverlay.CreateOrUpdateButton>();
            AddUntilStep("wait for join", () => RoomJoined);

            AddStep("queue a second map", () => MultiplayerClient.AddPlaylistItem(new MultiplayerPlaylistItem { BeatmapID = 9010, RulesetID = 0 }).FireAndForget());
            AddUntilStep("queued", () => MultiplayerClient.ServerRoom!.Playlist.Count == 2);
            AddStep("open settings", () => settingsOverlay.Show());

            AddStep("save", () => manager.Save("Room", "My room", form.BanchosucksReadSettings()));
            AddUntilStep("stored with both maps of the queue", () => manager.Store.Find("Room")?.Playlist.Count == 2);
            AddAssert("in queue order", () => manager.Store.Find("Room")!.Playlist.Select(i => i.BeatmapId),
                () => Is.EqualTo(MultiplayerClient.ServerRoom!.Playlist.OrderBy(i => i.PlaylistOrder).Select(i => i.BeatmapID)));
            AddAssert("names looked up", () => manager.Store.Find("Room")!.Playlist.All(i => i.Title.Length > 0));

            AddStep("open the list", () => controls.LoadButton.TriggerClick());
            AddUntilStep("list shown", () => loadPopover?.Rows.Any(r => r.IsLoaded) == true);
            AddStep("click delete once", () => loadPopover!.Rows.Single().DeleteButton.TriggerClick());
            AddAssert("still there", () => manager.Store.Find("Room") != null);
            AddStep("click delete again", () => loadPopover!.Rows.Single().DeleteButton.TriggerClick());
            AddAssert("deleted", () => manager.Store.Find("Room") == null && !loadPopover!.Rows.Any());
            AddAssert("gone from disk", () => new BanchosucksLobbyTemplateStore(templateStorage).Templates, () => Is.Empty);
        }

        [Test]
        public void TestOnlyTheHostCanLoad()
        {
            AddStep("create room", () => LoadScreen(screen = new TestMultiplayerMatchSubScreen(room = CreateDefaultRoom())));
            AddUntilStep("wait for load", () => screen.IsCurrentScreen() && screen.ChildrenOfType<BanchosucksLobbyTemplateControls>().Any(c => c.IsLoaded));
            ClickButtonWhenEnabled<MultiplayerMatchSettingsOverlay.CreateOrUpdateButton>();
            AddUntilStep("wait for join", () => RoomJoined);

            AddStep("a template exists", () => manager.Store.Save(cupTemplate(9002)));
            AddStep("open settings", () => settingsOverlay.Show());
            AddUntilStep("host may load", () => controls.LoadButton.Enabled.Value);

            AddStep("somebody else becomes host", () =>
            {
                MultiplayerClient.AddUser(new APIUser { Id = PLAYER_1_ID, Username = "Other" });
                MultiplayerClient.TransferHost(PLAYER_1_ID).FireAndForget();
            });
            AddUntilStep("host changed", () => MultiplayerClient.ServerRoom!.Host?.UserID == PLAYER_1_ID);
            AddUntilStep("load disabled", () => !controls.LoadButton.Enabled.Value);

            AddStep("try to apply anyway", () =>
            {
                var plan = BanchosucksLobbyTemplatePlan.Create(manager.Store.Find("Cup")!, form.BanchosucksReadSettings(), Array.Empty<(int, int)>(),
                    new System.Collections.Generic.HashSet<int> { 9002 }, "user", DateTime.Today);

                Assert.That(manager.Apply(plan, form), Is.False);
            });
            AddAssert("refused with a reason", () => manager.Status.Single().Warning && manager.Status.Single().Text.Contains("host"));
            AddAssert("room untouched", () => MultiplayerClient.ServerRoom!.Playlist.Count == 1 && MultiplayerClient.ServerRoom.Settings.MatchType == MatchType.HeadToHead);
        }

        private partial class TestMultiplayerMatchSubScreen : MultiplayerMatchSubScreen
        {
            [Resolved]
            private IDialogOverlay? dialogOverlay { get; set; }

            public TestMultiplayerMatchSubScreen(Room room)
                : base(room)
            {
            }

            public override bool OnExiting(ScreenExitEvent e)
            {
                // a room with a picked map that was never created asks before it is discarded; the test just leaves
                if (!ExitConfirmed && dialogOverlay?.CurrentDialog is ConfirmDiscardChangesDialog confirmDialog)
                {
                    confirmDialog.PerformAction<PopupDialogDangerousButton>();
                    return true;
                }

                return base.OnExiting(e);
            }
        }
    }
}
