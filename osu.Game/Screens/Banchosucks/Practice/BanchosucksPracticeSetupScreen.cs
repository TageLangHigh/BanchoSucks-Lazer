// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Screens;
using osu.Framework.Threading;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Screens.Banchosucks.Clicker;
using osu.Game.Screens.Footer;
using osu.Game.Screens.Play;
using osuTK;

namespace osu.Game.Screens.Banchosucks.Practice
{
    /// <summary>
    /// Banchosucks: picks the section (start, end) and the speed for a practice run of the selected difficulty, then
    /// starts <see cref="BanchosucksPracticePlayer"/>. Leaving the player comes back here, so the section can be adjusted.
    /// </summary>
    /// <remarks>
    /// Opened from song select (beatmap context menu and the options popover). "Retry from the miss" on the fail
    /// overlay opens it with the section already set and starts right away.
    /// </remarks>
    public partial class BanchosucksPracticeSetupScreen : ScreenWithBeatmapBackground
    {
        private const double slider_precision = 100;
        private const double preview_seek_debounce = 250;

        // The beatmap must not change underneath the sliders. The lease also gives back the mods on exit.
        public override bool DisallowExternalBeatmapRulesetChanges => true;

        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Purple);

        [Resolved]
        private MusicController? musicController { get; set; }

        private readonly double? failTime;
        private bool startImmediately;

        public readonly BindableDouble StartTime = new BindableDouble { Precision = slider_precision };
        public readonly BindableDouble EndTime = new BindableDouble { Precision = slider_precision };

        public readonly BindableDouble Rate = new BindableDouble(BanchosucksPracticeSection.MAX_RATE)
        {
            MinValue = BanchosucksPracticeSection.MIN_RATE,
            MaxValue = BanchosucksPracticeSection.MAX_RATE,
            Precision = 0.05,
        };

        /// <summary>
        /// Time of the first object and end time of the last one.
        /// </summary>
        public double MapStart { get; private set; }

        public double MapEnd { get; private set; }

        /// <summary>
        /// False if the difficulty has no objects (or could not be read); the start button is disabled then.
        /// </summary>
        public bool CanStart { get; private set; }

        /// <summary>
        /// The section of the run started last from this screen.
        /// </summary>
        public BanchosucksPracticeSection? LastSection { get; private set; }

        private double minLength;
        private IReadOnlyList<Mod>? modsBeforePractice;
        private ScheduledDelegate? previewSeek;

        private OsuSpriteText startLabel = null!;
        private OsuSpriteText endLabel = null!;
        private OsuSpriteText rateLabel = null!;
        private RoundedButton startButton = null!;

        public BanchosucksPracticeSetupScreen()
        {
            // same footer as song select and its player loader, so the bar does not jump
            FooterContent.Value = ScreenFooterContent.Argon();
        }

        /// <summary>
        /// "Retry from the miss": the section runs from shortly before <paramref name="failTime"/> to the end of the map and starts right away.
        /// </summary>
        public static BanchosucksPracticeSetupScreen FromFail(double failTime) => new BanchosucksPracticeSetupScreen(failTime);

        private BanchosucksPracticeSetupScreen(double failTime)
            : this()
        {
            this.failTime = failTime;
            startImmediately = true;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            readMapBounds();

            StartTime.MinValue = EndTime.MinValue = MapStart;
            StartTime.MaxValue = EndTime.MaxValue = MapEnd;

            var initial = failTime != null
                ? BanchosucksPracticeSection.FromFail(failTime.Value, MapStart, MapEnd)
                : BanchosucksPracticeSection.Create(MapStart, MapEnd, BanchosucksPracticeSection.MAX_RATE, MapStart, MapEnd);

            StartTime.Value = initial.StartTime;
            EndTime.Value = initial.EndTime;
            Rate.Value = initial.Rate;

            var metadata = Beatmap.Value.BeatmapInfo.Metadata;

            InternalChild = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Width = 620,
                AutoSizeAxes = Axes.Y,
                Masking = true,
                CornerRadius = 12,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background5,
                        Alpha = 0.92f,
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding(28),
                        Spacing = new Vector2(0, 10),
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Text = ClickerStrings.Text("Practise a section", "Abschnitt üben"),
                                Font = OsuFont.GetFont(size: 30, weight: FontWeight.Bold),
                            },
                            new TruncatingSpriteText
                            {
                                RelativeSizeAxes = Axes.X,
                                Text = $"{metadata.Artist} - {metadata.Title} [{Beatmap.Value.BeatmapInfo.DifficultyName}]",
                                Font = OsuFont.GetFont(size: 18),
                                Colour = colourProvider.Content2,
                            },
                            new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 15))
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Colour = colourProvider.Content2,
                                Text = ClickerStrings.Text(
                                    "The section repeats until you leave through the pause menu. A fail starts it again. Nothing is saved or submitted.",
                                    "Der Abschnitt wiederholt sich, bis du über das Pausenmenü gehst. Ein Fail startet ihn neu. Nichts wird gespeichert oder übertragen."),
                            },
                            startLabel = createLabel(),
                            new TimeSliderBar
                            {
                                RelativeSizeAxes = Axes.X,
                                Current = StartTime,
                                KeyboardStep = 1000,
                            },
                            endLabel = createLabel(),
                            new TimeSliderBar
                            {
                                RelativeSizeAxes = Axes.X,
                                Current = EndTime,
                                KeyboardStep = 1000,
                            },
                            rateLabel = createLabel(),
                            new RoundedSliderBar<double>
                            {
                                RelativeSizeAxes = Axes.X,
                                Current = Rate,
                                KeyboardStep = 0.05f,
                                DisplayAsPercentage = true,
                            },
                            startButton = new RoundedButton
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Margin = new MarginPadding { Top = 10 },
                                Width = 240,
                                Height = 44,
                                Text = CanStart
                                    ? ClickerStrings.Text("Start practice", "Übung starten")
                                    : ClickerStrings.Text("This difficulty has no objects", "Diese Schwierigkeit hat keine Objekte"),
                                BackgroundColour = colours.Green,
                                Action = Start,
                            },
                        }
                    }
                }
            };

            startButton.Enabled.Value = CanStart;
        }

        private static OsuSpriteText createLabel() => new OsuSpriteText
        {
            Margin = new MarginPadding { Top = 6 },
            Font = OsuFont.GetFont(size: 18, weight: FontWeight.SemiBold),
        };

        /// <summary>
        /// Reads where the objects of the difficulty begin and end. Never throws: an exception while a pushed screen
        /// loads would leave it stuck without a word in the log.
        /// </summary>
        private void readMapBounds()
        {
            try
            {
                // the same conversion the player will run, so the bounds match what it plays (a converted map can end elsewhere)
                var hitObjects = Beatmap.Value.GetPlayableBeatmap(Ruleset.Value, Mods.Value).HitObjects;

                if (hitObjects.Count > 0)
                {
                    MapStart = hitObjects.Min(h => h.StartTime);
                    MapEnd = hitObjects.Max(h => h.GetEndTime());
                    CanStart = true;
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Practice setup could not read the beatmap.");
                CanStart = false;
            }

            if (!CanStart)
            {
                MapStart = 0;
                MapEnd = 0;
            }

            // slider values are multiples of the precision, so the ends are widened to it and clamped again when the run starts
            MapStart = Math.Floor(MapStart / slider_precision) * slider_precision;
            MapEnd = Math.Max(MapStart, Math.Ceiling(MapEnd / slider_precision) * slider_precision);

            minLength = Math.Min(BanchosucksPracticeSection.MIN_LENGTH, MapEnd - MapStart);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            StartTime.BindValueChanged(_ =>
            {
                // the start pushes the end ahead of itself, and stops where the end cannot go any further
                if (EndTime.Value < StartTime.Value + minLength)
                    EndTime.Value = Math.Min(MapEnd, StartTime.Value + minLength);

                if (StartTime.Value > EndTime.Value - minLength)
                    StartTime.Value = Math.Max(MapStart, EndTime.Value - minLength);

                updateLabels();
                queuePreviewSeek();
            });

            EndTime.BindValueChanged(_ =>
            {
                if (StartTime.Value > EndTime.Value - minLength)
                    StartTime.Value = Math.Max(MapStart, EndTime.Value - minLength);

                if (EndTime.Value < StartTime.Value + minLength)
                    EndTime.Value = Math.Min(MapEnd, StartTime.Value + minLength);

                updateLabels();
            });

            Rate.BindValueChanged(_ => updateLabels());

            updateLabels();
        }

        private void updateLabels()
        {
            startLabel.Text = ClickerStrings.Text("Start: {0}", "Start: {0}", BanchosucksPracticeSection.FormatTime(StartTime.Value));
            endLabel.Text = ClickerStrings.Text("End: {0} (length {1})", "Ende: {0} (Länge {1})",
                BanchosucksPracticeSection.FormatTime(EndTime.Value),
                BanchosucksPracticeSection.FormatTime(EndTime.Value - StartTime.Value));
            rateLabel.Text = ClickerStrings.Text("Speed: {0}%", "Tempo: {0} %", (int)Math.Round(Rate.Value * 100));
        }

        /// <summary>
        /// Lets the player hear where the start point is: the music jumps there shortly after the slider stops moving.
        /// </summary>
        private void queuePreviewSeek()
        {
            if (!this.IsCurrentScreen())
                return;

            previewSeek?.Cancel();
            previewSeek = Scheduler.AddDelayed(() =>
            {
                // straight on the track: MusicController refuses to seek while a screen holds the beatmap lease
                if (this.IsCurrentScreen())
                    Beatmap.Value.Track.Seek(StartTime.Value);
            }, preview_seek_debounce);
        }

        /// <summary>
        /// Starts the practice run with the current section.
        /// </summary>
        public void Start()
        {
            if (!CanStart || !this.IsCurrentScreen())
                return;

            previewSeek?.Cancel();

            LastSection = BanchosucksPracticeSection.Create(StartTime.Value, EndTime.Value, Rate.Value, MapStart, MapEnd);

            // Autoplay and Cinema would make the practice player record a "play" with a mod it does not honour; take them out for the run.
            modsBeforePractice = Mods.Value;
            if (Mods.Value.Any(m => m is ICreateReplayData))
                Mods.Value = Mods.Value.Where(m => m is not ICreateReplayData).ToArray();

            var section = LastSection;
            this.Push(new PracticePlayerLoader(() => new BanchosucksPracticePlayer(section)));
        }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);

            this.FadeInFromZero(250, Easing.OutQuint);
            dimBackground();

            if (startImmediately)
            {
                startImmediately = false;
                Schedule(Start);
            }
        }

        public override void OnResuming(ScreenTransitionEvent e)
        {
            base.OnResuming(e);

            restoreMods();

            this.FadeIn(250, Easing.OutQuint);
            dimBackground();

            // gameplay stopped the track; play it again from the start point so the screen is not silent
            Beatmap.Value.Track.Seek(StartTime.Value);
            if (musicController?.UserPauseRequested != true)
                musicController?.Play();
        }

        public override void OnSuspending(ScreenTransitionEvent e)
        {
            base.OnSuspending(e);
            this.FadeOut(200);
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            previewSeek?.Cancel();
            restoreMods();

            this.FadeOut(200);
            return base.OnExiting(e);
        }

        private void restoreMods()
        {
            if (modsBeforePractice == null)
                return;

            Mods.Value = modsBeforePractice;
            modsBeforePractice = null;
        }

        private void dimBackground() => ApplyToBackground(b =>
        {
            b.IgnoreUserSettings.Value = true;
            b.BlurAmount.Value = 15;
            b.FadeColour(OsuColour.Gray(0.5f), 250);
        });

        private partial class TimeSliderBar : RoundedSliderBar<double>
        {
            protected override LocalisableString GetTooltipText(double value) => BanchosucksPracticeSection.FormatTime(value);
        }

        private partial class PracticePlayerLoader : PlayerLoader
        {
            public PracticePlayerLoader(Func<Player> createPlayer)
                : base(createPlayer)
            {
                FooterContent.Value = ScreenFooterContent.Argon();
            }
        }
    }
}
