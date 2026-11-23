// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Updater;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class BanchosucksReleaseNotesTest
    {
        private const string marked = @"**Mania: Sortieren**
- Erster Punkt mit `Code` und **fett**.
- Zweiter Punkt.

**Noch ein Abschnitt**
- Dritter Punkt.

**Mania: sorting (English)**
- First bullet.

**Another section (English)**
- Second bullet.
- Third bullet.
";

        private const string unmarked = "**Vorabversion 1.2.3: nicht installieren.**\r\nNur ein Test.\r\n\r\n**Pre-release 1.2.3: do not install.**\r\nJust a test.\r\n";

        [TestCase("2026.913.17-banchosucks", "2026.913.17")]
        [TestCase("2026.913.17", "2026.913.17")]
        [TestCase("v2026.913.17-banchosucks", "2026.913.17")]
        [TestCase("2026.913.17+abcdef", "2026.913.17")]
        [TestCase("2026.913.17.0", "2026.913.17")]
        [TestCase("2026.913.170-banchosucks", "2026.913.170")]
        [TestCase("test game", "")]
        [TestCase("local Debug", "")]
        [TestCase("0.0.0-lazer", "0.0.0")]
        [TestCase("", "")]
        [TestCase(null, "")]
        public void TestNormaliseVersion(string? version, string expected)
        {
            Assert.That(BanchosucksReleaseNotes.NormaliseVersion(version), Is.EqualTo(expected));
        }

        [Test]
        public void TestShownOnceAfterUpdate()
        {
            // updated from a version that did not have the dialog yet
            Assert.That(BanchosucksReleaseNotes.ShouldShow("2026.913.17-banchosucks", "", "2026.913.18-banchosucks"), Is.True);
            // updated again later
            Assert.That(BanchosucksReleaseNotes.ShouldShow("2026.913.18-banchosucks", "2026.913.18", "2026.913.19-banchosucks"), Is.True);
            // the start after the notes were shown
            Assert.That(BanchosucksReleaseNotes.ShouldShow("2026.913.19-banchosucks", "2026.913.19", "2026.913.19-banchosucks"), Is.False);
        }

        [Test]
        public void TestNothingOnFreshInstall()
        {
            Assert.That(BanchosucksReleaseNotes.ShouldShow("", "", "2026.913.18-banchosucks"), Is.False);
            Assert.That(BanchosucksReleaseNotes.ShouldShow(null, null, "2026.913.18-banchosucks"), Is.False);
        }

        [Test]
        public void TestNothingForLocalBuilds()
        {
            Assert.That(BanchosucksReleaseNotes.ShouldShow("2026.913.17-banchosucks", "", "local Debug"), Is.False);
            Assert.That(BanchosucksReleaseNotes.ShouldShow("2026.913.17-banchosucks", "", "test game"), Is.False);
        }

        [Test]
        public void TestParseWithEnglishMarker()
        {
            var notes = BanchosucksReleaseNotes.Parse(marked);

            Assert.That(notes.German.Select(s => s.Title), Is.EqualTo(new[] { "Mania: Sortieren", "Noch ein Abschnitt" }));
            Assert.That(notes.English.Select(s => s.Title), Is.EqualTo(new[] { "Mania: sorting", "Another section" }), "the marker is not shown");

            Assert.That(notes.German[0].Entries.Select(e => e.Text), Is.EqualTo(new[] { "Erster Punkt mit Code und fett.", "Zweiter Punkt." }));
            Assert.That(notes.German[0].Entries.All(e => e.Bullet));
            Assert.That(notes.English[1].Entries.Select(e => e.Text), Is.EqualTo(new[] { "Second bullet.", "Third bullet." }));

            Assert.That(notes.For(true), Is.SameAs(notes.German));
            Assert.That(notes.For(false), Is.SameAs(notes.English));
        }

        [Test]
        public void TestParseWithoutMarkerSplitsInHalves()
        {
            var notes = BanchosucksReleaseNotes.Parse(unmarked);

            Assert.That(notes.German, Has.Count.EqualTo(1));
            Assert.That(notes.English, Has.Count.EqualTo(1));
            Assert.That(notes.German[0].Title, Is.EqualTo("Vorabversion 1.2.3: nicht installieren."));
            Assert.That(notes.German[0].Entries.Single().Text, Is.EqualTo("Nur ein Test."));
            Assert.That(notes.German[0].Entries.Single().Bullet, Is.False);
            Assert.That(notes.English[0].Title, Is.EqualTo("Pre-release 1.2.3: do not install."));
        }

        [Test]
        public void TestParseSingleSectionServesBothLanguages()
        {
            var notes = BanchosucksReleaseNotes.Parse("**Nur ein Block**\n- Punkt\n");

            Assert.That(notes.German, Has.Count.EqualTo(1));
            Assert.That(notes.English, Is.SameAs(notes.German));
        }

        [Test]
        public void TestParseTextBeforeFirstTitleAndEmptyInput()
        {
            var notes = BanchosucksReleaseNotes.Parse("ohne Titel\n- Punkt\n");

            Assert.That(notes.German.Single().Title, Is.Empty);
            Assert.That(notes.German.Single().Entries, Has.Count.EqualTo(2));

            Assert.That(BanchosucksReleaseNotes.Parse(string.Empty).IsEmpty);
            Assert.That(BanchosucksReleaseNotes.Parse("\n \n").IsEmpty);
        }

        [Test]
        public void TestEmbeddedNotes()
        {
            string[] versions = BanchosucksReleaseNotes.EmbeddedVersions().ToArray();

            Assert.That(versions, Does.Contain("2026.913.17"), "release-notes/*.md are embedded in osu.Game");

            foreach (string version in versions)
            {
                var notes = BanchosucksReleaseNotes.LoadEmbedded(version);

                Assert.That(notes, Is.Not.Null, version);
                Assert.That(notes!.German, Is.Not.Empty, version);
                Assert.That(notes.English, Is.Not.Empty, version);
                Assert.That(notes.German, Is.Not.SameAs(notes.English), $"{version}: both languages found");
                Assert.That(notes.English.All(s => !s.Title.Contains("(English)")), version);
                Assert.That(notes.German.Concat(notes.English).All(s => s.Entries.Count > 0), $"{version}: no section without text");
            }

            var known = BanchosucksReleaseNotes.LoadEmbedded("2026.913.17-banchosucks")!;
            Assert.That(known.German.Single().Title, Is.EqualTo("Mania: Sortieren und Gruppieren nach Schwierigkeit"));
            Assert.That(known.English.Single().Title, Is.EqualTo("Mania: sorting and grouping by difficulty"));
            Assert.That(known.German.Single().Entries, Has.Count.EqualTo(3));
        }

        [Test]
        public void TestNoNotesForUnknownVersion()
        {
            Assert.That(BanchosucksReleaseNotes.LoadEmbedded("1999.101.0-banchosucks"), Is.Null);
            Assert.That(BanchosucksReleaseNotes.LoadEmbedded("test game"), Is.Null);
            Assert.That(BanchosucksReleaseNotes.LoadEmbedded(null), Is.Null);
        }
    }
}
