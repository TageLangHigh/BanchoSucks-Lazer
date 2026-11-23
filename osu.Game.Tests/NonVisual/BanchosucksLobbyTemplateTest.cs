// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Game.Online.API;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Rooms;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Banchosucks.Clicker;
using osu.Game.Screens.Banchosucks.Lobby;
using MatchType = osu.Game.Online.Rooms.MatchType;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class BanchosucksLobbyTemplateTest
    {
        private static readonly DateTime today = new DateTime(2026, 11, 23);

        private static BanchosucksLobbyTemplateItem item(int beatmapId, int rulesetId = 0, string title = "") => new BanchosucksLobbyTemplateItem
        {
            BeatmapId = beatmapId,
            RulesetId = rulesetId,
            Title = title,
        };

        private static BanchosucksLobbyTemplate template(string name = "Cup", params BanchosucksLobbyTemplateItem[] playlist) => new BanchosucksLobbyTemplate
        {
            Name = name,
            RoomName = "{user}'s cup {date}",
            MatchType = MatchType.TeamVersus,
            QueueMode = QueueMode.AllPlayers,
            AutoStartSeconds = 30,
            AutoSkip = true,
            Playlist = playlist.ToList(),
            Saved = new DateTimeOffset(2026, 11, 23, 12, 0, 0, TimeSpan.FromHours(1)),
        };

        private static readonly BanchosucksLobbyTemplateSettings current_settings = new BanchosucksLobbyTemplateSettings("Old room", MatchType.HeadToHead, QueueMode.HostOnly, 0, false);

        [TearDown]
        public void TearDown() => ClickerStrings.IsGerman = false;

        #region File

        [Test]
        public void TestFileRoundTrip()
        {
            var file = new BanchosucksLobbyTemplateFile();

            var first = item(101, 0, "Artist - Title [Hard]");
            first.RequiredMods.Add(BanchosucksLobbyTemplateStore.ModFrom(new APIMod(new OsuModDoubleTime { SpeedChange = { Value = 1.3 } })));
            first.AllowedMods.Add(BanchosucksLobbyTemplateStore.ModFrom(new APIMod(new OsuModHidden())));
            first.Freestyle = true;

            Assert.That(file.Upsert(template("Cup", first, item(102, 3))));

            var read = BanchosucksLobbyTemplateFile.Deserialise(file.Serialise())!;

            Assert.That(read.Version, Is.EqualTo(BanchosucksLobbyTemplateFile.CURRENT_VERSION));
            Assert.That(read.ReadOnly, Is.False);

            var loaded = read.Templates.Single();

            Assert.That(loaded.Name, Is.EqualTo("Cup"));
            Assert.That(loaded.RoomName, Is.EqualTo("{user}'s cup {date}"));
            Assert.That(loaded.MatchType, Is.EqualTo(MatchType.TeamVersus));
            Assert.That(loaded.QueueMode, Is.EqualTo(QueueMode.AllPlayers));
            Assert.That(loaded.AutoStartSeconds, Is.EqualTo(30));
            Assert.That(loaded.AutoSkip, Is.True);
            Assert.That(loaded.Saved, Is.EqualTo(new DateTimeOffset(2026, 11, 23, 12, 0, 0, TimeSpan.FromHours(1))));

            Assert.That(loaded.Playlist.Select(i => (i.BeatmapId, i.RulesetId)), Is.EqualTo(new[] { (101, 0), (102, 3) }));
            Assert.That(loaded.Playlist[0].Title, Is.EqualTo("Artist - Title [Hard]"));
            Assert.That(loaded.Playlist[0].Freestyle, Is.True);
            Assert.That(loaded.Playlist[1].Freestyle, Is.False);
            Assert.That(loaded.Playlist[1].DisplayTitle, Is.EqualTo("Beatmap #102"));

            // the mods come back as the same mods, settings included
            var required = BanchosucksLobbyTemplateStore.ToApiMod(loaded.Playlist[0].RequiredMods.Single());
            var doubleTime = (OsuModDoubleTime)required.ToMod(new OsuRuleset());

            Assert.That(doubleTime.SpeedChange.Value, Is.EqualTo(1.3).Within(1e-9));
            Assert.That(BanchosucksLobbyTemplateStore.ToApiMod(loaded.Playlist[0].AllowedMods.Single()).Acronym, Is.EqualTo("HD"));
            Assert.That(loaded.Playlist[0].AllowedMods.Single().Settings, Is.Null);
        }

        [Test]
        public void TestThereIsNoPlaceForAPassword()
        {
            var file = new BanchosucksLobbyTemplateFile();
            file.Upsert(template("Cup", item(101)));

            string json = file.Serialise();

            Assert.That(json, Does.Not.Contain("password").IgnoreCase);
            Assert.That(typeof(BanchosucksLobbyTemplate).GetProperties().Select(p => p.Name), Has.None.Contains("Password"));
            Assert.That(typeof(BanchosucksLobbyTemplateSettings).GetProperties().Select(p => p.Name), Has.None.Contains("Password"));
        }

        [Test]
        public void TestDamagedAndHandEditedFiles()
        {
            Assert.That(BanchosucksLobbyTemplateFile.Deserialise(string.Empty), Is.Null);
            Assert.That(BanchosucksLobbyTemplateFile.Deserialise("{ not json"), Is.Null);
            Assert.That(BanchosucksLobbyTemplateFile.Deserialise("[1, 2]"), Is.Null);
            Assert.That(BanchosucksLobbyTemplateFile.Deserialise("{\"version\": 0}"), Is.Null);

            const string json = @"{
                ""version"": 1,
                ""templates"": [
                    null,
                    { ""name"": ""  "" },
                    { ""name"": ""Cup"", ""match_type"": ""Matchmaking"", ""queue_mode"": ""SomethingNew"", ""auto_start_seconds"": -5, ""playlist"": null },
                    { ""name"": ""cup"", ""playlist"": [] },
                    { ""name"": ""Maps"", ""room_name"": null, ""playlist"": [
                        null,
                        { ""beatmap_id"": 0 },
                        { ""beatmap_id"": 7, ""ruleset_id"": 1, ""title"": null, ""required_mods"": [ null, { ""acronym"": """" }, { ""acronym"": ""HR"" } ], ""allowed_mods"": null }
                    ] }
                ]
            }";

            var file = BanchosucksLobbyTemplateFile.Deserialise(json)!;

            Assert.That(file.Templates.Select(t => t.Name), Is.EqualTo(new[] { "Cup", "Maps" }));

            // a match type nobody can pick in the room settings and an unknown queue mode fall back to the defaults
            Assert.That(file.Templates[0].MatchType, Is.EqualTo(MatchType.HeadToHead));
            Assert.That(file.Templates[0].QueueMode, Is.EqualTo(QueueMode.HostOnly));
            Assert.That(file.Templates[0].AutoStartSeconds, Is.Zero);
            Assert.That(file.Templates[0].Playlist, Is.Empty);

            var map = file.Templates[1].Playlist.Single();

            Assert.That(file.Templates[1].RoomName, Is.Empty);
            Assert.That(map.BeatmapId, Is.EqualTo(7));
            Assert.That(map.Title, Is.Empty);
            Assert.That(map.RequiredMods.Single().Acronym, Is.EqualTo("HR"));
            Assert.That(map.AllowedMods, Is.Empty);
        }

        [Test]
        public void TestNewerFileIsReadButMarkedReadOnlyAndKeepsUnknownFields()
        {
            const string json = @"{ ""version"": 2, ""colour"": ""red"", ""templates"": [ { ""name"": ""Cup"", ""win_condition"": ""accuracy"", ""playlist"": [ { ""beatmap_id"": 5, ""note"": ""finals"" } ] } ] }";

            var file = BanchosucksLobbyTemplateFile.Deserialise(json)!;

            Assert.That(file.ReadOnly, Is.True);
            Assert.That(file.Templates.Single().Playlist.Single().BeatmapId, Is.EqualTo(5));

            string written = file.Serialise();

            Assert.That(written, Does.Contain("colour"));
            Assert.That(written, Does.Contain("win_condition"));
            Assert.That(written, Does.Contain("finals"));
        }

        [Test]
        public void TestUpsertReplacesSameNameAndRespectsTheLimit()
        {
            var file = new BanchosucksLobbyTemplateFile();

            Assert.That(file.Upsert(template("Cup", item(1))));
            Assert.That(file.Upsert(template("  cup ", item(2), item(3))));
            Assert.That(file.Upsert(template("   ")), Is.False);

            Assert.That(file.Templates, Has.Count.EqualTo(1));
            Assert.That(file.Templates[0].Name, Is.EqualTo("cup"));
            Assert.That(file.Templates[0].Playlist, Has.Count.EqualTo(2));
            Assert.That(file.Find("CUP"), Is.SameAs(file.Templates[0]));

            for (int i = 1; i < BanchosucksLobbyTemplateFile.MAX_TEMPLATES; i++)
                Assert.That(file.Upsert(template($"T{i}")));

            Assert.That(file.Upsert(template("one too many")), Is.False);
            Assert.That(file.Upsert(template("T5", item(9))), "replacing still works when the file is full");

            Assert.That(file.Remove("t5"));
            Assert.That(file.Remove("t5"), Is.False);
            Assert.That(file.Templates, Has.Count.EqualTo(BanchosucksLobbyTemplateFile.MAX_TEMPLATES - 1));

            Assert.That(BanchosucksLobbyTemplateFile.CleanName(new string('x', 200)).Length, Is.EqualTo(BanchosucksLobbyTemplateFile.MAX_NAME_LENGTH));
        }

        [Test]
        public void TestWriteKeepsThePreviousVersionAsBackup()
        {
            string directory = Path.Combine(Path.GetTempPath(), "banchosucks-lobby-templates-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "banchosucks", "lobby-templates.json");
            string backup = Path.Combine(directory, "banchosucks", "lobby-templates.bak.json");

            try
            {
                var file = new BanchosucksLobbyTemplateFile();
                file.Upsert(template("First", item(1)));
                file.Write(path, backup);

                Assert.That(File.Exists(path));
                Assert.That(File.Exists(backup), Is.False);
                Assert.That(File.Exists(path + ".tmp"), Is.False);

                file.Upsert(template("Second", item(2)));
                file.Write(path, backup);

                Assert.That(BanchosucksLobbyTemplateFile.Read(path)!.Templates.Select(t => t.Name), Is.EqualTo(new[] { "First", "Second" }));
                Assert.That(BanchosucksLobbyTemplateFile.Read(backup)!.Templates.Select(t => t.Name), Is.EqualTo(new[] { "First" }));
                Assert.That(File.Exists(path + ".tmp"), Is.False);

                Assert.That(BanchosucksLobbyTemplateFile.Read(Path.Combine(directory, "missing.json")), Is.Null);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        #endregion

        #region Plan

        [Test]
        public void TestRoomNamePattern()
        {
            Assert.That(BanchosucksLobbyTemplatePlan.ExpandRoomName("{user}'s cup {date}", "Lidl", today), Is.EqualTo("Lidl's cup 2026-11-23"));
            Assert.That(BanchosucksLobbyTemplatePlan.ExpandRoomName("{NAME} vs {User}", "Lidl", today), Is.EqualTo("Lidl vs Lidl"));
            Assert.That(BanchosucksLobbyTemplatePlan.ExpandRoomName("  plain {other}  ", "Lidl", today), Is.EqualTo("plain {other}"));
            Assert.That(BanchosucksLobbyTemplatePlan.ExpandRoomName(null, "Lidl", today), Is.Empty);
            Assert.That(BanchosucksLobbyTemplatePlan.ExpandRoomName(new string('x', 300), "Lidl", today).Length, Is.EqualTo(BanchosucksLobbyTemplateFile.MAX_ROOM_NAME_LENGTH));
        }

        [Test]
        public void TestPlanListsEveryChangedSetting()
        {
            var plan = BanchosucksLobbyTemplatePlan.Create(template("Cup"), current_settings, Array.Empty<(int, int)>(), new HashSet<int>(), "Lidl", today);

            Assert.That(plan.Settings, Is.EqualTo(new BanchosucksLobbyTemplateSettings("Lidl's cup 2026-11-23", MatchType.TeamVersus, QueueMode.AllPlayers, 30, true)));
            Assert.That(plan.Changes.Select(c => c.Field), Is.EqualTo(new[]
            {
                BanchosucksLobbyTemplateField.RoomName,
                BanchosucksLobbyTemplateField.MatchType,
                BanchosucksLobbyTemplateField.QueueMode,
                BanchosucksLobbyTemplateField.AutoStart,
                BanchosucksLobbyTemplateField.AutoSkip,
            }));
            Assert.That(plan.ChangesAnything);

            // loading it a second time changes nothing
            var again = BanchosucksLobbyTemplatePlan.Create(template("Cup"), plan.Settings, Array.Empty<(int, int)>(), new HashSet<int>(), "Lidl", today);

            Assert.That(again.Changes, Is.Empty);
            Assert.That(again.ChangesAnything, Is.False);
        }

        [Test]
        public void TestTemplateWithoutRoomNameKeepsTheName()
        {
            var nameless = template("Cup");
            nameless.RoomName = "   ";

            var plan = BanchosucksLobbyTemplatePlan.Create(nameless, current_settings, Array.Empty<(int, int)>(), new HashSet<int>(), "Lidl", today);

            Assert.That(plan.Settings.Name, Is.EqualTo("Old room"));
            Assert.That(plan.Changes.Select(c => c.Field), Has.None.EqualTo(BanchosucksLobbyTemplateField.RoomName));
        }

        [Test]
        public void TestQueuedMapsAreSkippedAndMissingOnesReported()
        {
            var cup = template("Cup", item(1), item(2), item(3), item(4, 3), item(2), item(5));

            // 2 waits once in the queue, 4 only for another ruleset, 99 is not in the template; the server does not know 3
            var queue = new[] { (2, 0), (4, 0), (99, 0) };
            var onServer = new HashSet<int> { 1, 2, 4, 5 };

            var plan = BanchosucksLobbyTemplatePlan.Create(cup, current_settings, queue, onServer, "Lidl", today);

            Assert.That(plan.AlreadyQueued.Select(i => i.BeatmapId), Is.EqualTo(new[] { 2 }));
            Assert.That(plan.Unavailable.Select(i => i.BeatmapId), Is.EqualTo(new[] { 3 }));
            Assert.That(plan.ToAdd.Select(i => (i.BeatmapId, i.RulesetId)), Is.EqualTo(new[] { (1, 0), (4, 3), (2, 0), (5, 0) }), "the second copy of map 2 is still added, in template order");

            // nothing of the template got lost
            Assert.That(plan.AlreadyQueued.Count + plan.Unavailable.Count + plan.ToAdd.Count, Is.EqualTo(cup.Playlist.Count));
        }

        [Test]
        public void TestPreviewAndReportNameTheMissingMaps()
        {
            var cup = template("Cup", item(1, title: "A - One [Easy]"), item(3, title: "C - Three [Hard]"), item(4));
            var plan = BanchosucksLobbyTemplatePlan.Create(cup, current_settings, Array.Empty<(int, int)>(), new HashSet<int> { 1 }, "Lidl", today);

            var preview = BanchosucksLobbyTemplateText.Describe(plan, false);

            Assert.That(preview.Single(l => l.Warning).Text, Does.Contain("C - Three [Hard]").And.Contain("Beatmap #4").And.Contain("(2)"));
            Assert.That(preview.Any(l => l.Text.Contains("A - One [Easy]") && !l.Warning));
            Assert.That(preview.Any(l => l.Text.Contains("Lidl's cup 2026-11-23")));
            Assert.That(preview.Last().Text, Does.Contain("password"));

            var report = BanchosucksLobbyTemplateText.Report(plan, 1, new[] { (cup.Playlist[0], "queue is full") });

            Assert.That(report[0].Text, Does.Contain("1 map added"));
            Assert.That(report.Count(l => l.Warning), Is.EqualTo(2));
            Assert.That(report.Any(l => l.Warning && l.Text.Contains("queue is full") && l.Text.Contains("A - One [Easy]")));

            var failedSettings = BanchosucksLobbyTemplateText.Report(plan, 0, Array.Empty<(BanchosucksLobbyTemplateItem, string)>(), "not the host");

            Assert.That(failedSettings.Single().Warning);
            Assert.That(failedSettings.Single().Text, Does.Contain("not the host").And.Contain("No map was added"));

            ClickerStrings.IsGerman = true;

            Assert.That(BanchosucksLobbyTemplateText.Describe(plan, true).Single(l => l.Warning).Text, Does.Contain("Auf dem Server nicht gefunden"));
            Assert.That(BanchosucksLobbyTemplateText.Summary(cup), Is.EqualTo("Team gegen Team, Alle Spieler, 3 Maps"));
        }

        [Test]
        public void TestLongMapListsAreSummedUp()
        {
            var many = template("Many", Enumerable.Range(1, 20).Select(i => item(i, title: $"Map {i}")).ToArray());
            var plan = BanchosucksLobbyTemplatePlan.Create(many, current_settings, Array.Empty<(int, int)>(), Enumerable.Range(1, 20).ToHashSet(), "Lidl", today);

            string line = BanchosucksLobbyTemplateText.Describe(plan, true).First(l => l.Text.Contains("Map 1;")).Text;

            Assert.That(line, Does.Contain($"Map {BanchosucksLobbyTemplateText.MAX_LISTED_MAPS};"));
            Assert.That(line, Does.Not.Contain($"Map {BanchosucksLobbyTemplateText.MAX_LISTED_MAPS + 1}"));
            Assert.That(line, Does.Contain($"and {20 - BanchosucksLobbyTemplateText.MAX_LISTED_MAPS} more"));
        }

        #endregion
    }
}
