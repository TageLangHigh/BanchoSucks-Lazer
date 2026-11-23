// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Scoring.Legacy;
using osu.Game.Screens;
using osu.Game.Screens.Banchosucks.Clicker;

namespace osu.Game.Scoring
{
    /// <summary>
    /// Banchosucks: replay comparison. The player marks one score on a leaderboard and then picks a second one on
    /// the same beatmap: the first replay is watched as usual, the second one is drawn over it as a ghost cursor.
    /// </summary>
    /// <remarks>
    /// osu!standard only. Whether two scores fit on one playfield is decided by
    /// <see cref="BanchosucksReplayComparison"/>; scores whose replay is only on the server are downloaded with the
    /// client's normal score download first. The ghost is handed to <see cref="OsuGame.PresentScore"/>, which
    /// passes it on to the replay player; the osu! ruleset draws it.
    /// </remarks>
    public partial class BanchosucksReplayCompare : Component
    {
        /// <summary>
        /// How long a replay download may take before the comparison is given up, in milliseconds.
        /// </summary>
        private const double download_timeout = 45_000;

        private const double download_poll_interval = 250;

        [Resolved]
        private ScoreManager scoreManager { get; set; } = null!;

        [Resolved]
        private ScoreModelDownloader? downloader { get; set; }

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        [Resolved]
        private OsuGame? game { get; set; }

        /// <summary>
        /// The score marked for comparison: the one that will be watched.
        /// </summary>
        public ScoreInfo? First { get; private set; }

        /// <summary>
        /// The ghost score of the comparison started last (for tests).
        /// </summary>
        public Score? LastGhost { get; private set; }

        private ScheduledDelegate? waiting;

        /// <summary>
        /// Whether a score can take part in a comparison at all: osu!standard, with a replay stored locally or on the server.
        /// </summary>
        public static bool CanCompare(ScoreInfo score) =>
            score.Ruleset.ShortName == BanchosucksReplayComparison.SUPPORTED_RULESET && (score.Files.Count > 0 || (score.OnlineID > 0 && score.HasOnlineReplay));

        /// <summary>
        /// The context menu entries for a score on a leaderboard. Empty for scores that cannot be compared.
        /// </summary>
        public MenuItem[] CreateMenuItems(ScoreInfo score)
        {
            if (!CanCompare(score))
                return Array.Empty<MenuItem>();

            var items = new List<MenuItem>();

            bool isFirst = First != null && sameScore(First, score);

            if (First != null && !isFirst && sameBeatmap(First, score))
            {
                var watched = First;

                items.Add(new OsuMenuItem(
                    ClickerStrings.Text("Compare with {0} (as ghost)", "Mit {0} vergleichen (als Geist)", watched.User.Username),
                    MenuItemType.Highlighted,
                    () => Compare(watched, score)));
            }

            if (isFirst)
                items.Add(new OsuMenuItem(ClickerStrings.Text("Unmark for comparison", "Vormerkung für Vergleich aufheben"), MenuItemType.Standard, Clear));
            else
                items.Add(new OsuMenuItem(ClickerStrings.Text("Mark for comparison", "Für Vergleich vormerken"), MenuItemType.Standard, () => Mark(score)));

            return items.ToArray();
        }

        /// <summary>
        /// Marks the score that will be watched in the comparison.
        /// </summary>
        public void Mark(ScoreInfo score)
        {
            First = score;

            notifications?.Post(new SimpleNotification
            {
                Text = ClickerStrings.Text(
                    "Marked the score of {0}. Now right-click a second score on this beatmap and choose \"Compare\": its replay is shown as a ghost.",
                    "Score von {0} vorgemerkt. Jetzt einen zweiten Score auf dieser Map rechtsklicken und „Vergleichen“ wählen: sein Replay läuft als Geist mit.",
                    score.User.Username),
                Icon = FontAwesome.Solid.UserFriends,
            });
        }

        public void Clear() => First = null;

        /// <summary>
        /// Starts the comparison, or tells the player why it is not possible.
        /// </summary>
        /// <param name="watched">The score whose replay is played normally.</param>
        /// <param name="ghost">The score shown as a ghost cursor.</param>
        /// <returns>What the check of the two scores said.</returns>
        public BanchosucksReplayComparison.Result Compare(ScoreInfo watched, ScoreInfo ghost)
        {
            var result = BanchosucksReplayComparison.Check(watched, ghost);

            if (!result.Compatible)
            {
                refuse(Describe(result));
                return result;
            }

            waiting?.Cancel();

            whenLocal(watched, () => whenLocal(ghost, () => start(watched, ghost)));
            return result;
        }

        /// <summary>
        /// Runs an action once the score's replay is on this PC, downloading it when it is only on the server.
        /// </summary>
        private void whenLocal(ScoreInfo score, Action then)
        {
            if (isLocal(score))
            {
                then();
                return;
            }

            if (downloader == null || score.OnlineID <= 0 || !score.HasOnlineReplay)
            {
                refuse(ClickerStrings.Text("The replay of {0} is not available.", "Das Replay von {0} ist nicht verfügbar.", score.User.Username));
                return;
            }

            downloader.Download(score);

            double waited = 0;

            waiting = Scheduler.AddDelayed(() =>
            {
                waited += download_poll_interval;

                if (isLocal(score))
                {
                    waiting?.Cancel();
                    then();
                }
                else if (waited >= download_timeout)
                {
                    waiting?.Cancel();
                    refuse(ClickerStrings.Text("The replay of {0} could not be downloaded.", "Das Replay von {0} konnte nicht heruntergeladen werden.", score.User.Username));
                }
            }, download_poll_interval, true);
        }

        private bool isLocal(ScoreInfo score)
        {
            if (score.Files.Count > 0)
                return true;

            long onlineId = score.OnlineID;
            string hash = score.Hash;

            if (onlineId > 0 && scoreManager.Query(s => s.OnlineID == onlineId && !s.DeletePending) != null)
                return true;

            return !string.IsNullOrEmpty(hash) && scoreManager.Query(s => s.Hash == hash && !s.DeletePending) != null;
        }

        private void start(ScoreInfo watched, ScoreInfo ghost)
        {
            Score? watchedScore;
            Score? ghostScore;

            try
            {
                watchedScore = scoreManager.GetScore(watched);
                ghostScore = scoreManager.GetScore(ghost);
            }
            catch (LegacyScoreDecoder.BeatmapNotFoundException)
            {
                refuse(ClickerStrings.Text("The beatmap of these scores is not on this PC.", "Die Map dieser Scores ist nicht auf diesem PC."));
                return;
            }
            catch (Exception e)
            {
                Logger.Error(e, "Banchosucks: reading the replays for the comparison failed");
                refuse(ClickerStrings.Text("The replays could not be read.", "Die Replays konnten nicht gelesen werden."));
                return;
            }

            if (watchedScore?.Replay == null || ghostScore?.Replay == null || ghostScore.Replay.Frames.Count == 0)
            {
                refuse(ClickerStrings.Text("One of the scores has no replay data.", "Einer der Scores hat keine Replay-Daten."));
                return;
            }

            // once more with what is really stored: a replay file knows its beatmap and mods best
            var result = BanchosucksReplayComparison.Check(watchedScore.ScoreInfo, ghostScore.ScoreInfo);

            if (!result.Compatible)
            {
                refuse(Describe(result));
                return;
            }

            LastGhost = ghostScore;
            First = null;

            game?.PresentScore(watchedScore.ScoreInfo, ScorePresentType.Gameplay, banchosucksGhostScore: ghostScore);
        }

        private void refuse(LocalisableString reason) => notifications?.Post(new SimpleNotification
        {
            Text = reason,
            Icon = FontAwesome.Solid.ExclamationTriangle,
        });

        /// <summary>
        /// The message for a comparison that is not possible.
        /// </summary>
        public static LocalisableString Describe(BanchosucksReplayComparison.Result result)
        {
            string mod = result.Acronym ?? "?";

            switch (result.Verdict)
            {
                case BanchosucksReplayComparison.Verdict.Compatible:
                    return ClickerStrings.Text("These replays can be compared.", "Diese Replays lassen sich vergleichen.");

                case BanchosucksReplayComparison.Verdict.UnsupportedRuleset:
                    return ClickerStrings.Text("Replays can only be compared in osu!standard.", "Replays lassen sich nur in osu!standard vergleichen.");

                case BanchosucksReplayComparison.Verdict.DifferentBeatmap:
                    return ClickerStrings.Text(
                        "These scores were not set on the same beatmap (or not on the same version of it), so they cannot be compared.",
                        "Diese Scores stammen nicht von derselben Map (oder nicht von derselben Version), ein Vergleich ist nicht möglich.");

                case BanchosucksReplayComparison.Verdict.ModInOneScoreOnly:
                    return ClickerStrings.Text(
                        "Cannot compare: only one of the scores uses {0}. That mod changes the playfield, so the two replays would not fit on one map.",
                        "Vergleich nicht möglich: Nur einer der Scores nutzt {0}. Der Mod verändert das Spielfeld, die beiden Replays passen nicht auf eine Map.",
                        mod);

                case BanchosucksReplayComparison.Verdict.ModSettingsDiffer:
                    return ClickerStrings.Text(
                        "Cannot compare: both scores use {0}, but with different settings, so their playfields differ.",
                        "Vergleich nicht möglich: Beide Scores nutzen {0}, aber mit unterschiedlichen Einstellungen, die Spielfelder unterscheiden sich.",
                        mod);

                case BanchosucksReplayComparison.Verdict.RateDiffers:
                    return ClickerStrings.Text(
                        "Cannot compare: the scores were played at different speeds ({0}).",
                        "Vergleich nicht möglich: Die Scores wurden mit unterschiedlicher Geschwindigkeit gespielt ({0}).",
                        mod);

                case BanchosucksReplayComparison.Verdict.UnpredictableMod:
                    return ClickerStrings.Text(
                        "Cannot compare: with {0} the map depends on the play itself, so no two plays see the same playfield.",
                        "Vergleich nicht möglich: Mit {0} hängt die Map vom Spielverlauf ab, zwei Plays sehen nie dasselbe Spielfeld.",
                        mod);

                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result.Verdict, null);
            }
        }

        private static bool sameScore(ScoreInfo a, ScoreInfo b)
        {
            if (ReferenceEquals(a, b))
                return true;

            if (a.OnlineID > 0 && a.OnlineID == b.OnlineID)
                return true;

            if (!string.IsNullOrEmpty(a.Hash) && a.Hash == b.Hash)
                return true;

            return a.ID != Guid.Empty && a.ID == b.ID;
        }

        private static bool sameBeatmap(ScoreInfo a, ScoreInfo b)
        {
            var first = a.BeatmapInfo;
            var second = b.BeatmapInfo;

            if (first == null || second == null)
                return false;

            if (!string.IsNullOrEmpty(first.MD5Hash) && first.MD5Hash == second.MD5Hash)
                return true;

            return first.OnlineID > 0 && first.OnlineID == second.OnlineID;
        }
    }
}
