// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Screens.Banchosucks.Clicker;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Banchosucks.Guess
{
    /// <summary>
    /// Banchosucks: the song guesser. Library mode plays the preview point of one of the player's own beatmap sets;
    /// popular mode streams the official preview snippet of a set from the server's pool of most played maps, like the
    /// client's beatmap browser does. The global music pauses for the run and comes back afterwards.
    /// </summary>
    public partial class SongGuessScreen : GuessScreen
    {
        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private MusicController music { get; set; } = null!;

        private bool musicWasPlaying;

        protected override string GameKey => "song";
        protected override LocalisableString GameTitle => ClickerStrings.Text("Song Guesser", "Song-Guesser");

        protected override LocalisableString GameDescription => ClickerStrings.Text(
            "A snippet plays, four titles are offered. Your library, your quiz, or the songs everyone on banchosucks plays.",
            "Ein Schnipsel läuft, vier Titel stehen zur Wahl. Deine Bibliothek, dein Quiz, oder die Songs, die auf banchosucks alle spielen.");

        protected override LocalisableString PopularDescription => ClickerStrings.Text("The most played maps on banchosucks, until you miss", "Die meistgespielten Maps auf banchosucks, bis zum ersten Fehler");
        protected override LocalisableString TooFewItemsText => ClickerStrings.Text("You need at least four beatmap sets with different songs.", "Du brauchst mindestens vier Beatmap-Sets mit verschiedenen Songs.");
        protected override IconUsage GameIcon => FontAwesome.Solid.Music;
        protected override Color4 Accent => Colours.Pink;

        protected override List<GuessItem> LoadItems()
        {
            return beatmaps.GetAllUsableBeatmapSets()
                           .Where(s => !s.Protected && s.Beatmaps.Count > 0)
                           .Select(s => (set: s, meta: s.Metadata))
                           .Where(t => !string.IsNullOrWhiteSpace(t.meta.Title))
                           .GroupBy(t => $"{t.meta.Artist} - {t.meta.Title}", StringComparer.OrdinalIgnoreCase)
                           .Select(g => g.First())
                           .Select(t => new GuessItem(t.set.ID.ToString(), $"{t.meta.Artist} – {t.meta.Title}", t.set.Beatmaps[0]))
                           .ToList();
        }

        protected override GuessPrompt CreatePrompt(GuessItem item) => new LocalSongPrompt(beatmaps.GetWorkingBeatmap((BeatmapInfo)item.Payload), Accent);

        protected override GuessPrompt CreatePopularPrompt(GuessPoolItem item) => new OnlineSongPrompt(item, Accent);

        protected override void OnRunStarted(GuessMode mode)
        {
            musicWasPlaying = music.IsPlaying;
            music.Stop();
        }

        protected override void OnRunEnded(GuessMode mode)
        {
            if (musicWasPlaying)
                music.Play();
        }

        /// <summary>Equaliser bars, the "listen" line and, once answered, title, artist and cover. The subclasses supply the sound and the cover.</summary>
        private abstract partial class SongPrompt : GuessPrompt
        {
            protected readonly Color4 Accent;
            protected Container CoverHost = null!;
            protected OsuSpriteText StatusText = null!;

            private readonly List<Box> bars = new List<Box>();
            private FillFlowContainer titleFlow = null!;
            private readonly Random rng = new Random();
            private bool revealed;

            protected abstract string RevealTitle { get; }
            protected abstract string RevealArtist { get; }
            protected abstract bool Playing { get; }

            protected SongPrompt(Color4 accent)
            {
                Accent = accent;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                var barFlow = new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    AutoSizeAxes = Axes.X,
                    Height = 120,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(8, 0),
                };

                for (int i = 0; i < 16; i++)
                {
                    var bar = new Box
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        Width = 14,
                        Height = 20,
                        Colour = Accent,
                    };
                    bars.Add(bar);
                    barFlow.Add(new Container { Width = 14, RelativeSizeAxes = Axes.Y, Child = bar });
                }

                InternalChildren = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.White.Opacity(0.05f) },
                    CoverHost = new Container { RelativeSizeAxes = Axes.Both, Alpha = 0 },
                    barFlow,
                    StatusText = new OsuSpriteText
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Y = 18,
                        Text = ClickerStrings.Text("Listen …", "Hör zu …"),
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

            protected override void Update()
            {
                base.Update();

                bool playing = Playing && !revealed;

                foreach (var bar in bars)
                {
                    if (!bar.Transforms.Any())
                    {
                        float target = playing ? 20 + (float)rng.NextDouble() * 100 : 6;
                        bar.ResizeHeightTo(target, 140 + rng.Next(160), Easing.InOutSine);
                    }
                }
            }

            public override void Reveal(bool correct)
            {
                revealed = true;
                StatusText.FadeOut(200);
                titleFlow.FadeIn(300);
                ShowCover();

                foreach (var bar in bars)
                    bar.FadeTo(0.3f, 300);
            }

            protected abstract void ShowCover();

            protected void PresentCover(Drawable cover)
            {
                cover.Colour = Color4Extensions.FromHex("888888");
                CoverHost.Add(cover);
                CoverHost.FadeTo(0.9f, 400);
            }
        }

        /// <summary>A set from the player's library: its own track from the preview point, its own background as the cover.</summary>
        private partial class LocalSongPrompt : SongPrompt
        {
            private readonly WorkingBeatmap working;
            private Track? track;

            protected override string RevealTitle => working.Metadata.Title;
            protected override string RevealArtist => working.Metadata.Artist;
            protected override bool Playing => track?.IsRunning == true;

            public LocalSongPrompt(WorkingBeatmap working, Color4 accent)
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
                    track = working.LoadTrack();
                }
                catch (Exception)
                {
                    track = null;
                }
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                if (track == null)
                {
                    StatusText.Text = ClickerStrings.Text("No audio for this one. Guess anyway!", "Kein Ton für diese. Rate trotzdem!");
                    return;
                }

                double preview = working.Metadata.PreviewTime;
                if (preview < 0) preview = Math.Max(0, track.Length * 0.4);

                track.RestartPoint = preview;
                track.Looping = true;
                track.Seek(preview);
                track.Start();
            }

            protected override void ShowCover()
            {
                try
                {
                    var texture = working.GetBackground();

                    if (texture != null)
                    {
                        PresentCover(new Sprite
                        {
                            RelativeSizeAxes = Axes.Both,
                            FillMode = FillMode.Fill,
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Texture = texture,
                        });
                    }
                }
                catch (Exception)
                {
                    // no cover, no problem
                }
            }

            protected override void Dispose(bool isDisposing)
            {
                track?.Stop();
                base.Dispose(isDisposing);
            }
        }

        /// <summary>A set from the server's pool: the official preview snippet and cover, fetched by set id like the beatmap browser does.</summary>
        private partial class OnlineSongPrompt : SongPrompt
        {
            [Resolved]
            private PreviewTrackManager previewTracks { get; set; } = null!;

            private readonly GuessPoolItem item;
            private readonly APIBeatmapSet set;
            private PreviewTrack? preview;
            private bool disposing;

            protected override string RevealTitle => item.Title ?? item.Name;
            protected override string RevealArtist => item.Artist ?? string.Empty;
            protected override bool Playing => preview?.IsRunning == true;

            public OnlineSongPrompt(GuessPoolItem item, Color4 accent)
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
                        Cover = $"https://assets.ppy.sh/beatmaps/{item.Id}/covers/cover.jpg",
                        CoverLowRes = $"https://assets.ppy.sh/beatmaps/{item.Id}/covers/cover.jpg",
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                var track = previewTracks.Get(set);
                track.Stopped += () => Schedule(() =>
                {
                    // the official snippet is ten seconds; keep it going for the whole round
                    if (!disposing && preview == track && track.IsLoaded)
                        track.Start();
                });

                LoadComponentAsync(track, loaded =>
                {
                    if (disposing) return;

                    preview = loaded;
                    AddInternal(loaded);
                    loaded.Start();
                });
            }

            protected override void ShowCover()
            {
                LoadComponentAsync(new OnlineBeatmapSetCover(set)
                {
                    RelativeSizeAxes = Axes.Both,
                    FillMode = FillMode.Fill,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                }, cover =>
                {
                    if (!disposing) PresentCover(cover);
                });
            }

            protected override void Dispose(bool isDisposing)
            {
                disposing = true;
                preview?.Stop();
                base.Dispose(isDisposing);
            }
        }
    }
}
