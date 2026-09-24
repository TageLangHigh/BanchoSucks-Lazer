// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Net.Http;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.IO.Network;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Users;
using osu.Game.Users.Drawables;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Banchosucks
{
    public class ClickerSubmission
    {
        [JsonProperty("total_earned")]
        public double TotalEarned { get; set; }

        [JsonProperty("clicks")]
        public long Clicks { get; set; }

        [JsonProperty("best_bpm")]
        public double BestBpm { get; set; }

        [JsonProperty("best_combo")]
        public int BestCombo { get; set; }

        [JsonProperty("producers")]
        public Dictionary<string, int> Producers { get; set; } = new Dictionary<string, int>();

        [JsonProperty("upgrades")]
        public List<string> Upgrades { get; set; } = new List<string>();
    }

    public class ClickerSubmitResponse
    {
        [JsonProperty("accepted")]
        public bool Accepted { get; set; }

        [JsonProperty("reason")]
        public string? Reason { get; set; }

        [JsonProperty("rank_pp")]
        public int? RankPp { get; set; }

        [JsonProperty("rank_bpm")]
        public int? RankBpm { get; set; }
    }

    public class ClickerLeaderboardEntry
    {
        [JsonProperty("rank")]
        public int Rank { get; set; }

        [JsonProperty("user_id")]
        public long UserId { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; } = string.Empty;

        [JsonProperty("country_code")]
        public string? CountryCode { get; set; }

        [JsonProperty("avatar_url")]
        public string? AvatarUrl { get; set; }

        [JsonProperty("total_earned")]
        public double TotalEarned { get; set; }

        [JsonProperty("best_bpm")]
        public double BestBpm { get; set; }

        [JsonProperty("best_combo")]
        public int BestCombo { get; set; }

        [JsonProperty("clicks")]
        public long Clicks { get; set; }

        [JsonProperty("buildings")]
        public int Buildings { get; set; }
    }

    public class ClickerLeaderboardResponse
    {
        [JsonProperty("sort")]
        public string Sort { get; set; } = "pp";

        [JsonProperty("total")]
        public int Total { get; set; }

        [JsonProperty("entries")]
        public List<ClickerLeaderboardEntry> Entries { get; set; } = new List<ClickerLeaderboardEntry>();

        [JsonProperty("own")]
        public ClickerLeaderboardEntry? Own { get; set; }
    }

    /// <summary>
    /// Banchosucks: sends the osu! Clicker progress to the lazer server plugin <c>banchosucks_clicker</c>.
    /// </summary>
    public class SubmitClickerScoreRequest : APIRequest<ClickerSubmitResponse>
    {
        public readonly ClickerSubmission Submission;

        public SubmitClickerScoreRequest(ClickerSubmission submission)
        {
            Submission = submission;
        }

        protected override string Route => "plugins";

        protected override string Target => "banchosucks_clicker/submit";

        protected override WebRequest CreateWebRequest()
        {
            var req = base.CreateWebRequest();
            req.Method = HttpMethod.Post;
            req.ContentType = "application/json";
            req.AddRaw(JsonConvert.SerializeObject(Submission));
            return req;
        }
    }

    /// <summary>
    /// Banchosucks: osu! Clicker leaderboard, sorted by earned PP ("pp") or tapping speed ("bpm").
    /// </summary>
    public class GetClickerLeaderboardRequest : APIRequest<ClickerLeaderboardResponse>
    {
        public readonly string Sort;

        public GetClickerLeaderboardRequest(string sort)
        {
            Sort = sort;
        }

        protected override string Route => "plugins";

        protected override string Target => "banchosucks_clicker/leaderboard";

        protected override WebRequest CreateWebRequest()
        {
            var req = base.CreateWebRequest();
            req.AddParameter("sort", Sort);
            req.AddParameter("limit", "50");
            return req;
        }
    }

    /// <summary>
    /// The "Rangliste" tab of the osu! Clicker.
    /// </summary>
    internal partial class ClickerLeaderboardPanel : CompositeDrawable
    {
        private const double refresh_interval = 30_000;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        private string sort = "pp";
        private GetClickerLeaderboardRequest? request;
        private ClickerTabButton ppTab = null!;
        private ClickerTabButton bpmTab = null!;
        private OsuSpriteText ownText = null!;
        private OsuSpriteText statusText = null!;
        private FillFlowContainer rows = null!;
        private LoadingSpinner loading = null!;
        private double lastRefresh = double.MinValue;

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
                            Spacing = new Vector2(12, 0),
                            Padding = new MarginPadding { Horizontal = 16 },
                            Children = new Drawable[]
                            {
                                ppTab = new ClickerTabButton("PP", FontAwesome.Solid.Coins, colours.Pink, () => setSort("pp")),
                                bpmTab = new ClickerTabButton("Tapping-BPM", FontAwesome.Solid.Bolt, colours.Yellow, () => setSort("bpm")),
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
            ppTab.Active = true;
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
            ppTab.Active = sort == "pp";
            bpmTab.Active = sort == "bpm";
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
                statusText.Text = "Melde dich an, um die Rangliste zu sehen.";
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
                statusText.Text = "Rangliste gerade nicht erreichbar.";
                statusText.FadeIn(200);
            };

            api.Queue(request);
        }

        /// <summary>
        /// Shows the ranks the server returned after a submission, until the next refresh.
        /// </summary>
        public void SetOwnRanks(int? rankPp, int? rankBpm)
        {
            if (rankPp == null && rankBpm == null)
                return;

            ownText.Text = $"Dein Rang: PP #{rankPp?.ToString() ?? "–"} · BPM #{rankBpm?.ToString() ?? "–"}";
        }

        private void showBoard(ClickerLeaderboardResponse response)
        {
            rows.Clear();

            if (response.Entries.Count == 0)
            {
                statusText.Text = "Noch niemand in der Rangliste. Sei die erste Person!";
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

            ownText.Text = response.Own != null
                ? $"Dein Rang: #{response.Own.Rank} von {response.Total}"
                : api.IsLoggedIn
                    ? $"{response.Total} Spieler · Dein Fortschritt wird jede Minute eingereicht."
                    : $"{response.Total} Spieler · Melde dich an, um mitzumachen.";
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

                Enum.TryParse(entry.CountryCode, true, out CountryCode country);

                var user = new APIUser
                {
                    Id = (int)entry.UserId,
                    Username = entry.Username,
                    AvatarUrl = entry.AvatarUrl ?? string.Empty,
                    CountryCode = country,
                };

                string value = sort == "bpm" ? $"{entry.BestBpm:0} BPM" : $"{ClickerFormat.Number(entry.TotalEarned)} PP";
                string secondary = sort == "bpm"
                    ? $"{ClickerFormat.Number(entry.TotalEarned)} PP · Combo {entry.BestCombo:N0}"
                    : $"{entry.Buildings:N0} Gebäude · {entry.BestBpm:0} BPM";

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
                                Text = secondary,
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
                        Text = value,
                        Font = OsuFont.GetFont(size: 18, weight: FontWeight.Bold),
                        Colour = sort == "bpm" ? colours.Yellow : colours.Pink.Lighten(0.4f),
                    },
                };
            }
        }
    }
}
