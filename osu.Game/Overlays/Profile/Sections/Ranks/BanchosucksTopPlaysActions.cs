// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Banchosucks;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Notifications;
using osu.Game.Screens.Banchosucks.Clicker;
using osuTK;

namespace osu.Game.Overlays.Profile.Sections.Ranks
{
    /// <summary>
    /// Banchosucks: the two buttons above a player's best performances in the profile overlay: download the beatmap sets of the
    /// top plays that are not in the library, and put the top plays' difficulties into a local collection named after the player.
    /// </summary>
    /// <remarks>
    /// Both work on the plays the section has loaded; with "all 100" ticked the rest is fetched first (the standard
    /// <see cref="GetUserScoresRequest"/>, two requests of fifty). Nothing about the local player is sent: the downloads
    /// are the game's normal beatmap downloads, the collection is local.
    /// </remarks>
    public partial class BanchosucksTopPlaysActions : CompositeDrawable
    {
        private readonly IBindable<UserProfileData?> user = new Bindable<UserProfileData?>();
        private readonly Func<IReadOnlyList<SoloScoreInfo>> loadedScores;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        // absent in plain test scenes; the game always provides them
        [Resolved]
        private BanchosucksBeatmapDownloadQueue? downloadQueue { get; set; }

        [Resolved]
        private IDialogOverlay? dialogOverlay { get; set; }

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        private RoundedButton downloadButton = null!;
        private RoundedButton collectionButton = null!;
        private OsuCheckbox allCheckbox = null!;

        private readonly List<APIRequest> requests = new List<APIRequest>();
        private bool busy;

        /// <summary>Whether all top plays are to be used, not only the ones the section shows.</summary>
        public Bindable<bool> IncludeAll => allCheckbox.Current;

        /// <summary>The last message shown to the player (tests).</summary>
        public LocalisableString LastMessage { get; private set; }

        public BanchosucksTopPlaysActions(IBindable<UserProfileData?> user, Func<IReadOnlyList<SoloScoreInfo>> loadedScores)
        {
            this.user.BindTo(user);
            this.loadedScores = loadedScores;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Full,
                Spacing = new Vector2(10, 5),
                Margin = new MarginPadding { Bottom = 8 },
                Children = new Drawable[]
                {
                    downloadButton = new RoundedButton
                    {
                        Size = new Vector2(230, 30),
                        Text = ClickerStrings.Text("Download missing maps", "Fehlende Maps laden"),
                        TooltipText = ClickerStrings.Text("Downloads the beatmap sets of these top plays that are not in your library.",
                            "Lädt die Beatmap-Sets dieser Top-Plays, die nicht in deiner Bibliothek sind."),
                        Action = () => withPlays(confirmDownload),
                    },
                    collectionButton = new RoundedButton
                    {
                        Size = new Vector2(230, 30),
                        Text = ClickerStrings.Text("Top plays as collection", "Top-Plays als Sammlung"),
                        TooltipText = ClickerStrings.Text("Creates or extends a local collection with the difficulties of these top plays, also the ones you have not downloaded yet.",
                            "Legt eine lokale Sammlung mit den Schwierigkeiten dieser Top-Plays an oder ergänzt sie, auch um noch nicht geladene."),
                        Action = () => withPlays(saveCollection),
                    },
                    new Container
                    {
                        Size = new Vector2(340, 30),
                        Child = allCheckbox = new OsuCheckbox(nubOnRight: false)
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            // off: only the plays listed below; on: the rest is fetched first
                            LabelText = ClickerStrings.Text("all 100 top plays, not only the listed ones", "alle 100 Top-Plays, nicht nur die gelisteten"),
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            user.BindValueChanged(_ =>
            {
                cancelRequests();
                setBusy(false);
                updateVisibility();
            }, true);
        }

        private void updateVisibility() => Alpha = user.Value?.User != null && user.Value.User.ScoresBestCount > 0 ? 1 : 0;

        private void setBusy(bool value)
        {
            busy = value;
            downloadButton.Enabled.Value = !value;
            collectionButton.Enabled.Value = !value;
        }

        private void cancelRequests()
        {
            foreach (var request in requests)
                request.Cancel();

            requests.Clear();
        }

        /// <summary>
        /// Runs an action with the plays it should work on: the loaded ones, or all of them after fetching what is missing.
        /// </summary>
        private void withPlays(Action<APIUser, IReadOnlyList<SoloScoreInfo>> action)
        {
            var profile = user.Value;

            if (busy || profile?.User == null)
                return;

            var loaded = loadedScores();
            var pages = allCheckbox.Current.Value
                ? BanchosucksTopPlays.PagesForAll(loaded.Count, profile.User.ScoresBestCount)
                : Array.Empty<(int offset, int limit)>();

            if (pages.Count == 0)
            {
                action(profile.User, loaded);
                return;
            }

            setBusy(true);

            var all = new List<SoloScoreInfo>();
            fetchPage(0);

            void fetchPage(int index)
            {
                var (offset, limit) = pages[index];
                var request = new GetUserScoresRequest(profile.User.Id, ScoreType.Best, new PaginationParameters(offset, limit), profile.Ruleset);

                request.Success += scores => Schedule(() =>
                {
                    // the profile changed while the request was on its way
                    if (user.Value != profile)
                        return;

                    all.AddRange(scores);

                    if (index + 1 < pages.Count && scores.Count == limit)
                    {
                        fetchPage(index + 1);
                        return;
                    }

                    setBusy(false);
                    action(profile.User, all);
                });

                request.Failure += _ => Schedule(() =>
                {
                    if (user.Value != profile)
                        return;

                    setBusy(false);
                    say(ClickerStrings.Text("The top plays could not be loaded.", "Die Top-Plays konnten nicht geladen werden."));
                });

                requests.Add(request);
                api.Queue(request);
            }
        }

        private static IEnumerable<BanchosucksTopPlays.Play> toPlays(IEnumerable<SoloScoreInfo> scores)
            => scores.Select(s => new BanchosucksTopPlays.Play(s.Beatmap?.OnlineBeatmapSetID ?? 0, s.Beatmap?.Checksum));

        #region Download missing maps

        private void confirmDownload(APIUser player, IReadOnlyList<SoloScoreInfo> scores)
        {
            if (downloadQueue == null)
                return;

            var plan = BanchosucksTopPlays.PlanDownloads(toPlays(scores), isLocal, downloadQueue.IsBusy);

            if (plan.ToDownload.Count == 0)
            {
                say(plan.AlreadyDownloading > 0
                    ? ClickerStrings.Text("Nothing to download: {0} of {1} beatmap sets are in your library, {2} are downloading.",
                        "Nichts zu laden: {0} von {1} Beatmap-Sets sind in deiner Bibliothek, {2} laden gerade.", plan.AlreadyLocal, plan.Sets, plan.AlreadyDownloading)
                    : ClickerStrings.Text("Nothing to download: all {0} beatmap sets of these {1} top plays are in your library.",
                        "Nichts zu laden: alle {0} Beatmap-Sets dieser {1} Top-Plays sind in deiner Bibliothek.", plan.Sets, scores.Count));
                return;
            }

            // the set as the score carries it gives the download its title; an id alone is enough to download
            var models = new Dictionary<int, IBeatmapSetInfo>();

            foreach (var score in scores)
            {
                if (score.Beatmap?.BeatmapSet is APIBeatmapSet set && set.OnlineID > 0)
                    models.TryAdd(set.OnlineID, set);
            }

            var sets = plan.ToDownload.Select(id => models.GetValueOrDefault(id) ?? new APIBeatmapSet { OnlineID = id }).ToList();

            LocalisableString question = sets.Count == 1
                ? ClickerStrings.Text("Download 1 missing beatmap set from the {1} top plays of {2}?", "1 fehlendes Beatmap-Set aus den {1} Top-Plays von {2} laden?", sets.Count, scores.Count, player.Username)
                : ClickerStrings.Text("Download {0} missing beatmap sets from the {1} top plays of {2}?", "{0} fehlende Beatmap-Sets aus den {1} Top-Plays von {2} laden?", sets.Count, scores.Count, player.Username);

            if (dialogOverlay == null)
                return;

            dialogOverlay.Push(new BanchosucksDownloadConfirmDialog(question, () =>
            {
                int added = downloadQueue.Enqueue(sets);
                say(ClickerStrings.Text("{0} beatmap sets queued, two at a time.", "{0} Beatmap-Sets in der Warteschlange, zwei gleichzeitig.", added));
            }));
        }

        private bool isLocal(int setId) => realm.Run(r => r.All<BeatmapSetInfo>().Where(s => s.OnlineID == setId && !s.DeletePending).Any());

        #endregion

        #region Collection

        private void saveCollection(APIUser player, IReadOnlyList<SoloScoreInfo> scores)
        {
            var (english, german) = BanchosucksTopPlays.CollectionNames(player.Username);
            string name = ClickerStrings.Pick(english, german);

            int added = 0;
            int total = 0;
            int withoutHash = 0;
            bool created = false;

            realm.Write(r =>
            {
                // found under either name, so switching the client language does not start a second collection
                var collection = r.All<BeatmapCollection>().FirstOrDefault(c => c.Name == english) ?? r.All<BeatmapCollection>().FirstOrDefault(c => c.Name == german);

                if (collection == null)
                {
                    collection = r.Add(new BeatmapCollection(name));
                    created = true;
                }

                var toAdd = BanchosucksTopPlays.HashesToAdd(toPlays(scores), collection.BeatmapMD5Hashes, out withoutHash);

                foreach (string hash in toAdd)
                    collection.BeatmapMD5Hashes.Add(hash);

                if (toAdd.Count > 0 || created)
                    collection.LastModified = DateTimeOffset.UtcNow;

                name = collection.Name;
                added = toAdd.Count;
                total = collection.BeatmapMD5Hashes.Count;
            });

            LocalisableString message = created
                ? ClickerStrings.Text("Collection \"{0}\" created with {1} difficulties.", "Sammlung „{0}“ mit {1} Schwierigkeiten angelegt.", name, total)
                : ClickerStrings.Text("Collection \"{0}\": {1} added, {2} in total.", "Sammlung „{0}“: {1} neu, {2} insgesamt.", name, added, total);

            if (withoutHash > 0)
                message = LocalisableString.Interpolate($"{message} {ClickerStrings.Text("{0} plays came without a map hash and were left out.", "{0} Plays kamen ohne Map-Hash und fehlen.", withoutHash)}");

            say(message);
        }

        #endregion

        private void say(LocalisableString message)
        {
            LastMessage = message;
            notifications?.Post(new SimpleNotification { Text = message, Icon = FontAwesome.Solid.Trophy });
        }

        protected override void Dispose(bool isDisposing)
        {
            cancelRequests();
            base.Dispose(isDisposing);
        }

        /// <summary>
        /// The one confirmation before the downloads start; it names the count.
        /// </summary>
        public partial class BanchosucksDownloadConfirmDialog : PopupDialog
        {
            public BanchosucksDownloadConfirmDialog(LocalisableString question, Action onConfirm)
            {
                HeaderText = question;
                BodyText = ClickerStrings.Text("The sets are downloaded two at a time through the normal beatmap download. Sets you already have or that are downloading are skipped.",
                    "Die Sets werden über den normalen Beatmap-Download geladen, zwei gleichzeitig. Vorhandene und bereits ladende Sets werden übersprungen.");
                Icon = FontAwesome.Solid.Download;

                Buttons = new PopupDialogButton[]
                {
                    new PopupDialogOkButton
                    {
                        Text = ClickerStrings.Text("Download", "Laden"),
                        Action = onConfirm,
                    },
                    new PopupDialogCancelButton
                    {
                        Text = ClickerStrings.Text("Cancel", "Abbrechen"),
                    },
                };
            }
        }
    }
}
