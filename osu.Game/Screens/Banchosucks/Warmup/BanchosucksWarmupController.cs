// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Online.API;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Banchosucks.Clicker;
using osu.Game.Screens.Select;
using Realms;

namespace osu.Game.Screens.Banchosucks.Warmup
{
    /// <summary>
    /// Banchosucks: the warm-up button of solo song select. Reads the player's own local scores and the local library,
    /// lets <see cref="BanchosucksWarmupSelector"/> pick three maps a bit below the player's level and plays them one
    /// after another.
    /// </summary>
    /// <remarks>
    /// Everything happens on this PC: scores and maps come from the local database, star ratings from the stored values
    /// or the local difficulty calculation. Nothing is requested from or sent to the server for this.
    /// </remarks>
    public partial class BanchosucksWarmupController : Component
    {
        /// <summary>
        /// At most this many scores get their star rating calculated with mods; the newest ones win.
        /// Keeps the wait short for players whose scores nearly all carry rate or difficulty mods.
        /// </summary>
        private const int max_modded_lookups = 60;

        /// <summary>
        /// The star ratings with mods are given this long; what is not ready by then is left out.
        /// </summary>
        private const int lookup_timeout_ms = 20_000;

        private readonly BanchosucksWarmupSeries series;
        private readonly Func<BeatmapInfo, bool> startBeatmap;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> selectedMods { get; set; } = null!;

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        [Resolved]
        private IPerformFromScreenRunner? performer { get; set; }

        /// <summary>
        /// The random source of a draw; tests replace it with a seeded one.
        /// </summary>
        internal Func<Random> CreateRandom = () => new Random();

        /// <summary>
        /// Whether scores and library are being read right now.
        /// </summary>
        public bool Preparing { get; private set; }

        /// <summary>
        /// What the last press decided (for tests).
        /// </summary>
        internal BanchosucksWarmupPlan? LastPlan { get; private set; }

        /// <summary>
        /// The last text shown to the player as a notification (for tests).
        /// </summary>
        internal LocalisableString? LastMessage { get; private set; }

        internal BanchosucksWarmupSeries Series => series;

        private IScreen? owner;
        private bool continuing;
        private CancellationTokenSource? cancellation;

        /// <param name="series">The series shared with the results screen.</param>
        /// <param name="startBeatmap">Selects a difficulty in song select and starts it; returns whether a play was started.</param>
        public BanchosucksWarmupController(BanchosucksWarmupSeries series, Func<BeatmapInfo, bool> startBeatmap)
        {
            this.series = series;
            this.startBeatmap = startBeatmap;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            owner = Parent as IScreen;
            series.NextRequested += onNextRequested;
        }

        /// <summary>
        /// Builds a new series for the selected ruleset and starts its first map. Called from song select.
        /// </summary>
        public void Start()
        {
            if (Preparing)
                return;

            if (owner != null && !owner.IsCurrentScreen())
                return;

            series.End();

            RulesetInfo currentRuleset = ruleset.Value;
            string shortName = currentRuleset.ShortName;
            int userId = api.LocalUser.Value.Id;
            DateTimeOffset now = DateTimeOffset.Now;
            var database = realm;
            var cache = difficultyCache;

            Preparing = true;
            cancellation = new CancellationTokenSource();
            CancellationToken token = cancellation.Token;

            Task.Run(async () =>
            {
                var data = read(database, userId, shortName, now);
                var scores = await rateScores(data.Scores, cache, currentRuleset, token).ConfigureAwait(false);
                return (scores, data.Candidates);
            }, token).ContinueWith(task => Schedule(() =>
            {
                Preparing = false;

                if (token.IsCancellationRequested)
                    return;

                if (!task.IsCompletedSuccessfully)
                {
                    // the game is closing or the database is being moved; say so rather than start something half-read
                    Logger.Log($"Warm-up: scores and library could not be read ({task.Exception?.InnerException?.Message})");
                    post(ClickerStrings.Text("Warm-up: your scores could not be read. Please try again.",
                        "Aufwärmen: Deine Scores konnten nicht gelesen werden. Bitte versuch es noch einmal."));
                    return;
                }

                var (scores, candidates) = task.GetResultSafely();
                begin(BanchosucksWarmupSelector.Select(scores, candidates, now, CreateRandom()));
            }));
        }

        /// <summary>
        /// Called by song select whenever it becomes the current screen again: unless the player asked for the next map,
        /// coming back here means the series was left.
        /// </summary>
        public void OnSongSelectResumed()
        {
            if (!continuing)
                series.End();
        }

        /// <summary>
        /// Ends the series for good (song select is being left).
        /// </summary>
        public void Stop()
        {
            cancellation?.Cancel();
            continuing = false;
            series.End();
        }

        private void begin(BanchosucksWarmupPlan plan)
        {
            LastPlan = plan;

            if (owner != null && !owner.IsCurrentScreen())
                return;

            switch (plan.Failure)
            {
                case BanchosucksWarmupFailure.TooFewScores:
                    post(ClickerStrings.Text(
                        "Warm-up: too few recent plays to tell your level. It takes {0} different maps passed with at least {1} % accuracy in the last {2} weeks in this ruleset; found {3}.",
                        "Aufwärmen: Zu wenige aktuelle Plays, um dein Niveau zu bestimmen. Nötig sind {0} verschiedene Maps, die du in den letzten {2} Wochen in diesem Modus mit mindestens {1} % Genauigkeit bestanden hast; gefunden: {3}.",
                        BanchosucksWarmupSelector.MIN_SCORES, (int)Math.Round(BanchosucksWarmupSelector.MIN_ACCURACY * 100), BanchosucksWarmupSelector.RECENT_DAYS / 7, plan.ScoresUsed));
                    return;

                case BanchosucksWarmupFailure.TooFewCandidates:
                    if (plan.MissingStars is double missing)
                    {
                        post(ClickerStrings.Text(
                            "Warm-up: your level is about {0:0.0} stars, but your library has no fitting map around {1:0.0} stars (1 to 4 minutes long, not played today).",
                            "Aufwärmen: Dein Niveau liegt bei etwa {0:0.0} Sternen, aber in deiner Sammlung gibt es keine passende Map um {1:0.0} Sterne (1 bis 4 Minuten lang, heute noch nicht gespielt).",
                            plan.Level ?? 0, missing));
                    }
                    else
                    {
                        post(ClickerStrings.Text(
                            "Warm-up: your level is about {0:0.0} stars, but your library has no three fitting maps from different sets.",
                            "Aufwärmen: Dein Niveau liegt bei etwa {0:0.0} Sternen, aber in deiner Sammlung gibt es keine drei passenden Maps aus verschiedenen Sets.",
                            plan.Level ?? 0));
                    }

                    return;
            }

            var ids = plan.Picks.Select(p => p.BeatmapId).ToArray();
            var maps = realm.Run(r => ids.Select(id => r.Find<BeatmapInfo>(id)?.Detach()).ToArray());

            if (maps.Any(m => m == null))
            {
                // a set was deleted between the draw and now
                post(ClickerStrings.Text("Warm-up: one of the chosen maps is no longer in your library. Please try again.",
                    "Aufwärmen: Eine der gewählten Maps ist nicht mehr in deiner Sammlung. Bitte versuch es noch einmal."));
                return;
            }

            if (!StartSeries(maps!, plan.Level ?? 0))
            {
                post(ClickerStrings.Text("Warm-up: the first map could not be started.", "Aufwärmen: Die erste Map konnte nicht gestartet werden."));
                return;
            }

            post(ClickerStrings.Text(
                "Warm-up: three maps at {0:0.0}, {1:0.0} and {2:0.0} stars, a bit below your level of about {3:0.0}. The results screen leads on to the next one.",
                "Aufwärmen: drei Maps mit {0:0.0}, {1:0.0} und {2:0.0} Sternen, knapp unter deinem Niveau von etwa {3:0.0}. Vom Ergebnisbildschirm geht es zur nächsten.",
                plan.Picks[0].Stars, plan.Picks[1].Stars, plan.Picks[2].Stars, plan.Level ?? 0));

            // the maps are chosen by their star rating without mods; say so when the selected mods change it
            if (selectedMods.Value.Any(ChangesStarRating))
            {
                post(ClickerStrings.Text(
                    "Warm-up: the maps were chosen by their star rating without mods. Your selected mods change the difficulty.",
                    "Aufwärmen: Die Maps wurden nach ihren Sternen ohne Mods gewählt. Deine ausgewählten Mods verändern die Schwierigkeit."));
            }
        }

        /// <summary>
        /// Runs a series with the given maps and starts the first one. Returns false (and no series) when it could not be started.
        /// </summary>
        internal bool StartSeries(IReadOnlyList<BeatmapInfo> maps, double level)
        {
            series.Start(maps, level);

            if (startBeatmap(series.Current!))
                return true;

            series.End();
            return false;
        }

        private void onNextRequested()
        {
            if (!series.HasNext || continuing)
                return;

            continuing = true;

            if (performer != null)
            {
                performer.PerformFromScreen(screen =>
                {
                    if (ReferenceEquals(screen, owner))
                        continueSeries();
                    else
                    {
                        // ended up on another song select than the one the series belongs to
                        continuing = false;
                        series.End();
                    }
                }, new[] { typeof(SoloSongSelect) });
            }
            else if (owner != null)
            {
                // no game around the screen (tests): walk back ourselves
                owner.MakeCurrent();
                continueSeries();
            }
        }

        private void continueSeries()
        {
            continuing = false;

            var next = series.Advance();

            if (next == null)
                return;

            if (!startBeatmap(next))
            {
                series.End();
                post(ClickerStrings.Text("Warm-up: the next map could not be started, the series ends here.",
                    "Aufwärmen: Die nächste Map konnte nicht gestartet werden, die Serie endet hier."));
            }
        }

        private void post(LocalisableString text)
        {
            LastMessage = text;
            notifications?.Post(new SimpleNotification { Text = text });
        }

        /// <summary>
        /// Whether a mod can change the star rating, so the stored rating of the map does not describe the play.
        /// Mods that only change how a play fails or looks are left out; anything else is treated as changing it.
        /// </summary>
        public static bool ChangesStarRating(Mod mod)
        {
            switch (mod)
            {
                case ModNoFail:
                case ModFailCondition:
                case ModClassic:
                    return false;
            }

            switch (mod.Type)
            {
                case ModType.DifficultyIncrease:
                case ModType.DifficultyReduction:
                case ModType.Conversion:
                case ModType.Fun:
                    return true;

                default:
                    return false;
            }
        }

        private static (List<RawScore> Scores, List<BanchosucksWarmupCandidate> Candidates) read(RealmAccess realm, int userId, string shortName, DateTimeOffset now)
        {
            DateTimeOffset cutoff = now.AddDays(-BanchosucksWarmupSelector.RECENT_DAYS);

            return realm.Run(r =>
            {
                var scores = new List<RawScore>();

                var ownScores = r.GetAllLocalScoresForUser(userId)
                                 .Filter($@"{nameof(ScoreInfo.Ruleset)}.{nameof(RulesetInfo.ShortName)} == $0", shortName);

                foreach (var score in ownScores)
                {
                    // the date is compared here rather than in the query, like the activity log does
                    if (score.Date < cutoff || !score.Passed || score.Rank == ScoreRank.F || score.Accuracy < BanchosucksWarmupSelector.MIN_ACCURACY)
                        continue;

                    var beatmap = score.BeatmapInfo;

                    // converts have no stored star rating in the played ruleset and are left out
                    if (beatmap == null || beatmap.Ruleset.ShortName != shortName)
                        continue;

                    Mod[] mods;

                    try
                    {
                        mods = score.Mods;
                    }
                    catch (Exception)
                    {
                        // a mod this client does not know
                        continue;
                    }

                    // autoplay, relax and the like say nothing about the player's level, and what a mod this client
                    // does not know did to the map cannot be told
                    if (mods.Any(m => m.Type == ModType.Automation || m is UnknownMod))
                        continue;

                    bool modded = mods.Any(ChangesStarRating);

                    scores.Add(new RawScore(beatmap.ID, beatmap.StarRating, score.Accuracy, score.Date, modded ? beatmap.Detach() : null, modded ? mods : null));
                }

                var candidates = new List<BanchosucksWarmupCandidate>();

                var difficulties = r.All<BeatmapInfo>()
                                    .Filter($@"{nameof(BeatmapInfo.Ruleset)}.{nameof(RulesetInfo.ShortName)} == $0 && {nameof(BeatmapInfo.Hidden)} == false", shortName);

                foreach (var beatmap in difficulties)
                {
                    var set = beatmap.BeatmapSet;

                    if (set == null || set.DeletePending || set.Protected)
                        continue;

                    candidates.Add(new BanchosucksWarmupCandidate(beatmap.ID, set.ID, beatmap.StarRating, beatmap.Length, beatmap.LastPlayed));
                }

                return (scores, candidates);
            });
        }

        /// <summary>
        /// Gives every score the star rating it was played at: the stored one, or for scores with mods that change it the
        /// locally calculated one (newest scores first, bounded in number and time).
        /// </summary>
        private static async Task<List<BanchosucksWarmupScore>> rateScores(List<RawScore> raw, BeatmapDifficultyCache cache, RulesetInfo ruleset, CancellationToken token)
        {
            var rated = new List<BanchosucksWarmupScore>();
            var pending = new List<(RawScore Score, Task<StarDifficulty?> Task)>();

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);

            foreach (var score in raw.OrderByDescending(s => s.Date))
            {
                if (score.Beatmap == null || score.Mods == null)
                {
                    rated.Add(new BanchosucksWarmupScore(score.BeatmapId, score.StoredStars, score.Accuracy, true, score.Date));
                    continue;
                }

                if (pending.Count < max_modded_lookups)
                    pending.Add((score, cache.GetDifficultyAsync(score.Beatmap, ruleset, score.Mods, timeout.Token)));
            }

            if (pending.Count > 0)
            {
                Task all = Task.WhenAll(pending.Select(p => p.Task));

                try
                {
                    await Task.WhenAny(all, Task.Delay(lookup_timeout_ms, token)).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }

                timeout.Cancel();

                foreach (var (score, task) in pending)
                {
                    if (task.IsCompletedSuccessfully && task.GetResultSafely() is StarDifficulty difficulty)
                        rated.Add(new BanchosucksWarmupScore(score.BeatmapId, difficulty.Stars, score.Accuracy, true, score.Date));
                }
            }

            return rated;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            series.NextRequested -= onNextRequested;
            cancellation?.Cancel();
        }

        private readonly record struct RawScore(Guid BeatmapId, double StoredStars, double Accuracy, DateTimeOffset Date, BeatmapInfo? Beatmap, Mod[]? Mods);
    }
}
