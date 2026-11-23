// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Models;

namespace osu.Game.Beatmaps
{
    /// <summary>
    /// Banchosucks: reads what <see cref="BanchosucksLibraryCleanup"/> needs to know about the local library:
    /// the sets, their difficulties, their local scores, and whether the .osu and audio files are really in the file store.
    /// Read-only, and only local data.
    /// </summary>
    public static class BanchosucksLibraryScanner
    {
        // the same marker as in WorkingBeatmapCache.
        private const string virtual_track_filename = @"virtual";

        /// <summary>
        /// Scans every beatmap set that is not deleted and not protected. Meant for a background thread.
        /// </summary>
        /// <param name="realm">The database.</param>
        /// <param name="storage">The game's storage (the folder that contains <c>files</c>).</param>
        /// <param name="progress">Called with (sets done, sets in total) after every set.</param>
        /// <param name="cancellationToken">Stops the scan with an <see cref="OperationCanceledException"/>.</param>
        public static List<BanchosucksLibraryCleanup.Set> Scan(RealmAccess realm, Storage storage, Action<int, int>? progress = null, CancellationToken cancellationToken = default)
        {
            var files = storage.GetStorageForDirectory(@"files");

            // many sets share files (and one set uses its audio for every difficulty), so every file is looked up on disk once.
            var existsCache = new Dictionary<string, bool>();

            bool exists(RealmFile file)
            {
                if (!existsCache.TryGetValue(file.Hash, out bool result))
                    existsCache[file.Hash] = result = files.Exists(file.GetStoragePath());

                return result;
            }

            return realm.Run(r =>
            {
                var sets = r.All<BeatmapSetInfo>().Where(s => !s.DeletePending && !s.Protected);
                int total = sets.Count();

                var result = new List<BanchosucksLibraryCleanup.Set>(total);

                foreach (var set in sets)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var difficulties = new List<BanchosucksLibraryCleanup.Difficulty>(set.Beatmaps.Count);
                    int scores = 0;

                    foreach (var beatmap in set.Beatmaps)
                    {
                        var beatmapFile = beatmap.File;
                        bool beatmapFileMissing = beatmapFile == null || !exists(beatmapFile.File);

                        string audioName = beatmap.Metadata.AudioFile;
                        bool audioMissing = false;

                        // no audio file, or "virtual", is a map that is meant to be silent (the game plays it with a virtual track).
                        if (!string.IsNullOrEmpty(audioName) && audioName != virtual_track_filename)
                        {
                            // the first match, like the game does when it loads the track (file names are compared without case).
                            var audioFile = set.Files.FirstOrDefault(f => string.Equals(f.Filename, audioName, StringComparison.OrdinalIgnoreCase));
                            audioMissing = audioFile == null || !exists(audioFile.File);
                        }

                        difficulties.Add(new BanchosucksLibraryCleanup.Difficulty(beatmap.Hash, beatmapFileMissing, audioMissing));
                        scores += beatmap.Scores.Count(s => !s.DeletePending);
                    }

                    result.Add(new BanchosucksLibraryCleanup.Set(set.ID, set.ToString(), set.OnlineID, set.DateAdded, scores, difficulties));

                    progress?.Invoke(result.Count, total);
                }

                return result;
            });
        }
    }
}
