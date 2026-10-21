// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Extensions;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Difficulty.Banchosucks;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Skills;
using osu.Game.Rulesets.Mania.MathUtils;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty
{
    public class ManiaDifficultyCalculator : DifficultyCalculator
    {
        private const double difficulty_multiplier = 0.018;

        private readonly bool isForCurrentRuleset;

        // Banchosucks: bumped for our own rating (ManiaRating); the client recalculates every stored mania star rating once.
        public override int Version => 20261021;

        private ManiaRating.Result? banchosucksResult;
        private ManiaRatingProgress? banchosucksProgress;
        private double banchosucksRate;

        public ManiaDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
            isForCurrentRuleset = beatmap.BeatmapInfo.Ruleset.MatchesOnlineID(ruleset);
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills)
        {
            if (beatmap.HitObjects.Count == 0)
                return new ManiaDifficultyAttributes { Mods = mods };

            // the full playable beatmap: a timed calculation passes a wrapper here that is no ManiaBeatmap
            int totalColumns = ((ManiaBeatmap)Beatmap).TotalColumns;
            double starRating;
            int noteCount = 0;

            if (isForCurrentRuleset && totalColumns <= ManiaRating.MAX_KEYS)
            {
                // Banchosucks: mania-specific maps get the server's rating (ManiaRating). The map's own OD is used, not the
                // mod-adjusted one, and the rate is rounded like the server rounds it. A timed calculation hands partial
                // beatmaps in here thousands of times; those are served from the progressive aggregation of one full run.
                double rate = Math.Round(ModUtils.CalculateRateWithMods(mods), 2);

                if (banchosucksResult == null || banchosucksRate != rate)
                {
                    banchosucksResult = ManiaRating.Calculate(banchosucksNotes(Beatmap), totalColumns, WorkingBeatmap.BeatmapInfo.Difficulty.OverallDifficulty, rate);
                    banchosucksProgress = null;
                    banchosucksRate = rate;
                }

                noteCount = banchosucksResult.NoteCount;

                if (beatmap.HitObjects.Count >= Beatmap.HitObjects.Count)
                    starRating = banchosucksResult.StarRating;
                else
                {
                    banchosucksProgress ??= new ManiaRatingProgress(banchosucksResult);
                    double lastHead = Math.Truncate(beatmap.HitObjects[^1].StartTime);
                    banchosucksProgress.Advance(rate == 1.0 ? lastHead : Math.Floor(lastHead / rate));
                    starRating = banchosucksProgress.StarRating();
                }
            }
            else
                starRating = skills.OfType<Strain>().Single().DifficultyValue() * difficulty_multiplier;

            ManiaDifficultyAttributes attributes = new ManiaDifficultyAttributes
            {
                StarRating = starRating,
                Mods = mods,
                MaxCombo = beatmap.HitObjects.Sum(maxComboForObject),
                NoteCount = noteCount,
            };

            return attributes;
        }

        /// <summary>
        /// The notes of a converted mania beatmap for <see cref="ManiaRating"/>: times truncated to whole milliseconds like
        /// the server's parser reads them from the file.
        /// </summary>
        private static List<ManiaRating.Note> banchosucksNotes(IBeatmap beatmap)
        {
            var notes = new List<ManiaRating.Note>(beatmap.HitObjects.Count);

            foreach (var hitObject in beatmap.HitObjects.OfType<ManiaHitObject>())
            {
                int head = (int)Math.Truncate(hitObject.StartTime);
                int tail = hitObject is HoldNote hold ? (int)Math.Truncate(hold.EndTime) : -1;
                notes.Add(new ManiaRating.Note(hitObject.Column, head, tail));
            }

            return notes;
        }

        private static int maxComboForObject(HitObject hitObject)
        {
            if (hitObject is HoldNote hold)
                return 1 + (int)((hold.EndTime - hold.StartTime) / 100);

            return 1;
        }

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, Mod[] mods)
        {
            var sortedObjects = beatmap.HitObjects.ToArray();
            int totalColumns = ((ManiaBeatmap)beatmap).TotalColumns;

            double clockRate = ModUtils.CalculateRateWithMods(mods);

            LegacySortHelper<HitObject>.Sort(sortedObjects, Comparer<HitObject>.Create((a, b) => (int)Math.Round(a.StartTime) - (int)Math.Round(b.StartTime)));

            List<DifficultyHitObject> objects = new List<DifficultyHitObject>(beatmap.HitObjects.Count);
            List<DifficultyHitObject>[] perColumnObjects = new List<DifficultyHitObject>[totalColumns];

            for (int column = 0; column < totalColumns; column++)
                perColumnObjects[column] = new List<DifficultyHitObject>();

            for (int i = 1; i < sortedObjects.Length; i++)
            {
                var currentObject = new ManiaDifficultyHitObject(sortedObjects[i], sortedObjects[i - 1], clockRate, objects, perColumnObjects, objects.Count);
                objects.Add(currentObject);
                perColumnObjects[currentObject.Column].Add(currentObject);
            }

            return objects;
        }

        // Sorting is done in CreateDifficultyHitObjects, since the full list of hitobjects is required.
        protected override IEnumerable<DifficultyHitObject> SortObjects(IEnumerable<DifficultyHitObject> input) => input;

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods) => new Skill[]
        {
            new Strain(mods, ((ManiaBeatmap)Beatmap).TotalColumns)
        };

        protected override Mod[] DifficultyAdjustmentMods
        {
            get
            {
                var mods = new Mod[]
                {
                    new ManiaModDoubleTime(),
                    new ManiaModHalfTime(),
                    new ManiaModEasy(),
                    new ManiaModHardRock(),
                };

                if (isForCurrentRuleset)
                    return mods;

                // if we are a convert, we can be played in any key mod.
                return mods.Concat(new Mod[]
                {
                    new ManiaModKey1(),
                    new ManiaModKey2(),
                    new ManiaModKey3(),
                    new ManiaModKey4(),
                    new ManiaModKey5(),
                    new MultiMod(new ManiaModKey5(), new ManiaModDualStages()),
                    new ManiaModKey6(),
                    new MultiMod(new ManiaModKey6(), new ManiaModDualStages()),
                    new ManiaModKey7(),
                    new MultiMod(new ManiaModKey7(), new ManiaModDualStages()),
                    new ManiaModKey8(),
                    new MultiMod(new ManiaModKey8(), new ManiaModDualStages()),
                    new ManiaModKey9(),
                    new MultiMod(new ManiaModKey9(), new ManiaModDualStages()),
                }).ToArray();
            }
        }
    }
}
