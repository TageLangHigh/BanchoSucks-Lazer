// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Screens.Banchosucks.Clicker;
using osu.Game.Users;
using osu.Game.Users.Drawables;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Banchosucks
{
    /// <summary>
    /// The "Rangliste" tab of the osu! Clicker: lifetime PP, tapping BPM, prestige points or medals.
    /// </summary>
    internal partial class ClickerLeaderboardPanel : CompositeDrawable
    {
        private const double refresh_interval = 30_000;

        public const string SORT_PP = "pp";
        public const string SORT_BPM = "bpm";
        public const string SORT_PRESTIGE = "prestige";
        public const string SORT_MEDALS = "medals";

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        private string sort = SORT_PP;
        private GetClickerLeaderboardRequest? request;
        private readonly Dictionary<string, ClickerTabButton> sortTabs = new Dictionary<string, ClickerTabButton>();
        private OsuSpriteText ownText = null!;
        private OsuSpriteText statusText = null!;
        private FillFlowContainer rows = null!;
        private LoadingSpinner loading = null!;
        private double lastRefresh = double.MinValue;

        /// <summary>
        /// The sort currently shown (tests check which one was requested).
        /// </summary>
        public string Sort => sort;

        public ClickerLeaderboardPanel()
        {
            RelativeSizeAxes = Axes.Both;
            Padding = new MarginPadding { Top = 6 };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                RowDimensions = new[]
                {
                    new Dimension(GridSizeMode.Absolute, 44),
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(),
                },
                Content = new[]
                {
                    new Drawable[]
                    {
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(8, 0),
                            Padding = new MarginPadding { Horizontal = 12 },
                            Children = new Drawable[]
                            {
                                sortTabs[SORT_PP] = new ClickerTabButton(ClickerStrings.Text("PP", "PP"), FontAwesome.Solid.Coins, colours.Pink, () => setSort(SORT_PP), 15, 14),
                                sortTabs[SORT_BPM] = new ClickerTabButton(ClickerStrings.Text("Tapping BPM", "Tapping-BPM"), FontAwesome.Solid.Bolt, colours.Yellow, () => setSort(SORT_BPM), 15, 14),
                                sortTabs[SORT_PRESTIGE] = new ClickerTabButton(ClickerStrings.Text("Prestige", "Prestige"), FontAwesome.Solid.Star, colours.Purple, () => setSort(SORT_PRESTIGE), 15, 14),
                                sortTabs[SORT_MEDALS] = new ClickerTabButton(ClickerStrings.Text("Medals", "Medaillen"), FontAwesome.Solid.Medal, colours.Green, () => setSort(SORT_MEDALS), 15, 14),
                            },
                        },
                    },
                    new Drawable[]
                    {
                        ownText = new OsuSpriteText
                        {
                            Margin = new MarginPadding { Horizontal = 16, Vertical = 6 },
                            Font = OsuFont.GetFont(size: 15, weight: FontWeight.SemiBold),
                            Colour = colours.Gray9,
                        },
                    },
                    new Drawable[]
                    {
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                new OsuScrollContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Child = rows = new FillFlowContainer
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(0, 4),
                                        Padding = new MarginPadding { Horizontal = 12, Bottom = 12 },
                                    },
                                },
                                statusText = new OsuSpriteText
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Font = OsuFont.GetFont(size: 16, weight: FontWeight.SemiBold),
                                    Colour = colours.Gray9,
                                    Alpha = 0,
                                },
                                loading = new LoadingSpinner(),
                            },
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            sortTabs[SORT_PP].Active = true;
        }

        protected override void Update()
        {
            base.Update();

            // refresh every 30 seconds while the tab is shown
            if (IsPresent && Time.Current - lastRefresh > refresh_interval)
                Refresh();
        }

        private void setSort(string newSort)
        {
            sort = newSort;

            foreach (var (id, tab) in sortTabs)
                tab.Active = id == sort;

            Refresh();
        }

        public void Refresh()
        {
            lastRefresh = Time.Current;
            request?.Cancel();

            // the client only talks to the api while logged in
            if (!api.IsLoggedIn)
            {
                loading.Hide();
                rows.Clear();
                ownText.Text = string.Empty;
                statusText.Text = ClickerStrings.Text("Log in to see the leaderboard.", "Melde dich an, um die Rangliste zu sehen.");
                statusText.FadeIn(200);
                return;
            }

            loading.Show();
            request = new GetClickerLeaderboardRequest(sort);
            string requestedSort = sort;

            request.Success += response =>
            {
                if (IsDisposed || requestedSort != sort)
                    return;

                loading.Hide();
                showBoard(response);
            };
            request.Failure += e =>
            {
                if (IsDisposed || e is OperationCanceledException)
                    return;

                loading.Hide();
                rows.Clear();
                ownText.Text = string.Empty;
                statusText.Text = ClickerStrings.Text("The leaderboard is not reachable right now.", "Rangliste gerade nicht erreichbar.");
                statusText.FadeIn(200);
            };

            api.Queue(request);
        }

        /// <summary>
        /// Shows the ranks the server returned after a submission, until the next refresh.
        /// </summary>
        public void SetOwnRanks(ClickerSubmitResponse response)
        {
            if (response.RankPp == null && response.RankBpm == null && response.RankPrestige == null)
                return;

            submittedRanks = ClickerStrings.Pick(
                $"Your rank: PP #{rank(response.RankPp)} · BPM #{rank(response.RankBpm)} · Prestige #{rank(response.RankPrestige)}",
                $"Dein Rang: PP #{rank(response.RankPp)} · BPM #{rank(response.RankBpm)} · Prestige #{rank(response.RankPrestige)}");
            ownText.Text = submittedRanks;
        }

        private string? submittedRanks;

        private static string rank(int? value) => value?.ToString() ?? "–";

        private void showBoard(ClickerLeaderboardResponse response)
        {
            rows.Clear();

            if (response.Entries.Count == 0)
            {
                statusText.Text = ClickerStrings.Text("Nobody on the leaderboard yet. Be the first!", "Noch niemand in der Rangliste. Sei die erste Person!");
                statusText.FadeIn(200);
            }
            else
                statusText.FadeOut(100);

            long ownId = api.LocalUser.Value?.OnlineID ?? 0;
            bool ownShown = false;

            foreach (var entry in response.Entries)
            {
                bool own = entry.UserId == ownId;
                ownShown |= own;
                rows.Add(new LeaderboardRow(entry, sort, own));
            }

            if (response.Own != null && !ownShown)
            {
                rows.Add(new OsuSpriteText
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Text = "···",
                    Font = OsuFont.GetFont(size: 20, weight: FontWeight.Bold),
                    Colour = colours.Gray9,
                });
                rows.Add(new LeaderboardRow(response.Own, sort, true));
            }

            // the ranks of the last submission stay until the board itself knows where the player is
            ownText.Text = response.Own != null
                ? ClickerStrings.Pick($"Your rank: #{response.Own.Rank} of {response.Total}", $"Dein Rang: #{response.Own.Rank} von {response.Total}")
                : submittedRanks ?? (api.IsLoggedIn
                    ? ClickerStrings.Pick($"{response.Total} players · your progress is submitted every minute.", $"{response.Total} Spieler · Dein Fortschritt wird jede Minute eingereicht.")
                    : ClickerStrings.Pick($"{response.Total} players · log in to take part.", $"{response.Total} Spieler · Melde dich an, um mitzumachen."));
        }

        /// <summary>
        /// The big number of a row for the given sort.
        /// </summary>
        public static string ValueText(ClickerLeaderboardEntry entry, string sort)
        {
            switch (sort)
            {
                case SORT_BPM: return $"{entry.BestBpm:0} BPM";
                case SORT_PRESTIGE: return ClickerStrings.Pick($"{entry.Prestige} prestige", $"{entry.Prestige} Prestige");
                case SORT_MEDALS: return ClickerStrings.Pick($"{entry.Medals} medals", $"{entry.Medals} Medaillen");
                default: return $"{ClickerFormat.Number(entry.TotalEarned)} PP";
            }
        }

        private static string secondaryText(ClickerLeaderboardEntry entry, string sort)
        {
            switch (sort)
            {
                case SORT_BPM:
                    return ClickerStrings.Pick($"{ClickerFormat.Number(entry.TotalEarned)} PP · combo {ClickerFormat.Count(entry.BestCombo)}", $"{ClickerFormat.Number(entry.TotalEarned)} PP · Combo {ClickerFormat.Count(entry.BestCombo)}");

                case SORT_PRESTIGE:
                    return ClickerStrings.Pick($"{entry.Rebirths} rebirths · {ClickerFormat.Number(entry.TotalEarned)} PP", $"{entry.Rebirths} Rebirths · {ClickerFormat.Number(entry.TotalEarned)} PP");

                case SORT_MEDALS:
                    return ClickerStrings.Pick($"{entry.Prestige} prestige · {ClickerFormat.Count(entry.Buildings)} buildings", $"{entry.Prestige} Prestige · {ClickerFormat.Count(entry.Buildings)} Gebäude");

                default:
                    return ClickerStrings.Pick($"{ClickerFormat.Count(entry.Buildings)} buildings · {entry.Rebirths} rebirths · {entry.Medals} medals", $"{ClickerFormat.Count(entry.Buildings)} Gebäude · {entry.Rebirths} Rebirths · {entry.Medals} Medaillen");
            }
        }

        private partial class LeaderboardRow : CompositeDrawable
        {
            private readonly ClickerLeaderboardEntry entry;
            private readonly string sort;
            private readonly bool own;

            public LeaderboardRow(ClickerLeaderboardEntry entry, string sort, bool own)
            {
                this.entry = entry;
                this.sort = sort;
                this.own = own;

                RelativeSizeAxes = Axes.X;
                Height = 52;
                Masking = true;
                CornerRadius = 8;
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours)
            {
                Color4 rankColour = entry.Rank switch
                {
                    1 => Color4Extensions.FromHex("ffd966"),
                    2 => Color4Extensions.FromHex("d9e1ea"),
                    3 => Color4Extensions.FromHex("e0a36a"),
                    _ => Color4.White,
                };

                Color4 valueColour = sort switch
                {
                    SORT_BPM => colours.Yellow,
                    SORT_PRESTIGE => colours.Purple.Lighten(0.5f),
                    SORT_MEDALS => colours.Green.Lighten(0.3f),
                    _ => colours.Pink.Lighten(0.4f),
                };

                Enum.TryParse(entry.CountryCode, true, out CountryCode country);

                var user = new APIUser
                {
                    Id = (int)entry.UserId,
                    Username = entry.Username,
                    AvatarUrl = entry.AvatarUrl ?? string.Empty,
                    CountryCode = country,
                };

                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = own ? colours.Pink.Opacity(0.3f) : Color4.Black.Opacity(0.3f),
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreRight,
                        X = 46,
                        Text = $"#{entry.Rank}",
                        Font = OsuFont.GetFont(size: entry.Rank <= 3 ? 22 : 18, weight: FontWeight.Black),
                        Colour = rankColour,
                    },
                    new UpdateableAvatar(user, isInteractive: false)
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 56,
                        Size = new Vector2(38),
                        Masking = true,
                        CornerRadius = 8,
                    },
                    new FillFlowContainer
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 104,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Children = new Drawable[]
                        {
                            new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(6, 0),
                                Children = new Drawable[]
                                {
                                    new UpdateableFlag(country)
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Size = new Vector2(24, 17),
                                    },
                                    new OsuSpriteText
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Text = entry.Username,
                                        Font = OsuFont.GetFont(size: 17, weight: FontWeight.Bold),
                                        Colour = own ? colours.Pink.Lighten(0.5f) : Color4.White,
                                    },
                                },
                            },
                            new OsuSpriteText
                            {
                                Text = secondaryText(entry, sort),
                                Font = OsuFont.GetFont(size: 13),
                                Colour = colours.Gray9,
                            },
                        },
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        X = -14,
                        Text = ValueText(entry, sort),
                        Font = OsuFont.GetFont(size: 18, weight: FontWeight.Bold),
                        Colour = valueColour,
                    },
                };
            }
        }
    }
}
