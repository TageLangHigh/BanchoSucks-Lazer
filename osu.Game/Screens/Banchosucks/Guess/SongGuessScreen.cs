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
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Screens.Banchosucks.Clicker;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Banchosucks.Guess
{
    /// <summary>
    /// Banchosucks: the song guesser. A snippet from the preview point of one of the player's own beatmap sets plays,
    /// four titles are offered. The global music pauses for the run and comes back afterwards.
    /// </summary>
    public partial class SongGuessScreen : GuessScreen
    {
        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private MusicController music { get; set; } = null!;

        private bool musicWasPlaying;

        protected override string Kind => "song";
        protected override LocalisableString GameTitle => ClickerStrings.Text("Song Guesser", "Song-Guesser");

        protected override LocalisableString GameDescription => ClickerStrings.Text(
            "A snippet from one of your own beatmaps plays. Which song is it? Your library, your quiz: the more maps you have, the harder it gets.",
            "Ein Schnipsel aus einer deiner eigenen Beatmaps läuft. Welcher Song ist das? Deine Bibliothek, dein Quiz: Je mehr Maps du hast, desto schwerer wird es.");

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

        protected override GuessPrompt CreatePrompt(GuessItem item) => new SongPrompt(beatmaps.GetWorkingBeatmap((BeatmapInfo)item.Payload), Accent);

        protected override void OnRunStarted()
        {
            musicWasPlaying = music.IsPlaying;
            music.Stop();
        }

        protected override void OnRunEnded()
        {
            if (musicWasPlaying)
                music.Play();
        }

        /// <summary>Plays the preview and shows a pulsing equaliser; the cover and the title appear once answered.</summary>
        private partial class SongPrompt : GuessPrompt
        {
            private readonly WorkingBeatmap working;
            private readonly Color4 accent;
            private readonly List<Box> bars = new List<Box>();
            private Track? track;
            private Container coverHost = null!;
            private FillFlowContainer titleFlow = null!;
            private OsuSpriteText statusText = null!;
            private readonly Random rng = new Random();

            public SongPrompt(WorkingBeatmap working, Color4 accent)
            {
                this.working = working;
                this.accent = accent;
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
                        Colour = accent,
                    };
                    bars.Add(bar);
                    barFlow.Add(new Container { Width = 14, RelativeSizeAxes = Axes.Y, Child = bar });
                }

                InternalChildren = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.White.Opacity(0.05f) },
                    coverHost = new Container { RelativeSizeAxes = Axes.Both, Alpha = 0 },
                    barFlow,
                    statusText = new OsuSpriteText
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
                                Text = working.Metadata.Title,
                                Font = OsuFont.GetFont(size: 26, weight: FontWeight.Bold),
                                Shadow = true,
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Text = working.Metadata.Artist,
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

                try
                {
                    track = working.LoadTrack();
                    double preview = working.Metadata.PreviewTime;
                    if (preview < 0) preview = Math.Max(0, track.Length * 0.4);

                    track.RestartPoint = preview;
                    track.Looping = true;
                    track.Seek(preview);
                    track.Start();
                }
                catch (Exception)
                {
                    statusText.Text = ClickerStrings.Text("No audio for this one. Guess anyway!", "Kein Ton für diese. Rate trotzdem!");
                }
            }

            protected override void Update()
            {
                base.Update();

                bool playing = track?.IsRunning == true;

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
                statusText.FadeOut(200);
                titleFlow.FadeIn(300);

                try
                {
                    var texture = working.GetBackground();

                    if (texture != null)
                    {
                        coverHost.Add(new Sprite
                        {
                            RelativeSizeAxes = Axes.Both,
                            FillMode = FillMode.Fill,
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Texture = texture,
                            Colour = Color4Extensions.FromHex("888888"),
                        });
                        coverHost.FadeTo(0.9f, 400);
                    }
                }
                catch (Exception)
                {
                    // no cover, no problem
                }

                foreach (var bar in bars)
                    bar.FadeTo(0.3f, 300);
            }

            protected override void Dispose(bool isDisposing)
            {
                track?.Stop();
                base.Dispose(isDisposing);
            }
        }
    }
}
