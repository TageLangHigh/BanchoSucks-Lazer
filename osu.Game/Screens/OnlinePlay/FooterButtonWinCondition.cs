// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Online.Multiplayer;
using osu.Game.Overlays;
using osu.Game.Screens.Footer;
using osu.Game.Screens.Select;
using osuTK;

namespace osu.Game.Screens.OnlinePlay
{
    public partial class FooterButtonWinCondition : ScreenFooterButton, IHasPopover
    {
        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        public readonly Bindable<WinCondition> CurrentCondition = new Bindable<WinCondition>();
        public readonly Bindable<bool> Freestyle = new Bindable<bool>();

        private List<WinCondition> availableConditions => Freestyle.Value
            ? [WinCondition.Score, WinCondition.Accuracy]
            : Enum.GetValues<WinCondition>().ToList();

        private Container conditionWedge = null!;
        private OsuSpriteText conditionText = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            Text = OnlinePlayStrings.FooterButtonWinCondition;
            Icon = FontAwesome.Solid.Medal;
            AccentColour = colours.Orange1;

            Action = () =>
            {
                if (this.FindClosestParent<PopoverContainer>()?.CurrentTarget == this)
                    this.HidePopover();
                else
                    this.ShowPopover();
            };

            Add(conditionWedge = new InputBlockingContainer
            {
                Y = -5f,
                Depth = float.MaxValue,
                Origin = Anchor.BottomLeft,
                Shear = OsuGame.SHEAR,
                CornerRadius = CORNER_RADIUS,
                Size = new Vector2(BUTTON_WIDTH, FooterButtonMods.BAR_HEIGHT),
                Masking = true,
                EdgeEffect = new EdgeEffectParameters
                {
                    Type = EdgeEffectType.Shadow,
                    Radius = 4,
                    // Figma says 50% opacity, but it does not match up visually if taken at face value, and looks bad.
                    Colour = Colour4.Black.Opacity(0.25f),
                    Offset = new Vector2(0, 2),
                },
                Alpha = 0,
                Children = new Drawable[]
                {
                    new Box
                    {
                        Colour = colourProvider.Background4,
                        RelativeSizeAxes = Axes.Both,
                    },
                    conditionText = new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Font = OsuFont.MapleMono.With(size: 14f, weight: FontWeight.Bold),
                        Shear = -OsuGame.SHEAR,
                    },
                },
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            CurrentCondition.BindValueChanged(c =>
            {
                conditionWedge.ClearTransforms();

                conditionText.Text = c.NewValue.GetLocalisableDescription();
                conditionText.ScaleTo(1.25f, 100, Easing.InExpo).Then().ScaleTo(1, 500, Easing.OutExpo);

                if (c.NewValue is not WinCondition.Score)
                    conditionWedge.FadeIn(300, Easing.OutExpo);
                else
                    conditionWedge.Delay(600).FadeOut(300, Easing.OutExpo);
            }, true);
        }

        protected override bool OnScroll(ScrollEvent e)
        {
            this.HidePopover();

            // Scroll up => previous, vice versa
            int currentIndex = availableConditions.IndexOf(CurrentCondition.Value);
            int nextIndex = (currentIndex - Math.Sign(e.ScrollDelta.Y) + availableConditions.Count) % availableConditions.Count;

            CurrentCondition.Value = availableConditions[nextIndex];

            return true;
        }

        public Framework.Graphics.UserInterface.Popover GetPopover() => new Popover(this)
        {
            ColourProvider = colourProvider
        };
    }
}
