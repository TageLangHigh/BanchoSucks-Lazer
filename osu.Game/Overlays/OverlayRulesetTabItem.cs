// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets;
using osuTK.Graphics;
using osuTK;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Localisation;
using osu.Game.Extensions;
using osu.Game.Graphics.Containers;

namespace osu.Game.Overlays
{
    public partial class OverlayRulesetTabItem : TabItem<RulesetInfo>, IHasTooltip, IHasPopover
    {
        private Color4 accentColour;

        protected virtual Color4 AccentColour
        {
            get => accentColour;
            set
            {
                accentColour = value;
                icon.FadeColour(value, 120, Easing.OutQuint);
            }
        }

        protected override Container<Drawable> Content { get; }

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        private readonly Drawable icon;

        public LocalisableString TooltipText => !Value.HasSpecialRuleset() ? Value.Name : string.Empty;

        private Sample selectSample = null!;

        private readonly OverlayRulesetSelector overlayRulesetSelector;

        public OverlayRulesetTabItem(RulesetInfo value, OverlayRulesetSelector overlayRulesetSelector)
            : base(value)
        {
            this.overlayRulesetSelector = overlayRulesetSelector;

            AutoSizeAxes = Axes.Both;

            AddRangeInternal(new Drawable[]
            {
                Content = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(4, 0),
                    Child = icon = new ConstrainedIconContainer
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(20f),
                        Icon = value.CreateInstance().CreateIcon(),
                    },
                },
                new HoverSounds(HoverSampleSet.TabSelect)
            });

            Enabled.Value = true;
        }

        [BackgroundDependencyLoader]
        private void load(AudioManager audio)
        {
            selectSample = audio.Samples.Get(@"UI/tabselect-select");
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Enabled.BindValueChanged(_ => updateState(), true);
        }

        public override bool PropagatePositionalInputSubTree => Enabled.Value && base.PropagatePositionalInputSubTree;

        protected override bool OnHover(HoverEvent e)
        {
            base.OnHover(e);
            updateState();
            this.ShowPopover();
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            base.OnHoverLost(e);
            updateState();
        }

        protected override void OnActivated() => updateState();

        protected override void OnDeactivated() => updateState();

        protected override void OnActivatedByUser() => selectSample.Play();

        private void updateState()
        {
            AccentColour = Enabled.Value ? getActiveColour() : colourProvider.Foreground1;
        }

        protected bool IsActive => IsHovered || Active.Value;

        protected bool IsCurrentRuleset => Value == null || overlayRulesetSelector.Current.Value == null || !Value.HasSpecialRuleset()
            ? IsActive
            : overlayRulesetSelector.Current.Value.CreateNormalRuleset().Equals(Value);

        private Color4 getActiveColour() => IsActive || IsCurrentRuleset ? Color4.White : colourProvider.Highlight1;

        public Popover? GetPopover()
        {
            return Value.HasSpecialRuleset() ? new RulesetsPopover(Value, overlayRulesetSelector) : null;
        }
    }
}
