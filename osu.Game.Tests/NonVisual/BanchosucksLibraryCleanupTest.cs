// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using NUnit.Framework;
using osu.Framework.Extensions;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Models;
using osu.Game.Rulesets;
using osu.Game.Tests.Database;
using osu.Game.Tests.Resources;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>
    /// Banchosucks: what "clean up library" calls a duplicate or a broken beatmap set, and what the scan reads from the database and the file store.
    /// </summary>
    [TestFixture]
    public class BanchosucksLibraryCleanupTest : RealmTest
    {
        private static readonly DateTimeOffset day = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

        private static BanchosucksLibraryCleanup.Set set(string title, int onlineId = -1, int addedOnDay = 0, int scores = 0, params BanchosucksLibraryCleanup.Difficulty[] difficulties)
            => new BanchosucksLibraryCleanup.Set(Guid.NewGuid(), title, onlineId, day.AddDays(addedOnDay), scores,
                difficulties.Length > 0 ? difficulties : new[] { ok($"{title} only difficulty") });

        private static BanchosucksLibraryCleanup.Difficulty ok(string hash) => new BanchosucksLibraryCleanup.Difficulty(hash, false, false);
        private static BanchosucksLibraryCleanup.Difficulty noFile(string hash) => new BanchosucksLibraryCleanup.Difficulty(hash, true, false);
        private static BanchosucksLibraryCleanup.Difficulty noAudio(string hash) => new BanchosucksLibraryCleanup.Difficulty(hash, false, true);

        #region Duplicates

        [Test]
        public void TestCleanLibraryHasNothingToRemove()
        {
            var result = BanchosucksLibraryCleanup.Analyse(new[] { set("a", 1), set("b", 2), set("c"), set("d") });

            Assert.That(result.ScannedSets, Is.EqualTo(4));
            Assert.That(result.AnythingToRemove, Is.False);
            Assert.That(result.DuplicateGroups, Is.Empty);
            Assert.That(result.BrokenSets, Is.Empty);
            Assert.That(result.PartlyDamagedSets, Is.Empty);
        }

        [Test]
        public void TestSameOnlineIdKeepsTheMostRecentImport()
        {
            var oldest = set("oldest", 100, addedOnDay: 1);
            var newest = set("newest", 100, addedOnDay: 3);
            var middle = set("middle", 100, addedOnDay: 2);

            var result = BanchosucksLibraryCleanup.Analyse(new[] { oldest, newest, middle, set("other", 200) });

            Assert.That(result.DuplicateGroups, Has.Count.EqualTo(1));
            Assert.That(result.DuplicateGroups[0].Keep, Is.EqualTo(new[] { newest }));
            Assert.That(result.DuplicateGroups[0].Remove, Is.EquivalentTo(new[] { oldest, middle }));
            Assert.That(result.DuplicatesToRemove, Is.EquivalentTo(new[] { oldest, middle }));
        }

        [Test]
        public void TestSameDifficultiesAreDuplicatesWithoutAnOnlineId()
        {
            // the usual case in lazer: the same .osu files imported from two different archives; the older copy lost its online id.
            var older = set("older", -1, 1, 0, ok("easy"), ok("hard"));
            var newer = set("newer", 555, 2, 0, ok("hard"), ok("easy"));
            var differentSpread = set("only one of them", -1, 3, 0, ok("easy"));

            var result = BanchosucksLibraryCleanup.Analyse(new[] { older, newer, differentSpread });

            Assert.That(result.DuplicateGroups, Has.Count.EqualTo(1));
            Assert.That(result.DuplicateGroups[0].Keep, Is.EqualTo(new[] { newer }));
            Assert.That(result.DuplicateGroups[0].Remove, Is.EqualTo(new[] { older }));
        }

        [Test]
        public void TestCopyWithScoresIsKeptEvenWhenOlder()
        {
            var played = set("played", 100, addedOnDay: 1, scores: 3);
            var fresh = set("fresh", 100, addedOnDay: 9);

            var result = BanchosucksLibraryCleanup.Analyse(new[] { fresh, played });

            Assert.That(result.DuplicateGroups[0].Keep, Is.EqualTo(new[] { played }));
            Assert.That(result.DuplicateGroups[0].Remove, Is.EqualTo(new[] { fresh }));
        }

        [Test]
        public void TestEveryCopyWithScoresIsKept()
        {
            var playedOnce = set("played once", 100, addedOnDay: 1, scores: 1);
            var playedOften = set("played often", 100, addedOnDay: 2, scores: 20);
            var unplayed = set("unplayed", 100, addedOnDay: 3);

            var result = BanchosucksLibraryCleanup.Analyse(new[] { playedOnce, playedOften, unplayed });

            Assert.That(result.DuplicateGroups[0].Keep, Is.EquivalentTo(new[] { playedOnce, playedOften }));
            Assert.That(result.DuplicatesToRemove, Is.EqualTo(new[] { unplayed }));

            // only played copies: a duplicate group, but nothing that may be removed.
            var onlyPlayed = BanchosucksLibraryCleanup.Analyse(new[] { playedOnce, playedOften });

            Assert.That(onlyPlayed.DuplicateGroups, Has.Count.EqualTo(1));
            Assert.That(onlyPlayed.DuplicatesToRemove, Is.Empty);
            Assert.That(onlyPlayed.AnythingToRemove, Is.False);
        }

        [Test]
        public void TestLinksByIdAndByDifficultiesAreOneGroup()
        {
            var a = set("a", 100, 1, 0, ok("x"));
            var b = set("b", 100, 2, 0, ok("y"));
            var c = set("c", -1, 3, 0, ok("y"));

            var result = BanchosucksLibraryCleanup.Analyse(new[] { a, b, c });

            Assert.That(result.DuplicateGroups, Has.Count.EqualTo(1));
            Assert.That(result.DuplicateGroups[0].Keep, Is.EqualTo(new[] { c }));
            Assert.That(result.DuplicateGroups[0].Remove, Is.EquivalentTo(new[] { a, b }));
        }

        [Test]
        public void TestSetsWithoutOnlineIdAreNotDuplicatesOfEachOther()
        {
            var result = BanchosucksLibraryCleanup.Analyse(new[] { set("a", -1), set("b", -1), set("c", 0), set("d", 0) });

            Assert.That(result.DuplicateGroups, Is.Empty);
        }

        [Test]
        public void TestSameInstantIsDecidedTheSameWayEveryTime()
        {
            var a = set("a", 100);
            var b = set("b", 100);

            var first = BanchosucksLibraryCleanup.Analyse(new[] { a, b });
            var second = BanchosucksLibraryCleanup.Analyse(new[] { b, a });

            Assert.That(first.DuplicateGroups[0].Keep.Single(), Is.EqualTo(second.DuplicateGroups[0].Keep.Single()));
            Assert.That(first.DuplicatesToRemove.Single(), Is.EqualTo(second.DuplicatesToRemove.Single()));
        }

        [Test]
        public void TestNoSetWithScoresIsEverProposedAsDuplicate()
        {
            var random = new Random(16);

            for (int run = 0; run < 200; run++)
            {
                var sets = Enumerable.Range(0, 40).Select(i => set($"set {i}", random.Next(-1, 6), random.Next(0, 5), random.Next(0, 3) == 0 ? random.Next(1, 4) : 0,
                    ok($"hash {random.Next(0, 8)}"))).ToList();

                var result = BanchosucksLibraryCleanup.Analyse(sets);

                Assert.That(result.DuplicatesToRemove.Where(s => s.LocalScoreCount > 0), Is.Empty);

                foreach (var group in result.DuplicateGroups)
                {
                    Assert.That(group.Keep, Is.Not.Empty, "a group never loses every copy");
                    Assert.That(group.Keep.Intersect(group.Remove), Is.Empty);
                }

                Assert.That(result.DuplicatesToRemove, Is.Unique);
            }
        }

        #endregion

        #region Broken sets

        [Test]
        public void TestBrokenSets()
        {
            var empty = new BanchosucksLibraryCleanup.Set(Guid.NewGuid(), "no difficulties", 1, day, 0, Array.Empty<BanchosucksLibraryCleanup.Difficulty>());
            var noFiles = set("no .osu files", 2, 0, 0, noFile("a"), noFile("b"));
            var silent = set("no audio", 3, 0, 0, noAudio("c"));
            var mixed = set("one without file, one without audio", 4, 0, 5, noFile("d"), noAudio("e"));
            var fine = set("fine", 5);

            var result = BanchosucksLibraryCleanup.Analyse(new[] { empty, noFiles, silent, mixed, fine });

            Assert.That(result.BrokenSets.Select(b => b.Set), Is.EquivalentTo(new[] { empty, noFiles, silent, mixed }));
            Assert.That(result.BrokenSets.Single(b => b.Set == empty).Reasons, Is.EqualTo(BanchosucksLibraryCleanup.BrokenReasons.NoDifficulties));
            Assert.That(result.BrokenSets.Single(b => b.Set == noFiles).Reasons, Is.EqualTo(BanchosucksLibraryCleanup.BrokenReasons.BeatmapFileMissing));
            Assert.That(result.BrokenSets.Single(b => b.Set == silent).Reasons, Is.EqualTo(BanchosucksLibraryCleanup.BrokenReasons.AudioMissing));
            Assert.That(result.BrokenSets.Single(b => b.Set == mixed).Reasons,
                Is.EqualTo(BanchosucksLibraryCleanup.BrokenReasons.BeatmapFileMissing | BanchosucksLibraryCleanup.BrokenReasons.AudioMissing));
            Assert.That(result.BrokenSetsWithScores, Is.EqualTo(1));
            Assert.That(result.PartlyDamagedSets, Is.Empty);
            Assert.That(result.AnythingToRemove, Is.True);
        }

        [Test]
        public void TestSetWithAPlayableDifficultyIsNeverRemoved()
        {
            var partly = set("partly damaged", 100, 5, 0, ok("easy"), noFile("hard"));
            var intactCopy = set("intact copy", 100, 1, 0, ok("easy"), ok("hard"));

            var result = BanchosucksLibraryCleanup.Analyse(new[] { partly, intactCopy });

            Assert.That(result.PartlyDamagedSets.Select(b => b.Set), Is.EqualTo(new[] { partly }));
            Assert.That(result.BrokenSets, Is.Empty);
            // and it takes no part in the duplicate search: neither is it removed as a copy, nor does it get the intact copy removed.
            Assert.That(result.DuplicateGroups, Is.Empty);
            Assert.That(result.AnythingToRemove, Is.False);
        }

        [Test]
        public void TestBrokenCopyDoesNotCountAsDuplicate()
        {
            var brokenCopy = set("broken copy", 100, 9, 0, noFile("easy"));
            var intact = set("intact", 100, 1, 0, ok("easy"));

            var result = BanchosucksLibraryCleanup.Analyse(new[] { brokenCopy, intact });

            Assert.That(result.BrokenSets.Select(b => b.Set), Is.EqualTo(new[] { brokenCopy }));
            Assert.That(result.DuplicateGroups, Is.Empty, "the intact set is the only good copy and must not be proposed");
        }

        #endregion

        #region Scan

        [Test]
        public void TestScanReadsFilesAndScores()
        {
            RunTestWithRealm((realm, storage) =>
            {
                var fileStore = new RealmFileStore(realm, storage);
                var ids = new Dictionary<string, Guid>();

                realm.Write(r =>
                {
                    var ruleset = r.Add(CreateRuleset());

                    RealmFile stored(string content) => fileStore.Add(new MemoryStream(Encoding.UTF8.GetBytes(content)), r);
                    RealmFile lost(string content) => r.Add(new RealmFile { Hash = sha(content) });

                    void add(string name, Action<BeatmapSetInfo> build, Action<BeatmapSetInfo>? adjust = null)
                    {
                        var beatmapSet = new BeatmapSetInfo { DateAdded = day };
                        build(beatmapSet);
                        adjust?.Invoke(beatmapSet);
                        ids[name] = r.Add(beatmapSet).ID;
                    }

                    void difficulty(BeatmapSetInfo beatmapSet, RealmFile? osuFile, string audioName, RealmFile? audioFile, string hash = "")
                    {
                        var beatmap = new BeatmapInfo(ruleset, new BeatmapDifficulty(), new BeatmapMetadata { Title = "title", Artist = "artist", AudioFile = audioName })
                        {
                            Hash = osuFile?.Hash ?? hash,
                            DifficultyName = $"difficulty {beatmapSet.Beatmaps.Count}",
                            BeatmapSet = beatmapSet,
                        };

                        beatmapSet.Beatmaps.Add(beatmap);

                        if (osuFile != null)
                            beatmapSet.Files.Add(new RealmNamedFileUsage(osuFile, $"{beatmapSet.Beatmaps.Count}.osu"));

                        if (audioFile != null && beatmapSet.Files.All(f => f.Filename != audioName))
                            beatmapSet.Files.Add(new RealmNamedFileUsage(audioFile, audioName));
                    }

                    add("intact", s =>
                    {
                        var audio = stored("intact audio");
                        difficulty(s, stored("intact easy"), "audio.mp3", audio);
                        // file names are compared without case, as the game does when it loads the track.
                        difficulty(s, stored("intact hard"), "AUDIO.MP3", audio);
                    });
                    add("osu file not on disk", s => difficulty(s, lost("lost osu"), "audio.mp3", stored("audio 2")));
                    add("osu file not in the set", s => difficulty(s, null, "audio.mp3", stored("audio 3"), sha("never stored")));
                    add("audio not on disk", s => difficulty(s, stored("osu 4"), "audio.mp3", lost("lost audio")));
                    add("audio not in the set", s => difficulty(s, stored("osu 5"), "audio.mp3", null));
                    add("virtual track", s => difficulty(s, stored("osu 6"), "virtual", null));
                    add("no audio named", s => difficulty(s, stored("osu 7"), string.Empty, null));
                    add("no difficulties", _ => { });
                    add("deleted", s => difficulty(s, lost("osu 8"), "audio.mp3", null), s => s.DeletePending = true);
                    add("protected", s => difficulty(s, lost("osu 9"), "audio.mp3", null), s => s.Protected = true);
                    add("online id", s => difficulty(s, stored("osu 10"), "audio.mp3", stored("audio 10")), s => s.OnlineID = 4711);
                });

                realm.Write(r =>
                {
                    var beatmap = r.Find<BeatmapSetInfo>(ids["online id"])!.Beatmaps.First();

                    r.Add(TestResources.CreateTestScoreInfo(beatmap));
                    r.Add(TestResources.CreateTestScoreInfo(beatmap));

                    var deletedScore = TestResources.CreateTestScoreInfo(beatmap);
                    deletedScore.DeletePending = true;
                    r.Add(deletedScore);
                });

                var progress = new List<(int done, int total)>();
                var scanned = BanchosucksLibraryScanner.Scan(realm, storage, (done, total) => progress.Add((done, total))).ToDictionary(s => s.ID);

                BanchosucksLibraryCleanup.Set get(string name) => scanned[ids[name]];

                Assert.That(scanned.Keys, Is.EquivalentTo(ids.Where(kvp => kvp.Key != "deleted" && kvp.Key != "protected").Select(kvp => kvp.Value)));
                Assert.That(progress.Select(p => p.done), Is.EqualTo(Enumerable.Range(1, 9)));
                Assert.That(progress.Select(p => p.total), Has.All.EqualTo(9));

                Assert.That(get("intact").Difficulties, Has.Count.EqualTo(2).And.All.Matches<BanchosucksLibraryCleanup.Difficulty>(d => d.Playable));
                Assert.That(get("intact").Difficulties.Select(d => d.Hash), Is.EquivalentTo(new[] { sha("intact easy"), sha("intact hard") }));
                Assert.That(get("intact").Title, Does.Contain("title"));
                Assert.That(get("intact").DateAdded, Is.EqualTo(day));

                Assert.That(get("osu file not on disk").Difficulties.Single(), Is.EqualTo(new BanchosucksLibraryCleanup.Difficulty(sha("lost osu"), true, false)));
                Assert.That(get("osu file not in the set").Difficulties.Single().BeatmapFileMissing, Is.True);
                Assert.That(get("audio not on disk").Difficulties.Single(), Is.EqualTo(new BanchosucksLibraryCleanup.Difficulty(sha("osu 4"), false, true)));
                Assert.That(get("audio not in the set").Difficulties.Single().AudioMissing, Is.True);
                Assert.That(get("virtual track").Difficulties.Single().Playable, Is.True);
                Assert.That(get("no audio named").Difficulties.Single().Playable, Is.True);
                Assert.That(get("no difficulties").Difficulties, Is.Empty);

                Assert.That(get("online id").OnlineID, Is.EqualTo(4711));
                Assert.That(get("online id").LocalScoreCount, Is.EqualTo(2), "deleted scores do not count");
                Assert.That(get("intact").LocalScoreCount, Is.Zero);

                var result = BanchosucksLibraryCleanup.Analyse(scanned.Values);

                Assert.That(result.BrokenSets.Select(b => b.Set.ID), Is.EquivalentTo(new[]
                {
                    ids["osu file not on disk"], ids["osu file not in the set"], ids["audio not on disk"], ids["audio not in the set"], ids["no difficulties"],
                }));
                Assert.That(result.DuplicateGroups, Is.Empty);

                // the scan itself changes nothing.
                Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count(s => s.DeletePending)), Is.EqualTo(1));
                Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count()), Is.EqualTo(11));
            });
        }

        [Test]
        public void TestScanCanBeCancelled()
        {
            RunTestWithRealm((realm, storage) =>
            {
                realm.Write(r =>
                {
                    var ruleset = r.Add(CreateRuleset());

                    for (int i = 0; i < 5; i++)
                        r.Add(CreateBeatmapSet(ruleset));
                });

                using (var cancellation = new CancellationTokenSource())
                {
                    int seen = 0;

                    Assert.Throws<OperationCanceledException>(() => BanchosucksLibraryScanner.Scan(realm, storage, (done, _) =>
                    {
                        seen = done;

                        if (done == 2)
                            cancellation.Cancel();
                    }, cancellation.Token));

                    Assert.That(seen, Is.EqualTo(2), "stops before the next set");
                }

                Assert.That(realm.Run(r => r.All<BeatmapSetInfo>().Count(s => !s.DeletePending)), Is.EqualTo(5));
            });
        }

        private static string sha(string content) => new MemoryStream(Encoding.UTF8.GetBytes(content)).ComputeSHA2Hash();

        #endregion
    }
}
