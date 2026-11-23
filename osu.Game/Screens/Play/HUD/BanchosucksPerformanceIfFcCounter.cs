// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Banchosucks.Clicker;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Screens.Play.HUD
{
    /// <summary>
    /// Banchosucks: "pp if FC". Shows what the run so far would be worth had nothing broken the combo, next to the
    /// normal pp counter. A HUD component of its own: the player adds it in the skin layout editor, nothing changes
    /// for anyone who does not.
    /// </summary>
    /// <remarks>
    /// It is the normal counter with one extra step: before the ruleset's performance calculator rates the score,
    /// <see cref="BanchosucksFullComboProjection"/> replaces the combo breaks (the rule is written down there).
    /// Difficulty attributes, calculator and the moment of refreshing (once per judgement) are those of
    /// <see cref="PerformancePointsCounter"/>, so osu!mania uses the Banchosucks formula like everything else does.
    /// While the run is a full combo both counters show the same number.
    /// </remarks>
    public partial class BanchosucksPerformanceIfFcCounter : PerformancePointsCounter, ISerialisableDrawable
    {
        protected override bool IsRollingProportional => true;

        protected override double RollingDuration => 500;

        private const float alpha_when_invalid = 0.3f;

        [SettingSource("Beschriftung zeigen / Show label", "Zeigt „bei FC“ hinter der Zahl. / Shows \"if FC\" behind the number.")]
        public Bindable<bool> ShowLabel { get; } = new BindableBool(true);

        /// <summary>
        /// The last projection that was rated (tests).
        /// </summary>
        public BanchosucksFullComboProjection.Projection? LastProjection { get; private set; }

        private TextComponent text = null!;

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            Colour = colours.GreenLighter;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            ShowLabel.BindValueChanged(show => text.ShowLabel = show.NewValue, true);
        }

        protected override void AdjustScoreBeforeCalculation(ScoreInfo score, ScoreProcessor processor)
            => LastProjection = BanchosucksFullComboProjection.ApplyTo(score, processor);

        public override bool IsValid
        {
            get => base.IsValid;
            set
            {
                if (value == IsValid)
                    return;

                base.IsValid = value;
                DrawableCount.FadeTo(value ? 1 : alpha_when_invalid, 1000, Easing.OutQuint);
            }
        }

        protected override LocalisableString FormatCount(int count) => count.ToString(@"D");

        protected override IHasText CreateText() => text = new TextComponent
        {
            Alpha = alpha_when_invalid
        };

        private partial class TextComponent : CompositeDrawable, IHasText
        {
            public LocalisableString Text
            {
                get => number.Text;
                set => number.Text = value;
            }

            public bool ShowLabel
            {
                set => suffix.Text = value ? ClickerStrings.Text("pp if FC", "pp bei FC") : (LocalisableString)"pp";
            }

            private readonly OsuSpriteText number;
            private readonly OsuSpriteText suffix;

            public TextComponent()
            {
                AutoSizeAxes = Axes.Both;

                InternalChild = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Spacing = new Vector2(2),
                    Children = new Drawable[]
                    {
                        number = new OsuSpriteText
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            Font = OsuFont.Numeric.With(size: 16, fixedWidth: true)
                        },
                        suffix = new OsuSpriteText
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            Font = OsuFont.GetFont(size: 11, weight: FontWeight.Bold),
                            Padding = new MarginPadding { Bottom = 1.5f }, // align baseline better
                        }
                    }
                };
            }
        }
    }
}
