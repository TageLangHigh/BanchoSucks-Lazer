// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.IPC;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>
    /// The JSON document of the overlay data source. The key names asserted here are tosu's v2 names as far as they are
    /// known; a corrected name is changed here and in <see cref="BanchosucksOverlayJson"/> together.
    /// </summary>
    [TestFixture]
    public class BanchosucksOverlayJsonTest
    {
        private static JsonElement build(BanchosucksOverlayState state) => JsonDocument.Parse(BanchosucksOverlayJson.Build(state)).RootElement;

        private static JsonElement at(JsonElement root, string path)
        {
            foreach (string key in path.Split('.'))
                root = root.GetProperty(key);

            return root;
        }

        private static string[] keys(JsonElement element) => element.EnumerateObject().Select(p => p.Name).ToArray();

        [Test]
        public void TestTopLevelKeys()
        {
            Assert.That(keys(build(new BanchosucksOverlayState())), Is.EqualTo(new[]
            {
                "client", "server", "state", "session", "settings", "profile", "beatmap", "play",
                "leaderboard", "performance", "resultsScreen", "folders", "files", "directPath",
            }));
        }

        [Test]
        public void TestNestedKeys()
        {
            var root = build(new BanchosucksOverlayState());

            Assert.That(keys(at(root, "state")), Is.EqualTo(new[] { "number", "name" }));
            Assert.That(keys(at(root, "session")), Is.EqualTo(new[] { "playTime", "playCount" }));
            Assert.That(keys(at(root, "profile")), Is.EqualTo(new[]
            {
                "userStatus", "banchoStatus", "id", "name", "mode", "rankedScore", "level", "accuracy", "pp", "playCount", "globalRank", "countryCode", "backgroundColour",
            }));
            Assert.That(keys(at(root, "beatmap")), Is.EqualTo(new[]
            {
                "isKiai", "isBreak", "isConvert", "time", "status", "checksum", "id", "set", "mode",
                "artist", "artistUnicode", "title", "titleUnicode", "mapper", "version", "stats",
            }));
            Assert.That(keys(at(root, "beatmap.time")), Is.EqualTo(new[] { "live", "firstObject", "lastObject", "mp3Length" }));
            Assert.That(keys(at(root, "beatmap.stats")), Is.EqualTo(new[] { "stars", "ar", "cs", "od", "hp", "bpm", "objects", "maxCombo" }));
            Assert.That(keys(at(root, "beatmap.stats.ar")), Is.EqualTo(new[] { "original", "converted" }));
            Assert.That(keys(at(root, "beatmap.stats.bpm")), Is.EqualTo(new[] { "realtime", "common", "min", "max" }));
            Assert.That(keys(at(root, "beatmap.stats.objects")), Is.EqualTo(new[] { "circles", "sliders", "spinners", "holds", "total" }));
            Assert.That(keys(at(root, "play")), Is.EqualTo(new[]
            {
                "playerName", "mode", "score", "accuracy", "healthBar", "hits", "hitErrorArray", "combo", "mods", "rank", "pp", "unstableRate",
            }));
            Assert.That(keys(at(root, "play.hits")), Is.EqualTo(new[] { "300", "geki", "100", "katu", "50", "0", "sliderBreaks", "sliderEndHits", "smallTickHits", "largeTickHits" }));
            Assert.That(keys(at(root, "play.combo")), Is.EqualTo(new[] { "current", "max" }));
            Assert.That(keys(at(root, "play.mods")), Is.EqualTo(new[] { "checksum", "number", "name", "array", "rate" }));
            Assert.That(keys(at(root, "play.rank")), Is.EqualTo(new[] { "current", "maxThisPlay" }));
            Assert.That(keys(at(root, "play.pp")), Is.EqualTo(new[] { "current", "fc", "maxAchieved", "maxAchievedThisPlay", "detailed" }));
            Assert.That(keys(at(root, "resultsScreen")), Is.EqualTo(new[]
            {
                "scoreId", "playerName", "mode", "score", "accuracy", "name", "hits", "mods", "maxCombo", "rank", "pp", "createdAt",
            }));
            Assert.That(keys(at(root, "folders")), Is.EqualTo(new[] { "game", "skin", "songs", "beatmap" }));
            Assert.That(keys(at(root, "files")), Is.EqualTo(new[] { "beatmap", "background", "audio" }));
            Assert.That(keys(at(root, "settings")), Does.Contain("audio").And.Contain("background").And.Contain("mode").And.Contain("resolution"));
        }

        [Test]
        public void TestDefaultsAreNeutral()
        {
            var root = build(new BanchosucksOverlayState());

            Assert.That(at(root, "client").GetString(), Is.EqualTo("lazer"));
            Assert.That(at(root, "state.number").GetInt32(), Is.EqualTo(0));
            Assert.That(at(root, "state.name").GetString(), Is.EqualTo("menu"));
            Assert.That(at(root, "beatmap.title").GetString(), Is.Empty);
            Assert.That(at(root, "beatmap.status.name").GetString(), Is.EqualTo("unknown"));
            Assert.That(at(root, "play.score").GetInt64(), Is.EqualTo(0));
            Assert.That(at(root, "play.accuracy").GetDouble(), Is.EqualTo(100));
            Assert.That(at(root, "play.rank.current").GetString(), Is.Empty);
            Assert.That(at(root, "play.mods.name").GetString(), Is.Empty);
            Assert.That(at(root, "play.mods.array").GetArrayLength(), Is.EqualTo(0));
            Assert.That(at(root, "play.mods.rate").GetDouble(), Is.EqualTo(1));
            Assert.That(at(root, "play.hitErrorArray").GetArrayLength(), Is.EqualTo(0));
            Assert.That(at(root, "leaderboard").GetArrayLength(), Is.EqualTo(0));
            Assert.That(at(root, "profile.name").GetString(), Is.Empty);
            Assert.That(at(root, "profile.userStatus.name").GetString(), Is.EqualTo("disconnected"));
        }

        [Test]
        public void TestNothingPrivateInTheDocument()
        {
            var state = filledState();
            string json = BanchosucksOverlayJson.Build(state);
            var root = JsonDocument.Parse(json).RootElement;

            foreach (string forbidden in new[] { "password", "token", "email", "e-mail", "ipAddress", "chat\":\"", "C:\\\\" })
                Assert.That(json, Does.Not.Contain(forbidden).IgnoreCase, forbidden);

            Assert.That(at(root, "profile.countryCode.name").GetString(), Is.Empty, "the country is not handed out");
            Assert.That(at(root, "settings.chatVisibilityStatus.name").GetString(), Is.EqualTo("hidden"));

            foreach (string group in new[] { "folders", "files", "directPath" })
            {
                foreach (var path in at(root, group).EnumerateObject())
                    Assert.That(path.Value.GetString(), Is.Empty, $"{group}.{path.Name}: no local paths");
            }
        }

        [Test]
        public void TestFilledDocument()
        {
            var root = build(filledState());

            Assert.That(at(root, "state.number").GetInt32(), Is.EqualTo(2));
            Assert.That(at(root, "state.name").GetString(), Is.EqualTo("play"));
            Assert.That(at(root, "session.playCount").GetInt32(), Is.EqualTo(3));
            Assert.That(at(root, "settings.mode.name").GetString(), Is.EqualTo("mania"));
            Assert.That(at(root, "settings.audio.volume.music").GetDouble(), Is.EqualTo(35));
            Assert.That(at(root, "settings.background.dim").GetDouble(), Is.EqualTo(70));
            Assert.That(at(root, "settings.client.version").GetString(), Is.EqualTo("2026.913.18-banchosucks"));

            Assert.That(at(root, "profile.id").GetInt32(), Is.EqualTo(1001));
            Assert.That(at(root, "profile.name").GetString(), Is.EqualTo("Lidl"));
            Assert.That(at(root, "profile.userStatus.name").GetString(), Is.EqualTo("connected"));
            Assert.That(at(root, "profile.banchoStatus.name").GetString(), Is.EqualTo("playing"));
            Assert.That(at(root, "profile.pp").GetDouble(), Is.EqualTo(4321.5));
            Assert.That(at(root, "profile.globalRank").GetInt32(), Is.EqualTo(7));

            Assert.That(at(root, "beatmap.artist").GetString(), Is.EqualTo("supercell"));
            Assert.That(at(root, "beatmap.titleUnicode").GetString(), Is.EqualTo("君の知らない物語"));
            Assert.That(at(root, "beatmap.mapper").GetString(), Is.EqualTo("mapper"));
            Assert.That(at(root, "beatmap.version").GetString(), Is.EqualTo("Insane"));
            Assert.That(at(root, "beatmap.id").GetInt32(), Is.EqualTo(123));
            Assert.That(at(root, "beatmap.set").GetInt32(), Is.EqualTo(45));
            Assert.That(at(root, "beatmap.checksum").GetString(), Is.EqualTo("0123456789abcdef0123456789abcdef"));
            Assert.That(at(root, "beatmap.status.number").GetInt32(), Is.EqualTo(4));
            Assert.That(at(root, "beatmap.status.name").GetString(), Is.EqualTo("ranked"));
            Assert.That(at(root, "beatmap.mode.name").GetString(), Is.EqualTo("osu"));
            Assert.That(at(root, "beatmap.isConvert").GetBoolean(), Is.True);
            Assert.That(at(root, "beatmap.isKiai").GetBoolean(), Is.True);
            Assert.That(at(root, "beatmap.time.live").GetInt64(), Is.EqualTo(61235), "whole milliseconds");
            Assert.That(at(root, "beatmap.time.firstObject").GetInt64(), Is.EqualTo(1500));
            Assert.That(at(root, "beatmap.time.lastObject").GetInt64(), Is.EqualTo(240000));
            Assert.That(at(root, "beatmap.time.mp3Length").GetInt64(), Is.EqualTo(245000));
            Assert.That(at(root, "beatmap.stats.stars.total").GetDouble(), Is.EqualTo(5.43), "two decimals");
            Assert.That(at(root, "beatmap.stats.stars.live").GetDouble(), Is.EqualTo(2.1));
            Assert.That(at(root, "beatmap.stats.ar.original").GetDouble(), Is.EqualTo(9));
            Assert.That(at(root, "beatmap.stats.ar.converted").GetDouble(), Is.EqualTo(10.33));
            Assert.That(at(root, "beatmap.stats.bpm.common").GetDouble(), Is.EqualTo(180));
            Assert.That(at(root, "beatmap.stats.bpm.max").GetDouble(), Is.EqualTo(240));
            Assert.That(at(root, "beatmap.stats.objects.sliders").GetInt32(), Is.EqualTo(300));
            Assert.That(at(root, "beatmap.stats.maxCombo").GetInt32(), Is.EqualTo(1234));
            Assert.That(at(root, "performance.accuracy.100").GetDouble(), Is.EqualTo(512.34));
            Assert.That(at(root, "performance.accuracy.95").GetDouble(), Is.EqualTo(0), "not known");

            Assert.That(at(root, "play.playerName").GetString(), Is.EqualTo("Lidl"));
            Assert.That(at(root, "play.mode.number").GetInt32(), Is.EqualTo(3));
            Assert.That(at(root, "play.score").GetInt64(), Is.EqualTo(987654));
            Assert.That(at(root, "play.accuracy").GetDouble(), Is.EqualTo(98.77));
            Assert.That(at(root, "play.healthBar.normal").GetDouble(), Is.EqualTo(150), "stable's 0 to 200");
            Assert.That(at(root, "play.hits.300").GetInt32(), Is.EqualTo(400));
            Assert.That(at(root, "play.hits.geki").GetInt32(), Is.EqualTo(350));
            Assert.That(at(root, "play.hits.100").GetInt32(), Is.EqualTo(12));
            Assert.That(at(root, "play.hits.katu").GetInt32(), Is.EqualTo(20));
            Assert.That(at(root, "play.hits.50").GetInt32(), Is.EqualTo(3));
            Assert.That(at(root, "play.hits.0").GetInt32(), Is.EqualTo(2));
            Assert.That(at(root, "play.combo.current").GetInt32(), Is.EqualTo(321));
            Assert.That(at(root, "play.combo.max").GetInt32(), Is.EqualTo(654));
            Assert.That(at(root, "play.mods.name").GetString(), Is.EqualTo("HDDT"));
            Assert.That(at(root, "play.mods.number").GetInt32(), Is.EqualTo(72));
            Assert.That(at(root, "play.mods.rate").GetDouble(), Is.EqualTo(1.5));
            Assert.That(at(root, "play.mods.array").EnumerateArray().Select(m => m.GetProperty("acronym").GetString()), Is.EqualTo(new[] { "HD", "DT" }));
            Assert.That(at(root, "play.rank.current").GetString(), Is.EqualTo("SH"));
            Assert.That(at(root, "play.pp.current").GetDouble(), Is.EqualTo(123.46));
            Assert.That(at(root, "play.pp.fc").GetDouble(), Is.EqualTo(234.5));
            Assert.That(at(root, "play.pp.maxAchievedThisPlay").GetDouble(), Is.EqualTo(130));
            Assert.That(at(root, "play.pp.detailed.current.aim").GetDouble(), Is.EqualTo(60));
            Assert.That(at(root, "play.unstableRate").GetDouble(), Is.EqualTo(88.12));

            Assert.That(at(root, "resultsScreen.playerName").GetString(), Is.EqualTo("SomeoneElse"));
            Assert.That(at(root, "resultsScreen.score").GetInt64(), Is.EqualTo(555555));
            Assert.That(at(root, "resultsScreen.rank").GetString(), Is.EqualTo("A"));
            Assert.That(at(root, "resultsScreen.maxCombo").GetInt32(), Is.EqualTo(700));
            Assert.That(at(root, "resultsScreen.pp.current").GetDouble(), Is.EqualTo(99.9));
            Assert.That(at(root, "resultsScreen.hits.0").GetInt32(), Is.EqualTo(9));
            Assert.That(at(root, "resultsScreen.hits").TryGetProperty("sliderBreaks", out _), Is.False);
            Assert.That(at(root, "resultsScreen.createdAt").GetString(), Is.EqualTo("2026-11-23T18:00:00.000Z"));
        }

        [Test]
        public void TestUnicodeIsNotEscapedAndQuotesAre()
        {
            var state = new BanchosucksOverlayState();
            state.Beatmap.Title = "He said \"hi\" \\ <b>";
            state.Beatmap.TitleUnicode = "君の知らない物語";

            string json = BanchosucksOverlayJson.Build(state);

            Assert.That(json, Does.Contain("君の知らない物語"));
            Assert.That(JsonDocument.Parse(json).RootElement.GetProperty("beatmap").GetProperty("title").GetString(), Is.EqualTo("He said \"hi\" \\ <b>"));
        }

        [Test]
        public void TestNotANumberBecomesZero()
        {
            var state = new BanchosucksOverlayState();
            state.Play.Accuracy = double.NaN;
            state.Play.PP.Total = double.PositiveInfinity;
            state.Beatmap.TimeLive = double.NaN;

            var root = build(state);

            Assert.That(at(root, "play.accuracy").GetDouble(), Is.EqualTo(0));
            Assert.That(at(root, "play.pp.current").GetDouble(), Is.EqualTo(0));
            Assert.That(at(root, "beatmap.time.live").GetInt64(), Is.EqualTo(0));
        }

        [Test]
        public void TestNames()
        {
            Assert.That(BanchosucksOverlayJson.GameStateName(BanchosucksOverlayGameState.Menu), Is.EqualTo("menu"));
            Assert.That(BanchosucksOverlayJson.GameStateName(BanchosucksOverlayGameState.SelectPlay), Is.EqualTo("selectPlay"));
            Assert.That(BanchosucksOverlayJson.GameStateName(BanchosucksOverlayGameState.ResultScreen), Is.EqualTo("resultScreen"));
            Assert.That((int)BanchosucksOverlayGameState.Play, Is.EqualTo(2));
            Assert.That((int)BanchosucksOverlayGameState.SelectPlay, Is.EqualTo(5));
            Assert.That((int)BanchosucksOverlayGameState.ResultScreen, Is.EqualTo(7));

            Assert.That(new[] { 0, 1, 2, 3 }.Select(BanchosucksOverlayJson.ModeName), Is.EqualTo(new[] { "osu", "taiko", "fruits", "mania" }));

            Assert.That(BanchosucksOverlayMapping.RankedStatus(BeatmapOnlineStatus.Ranked), Is.EqualTo(4));
            Assert.That(BanchosucksOverlayMapping.RankedStatus(BeatmapOnlineStatus.Approved), Is.EqualTo(5));
            Assert.That(BanchosucksOverlayMapping.RankedStatus(BeatmapOnlineStatus.Qualified), Is.EqualTo(6));
            Assert.That(BanchosucksOverlayMapping.RankedStatus(BeatmapOnlineStatus.Loved), Is.EqualTo(7));
            Assert.That(BanchosucksOverlayMapping.RankedStatus(BeatmapOnlineStatus.Pending), Is.EqualTo(2));
            Assert.That(BanchosucksOverlayMapping.RankedStatus(BeatmapOnlineStatus.Graveyard), Is.EqualTo(2));
            Assert.That(BanchosucksOverlayMapping.RankedStatus(BeatmapOnlineStatus.LocallyModified), Is.EqualTo(1));
            Assert.That(BanchosucksOverlayMapping.RankedStatus(BeatmapOnlineStatus.None), Is.EqualTo(0));
            Assert.That(BanchosucksOverlayJson.RankedStatusName(7), Is.EqualTo("loved"));
            Assert.That(BanchosucksOverlayJson.RankedStatusName(3), Is.EqualTo("unknown"));
        }

        [Test]
        public void TestHitCountsPerRuleset()
        {
            var statistics = new Dictionary<HitResult, int>
            {
                [HitResult.Perfect] = 6,
                [HitResult.Great] = 5,
                [HitResult.Good] = 4,
                [HitResult.Ok] = 3,
                [HitResult.Meh] = 2,
                [HitResult.Miss] = 1,
                [HitResult.LargeTickHit] = 10,
                [HitResult.LargeTickMiss] = 11,
                [HitResult.SmallTickHit] = 12,
                [HitResult.SmallTickMiss] = 13,
                [HitResult.SliderTailHit] = 14,
            };

            var osu = new BanchosucksOverlayState.HitsState();
            BanchosucksOverlayMapping.FillHits(osu, statistics, 0);
            Assert.That(new[] { osu.Great, osu.Geki, osu.Ok, osu.Katu, osu.Meh, osu.Miss }, Is.EqualTo(new[] { 5, 0, 3, 0, 2, 1 }));
            Assert.That(new[] { osu.SliderEndHits, osu.SmallTickHits, osu.LargeTickHits }, Is.EqualTo(new[] { 14, 12, 10 }));

            var mania = new BanchosucksOverlayState.HitsState();
            BanchosucksOverlayMapping.FillHits(mania, statistics, 3);
            Assert.That(new[] { mania.Great, mania.Geki, mania.Ok, mania.Katu, mania.Meh, mania.Miss }, Is.EqualTo(new[] { 5, 6, 3, 4, 2, 1 }));

            var fruits = new BanchosucksOverlayState.HitsState();
            BanchosucksOverlayMapping.FillHits(fruits, statistics, 2);
            Assert.That(new[] { fruits.Great, fruits.Geki, fruits.Ok, fruits.Katu, fruits.Meh, fruits.Miss }, Is.EqualTo(new[] { 5, 0, 10, 13, 12, 12 }));

            var empty = new BanchosucksOverlayState.HitsState();
            BanchosucksOverlayMapping.FillHits(empty, new Dictionary<HitResult, int>(), 0);
            Assert.That(empty.Great + empty.Miss + empty.Ok, Is.EqualTo(0));
        }

        [Test]
        public void TestFullComboStatistics()
        {
            var maximum = new Dictionary<HitResult, int>
            {
                [HitResult.Great] = 100,
                [HitResult.LargeTickHit] = 40,
                [HitResult.SliderTailHit] = 20,
                [HitResult.LargeBonus] = 30,
            };

            // 50 objects into the map: 40 great, 5 ok, 2 meh, 3 misses, some ticks missed
            var current = new Dictionary<HitResult, int>
            {
                [HitResult.Great] = 40,
                [HitResult.Ok] = 5,
                [HitResult.Meh] = 2,
                [HitResult.Miss] = 3,
                [HitResult.LargeTickHit] = 15,
                [HitResult.LargeTickMiss] = 4,
                [HitResult.SliderTailHit] = 8,
                [HitResult.LargeBonus] = 6,
            };

            var fullCombo = BanchosucksOverlayFullCombo.Statistics(current, maximum);

            Assert.That(fullCombo[HitResult.Ok], Is.EqualTo(5), "what was not a miss stays");
            Assert.That(fullCombo[HitResult.Meh], Is.EqualTo(2));
            Assert.That(fullCombo[HitResult.Great], Is.EqualTo(93), "misses and the rest of the map become the best judgement");
            Assert.That(fullCombo.ContainsKey(HitResult.Miss), Is.False);
            Assert.That(fullCombo.ContainsKey(HitResult.LargeTickMiss), Is.False);
            Assert.That(fullCombo[HitResult.LargeTickHit], Is.EqualTo(40), "all ticks hit");
            Assert.That(fullCombo[HitResult.SliderTailHit], Is.EqualTo(20));
            Assert.That(fullCombo[HitResult.LargeBonus], Is.EqualTo(6), "bonus is not touched");

            Assert.That(fullCombo.Where(kvp => kvp.Key.IsBasic()).Sum(kvp => kvp.Value), Is.EqualTo(100));
        }

        [Test]
        public void TestFullComboStatisticsMania()
        {
            var maximum = new Dictionary<HitResult, int> { [HitResult.Perfect] = 500 };
            var current = new Dictionary<HitResult, int> { [HitResult.Perfect] = 100, [HitResult.Great] = 50, [HitResult.Miss] = 10 };

            var fullCombo = BanchosucksOverlayFullCombo.Statistics(current, maximum);

            Assert.That(fullCombo[HitResult.Great], Is.EqualTo(50));
            Assert.That(fullCombo[HitResult.Perfect], Is.EqualTo(450));
        }

        [TestCase(null, true)]
        [TestCase("", true)]
        [TestCase("null", true)]
        [TestCase("file://", true)]
        [TestCase("http://127.0.0.1:24050", true)]
        [TestCase("http://localhost:24050", true)]
        [TestCase("http://localhost", true)]
        [TestCase("https://localhost:5173", true)]
        [TestCase("http://overlay.localhost:8080", true)]
        [TestCase("http://[::1]:3000", true)]
        [TestCase("https://evil.example", false)]
        [TestCase("http://127.0.0.1.evil.example", false)]
        [TestCase("http://localhost.evil.example", false)]
        [TestCase("https://banchosucks.cc", false)]
        [TestCase("http://192.168.0.5:24050", false)]
        [TestCase("chrome-extension://abcdef", false)]
        [TestCase("not a url", false)]
        public void TestAllowedOrigins(string? origin, bool allowed)
        {
            Assert.That(BanchosucksOverlayHttp.IsAllowedOrigin(origin), Is.EqualTo(allowed));
        }

        [Test]
        public void TestPaths()
        {
            Assert.That(BanchosucksOverlayHttp.IsWebSocketPath("/websocket/v2"));
            Assert.That(BanchosucksOverlayHttp.IsWebSocketPath("/websocket/v2/"));
            Assert.That(BanchosucksOverlayHttp.IsWebSocketPath("/websocket/v2/precise"), Is.False);
            Assert.That(BanchosucksOverlayHttp.IsWebSocketPath("/"), Is.False);
            Assert.That(BanchosucksOverlayHttp.IsJsonPath("/json/v2"));
            Assert.That(BanchosucksOverlayHttp.IsJsonPath("/json"), Is.False);
            Assert.That(BanchosucksOverlayHttp.DEFAULT_PORT, Is.EqualTo(24050));
        }

        [Test]
        public void TestStaticFileResolution()
        {
            string root = Path.Combine(Path.GetTempPath(), "banchosucks-overlay-test-root");

            Assert.That(BanchosucksOverlayHttp.ResolveStaticFile(root, "/pp-counter/index.html"), Is.EqualTo(Path.Combine(root, "pp-counter", "index.html")));
            Assert.That(BanchosucksOverlayHttp.ResolveStaticFile(root, "/pp-counter/"), Is.EqualTo(Path.Combine(root, "pp-counter", "index.html")));
            Assert.That(BanchosucksOverlayHttp.ResolveStaticFile(root, "/pp-counter"), Is.EqualTo(Path.Combine(root, "pp-counter", "index.html")));
            Assert.That(BanchosucksOverlayHttp.ResolveStaticFile(root, "/"), Is.EqualTo(Path.Combine(root, "index.html")));
            Assert.That(BanchosucksOverlayHttp.ResolveStaticFile(root, "/pp-counter/js/socket.js"), Is.EqualTo(Path.Combine(root, "pp-counter", "js", "socket.js")));
            Assert.That(BanchosucksOverlayHttp.ResolveStaticFile(root, "/my%20overlay/style.css"), Is.EqualTo(Path.Combine(root, "my overlay", "style.css")));

            foreach (string refused in new[]
                     {
                         "/../secret.txt",
                         "/pp-counter/../../secret.txt",
                         "/%2e%2e/secret.txt",
                         "/pp-counter/%2e%2e/%2e%2e/secret.txt",
                         "/..%2fsecret.txt",
                         "/..%5csecret.txt",
                         "/pp-counter\\..\\..\\secret.txt",
                         "/C:/Windows/win.ini",
                         "/.git/config",
                         "/pp-counter/.env",
                         "/pp-counter/file.txt.",
                         "/pp-counter/%00.html",
                         "pp-counter/index.html",
                         "",
                     })
                Assert.That(BanchosucksOverlayHttp.ResolveStaticFile(root, refused), Is.Null, refused);
        }

        [Test]
        public void TestContentTypes()
        {
            Assert.That(BanchosucksOverlayHttp.ContentTypeFor("index.HTML"), Does.StartWith("text/html"));
            Assert.That(BanchosucksOverlayHttp.ContentTypeFor("a/b/app.js"), Does.StartWith("text/javascript"));
            Assert.That(BanchosucksOverlayHttp.ContentTypeFor("style.css"), Does.StartWith("text/css"));
            Assert.That(BanchosucksOverlayHttp.ContentTypeFor("font.woff2"), Is.EqualTo("font/woff2"));
            Assert.That(BanchosucksOverlayHttp.ContentTypeFor("thing.bin"), Is.EqualTo("application/octet-stream"));
        }

        private static BanchosucksOverlayState filledState()
        {
            var state = new BanchosucksOverlayState
            {
                Game = BanchosucksOverlayGameState.Play,
                ClientVersion = "2026.913.18-banchosucks",
                SessionPlayTime = 123456,
                SessionPlayCount = 3,
                RulesetId = 3,
            };

            state.Settings.VolumeMaster = 80;
            state.Settings.VolumeMusic = 35;
            state.Settings.VolumeEffect = 60;
            state.Settings.BackgroundDim = 70;
            state.Settings.Storyboard = true;
            state.Settings.SkinName = "my skin";
            state.Settings.WindowWidth = 1920;
            state.Settings.WindowHeight = 1080;

            state.Profile.Online = true;
            state.Profile.ActivityNumber = 2;
            state.Profile.ActivityName = "playing";
            state.Profile.Id = 1001;
            state.Profile.Name = "Lidl";
            state.Profile.PP = 4321.5;
            state.Profile.GlobalRank = 7;
            state.Profile.Accuracy = 97.31;
            state.Profile.PlayCount = 5000;

            var map = state.Beatmap;
            map.IsKiai = true;
            map.IsConvert = true;
            map.TimeLive = 61234.6;
            map.TimeFirstObject = 1500;
            map.TimeLastObject = 240000;
            map.TimeMp3Length = 245000;
            map.StatusNumber = 4;
            map.Checksum = "0123456789abcdef0123456789abcdef";
            map.Id = 123;
            map.SetId = 45;
            map.RulesetId = 0;
            map.Artist = "supercell";
            map.ArtistUnicode = "supercell";
            map.Title = "Kimi no Shiranai Monogatari";
            map.TitleUnicode = "君の知らない物語";
            map.Mapper = "mapper";
            map.Version = "Insane";
            map.Stars = 5.4321;
            map.StarsLive = 2.1;
            map.MaxPP = 512.341;
            map.ApproachRate = 9;
            map.ApproachRateConverted = 10.333;
            map.BpmCommon = 180;
            map.BpmMin = 90;
            map.BpmMax = 240;
            map.BpmRealtime = 180;
            map.Circles = 500;
            map.Sliders = 300;
            map.Spinners = 2;
            map.Objects = 802;
            map.MaxCombo = 1234;

            var play = state.Play;
            play.Active = true;
            play.PlayerName = "Lidl";
            play.RulesetId = 3;
            play.Score = 987654;
            play.Accuracy = 98.768;
            play.Health = 0.75;
            play.Hits.Great = 400;
            play.Hits.Geki = 350;
            play.Hits.Ok = 12;
            play.Hits.Katu = 20;
            play.Hits.Meh = 3;
            play.Hits.Miss = 2;
            play.Combo = 321;
            play.MaxCombo = 654;
            play.Mods = new BanchosucksOverlayState.ModsState { Number = 72, Acronyms = new[] { "HD", "DT" }, Rate = 1.5 };
            play.Rank = "SH";
            play.PP = new BanchosucksOverlayState.PerformanceState { Total = 123.456, Aim = 60 };
            play.PPIfFullCombo = new BanchosucksOverlayState.PerformanceState { Total = 234.5 };
            play.PPMaxThisPlay = 130;
            play.UnstableRate = 88.123;

            var results = state.Results;
            results.Active = true;
            results.PlayerName = "SomeoneElse";
            results.Score = 555555;
            results.Accuracy = 91.2;
            results.MaxCombo = 700;
            results.Rank = "A";
            results.PP = 99.9;
            results.Hits.Miss = 9;
            results.CreatedAt = "2026-11-23T18:00:00.000Z";

            return state;
        }
    }
}
