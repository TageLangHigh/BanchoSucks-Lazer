// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Globalization;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
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

namespace osu.Game.Screens.Banchosucks.CircleRush
{
    /// <summary>
    /// Top 50 of one Circle Rush board (mode × difficulty) plus the player's own row.
    /// </summary>
    internal partial class CircleRushLeaderboardPanel : CompositeDrawable
    {
        private const double refresh_interval = 30_000;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        private CircleRushMode mode = CircleRushMode.Minute;
        private CircleRushDifficulty difficulty = CircleRushDifficulty.Medium;
        private GetCircleRushLeaderboardRequest? request;
        private OsuSpriteText titleText = null!;
        private OsuSpriteText ownText = null!;
        private OsuSpriteText statusText = null!;
        private FillFlowContainer rows = null!;
        private LoadingSpinner loading = null!;
        private double lastRefresh = double.MinValue;

        public CircleRushLeaderboardPanel()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 16,
                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black.Opacity(0.35f) },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        RowDimensions = new[]
                        {
                            new Dimension(GridSizeMode.AutoSize),
                            new Dimension(GridSizeMode.AutoSize),
                            new Dimension(),
                        },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                titleText = new OsuSpriteText
                                {
                                    Margin = new MarginPadding { Horizontal = 16, Top = 14, Bottom = 4 },
                                    Font = OsuFont.GetFont(size: 20, weight: FontWeight.Bold),
                                },
                            },
                            new Drawable[]
                            {
                                ownText = new OsuSpriteText
                                {
                                    Margin = new MarginPadding { Horizontal = 16, Bottom = 8 },
                                    Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold),
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
                                                Spacing = new Vector2(0, 3),
                                                Padding = new MarginPadding { Horizontal = 12, Bottom = 12 },
                                            },
                                        },
                                        statusText = new OsuSpriteText
                                        {
                                            Anchor = Anchor.Centre,
                                            Origin = Anchor.Centre,
                                            Font = OsuFont.GetFont(size: 15, weight: FontWeight.SemiBold),
                                            Colour = colours.Gray9,
                                            Alpha = 0,
                                        },
                                        loading = new LoadingSpinner(),
                                    },
                                },
                            },
                        },
                    },
                },
            };
        }

        protected override void Update()
        {
            base.Update();

            if (IsPresent && Time.Current - lastRefresh > refresh_interval)
                Refresh();
        }

        /// <summary>Switches to another board and reloads it.</summary>
        public void SetBoard(CircleRushMode newMode, CircleRushDifficulty newDifficulty)
        {
            mode = newMode;
            difficulty = newDifficulty;
            titleText.Text = ClickerStrings.Pick(
                $"Leaderboard · {CircleRushStrings.ModeName(mode)} · {CircleRushStrings.DifficultyName(difficulty)}",
                $"Rangliste · {CircleRushStrings.ModeName(mode)} · {CircleRushStrings.DifficultyName(difficulty)}");
            Refresh();
        }

        public void Refresh()
        {
            lastRefresh = Time.Current;
            request?.Cancel();
            statusText.FadeOut(100);

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
            var requested = request = new GetCircleRushLeaderboardRequest(mode, difficulty);

            requested.Success += response =>
            {
                if (IsDisposed || requested != request)
                    return;

                loading.Hide();
                showBoard(response);
            };
            requested.Failure += e =>
            {
                if (IsDisposed || e is OperationCanceledException || requested != request)
                    return;

                loading.Hide();
                rows.Clear();
                ownText.Text = string.Empty;
                statusText.Text = ClickerStrings.Text("The leaderboard is not reachable right now.", "Rangliste gerade nicht erreichbar.");
                statusText.FadeIn(200);
            };

            api.Queue(requested);
        }

        private void showBoard(CircleRushLeaderboardResponse response)
        {
            rows.Clear();
            long ownId = api.LocalUser.Value.OnlineID;

            if (response.Entries.Count == 0)
            {
                statusText.Text = ClickerStrings.Text("No runs on this board yet. Be the first!", "Noch keine Läufe auf dieser Rangliste. Sei der Erste!");
                statusText.FadeIn(200);
            }

            foreach (var entry in response.Entries)
                rows.Add(new BoardRow(entry, entry.UserId == ownId, colours));

            if (response.Own != null)
            {
                ownText.Text = ClickerStrings.Pick(
                    $"You: #{response.Own.Rank} with {response.Own.Score.ToString("N0", CultureInfo.InvariantCulture)} {modsSuffix(response.Own.Mods)}",
                    $"Du: #{response.Own.Rank} mit {response.Own.Score.ToString("N0", CultureInfo.GetCultureInfo("de-DE"))} {modsSuffix(response.Own.Mods)}");
            }
            else
                ownText.Text = ClickerStrings.Text("No run of yours on this board yet.", "Noch kein Lauf von dir auf dieser Rangliste.");
        }

        private static string modsSuffix(int mods)
        {
            string acronyms = CircleRushRules.ModsAcronyms((CircleRushMods)mods);
            return acronyms.Length > 0 ? $"(+{acronyms})" : string.Empty;
        }

        private partial class BoardRow : CompositeDrawable
        {
            public BoardRow(CircleRushLeaderboardEntry entry, bool own, OsuColour colours)
            {
                RelativeSizeAxes = Axes.X;
                Height = 34;
                Masking = true;
                CornerRadius = 8;

                string mods = CircleRushRules.ModsAcronyms((CircleRushMods)entry.Mods);
                var culture = ClickerStrings.IsGerman ? CultureInfo.GetCultureInfo("de-DE") : CultureInfo.InvariantCulture;
                Enum.TryParse(entry.CountryCode, true, out CountryCode country);

                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = own ? colours.Pink.Opacity(0.25f) : Color4.White.Opacity(0.04f),
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 10,
                        Text = $"#{entry.Rank}",
                        Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold),
                        Colour = entry.Rank <= 3 ? colours.Yellow : colours.Gray9,
                    },
                    new UpdateableFlag(country)
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 48,
                        Size = new Vector2(24, 17),
                    },
                    new TruncatingSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 80,
                        Text = entry.Username,
                        Font = OsuFont.GetFont(size: 15, weight: own ? FontWeight.Bold : FontWeight.SemiBold),
                        MaxWidth = 128,
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 214,
                        Text = mods,
                        Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                        Colour = colours.Blue,
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        X = -10,
                        Text = entry.Score.ToString("N0", culture),
                        Font = OsuFont.GetFont(size: 15, weight: FontWeight.Bold),
                        Colour = colours.Pink,
                    },
                };
            }
        }
    }

    /// <summary>Names of the modes, difficulties and mods in the client language.</summary>
    internal static class CircleRushStrings
    {
        public static string ModeName(CircleRushMode mode) => mode == CircleRushMode.Minute
            ? ClickerStrings.Pick("1 minute", "1 Minute")
            : ClickerStrings.Pick("Endless", "Endlos");

        public static string DifficultyName(CircleRushDifficulty difficulty) => difficulty switch
        {
            CircleRushDifficulty.Easy => "Easy",
            CircleRushDifficulty.Medium => "Medium",
            CircleRushDifficulty.Hard => "Hard",
            _ => "Extreme",
        };
    }
}
