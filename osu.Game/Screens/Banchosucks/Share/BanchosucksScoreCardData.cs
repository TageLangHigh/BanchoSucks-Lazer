// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.Localisation;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Utils;

namespace osu.Game.Screens.Banchosucks.Share
{
    /// <summary>
    /// Banchosucks: the fixed words on the share cards, in the player's language.
    /// </summary>
    public class BanchosucksCardLabels
    {
        public string Accuracy { get; private init; } = "ACCURACY";
        public string Combo { get; private init; } = "COMBO";
        public string Score { get; private init; } = "SCORE";
        public string PlayedBy { get; private init; } = "played by";
        public string MappedBy { get; private init; } = "mapped by";
        public string GlobalRank { get; private init; } = "GLOBAL";
        public string CountryRank { get; private init; } = "COUNTRY";
        public string PlayCount { get; private init; } = "PLAYS";
        public string Level { get; private init; } = "LEVEL";
        public string BestPlay { get; private init; } = "BEST PLAY";

        public static readonly BanchosucksCardLabels ENGLISH = new BanchosucksCardLabels();

        public static readonly BanchosucksCardLabels GERMAN = new BanchosucksCardLabels
        {
            Accuracy = "GENAUIGKEIT",
            Combo = "COMBO",
            Score = "PUNKTE",
            PlayedBy = "gespielt von",
            MappedBy = "gemappt von",
            GlobalRank = "WELTWEIT",
            CountryRank = "LAND",
            PlayCount = "SPIELE",
            Level = "LEVEL",
            BestPlay = "BESTER SCORE",
        };

        public static BanchosucksCardLabels For(bool german) => german ? GERMAN : ENGLISH;

        /// <summary>
        /// Numbers and dates as the language writes them (1,234.5 / 1.234,5).
        /// </summary>
        public static CultureInfo CultureFor(bool german) => CultureInfo.GetCultureInfo(german ? "de-DE" : "en-US");
    }

    /// <summary>
    /// Banchosucks: everything the score card shows, worked out from a score. The drawable
    /// (<see cref="BanchosucksScoreCard"/>) only lays these texts out.
    /// </summary>
    public class BanchosucksScoreCardData
    {
        public string Title { get; private init; } = string.Empty;
        public string TitleUnicode { get; private init; } = string.Empty;
        public string Artist { get; private init; } = string.Empty;
        public string ArtistUnicode { get; private init; } = string.Empty;
        public string Difficulty { get; private init; } = string.Empty;
        public string Mapper { get; private init; } = string.Empty;

        /// <summary>
        /// Star rating with the score's mods ("5.21"), empty when it is not known.
        /// </summary>
        public string StarText { get; private init; } = string.Empty;

        public IReadOnlyList<ModEntry> Mods { get; private init; } = Array.Empty<ModEntry>();

        public ScoreRank Rank { get; private init; }

        /// <summary>
        /// "98.76%", never rounded up (as everywhere in the game).
        /// </summary>
        public string AccuracyText { get; private init; } = string.Empty;

        /// <summary>
        /// "1,234x".
        /// </summary>
        public string ComboText { get; private init; } = string.Empty;

        /// <summary>
        /// "/ 1,500x" when the highest possible combo is known, empty otherwise.
        /// </summary>
        public string MaxComboText { get; private init; } = string.Empty;

        /// <summary>
        /// "321", null when no pp value is known for the score.
        /// </summary>
        public string? PpText { get; private init; }

        public string ScoreText { get; private init; } = string.Empty;

        public IReadOnlyList<HitEntry> Hits { get; private init; } = Array.Empty<HitEntry>();

        public string Player { get; private init; } = string.Empty;

        public string DateText { get; private init; } = string.Empty;

        public string RulesetName { get; private init; } = string.Empty;

        public BanchosucksCardLabels Labels { get; private init; } = BanchosucksCardLabels.ENGLISH;

        /// <summary>
        /// A name for the image file: player, title and difficulty, without characters a file name cannot have.
        /// </summary>
        public string FileName { get; private init; } = string.Empty;

        /// <summary>
        /// One mod on the card: its acronym, the short extra text mods with settings have (the rate of DT, for example) and its type for the colour.
        /// </summary>
        public readonly record struct ModEntry(string Acronym, string Extra, ModType Type)
        {
            public string Text => string.IsNullOrEmpty(Extra) ? Acronym : $"{Acronym} {Extra}";
        }

        /// <summary>
        /// One hit count on the card: "1,500" for judgements, "80/84" for slider ticks and ends (hit out of possible).
        /// </summary>
        public readonly record struct HitEntry(HitResult Result, LocalisableString Name, string Count);

        /// <summary>
        /// How many mods are shown one by one; a play with more gets "+N" for the rest.
        /// </summary>
        public const int MAX_MODS = 8;

        private static ModEntry[] limitMods(ModEntry[] mods)
        {
            if (mods.Length <= MAX_MODS)
                return mods;

            return mods.Take(MAX_MODS - 1).Append(new ModEntry($"+{mods.Length - (MAX_MODS - 1)}", string.Empty, ModType.System)).ToArray();
        }

        /// <param name="score">The score.</param>
        /// <param name="starRating">The star rating with the score's mods, if it could be worked out.</param>
        /// <param name="pp">A pp value calculated locally, used when the score itself carries none.</param>
        /// <param name="german">Whether labels, numbers and the date are written the German way.</param>
        public static BanchosucksScoreCardData From(ScoreInfo score, double? starRating = null, double? pp = null, bool german = false)
        {
            var culture = BanchosucksCardLabels.CultureFor(german);
            var beatmap = score.BeatmapInfo;
            var metadata = beatmap?.Metadata;

            string title = metadata?.Title ?? string.Empty;
            string artist = metadata?.Artist ?? string.Empty;
            string difficulty = beatmap?.DifficultyName ?? string.Empty;
            string player = score.User.Username;

            double? stars = starRating ?? (beatmap?.StarRating > 0 ? beatmap.StarRating : null);
            double? shownPp = score.PP ?? pp;

            int maxCombo = score.GetMaximumAchievableCombo();

            return new BanchosucksScoreCardData
            {
                Title = title,
                TitleUnicode = string.IsNullOrEmpty(metadata?.TitleUnicode) ? title : metadata.TitleUnicode,
                Artist = artist,
                ArtistUnicode = string.IsNullOrEmpty(metadata?.ArtistUnicode) ? artist : metadata.ArtistUnicode,
                Difficulty = difficulty,
                Mapper = metadata?.Author.Username ?? string.Empty,
                StarText = stars is double s ? s.FloorToDecimalDigits(2).ToString("0.00", culture) : string.Empty,
                Mods = limitMods(score.Mods.AsOrdered().Select(m => new ModEntry(m.Acronym, m.ExtendedIconInformation, m.Type)).ToArray()),
                Rank = score.Rank,
                AccuracyText = score.Accuracy.FloorToDecimalDigits(4).ToString("0.00%", culture).Replace(" ", string.Empty).Replace(" ", string.Empty),
                ComboText = $"{score.MaxCombo.ToString("N0", culture)}x",
                MaxComboText = maxCombo > 0 ? $"/ {maxCombo.ToString("N0", culture)}x" : string.Empty,
                PpText = shownPp is double p ? Math.Round(p, MidpointRounding.AwayFromZero).ToString("N0", culture) : null,
                ScoreText = score.TotalScore.ToString("N0", culture),
                // spinner bonus counts say little about a play and would crowd the row
                Hits = score.GetStatisticsForDisplay()
                            .Where(h => !h.Result.IsBonus())
                            .Select(h => new HitEntry(h.Result, h.DisplayName, h.MaxCount is int max
                                ? $"{h.Count.ToString("N0", culture)}/{max.ToString("N0", culture)}"
                                : h.Count.ToString("N0", culture)))
                            .ToArray(),
                Player = player,
                DateText = score.Date.ToLocalTime().ToString(german ? "dd.MM.yyyy HH:mm" : "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                RulesetName = score.Ruleset.Name,
                Labels = BanchosucksCardLabels.For(german),
                FileName = BanchosucksCardImage.SafeFileName($"{player} - {title} [{difficulty}]"),
            };
        }
    }
}
