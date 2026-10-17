// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Screens.Banchosucks.Clicker;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Banchosucks.Guess
{
    /// <summary>
    /// Banchosucks: the background guesser. The background of a beatmap set is shown, zoomed in at first and slowly
    /// zooming out, and four titles are offered. Library mode uses the player's own sets, popular mode the server's pool
    /// of most played maps with the official cover image. Same rounds, points and boards as the song guesser
    /// (kinds "background" and "background_endless").
    /// </summary>
    public partial class BackgroundGuessScreen : GuessScreen
    {
        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        protected override string GameKey => "background";
        protected override LocalisableString GameTitle => ClickerStrings.Text("Background Guesser", "Hintergrund-Guesser");

        protected override LocalisableString GameDescription => ClickerStrings.Text(
            "A background appears and slowly zooms out, four titles are offered. Your library, your quiz, or the maps everyone on banchosucks plays.",
            "Ein Hintergrund erscheint und zoomt langsam heraus, vier Titel stehen zur Wahl. Deine Bibliothek, dein Quiz, oder die Maps, die auf banchosucks alle spielen.");

        protected override LocalisableString PopularDescription => ClickerStrings.Text("The most played maps on banchosucks, until you miss", "Die meistgespielten Maps auf banchosucks, bis zum ersten Fehler");
        protected override LocalisableString TooFewItemsText => ClickerStrings.Text("You need at least four beatmap sets with a background and different songs.", "Du brauchst mindestens vier Beatmap-Sets mit Hintergrund und verschiedenen Songs.");
        protected override IconUsage GameIcon => FontAwesome.Solid.Image;
        protected override Color4 Accent => Colours.Blue;

        protected override List<GuessItem> LoadItems()
        {
            return beatmaps.GetAllUsableBeatmapSets()
                           .Where(s => !s.Protected && s.Beatmaps.Count > 0)
                           .Select(s => (set: s, meta: s.Metadata))
                           .Where(t => !string.IsNullOrWhiteSpace(t.meta.Title) && !string.IsNullOrWhiteSpace(t.meta.BackgroundFile))
                           .GroupBy(t => $"{t.meta.Artist} - {t.meta.Title}", StringComparer.OrdinalIgnoreCase)
                           .Select(g => g.First())
                           .Select(t => new GuessItem(t.set.ID.ToString(), $"{t.meta.Artist} – {t.meta.Title}", t.set.Beatmaps[0]))
                           .ToList();
        }

        protected override GuessPrompt CreatePrompt(GuessItem item) => new LocalBackgroundPrompt(beatmaps.GetWorkingBeatmap((BeatmapInfo)item.Payload), Accent);

        protected override GuessPrompt CreatePopularPrompt(GuessPoolItem item) => new OnlineBackgroundPrompt(item, Accent);

        /// <summary>The picture, zoomed in at first and zooming out over the round; title and artist appear once answered.</summary>
        private abstract partial class BackgroundPrompt : GuessPrompt
        {
            private const double zoom_duration = 12_000;

            protected readonly Color4 Accent;
            protected Container ImageHost = null!;
            protected OsuSpriteText StatusText = null!;
            private FillFlowContainer titleFlow = null!;

            protected abstract string RevealTitle { get; }
            protected abstract string RevealArtist { get; }

            protected BackgroundPrompt(Color4 accent)
            {
                Accent = accent;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                InternalChildren = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.White.Opacity(0.05f) },
                    ImageHost = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Masking = true,
                        Scale = new Vector2(1.7f),
                    },
                    StatusText = new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = ClickerStrings.Text("Loading the picture …", "Bild wird geladen …"),
                        Font = OsuFont.GetFont(size: 18, weight: FontWeight.SemiBold),
                        Colour = Color4.White.Opacity(0.8f),
                    },
                    titleFlow = new FillFlowContainer
                    {
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.BottomCentre,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Y = -18,
                        Alpha = 0,
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Text = RevealTitle,
                                Font = OsuFont.GetFont(size: 26, weight: FontWeight.Bold),
                                Shadow = true,
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Text = RevealArtist,
                                Font = OsuFont.GetFont(size: 18),
                                Shadow = true,
                            },
                        },
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                // the picture starts close up and zooms out over most of the round: the faster you know it, the harder it was
                ImageHost.ScaleTo(1, zoom_duration, Easing.OutSine);
            }

            /// <summary>The picture is there: show it, drop the status line.</summary>
            protected void Present(Sprite image)
            {
                image.RelativeSizeAxes = Axes.Both;
                image.Anchor = Anchor.Centre;
                image.Origin = Anchor.Centre;
                image.FillMode = FillMode.Fill;
                image.Alpha = 0;
                ImageHost.Add(image);
                image.FadeIn(300);
                StatusText.FadeOut(300);
            }

            protected void NoImage() => StatusText.Text = ClickerStrings.Text("No picture for this one. Guess anyway!", "Kein Bild für diese. Rate trotzdem!");

            public override void Reveal(bool correct)
            {
                ImageHost.ClearTransforms();
                ImageHost.ScaleTo(1, 400, Easing.OutQuint);
                ImageHost.FadeColour(Color4Extensions.FromHex("888888"), 400);
                StatusText.FadeOut(200);
                titleFlow.FadeIn(300);
            }
        }

        /// <summary>A set from the player's library: its own background file.</summary>
        private partial class LocalBackgroundPrompt : BackgroundPrompt
        {
            private readonly WorkingBeatmap working;
            private Texture? texture;

            protected override string RevealTitle => working.Metadata.Title;
            protected override string RevealArtist => working.Metadata.Artist;

            public LocalBackgroundPrompt(WorkingBeatmap working, Color4 accent)
                : base(accent)
            {
                this.working = working;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                try
                {
                    // loading happens off the update thread (the screen loads prompts asynchronously)
                    texture = working.GetBackground();
                }
                catch (Exception)
                {
                    texture = null;
                }
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                if (texture == null)
                {
                    NoImage();
                    return;
                }

                Present(new Sprite { Texture = texture });
            }
        }

        /// <summary>A set from the server's pool: the official cover image, fetched by set id like the beatmap browser does.</summary>
        private partial class OnlineBackgroundPrompt : BackgroundPrompt
        {
            private readonly GuessPoolItem item;
            private readonly APIBeatmapSet set;
            private bool disposing;

            protected override string RevealTitle => item.Title ?? item.Name;
            protected override string RevealArtist => item.Artist ?? string.Empty;

            public OnlineBackgroundPrompt(GuessPoolItem item, Color4 accent)
                : base(accent)
            {
                this.item = item;
                set = new APIBeatmapSet
                {
                    OnlineID = (int)item.Id,
                    Title = item.Title ?? item.Name,
                    Artist = item.Artist ?? string.Empty,
                    Covers = new BeatmapSetOnlineCovers
                    {
                        Cover = $"https://assets.ppy.sh/beatmaps/{item.Id}/covers/cover@2x.jpg",
                        CoverLowRes = $"https://assets.ppy.sh/beatmaps/{item.Id}/covers/cover.jpg",
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                LoadComponentAsync(new OnlineBeatmapSetCover(set), cover =>
                {
                    if (!disposing) Present(cover);
                });
            }

            protected override void Dispose(bool isDisposing)
            {
                disposing = true;
                base.Dispose(isDisposing);
            }
        }
    }
}
