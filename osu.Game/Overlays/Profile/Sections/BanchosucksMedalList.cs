// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.API;
using osu.Game.Online.Banchosucks;
using osu.Game.Screens.Banchosucks.Clicker;
using osu.Game.Users;
using osuTK;

namespace osu.Game.Overlays.Profile.Sections
{
    /// <summary>
    /// Banchosucks: the medal overview in the profile overlay's "Medals" section: every medal of the server's catalogue, grouped
    /// by origin (Banchosucks, g0v0, osu!), the shown player's medals highlighted and counted ("x of y").
    /// </summary>
    /// <remarks>
    /// <para>
    /// The catalogue (<c>banchosucks_medals/catalogue</c>) is asked for once and kept for the session
    /// (<see cref="BanchosucksMedalCatalogueCache"/>, again after thirty minutes at the earliest). Which medals the player has
    /// comes from the user data the profile loaded anyway. The catalogue carries no conditions or progress, so a medal is
    /// either achieved or not; nothing in between is shown.
    /// </para>
    /// <para>
    /// Our own medals are always listed in full. Of the other groups only the player's medals are shown until "show all" is
    /// clicked: the hundreds of osu! medal images are not loaded for every profile that is opened, and images are only
    /// loaded once they scroll into view.
    /// </para>
    /// </remarks>
    public partial class BanchosucksMedalList : CompositeDrawable
    {
        private readonly IBindable<UserProfileData?> user = new Bindable<UserProfileData?>();
        private readonly BanchosucksMedalCatalogueCache cache;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        private OsuSpriteText summary = null!;
        private FillFlowContainer groups = null!;

        /// <summary>What is on screen (tests).</summary>
        public BanchosucksMedalOverview? Overview { get; private set; }

        /// <summary>The line above the groups (tests).</summary>
        public string SummaryText => summary.Text.ToString();

        public BanchosucksMedalList(IBindable<UserProfileData?> user, BanchosucksMedalCatalogueCache? cache = null)
        {
            this.user.BindTo(user);
            this.cache = cache ?? BanchosucksMedalCatalogueCache.SHARED;

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
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 12),
                Children = new Drawable[]
                {
                    summary = new OsuSpriteText
                    {
                        Font = OsuFont.GetFont(size: 15, weight: FontWeight.SemiBold),
                    },
                    groups = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 16),
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            cache.Changed += onCatalogueChanged;

            user.BindValueChanged(_ => rebuild(), true);
            fetchCatalogue();
        }

        // one clock for every list and the shared cache
        private static double now => Environment.TickCount64;

        private void fetchCatalogue()
        {
            // the client sends no api requests while logged out
            if (api.State.Value != APIState.Online || !cache.ShouldRefresh(now))
                return;

            cache.Started(now);

            // the answer goes to the cache, not to this list: the list may be gone by then (another profile was opened)
            // while the next one already waits for the same request. The cache tells every list that is still there.
            var cacheForRequest = cache;
            var request = new GetBanchosucksMedalCatalogueRequest();

            request.Success += response => cacheForRequest.Succeeded(now, response.Medals);
            request.Failure += _ => cacheForRequest.Failed();

            api.Queue(request);
        }

        private void rebuild()
        {
            groups.Clear();

            var player = user.Value?.User;

            if (player == null)
            {
                Overview = null;
                summary.Text = string.Empty;
                return;
            }

            if (cache.Medals == null)
            {
                Overview = null;
                summary.Text = cache.LastFailed || api.State.Value != APIState.Online
                    ? ClickerStrings.Text("The medal list could not be loaded.", "Die Medaillenliste konnte nicht geladen werden.")
                    : ClickerStrings.Text("Loading medals...", "Medaillen werden geladen...");
                return;
            }

            var overview = Overview = BanchosucksMedalOverview.Build(cache.Medals, player.Achievements?.Select(a => (a.ID, a.AchievedAt)));

            summary.Text = overview.AchievementsKnown
                ? ClickerStrings.Text("{0} of {1} medals", "{0} von {1} Medaillen", overview.Achieved, overview.Total)
                : ClickerStrings.Text("{0} medals (the server did not send which ones this player has)", "{0} Medaillen (welche dieser Spieler hat, hat der Server nicht mitgeschickt)", overview.Total);

            foreach (var group in overview.Groups)
                groups.Add(new MedalGroup(group, overview.AchievementsKnown));
        }

        private void onCatalogueChanged() => Schedule(rebuild);

        protected override void Dispose(bool isDisposing)
        {
            cache.Changed -= onCatalogueChanged;
            base.Dispose(isDisposing);
        }

        public partial class MedalGroup : CompositeDrawable
        {
            public readonly BanchosucksMedalOverview.Group Group;

            private readonly bool achievementsKnown;

            private FillFlowContainer tiles = null!;
            private RoundedButton showAllButton = null!;

            /// <summary>Whether the medals the player does not have are listed too.</summary>
            public bool ShowsAll { get; private set; }

            /// <summary>Medals currently listed (tests).</summary>
            public int ShownMedals => tiles.Count;

            public MedalGroup(BanchosucksMedalOverview.Group group, bool achievementsKnown)
            {
                Group = group;
                this.achievementsKnown = achievementsKnown;

                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;

                // our own medals are few and the point of the list; the others open on request
                ShowsAll = group.Kind == BanchosucksMedalGroup.Banchosucks;
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours)
            {
                string name = Group.Kind switch
                {
                    BanchosucksMedalGroup.Banchosucks => @"Banchosucks",
                    BanchosucksMedalGroup.G0v0 => @"g0v0",
                    _ => @"osu!",
                };

                int hidden = Group.Medals.Count - Group.Achieved;

                InternalChild = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 6),
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = achievementsKnown
                                ? ClickerStrings.Text("{0}: {1} of {2}", "{0}: {1} von {2}", name, Group.Achieved, Group.Medals.Count)
                                : ClickerStrings.Text("{0}: {1} medals", "{0}: {1} Medaillen", name, Group.Medals.Count),
                            Font = OsuFont.GetFont(size: 13, weight: FontWeight.Bold),
                            Colour = colours.Yellow,
                        },
                        tiles = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Full,
                            Spacing = new Vector2(6),
                        },
                        showAllButton = new RoundedButton
                        {
                            Size = new Vector2(260, 28),
                            Text = ClickerStrings.Text("Show the other {0} medals", "Die anderen {0} Medaillen zeigen", hidden),
                            Alpha = ShowsAll || hidden == 0 ? 0 : 1,
                            Action = ShowAll,
                        },
                    },
                };

                fill();
            }

            public void ShowAll()
            {
                if (ShowsAll)
                    return;

                ShowsAll = true;
                showAllButton.Hide();
                fill();
            }

            private void fill()
            {
                tiles.Clear();

                foreach (var entry in Group.Medals)
                {
                    if (entry.Achieved || ShowsAll)
                        tiles.Add(new MedalTile(entry, achievementsKnown));
                }
            }
        }

        public partial class MedalTile : CompositeDrawable, IHasTooltip
        {
            public const float WIDTH = 104;
            private const float icon_size = 56;

            public readonly BanchosucksMedalOverview.Entry Entry;

            private readonly bool achievementsKnown;

            public MedalTile(BanchosucksMedalOverview.Entry entry, bool achievementsKnown)
            {
                Entry = entry;
                this.achievementsKnown = achievementsKnown;

                Size = new Vector2(WIDTH, icon_size + 22);

                // achieved medals stand out, the others are there but faint
                Alpha = entry.Achieved || !achievementsKnown ? 1 : 0.3f;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                InternalChildren = new Drawable[]
                {
                    new DelayedLoadWrapper(() => new MedalImage(new Medal { InternalName = Entry.Medal.Slug }.ImageUrl)
                    {
                        RelativeSizeAxes = Axes.Both,
                    })
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Size = new Vector2(icon_size),
                    },
                    new TruncatingSpriteText
                    {
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.BottomCentre,
                        Text = Entry.Medal.Name,
                        Font = OsuFont.GetFont(size: 12, weight: Entry.Achieved ? FontWeight.SemiBold : FontWeight.Regular),
                        MaxWidth = WIDTH,
                    },
                };
            }

            public LocalisableString TooltipText
            {
                get
                {
                    string head = string.IsNullOrWhiteSpace(Entry.Medal.Description) ? Entry.Medal.Name : $"{Entry.Medal.Name}\n{Entry.Medal.Description}";

                    if (!achievementsKnown)
                        return head;

                    if (Entry.AchievedAt is DateTimeOffset at)
                    {
                        string date = at.ToLocalTime().ToString("d", ClickerStrings.IsGerman ? ClickerFormat.GermanCulture : ClickerFormat.EnglishCulture);
                        return $"{head}\n{ClickerStrings.Pick("Achieved on", "Erreicht am")} {date}";
                    }

                    return $"{head}\n{ClickerStrings.Pick("Not achieved yet", "Noch nicht erreicht")}";
                }
            }
        }

        [LongRunningLoad]
        private partial class MedalImage : Sprite
        {
            private readonly string url;

            public MedalImage(string url)
            {
                this.url = url;
                FillMode = FillMode.Fit;
            }

            [BackgroundDependencyLoader]
            private void load(LargeTextureStore textures)
            {
                Texture = textures.Get(url);
            }
        }
    }
}
