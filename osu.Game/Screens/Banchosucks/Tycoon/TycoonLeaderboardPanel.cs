// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API;
using osu.Game.Screens.Banchosucks.Clicker;
using osu.Game.Users;
using osu.Game.Users.Drawables;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Banchosucks.Tycoon
{
    /// <summary>
    /// Top 50 of the tycoon by lifetime donations, player record or seasons, plus the player's own row.
    /// </summary>
    internal partial class TycoonLeaderboardPanel : CompositeDrawable
    {
        private const double refresh_interval = 45_000;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        private string sort = "total";
        private GetTycoonLeaderboardRequest? request;
        private OsuSpriteText ownText = null!;
        private OsuSpriteText statusText = null!;
        private FillFlowContainer rows = null!;
        private FillFlowContainer sortButtons = null!;
        private LoadingSpinner loading = null!;
        private double lastRefresh = double.MinValue;

        public TycoonLeaderboardPanel()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 8),
                    Children = new Drawable[]
                    {
                        sortButtons = new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(6, 0),
                            Children = new Drawable[]
                            {
                                new SortButton(ClickerStrings.Text("Donations", "Spenden"), "total", this),
                                new SortButton(ClickerStrings.Text("Player record", "Spieler-Rekord"), "players", this),
                                new SortButton(ClickerStrings.Text("Prestige", "Prestige"), "prestige", this),
                            },
                        },
                        ownText = new OsuSpriteText
                        {
                            Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold),
                            Colour = colours.Yellow,
                        },
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            // the flow above: tabs, own row and spacing
                            Padding = new MarginPadding { Bottom = 70 },
                            Children = new Drawable[]
                            {
                                new OsuScrollContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    ScrollbarVisible = false,
                                    Child = rows = new FillFlowContainer
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(0, 4),
                                    },
                                },
                                loading = new LoadingSpinner { Anchor = Anchor.Centre, Origin = Anchor.Centre },
                                statusText = new OsuSpriteText
                                {
                                    Anchor = Anchor.TopCentre,
                                    Origin = Anchor.TopCentre,
                                    Y = 20,
                                    Font = OsuFont.GetFont(size: 14),
                                    Colour = colours.Gray9,
                                },
                            },
                        },
                    },
                },
            };

            updateSortButtons();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Refresh();
        }

        protected override void Update()
        {
            base.Update();

            if (IsPresent && Time.Current - lastRefresh > refresh_interval)
                Refresh();
        }

        public void SetSort(string newSort)
        {
            if (sort == newSort) return;

            sort = newSort;
            updateSortButtons();
            Refresh();
        }

        private void updateSortButtons()
        {
            foreach (var button in sortButtons.Children)
            {
                if (button is SortButton sortButton)
                    sortButton.Active = sortButton.Sort == sort;
            }
        }

        public void Refresh()
        {
            lastRefresh = Time.Current;
            request?.Cancel();
            loading.Show();
            statusText.Text = string.Empty;

            var requested = request = new GetTycoonLeaderboardRequest(sort);

            requested.Success += response =>
            {
                if (IsDisposed || requested != request) return;

                loading.Hide();
                rows.Clear();

                if (response.Entries.Count == 0)
                    statusText.Text = ClickerStrings.Text("No servers founded yet. Be the first.", "Noch kein Server gegründet. Sei der Erste.");

                foreach (var entry in response.Entries)
                    rows.Add(new BoardRow(entry, sort, entry.UserId == api.LocalUser.Value.OnlineID, colours));

                if (response.Own != null)
                {
                    ownText.Text = ClickerStrings.Pick(
                        $"You: rank #{response.Own.Rank} · {valueText(response.Own, sort)}",
                        $"Du: Rang #{response.Own.Rank} · {valueText(response.Own, sort)}");
                }
                else
                    ownText.Text = api.IsLoggedIn ? ClickerStrings.Text("You are not on this board yet.", "Du bist noch nicht auf dieser Rangliste.") : ClickerStrings.Text("Log in to save your server.", "Melde dich an, damit dein Server gespeichert wird.");
            };

            requested.Failure += e =>
            {
                if (IsDisposed || e is OperationCanceledException || requested != request) return;

                loading.Hide();
                statusText.Text = ClickerStrings.Text("The server could not be reached.", "Der Server war nicht erreichbar.");
            };

            api.Queue(requested);
        }

        private static string valueText(TycoonLeaderboardEntry entry, string sort) => sort switch
        {
            "players" => ClickerStrings.Pick($"{ClickerFormat.Number(entry.MaxPlayers)} players", $"{ClickerFormat.Number(entry.MaxPlayers)} Spieler"),
            "prestige" => ClickerStrings.Pick($"season {entry.Prestige + 1}, {entry.Legacy} legacy", $"Saison {entry.Prestige + 1}, {entry.Legacy} Legacy"),
            _ => ClickerStrings.Pick($"{ClickerFormat.Number(entry.LifetimeTotal)} donations", $"{ClickerFormat.Number(entry.LifetimeTotal)} Spenden"),
        };

        private partial class SortButton : OsuClickableContainer
        {
            public readonly string Sort;
            private readonly Box background;
            private readonly OsuSpriteText label;

            public bool Active
            {
                set
                {
                    background.FadeColour(value ? Color4Extensions.FromHex("ffcc22") : Color4.White.Opacity(0.08f), 120);
                    label.FadeColour(value ? Color4.Black : Color4.White, 120);
                }
            }

            public SortButton(LocalisableString text, string sort, TycoonLeaderboardPanel panel)
            {
                Sort = sort;
                Action = () => panel.SetSort(sort);
                AutoSizeAxes = Axes.X;
                Height = 28;
                Masking = true;
                CornerRadius = 14;
                Children = new Drawable[]
                {
                    background = new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.White.Opacity(0.08f) },
                    label = new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Margin = new MarginPadding { Horizontal = 12 },
                        Text = text,
                        Font = OsuFont.GetFont(size: 13, weight: FontWeight.Bold),
                    },
                };
            }
        }

        private partial class BoardRow : CompositeDrawable
        {
            public BoardRow(TycoonLeaderboardEntry entry, string sort, bool own, OsuColour colours)
            {
                RelativeSizeAxes = Axes.X;
                Height = 40;
                Masking = true;
                CornerRadius = 8;

                Enum.TryParse(entry.CountryCode, true, out CountryCode country);
                var hardware = TycoonRules.Hardware[Math.Max(0, Array.FindIndex(TycoonRules.Hardware, h => h.Key == entry.Hardware))];

                InternalChildren = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = own ? colours.Yellow.Opacity(0.18f) : Color4.White.Opacity(0.05f) },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 10,
                        Width = 34,
                        Text = $"#{entry.Rank}",
                        Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold),
                        Colour = entry.Rank <= 3 ? colours.Yellow : Color4.White,
                    },
                    new UpdateableFlag(country)
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 46,
                        Size = new Vector2(22, 15),
                    },
                    new TruncatingSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 76,
                        Text = entry.Username,
                        Font = OsuFont.GetFont(size: 14, weight: own ? FontWeight.Bold : FontWeight.SemiBold),
                        MaxWidth = 120,
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 200,
                        Text = ClickerStrings.Text(hardware.NameEn, hardware.NameDe),
                        Font = OsuFont.GetFont(size: 12),
                        Colour = colours.Gray9,
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        X = -10,
                        Text = sort switch
                        {
                            "players" => ClickerFormat.Number(entry.MaxPlayers),
                            "prestige" => ClickerStrings.Pick($"S{entry.Prestige + 1} · {entry.Legacy}", $"S{entry.Prestige + 1} · {entry.Legacy}"),
                            _ => ClickerFormat.Number(entry.LifetimeTotal),
                        },
                        Font = OsuFont.GetFont(size: 15, weight: FontWeight.Bold, fixedWidth: true),
                    },
                };
            }
        }
    }
}
