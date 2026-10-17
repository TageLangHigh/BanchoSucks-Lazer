// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Screens.Banchosucks.Clicker;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Banchosucks.Tycoon
{
    /// <summary>
    /// The third part of the tycoon screen: side panel extras (help, audio tab, income graph, season titles,
    /// legacy perks text, bulk buying with Shift) and the two sounds the game makes.
    /// </summary>
    public partial class TycoonScreen
    {
        private const double income_sample_interval = 5000;
        private const int income_samples = 60;

        [Resolved]
        private AudioManager audio { get; set; } = null!;

        private readonly List<double> incomeHistory = new List<double>();
        private double lastIncomeSample;
        private string ownerName = string.Empty;
        private Sample? popSampleCache;
        private Sample? collectSampleCache;

        private Sample? popSample => popSampleCache ??= audio.Samples.Get(@"UI/notification-pop");
        private Sample? collectSample => collectSampleCache ??= audio.Samples.Get(@"UI/check-on");

        private static readonly (string en, string de)[] season_titles =
        {
            ("Hobby admin", "Hobby-Admin"),
            ("Server admin", "Server-Admin"),
            ("Hoster", "Hoster"),
            ("Data centre boss", "Rechenzentrum-Chef"),
            ("Cloud mogul", "Cloud-Mogul"),
            ("Legend", "Legende"),
        };

        private static string seasonTitle(int prestige)
        {
            var (en, de) = season_titles[Math.Clamp(prestige, 0, season_titles.Length - 1)];
            return ClickerStrings.Pick(en, de);
        }

        private bool shiftHeld() => GetContainingInputManager()?.CurrentState.Keyboard.ShiftPressed ?? false;

        private void sampleIncome()
        {
            if (Time.Current - lastIncomeSample < income_sample_interval) return;

            lastIncomeSample = Time.Current;
            incomeHistory.Add(TycoonRules.DonationRate(state, boosts.Donation));
            if (incomeHistory.Count > income_samples) incomeHistory.RemoveAt(0);
        }

        /// <summary>Every factor of the income, for the tooltip on the rate.</summary>
        private string incomeBreakdown()
        {
            string m(double v) => ClickerFormat.Multiplier(v);

            return ClickerStrings.Pick(
                $"{n(state.Players)} players x 0.05 · reputation {m(TycoonRules.RufFactor(state.Ruf))} · features {m(TycoonRules.DonationMult(state.Features))} · team {m(TycoonRules.StaffDonationMult(state))} · comfort {m(TycoonRules.ComfortMult(state.Layout))} · legacy {m(TycoonRules.LegacyMult(state.Legacy))} · boost {m(boosts.Donation)}",
                $"{n(state.Players)} Spieler x 0,05 · Ruf {m(TycoonRules.RufFactor(state.Ruf))} · Features {m(TycoonRules.DonationMult(state.Features))} · Team {m(TycoonRules.StaffDonationMult(state))} · Gemütlichkeit {m(TycoonRules.ComfortMult(state.Layout))} · Legacy {m(TycoonRules.LegacyMult(state.Legacy))} · Boost {m(boosts.Donation)}");
        }

        /// <summary>Progress to the next legacy point: the thresholds grow quadratically.</summary>
        private double prestigeProgress(int gain)
        {
            double unit = TycoonRules.PRESTIGE_MIN_TOTAL;
            double current = (double)gain * gain * unit;
            double next = (double)(gain + 1) * (gain + 1) * unit;
            return Math.Clamp((state.RunTotal - current) / (next - current), 0, 1);
        }

        private void pulsePrestige(bool available)
        {
            if (available == prestigePulsing) return;

            prestigePulsing = available;
            prestigeButton.ClearTransforms();

            if (available)
                prestigeButton.ScaleTo(1.03f, 700, Easing.InOutSine).Then().ScaleTo(1, 700, Easing.InOutSine).Loop();
            else
                prestigeButton.ScaleTo(1, 200);
        }

        /// <summary>The confirm text of a season restart: what you keep, which perks come with it.</summary>
        private LocalisableString prestigeSummary(int gain)
        {
            int newLegacy = state.Legacy + gain;
            string fresh = string.Join(", ", TycoonRules.LegacyPerks.Where(p => p.Legacy > state.Legacy && p.Legacy <= newLegacy).Select(p => ClickerStrings.Pick(p.NameEn, p.NameDe)));
            var upcoming = TycoonRules.LegacyPerks.Where(p => p.Legacy > newLegacy).ToList();

            string perks;

            if (fresh.Length > 0)
                perks = ClickerStrings.Pick($" New perks: {fresh}.", $" Neue Perks: {fresh}.");
            else if (upcoming.Count > 0)
                perks = ClickerStrings.Pick($" Next perk at legacy {upcoming[0].Legacy}: {upcoming[0].NameEn}.", $" Nächster Perk bei Legacy {upcoming[0].Legacy}: {upcoming[0].NameDe}.");
            else
                perks = string.Empty;

            return t("Hardware, marketing, features, the team, the plot and the cash start over. You keep +{0} legacy: +{1} % on donations and players for ever.{2}",
                "Hardware, Marketing, Features, Team, Bauplatz und Kasse beginnen von vorn. Du behältst +{0} Legacy: dauerhaft +{1} % auf Spenden und Zulauf.{2}", gain, gain * 5, perks);
        }

        private void toggleHelp() => helpOverlay.Toggle();

        /// <summary>A sprite text with a tooltip, for the income line.</summary>
        private partial class TooltipSpriteText : OsuSpriteText, IHasTooltip
        {
            public LocalisableString TooltipText { get; set; }
        }

        /// <summary>The rules of the game in a few lines, over the plot; a click closes it.</summary>
        private partial class HelpOverlay : OsuClickableContainer
        {
            private bool shown;

            public HelpOverlay()
            {
                RelativeSizeAxes = Axes.Both;
                Alpha = 0;
                Action = Toggle;

                var text = new OsuTextFlowContainer(s => s.Font = OsuFont.GetFont(size: 14))
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    ParagraphSpacing = 0.5f,
                };

                foreach (var (en, de) in new[]
                         {
                             ("Marketing buildings bring players, players donate. Donations pile up in the collector box until you click it (or press C / Enter).",
                                 "Marketing-Gebäude bringen Spieler, Spieler spenden. Spenden sammeln sich in der Spendenbox, bis du sie anklickst (oder C / Enter drückst)."),
                             ("Hardware decides how many players fit. Too many players means lag: reputation falls, players leave. Hardware first, then marketing; every marketing kind needs a hardware level.",
                                 "Hardware entscheidet, wie viele Spieler Platz haben. Zu viele Spieler heißt Lag: Ruf fällt, Spieler gehen. Erst Hardware, dann Marketing; jede Marketing-Art braucht eine Hardware-Stufe."),
                             ("Green pads you can afford, red ones not yet. Hold Shift to buy ten marketing buildings at once. Hover a pad for the details.",
                                 "Grüne Pads kannst du dir leisten, rote noch nicht. Mit gehaltener Shift-Taste kaufst du zehn Marketing-Gebäude auf einmal. Fahr über ein Pad für die Details."),
                             ("Features make events harmless or give boosts. The team gives small boosts per head. Decorations add comfort (up to +15 % donations); right click sells them.",
                                 "Features machen Ereignisse harmlos oder bringen Boni. Das Team gibt kleine Boni je Kopf. Deko bringt Gemütlichkeit (bis +15 % Spenden); Rechtsklick verkauft sie."),
                             ("Quests guide the run and pay rewards. Supply drops land every few minutes: click them in time.",
                                 "Aufträge führen durch den Lauf und belohnen. Lieferungen landen alle paar Minuten: rechtzeitig anklicken."),
                             ("From a million donations in a season you can start a new one. Legacy points give +5 % each for ever and unlock perks: quick start, discounts, regulars, night shift.",
                                 "Ab einer Million Spenden in einer Saison kannst du eine neue starten. Legacy-Punkte bringen dauerhaft +5 % je Punkt und schalten Perks frei: Schnellstart, Rabatte, Stammspieler, Nachtschicht."),
                             ("Space or the Discord booth: post, players join at once. The Audio tab sets the music and effect volume.",
                                 "Leertaste oder die Discord-Bude: posten, Spieler kommen sofort. Der Audio-Tab regelt Musik und Effekte."),
                         })
                {
                    text.AddParagraph(ClickerStrings.Text("• " + en, "• " + de));
                }

                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black.Opacity(0.7f) },
                    new Container
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Width = 640,
                        AutoSizeAxes = Axes.Y,
                        Masking = true,
                        CornerRadius = 14,
                        Children = new Drawable[]
                        {
                            new Box { RelativeSizeAxes = Axes.Both, Colour = Color4Extensions.FromHex("161a2b") },
                            new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Padding = new MarginPadding(22),
                                Spacing = new Vector2(0, 12),
                                Children = new Drawable[]
                                {
                                    new OsuSpriteText { Text = ClickerStrings.Text("How to play", "Wie spielt man?"), Font = OsuFont.GetFont(size: 22, weight: FontWeight.Bold) },
                                    text,
                                    new OsuSpriteText { Text = ClickerStrings.Text("Click anywhere to close", "Klick irgendwo zum Schließen"), Font = OsuFont.GetFont(size: 11), Colour = Color4.White.Opacity(0.6f) },
                                },
                            },
                        },
                    },
                };
            }

            public void Toggle()
            {
                shown = !shown;
                this.FadeTo(shown ? 1 : 0, 200);
            }
        }

        /// <summary>Donations per second over the last five minutes, one bar per five seconds.</summary>
        private partial class IncomeGraph : CompositeDrawable
        {
            public IncomeGraph(IReadOnlyList<double> values, OsuColour colours)
            {
                RelativeSizeAxes = Axes.X;
                Height = 56;
                Masking = true;
                CornerRadius = 6;

                double max = values.Count > 0 ? Math.Max(1e-9, values.Max()) : 1;
                var bars = new Container { RelativeSizeAxes = Axes.Both, Padding = new MarginPadding { Top = 18, Bottom = 4, Horizontal = 6 } };

                InternalChildren = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.White.Opacity(0.05f) },
                    new OsuSpriteText
                    {
                        Position = new Vector2(8, 3),
                        Text = ClickerStrings.Text("Donations per second, last five minutes", "Spenden pro Sekunde, letzte fünf Minuten"),
                        Font = OsuFont.GetFont(size: 10, weight: FontWeight.Bold),
                        Colour = colours.Gray9,
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Position = new Vector2(-8, 3),
                        Text = values.Count > 0 ? $"max {ClickerFormat.Number(values.Max())}/s" : string.Empty,
                        Font = OsuFont.GetFont(size: 10),
                        Colour = colours.Gray9,
                    },
                    bars,
                };

                for (int i = 0; i < values.Count; i++)
                {
                    bars.Add(new Box
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        RelativeSizeAxes = Axes.Both,
                        RelativePositionAxes = Axes.X,
                        X = (float)(income_samples - values.Count + i) / income_samples,
                        Width = 0.8f / income_samples,
                        Height = (float)Math.Max(0.04, values[i] / max),
                        Colour = colours.Yellow.Opacity(0.85f),
                    });
                }
            }
        }

        /// <summary>Master, music and effect volume (the osu! volumes, they apply everywhere) and the music controls.</summary>
        private partial class AudioPanel : CompositeDrawable
        {
            [Resolved(canBeNull: true)]
            private MusicController? music { get; set; }

            private IBindable<WorkingBeatmap> beatmap = null!;
            private OsuSpriteText trackText = null!;

            public AudioPanel()
            {
                RelativeSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours, AudioManager audio, IBindable<WorkingBeatmap> beatmap)
            {
                this.beatmap = beatmap.GetBoundCopy();

                Drawable slider(LocalisableString label, BindableNumber<double> volume) => new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 3),
                    Children = new Drawable[]
                    {
                        new OsuSpriteText { Text = label, Font = OsuFont.GetFont(size: 11, weight: FontWeight.Bold), Colour = colours.Gray9 },
                        new RoundedSliderBar<double>
                        {
                            RelativeSizeAxes = Axes.X,
                            Current = volume,
                            KeyboardStep = 0.01f,
                            DisplayAsPercentage = true,
                        },
                    },
                };

                InternalChild = new OsuScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    ScrollbarVisible = true,
                    Child = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 10),
                        Padding = new MarginPadding { Right = 6, Top = 2 },
                        Children = new Drawable[]
                        {
                            slider(ClickerStrings.Text("Master volume", "Gesamtlautstärke"), audio.Volume),
                            slider(ClickerStrings.Text("Music", "Musik"), audio.VolumeTrack),
                            slider(ClickerStrings.Text("Effects", "Effekte"), audio.VolumeSample),
                            new OsuSpriteText
                            {
                                Text = ClickerStrings.Text("Now playing", "Läuft gerade"),
                                Font = OsuFont.GetFont(size: 11, weight: FontWeight.Bold),
                                Colour = colours.Gray9,
                                Margin = new MarginPadding { Top = 4 },
                            },
                            trackText = new TruncatingSpriteText { RelativeSizeAxes = Axes.X, Font = OsuFont.GetFont(size: 13, weight: FontWeight.SemiBold) },
                            new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(6, 0),
                                Children = new Drawable[]
                                {
                                    new ClickerPillButton(ClickerStrings.Text("Previous", "Zurück"), colours.Blue, () => music?.PreviousTrack(), FontAwesome.Solid.StepBackward),
                                    new ClickerPillButton(ClickerStrings.Text("Play / pause", "Play / Pause"), colours.Blue, () => music?.TogglePause(), FontAwesome.Solid.Pause),
                                    new ClickerPillButton(ClickerStrings.Text("Next", "Weiter"), colours.Blue, () => music?.NextTrack(), FontAwesome.Solid.StepForward),
                                },
                            },
                            new OsuSpriteText
                            {
                                Text = ClickerStrings.Text("These are the osu! volumes; they apply to the whole game.", "Das sind die osu!-Lautstärken; sie gelten im ganzen Spiel."),
                                Font = OsuFont.GetFont(size: 11),
                                Colour = colours.Gray9,
                                Margin = new MarginPadding { Top = 4 },
                            },
                        },
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                beatmap.BindValueChanged(b =>
                {
                    var metadata = b.NewValue.Metadata;
                    trackText.Text = string.IsNullOrEmpty(metadata.Title) ? ClickerStrings.Text("No track", "Kein Titel") : $"{metadata.Artist} – {metadata.Title}";
                }, true);
            }
        }
    }
}
