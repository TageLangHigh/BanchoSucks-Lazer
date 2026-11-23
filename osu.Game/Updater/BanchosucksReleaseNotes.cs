// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace osu.Game.Updater
{
    /// <summary>
    /// Banchosucks: the release notes of one version (<c>release-notes/&lt;version&gt;.md</c> in the repository, embedded
    /// into this assembly at build time), parsed for the "what's new" dialog. No drawables, no settings access.
    /// </summary>
    /// <remarks>
    /// Format of the files: the German block first, then the English block. A block is one or more sections; a section
    /// is a line in bold (<c>**Title**</c>) followed by bullets (<c>- text</c>) or plain paragraphs. The titles of the
    /// English block end with "(English)"; where that marker is missing and the number of sections is even, the second
    /// half is taken as English.
    /// </remarks>
    public sealed class BanchosucksReleaseNotes
    {
        /// <summary>
        /// Prefix of the embedded resources, see <c>osu.Game.csproj</c>.
        /// </summary>
        public const string RESOURCE_PREFIX = "banchosucks-release-notes/";

        private const string english_marker = "(English)";

        public readonly IReadOnlyList<Section> German;
        public readonly IReadOnlyList<Section> English;

        private BanchosucksReleaseNotes(IReadOnlyList<Section> german, IReadOnlyList<Section> english)
        {
            German = german;
            English = english;
        }

        public IReadOnlyList<Section> For(bool german) => german ? German : English;

        /// <summary>
        /// Whether there is anything to show.
        /// </summary>
        public bool IsEmpty => German.Count == 0 && English.Count == 0;

        /// <summary>
        /// "2026.913.17-banchosucks" becomes "2026.913.17". Anything that is not a release version (local builds, tests) becomes an empty string.
        /// </summary>
        public static string NormaliseVersion(string? version)
        {
            if (string.IsNullOrEmpty(version))
                return string.Empty;

            var match = Regex.Match(version, @"^\s*v?(\d+\.\d+\.\d+)(?:$|[-+ .])");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        /// <summary>
        /// Whether the notes of the running version are due.
        /// </summary>
        /// <param name="previousVersion">The version stored by the previous start (<c>OsuSetting.Version</c>); empty on the first start of a fresh install.</param>
        /// <param name="shownVersion">The version whose notes were last shown or skipped (<c>OsuSetting.BanchosucksWhatsNewShownVersion</c>).</param>
        /// <param name="currentVersion">The running version.</param>
        public static bool ShouldShow(string? previousVersion, string? shownVersion, string? currentVersion)
        {
            string current = NormaliseVersion(currentVersion);

            // not a release build
            if (current.Length == 0)
                return false;

            // a fresh install has nothing "new" to tell
            if (string.IsNullOrEmpty(previousVersion))
                return false;

            return shownVersion != current;
        }

        public static BanchosucksReleaseNotes Parse(string markdown)
        {
            var sections = new List<Section>();
            List<Entry>? entries = null;

            foreach (string rawLine in markdown.Replace("\r", string.Empty).Split('\n'))
            {
                string line = rawLine.Trim();

                if (line.Length == 0)
                    continue;

                if (line.Length > 4 && line.StartsWith("**", StringComparison.Ordinal) && line.EndsWith("**", StringComparison.Ordinal))
                {
                    entries = new List<Entry>();
                    sections.Add(new Section(cleanInline(line.Substring(2, line.Length - 4)), entries));
                    continue;
                }

                if (entries == null)
                {
                    // text before the first title
                    entries = new List<Entry>();
                    sections.Add(new Section(string.Empty, entries));
                }

                bool bullet = line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal);

                entries.Add(new Entry(bullet, cleanInline(bullet ? line.Substring(2) : line)));
            }

            int firstEnglish = sections.FindIndex(s => s.Title.Contains(english_marker, StringComparison.OrdinalIgnoreCase));

            if (firstEnglish < 0)
            {
                // no marker: "German, then English" still holds when the halves match up
                firstEnglish = sections.Count >= 2 && sections.Count % 2 == 0 ? sections.Count / 2 : -1;
            }

            if (firstEnglish < 0)
                return new BanchosucksReleaseNotes(sections, sections);

            var german = sections.Take(firstEnglish).ToList();
            var english = sections.Skip(firstEnglish).Select(s => new Section(removeMarker(s.Title), s.Entries)).ToList();

            if (german.Count == 0)
                german = english;

            return new BanchosucksReleaseNotes(german, english);
        }

        /// <summary>
        /// Reads the embedded notes of a version.
        /// </summary>
        /// <param name="version">A version, with or without suffix.</param>
        /// <param name="assembly">The assembly holding the resources; this one by default.</param>
        /// <returns>The notes, or null when there is no file for that version or it has no content.</returns>
        public static BanchosucksReleaseNotes? LoadEmbedded(string? version, Assembly? assembly = null)
        {
            string normalised = NormaliseVersion(version);

            if (normalised.Length == 0)
                return null;

            assembly ??= typeof(BanchosucksReleaseNotes).Assembly;

            using (var stream = assembly.GetManifestResourceStream(RESOURCE_PREFIX + normalised + ".md"))
            {
                if (stream == null)
                    return null;

                using (var reader = new StreamReader(stream))
                {
                    var notes = Parse(reader.ReadToEnd());
                    return notes.IsEmpty ? null : notes;
                }
            }
        }

        /// <summary>
        /// The versions notes are embedded for.
        /// </summary>
        public static IEnumerable<string> EmbeddedVersions(Assembly? assembly = null)
        {
            assembly ??= typeof(BanchosucksReleaseNotes).Assembly;

            return assembly.GetManifestResourceNames()
                           .Where(n => n.StartsWith(RESOURCE_PREFIX, StringComparison.Ordinal) && n.EndsWith(".md", StringComparison.Ordinal))
                           .Select(n => n.Substring(RESOURCE_PREFIX.Length, n.Length - RESOURCE_PREFIX.Length - 3));
        }

        private static string removeMarker(string title)
        {
            int index = title.IndexOf(english_marker, StringComparison.OrdinalIgnoreCase);
            return index < 0 ? title : title.Remove(index, english_marker.Length).Trim();
        }

        // the dialog draws plain text: bold and code markers inside a line are dropped
        private static string cleanInline(string text) => text.Replace("**", string.Empty).Replace("`", string.Empty).Trim();

        public sealed class Section
        {
            /// <summary>
            /// The bold line; empty for text before the first title.
            /// </summary>
            public readonly string Title;

            public readonly IReadOnlyList<Entry> Entries;

            public Section(string title, IReadOnlyList<Entry> entries)
            {
                Title = title;
                Entries = entries;
            }
        }

        public readonly struct Entry
        {
            /// <summary>
            /// A list item; otherwise a plain paragraph.
            /// </summary>
            public readonly bool Bullet;

            public readonly string Text;

            public Entry(bool bullet, string text)
            {
                Bullet = bullet;
                Text = text;
            }
        }
    }
}
