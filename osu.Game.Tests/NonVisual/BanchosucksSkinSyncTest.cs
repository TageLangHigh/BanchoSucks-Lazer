// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Skinning;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>
    /// Skin sync from osu!stable (F43): the bookkeeping that decides which folders have to be imported.
    /// </summary>
    [TestFixture]
    public class BanchosucksSkinSyncTest
    {
        [Test]
        public void TestNewAndChangedFoldersAreImported()
        {
            var record = new BanchosucksSkinSyncRecord();

            var found = new Dictionary<string, BanchosucksSkinFingerprint>
            {
                ["Zeta"] = new BanchosucksSkinFingerprint(100, 5000, 12),
                ["alpha"] = new BanchosucksSkinFingerprint(200, 800, 3),
                ["Leerer Ordner"] = new BanchosucksSkinFingerprint(0, 0, 0),
            };

            Assert.That(record.FoldersToImport(found), Is.EqualTo(new[] { "alpha", "Zeta" }), "everything with files, by name");

            record.MarkImported("alpha", found["alpha"], Guid.NewGuid());
            record.MarkImported("Zeta", found["Zeta"], Guid.NewGuid());

            Assert.That(record.FoldersToImport(found), Is.Empty, "nothing changed, nothing to do");

            // folder names are the same folder whatever their case (Windows)
            Assert.That(record.FoldersToImport(new Dictionary<string, BanchosucksSkinFingerprint> { ["ALPHA"] = found["alpha"] }), Is.Empty);

            // a newer file
            found["alpha"] = new BanchosucksSkinFingerprint(201, 800, 3);
            Assert.That(record.FoldersToImport(found), Is.EqualTo(new[] { "alpha" }));

            // a file of another size with an old date
            found["alpha"] = new BanchosucksSkinFingerprint(200, 801, 3);
            Assert.That(record.FoldersToImport(found), Is.EqualTo(new[] { "alpha" }));

            // one file less
            found["alpha"] = new BanchosucksSkinFingerprint(200, 800, 2);
            Assert.That(record.FoldersToImport(found), Is.EqualTo(new[] { "alpha" }));

            // a folder that is gone from stable is no reason to do anything, and its entry stays
            found.Remove("Zeta");
            found["alpha"] = new BanchosucksSkinFingerprint(200, 800, 3);

            Assert.That(record.FoldersToImport(found), Is.Empty);
            Assert.That(record.Folders.ContainsKey("Zeta"), Is.True);

            // a new one next to the known ones
            found["Neu"] = new BanchosucksSkinFingerprint(1, 1, 1);
            Assert.That(record.FoldersToImport(found), Is.EqualTo(new[] { "Neu" }));
        }

        [Test]
        public void TestRecordRoundTrip()
        {
            var record = new BanchosucksSkinSyncRecord();
            var id = Guid.NewGuid();

            record.MarkImported("Mein Skin „v2“", new BanchosucksSkinFingerprint(639000000000000000, 123456789012, 240), id);
            record.MarkImported("ohne-id", new BanchosucksSkinFingerprint(5, 6, 7), null);

            string text = record.Serialise();
            var read = BanchosucksSkinSyncRecord.Deserialise(text);

            Assert.That(read.Folders.Keys, Is.EquivalentTo(new[] { "Mein Skin „v2“", "ohne-id" }));
            Assert.That(read.Folders["Mein Skin „v2“"].Fingerprint, Is.EqualTo(new BanchosucksSkinFingerprint(639000000000000000, 123456789012, 240)));
            Assert.That(read.Folders["Mein Skin „v2“"].SkinID, Is.EqualTo(id));
            Assert.That(read.Folders["ohne-id"].SkinID, Is.Null);

            // the file holds folder names and numbers, no paths
            Assert.That(text, Does.Not.Contain(":\\"));
            Assert.That(text, Does.Not.Contain(Path.GetTempPath().TrimEnd('\\', '/')));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("kaputt")]
        [TestCase("[]")]
        [TestCase(@"{ ""folders"": [1, 2] }")]
        public void TestBrokenRecordIsEmpty(string? text)
        {
            Assert.That(BanchosucksSkinSyncRecord.Deserialise(text).Folders, Is.Empty);
        }

        [Test]
        public void TestBrokenEntriesAreLeftOut()
        {
            var read = BanchosucksSkinSyncRecord.Deserialise(@"{ ""version"": 9, ""folders"": {
                ""gut"": { ""newest"": 10, ""size"": 20, ""files"": 3, ""skin"": ""keine-guid"", ""neu"": true },
                ""ohne Größe"": { ""newest"": 10, ""files"": 3 },
                ""Text"": ""x"",
                ""falscher Typ"": { ""newest"": ""10"", ""size"": 20, ""files"": 3 }
            } }");

            Assert.That(read.Folders.Keys, Is.EqualTo(new[] { "gut" }));
            Assert.That(read.Folders["gut"].Fingerprint, Is.EqualTo(new BanchosucksSkinFingerprint(10, 20, 3)));
            Assert.That(read.Folders["gut"].SkinID, Is.Null);
        }

        [Test]
        public void TestMeasuringFolders()
        {
            using (var storage = new TemporaryNativeStorage($"{nameof(BanchosucksSkinSyncTest)}-{Guid.NewGuid()}"))
            {
                string root = storage.GetFullPath(string.Empty);
                string skin = Path.Combine(root, "Mein Skin");
                string empty = Path.Combine(root, "Leer");

                Directory.CreateDirectory(Path.Combine(skin, "extra"));
                Directory.CreateDirectory(empty);

                var old = new DateTime(2024, 3, 1, 12, 0, 0, DateTimeKind.Utc);
                var newer = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

                write(Path.Combine(skin, "skin.ini"), new byte[100], old);
                write(Path.Combine(skin, "cursor.png"), new byte[2000], old);
                write(Path.Combine(skin, "extra", "hit300.png"), new byte[300], newer);

                // a file next to the folders is not a skin
                write(Path.Combine(root, "liesmich.txt"), new byte[10], newer);

                var measured = BanchosucksSkinSyncRecord.Measure(skin);

                Assert.That(measured, Is.EqualTo(new BanchosucksSkinFingerprint(newer.Ticks, 2400, 3)), "files in subfolders count");
                Assert.That(BanchosucksSkinSyncRecord.Measure(empty), Is.EqualTo(new BanchosucksSkinFingerprint(0, 0, 0)));

                var all = BanchosucksSkinSyncRecord.MeasureAll(root);

                Assert.That(all.Keys, Is.EquivalentTo(new[] { "Mein Skin", "Leer" }));
                Assert.That(all["Mein Skin"], Is.EqualTo(measured));

                // the same bytes again, saved later
                var later = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                write(Path.Combine(skin, "cursor.png"), new byte[2000], later);

                Assert.That(BanchosucksSkinSyncRecord.Measure(skin), Is.EqualTo(new BanchosucksSkinFingerprint(later.Ticks, 2400, 3)));

                // measuring leaves the folder exactly as it was
                Assert.That(Directory.GetFiles(root, "*", SearchOption.AllDirectories), Has.Length.EqualTo(4));
                Assert.That(File.GetLastWriteTimeUtc(Path.Combine(skin, "skin.ini")), Is.EqualTo(old));
            }
        }

        private static void write(string path, byte[] content, DateTime writeTime)
        {
            File.WriteAllBytes(path, content);
            File.SetLastWriteTimeUtc(path, writeTime);
        }
    }
}
