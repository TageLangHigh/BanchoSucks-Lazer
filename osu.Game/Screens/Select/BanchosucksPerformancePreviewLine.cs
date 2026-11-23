// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Banchosucks.Clicker;
using osuTK;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// Banchosucks: one compact line in song select's difficulty wedge, "95% 210  98% 245  100% 280 pp": what the selected
    /// difficulty is worth with the selected mods and a full combo at these accuracies.
    /// </summary>
    /// <remarks>
    /// The calculation runs off the update thread and is cancelled when the selection, the ruleset or the mods change.
    /// A value that cannot be calculated, or that is 0, is shown as a dash. See <see cref="BanchosucksPerformancePreview"/>.
    /// </remarks>
    public partial class BanchosucksPerformancePreviewLine : CompositeDrawable, IHasTooltip
    {
        private const string no_value = "–";

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        [Resolved]
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        public LocalisableString TooltipText { get; private set; }

        /// <summary>
        /// What is currently shown, in the order of <see cref="BanchosucksPerformancePreview.ACCURACIES"/>; <c>null</c> entries are dashes.
        /// </summary>
        public IReadOnlyList<double?> DisplayedValues => displayedValues;

        /// <summary>
        /// Whether a calculation for the current selection is still running.
        /// </summary>
        public bool Calculating { get; private set; }

        private readonly double?[] displayedValues = new double?[BanchosucksPerformancePreview.ACCURACIES.Count];

        private FillFlowContainer flow = null!;
        private OsuSpriteText[] valueTexts = null!;

        private ModSettingChangeTracker? settingChangeTracker;
        private CancellationTokenSource? cancellationSource;

        public BanchosucksPerformancePreviewLine()
        {
            AutoSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider? colourProvider, OsuColour colours)
        {
            var labelColour = colourProvider?.Content2 ?? colours.Gray9;

            valueTexts = new OsuSpriteText[BanchosucksPerformancePreview.ACCURACIES.Count];

            InternalChild = flow = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(7f, 0f),
                Alpha = 0,
            };

            for (int i = 0; i < valueTexts.Length; i++)
            {
                flow.Add(new FillFlowContainer
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(3f, 0f),
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            Text = FormattableString.Invariant($"{BanchosucksPerformancePreview.ACCURACIES[i] * 100:0}%"),
                            Font = OsuFont.Style.Caption2,
                            Colour = labelColour,
                        },
                        valueTexts[i] = new OsuSpriteText
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            Text = no_value,
                            Font = OsuFont.Style.Caption1.With(weight: FontWeight.SemiBold),
                        },
                    }
                });
            }

            flow.Add(new OsuSpriteText
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                Text = "pp",
                Font = OsuFont.Style.Caption2,
                Colour = labelColour,
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // the three bindables often change within one frame and are briefly inconsistent in between (mods of another ruleset),
            // so everything is collected and calculated once on the next frame, like the star rating next to this line.
            beatmap.BindValueChanged(_ => Scheduler.AddOnce(updateDisplay));
            ruleset.BindValueChanged(_ => Scheduler.AddOnce(updateDisplay));

            mods.BindValueChanged(m =>
            {
                settingChangeTracker?.Dispose();
                settingChangeTracker = new ModSettingChangeTracker(m.NewValue);
                settingChangeTracker.SettingChanged += _ => Scheduler.AddOnce(updateDisplay);

                Scheduler.AddOnce(updateDisplay);
            }, true);
        }

        private void updateDisplay()
        {
            cancellationSource?.Cancel();
            cancellationSource = null;

            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (beatmap.IsDefault || ruleset.Value == null)
            {
                Calculating = false;
                flow.FadeOut(200, Easing.OutQuint);
                return;
            }

            var working = beatmap.Value;
            var rulesetInfo = ruleset.Value;
            // the player may keep changing mod settings while this is calculated.
            Mod[] modsCopy = mods.Value.Select(m => m.DeepClone()).ToArray();

            TooltipText = createTooltip(rulesetInfo.ShortName);

            cancellationSource = new CancellationTokenSource();
            var token = cancellationSource.Token;

            Calculating = true;
            flow.FadeTo(0.4f, 200, Easing.OutQuint);

            calculate(working, rulesetInfo, modsCopy, token).ContinueWith(task => Schedule(() =>
            {
                if (token.IsCancellationRequested)
                    return;

                Calculating = false;
                show(task.IsCompletedSuccessfully ? task.GetResultSafely() : null);
            }), CancellationToken.None);
        }

        private async Task<BanchosucksPerformancePreview.Value[]?> calculate(WorkingBeatmap working, RulesetInfo rulesetInfo, Mod[] modsCopy, CancellationToken token)
        {
            try
            {
                // the star rating display asks for the same thing, so this is one calculation for both.
                var difficulty = await difficultyCache.GetDifficultyAsync(working.BeatmapInfo, rulesetInfo, modsCopy, token, SongSelect.DIFFICULTY_CALCULATION_DEBOUNCE).ConfigureAwait(false);

                if (difficulty?.DifficultyAttributes == null)
                    return null;

                token.ThrowIfCancellationRequested();

                // not on the difficulty cache's single thread, which every star rating on screen waits for.
                return await Task.Run(() => BanchosucksPerformancePreview.Calculate(working, rulesetInfo.CreateInstance(), modsCopy, difficulty.Value.DifficultyAttributes, token), token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception e)
            {
                // a beatmap that cannot be converted, a broken file, ...: dashes, never a made-up number.
                Logger.Log($"Banchosucks: pp preview failed for {working.BeatmapInfo} ({e.Message})", LoggingTarget.Runtime, LogLevel.Debug);
                return null;
            }
        }

        private void show(BanchosucksPerformancePreview.Value[]? values)
        {
            for (int i = 0; i < valueTexts.Length; i++)
            {
                double? performance = values?[i].Performance;

                displayedValues[i] = performance;
                valueTexts[i].Text = performance == null
                    ? no_value
                    : Math.Round(performance.Value, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);
            }

            flow.FadeIn(200, Easing.OutQuint);
        }

        private static LocalisableString createTooltip(string rulesetShortName)
        {
            (string en, string de) rule;

            switch (BanchosucksFullComboPlay.GetRule(rulesetShortName))
            {
                case BanchosucksFullComboPlay.Rule.GreatAndOk:
                    rule = (" Counted with GREAT and OK (100) only.", " Gerechnet nur mit GREAT und OK (100).");
                    break;

                case BanchosucksFullComboPlay.Rule.PerfectGreatAndGood:
                    rule = ($" Counted without misses, with {BanchosucksFullComboPlay.MANIA_PERFECT_TO_GREAT_RATIO} PERFECT per GREAT and GOOD (200) for the rest.",
                        $" Gerechnet ohne Miss, mit {BanchosucksFullComboPlay.MANIA_PERFECT_TO_GREAT_RATIO} PERFECT je GREAT und GOOD (200) für den Rest.");
                    break;

                case BanchosucksFullComboPlay.Rule.TinyDroplets:
                    rule = (" Counted with every fruit and droplet caught and only tiny droplets missed.",
                        " Gerechnet mit allen Früchten und Tropfen gefangen, verfehlt werden nur winzige Tropfen.");
                    break;

                default:
                    rule = (string.Empty, string.Empty);
                    break;
            }

            return ClickerStrings.Text(
                "pp for a full combo at 95 %, 98 % and 100 % accuracy with the selected mods." + rule.en
                                                                                           + " A dash means there are no pp for it (a convert or mods without pp in osu!mania, or no such full combo exists).",
                "pp für einen Full Combo mit 95 %, 98 % und 100 % Genauigkeit und den gewählten Mods." + rule.de
                + " Ein Strich heißt: Dafür gibt es keine pp (Convert oder Mods ohne pp in osu!mania, oder so ein Full Combo ist nicht möglich).");
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            settingChangeTracker?.Dispose();
            cancellationSource?.Cancel();
        }
    }
}
