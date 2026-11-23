// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Threading;
using osu.Game.Graphics;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Screens.Menu
{
    internal partial class BackgroundEditorHoldTrigger : CompositeDrawable
    {
        /// <summary>
        /// Invoked when the hold completes and the background editor should be shown.
        /// </summary>
        public Action? BeginEdit;

        private const double hold_duration = 900;
        private const int fade_duration = 200;
        private const int release_fade_delay = 500;

        private const float indicator_size = 56;

        private readonly HoldIndicator indicator;

        private ScheduledDelegate? holdDelegate;
        private bool holdFired;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        private InputManager inputManager = null!;

        public BackgroundEditorHoldTrigger()
        {
            RelativeSizeAxes = Axes.Both;

            AddInternal(indicator = new HoldIndicator());
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            inputManager = GetContainingInputManager()!;
            indicator.RingColour = colours.Blue;
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (e.Button != MouseButton.Right)
                return false;

            holdFired = false;
            indicator.ShowAt(inputManager.CurrentState.Mouse.Position);

            progressRingAnimate();

            holdDelegate?.Cancel();
            holdDelegate = Scheduler.AddDelayed(onHoldComplete, hold_duration);

            return true;
        }

        private void progressRingAnimate()
        {
            indicator.ProgressRing.ClearTransforms();
            indicator.ProgressRing.Progress = 0;
            indicator.ProgressRing.ProgressTo(1, hold_duration);
        }

        private void onHoldComplete()
        {
            holdFired = true;
            indicator.FadeOut(fade_duration, Easing.OutQuint);

            BeginEdit?.Invoke();
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            base.OnMouseUp(e);

            if (e.Button != MouseButton.Right || holdFired)
                return;

            holdDelegate?.Cancel();
            indicator.ProgressRing.ClearTransforms();

            // linger briefly after release before fading out.
            indicator.Delay(release_fade_delay).FadeOut(fade_duration, Easing.OutQuint);
        }

        protected override void Update()
        {
            base.Update();

            if (indicator.Alpha > 0)
                indicator.TrackTo(inputManager.CurrentState.Mouse.Position, Parent!.ChildSize);
        }

        private partial class HoldIndicator : CircularContainer
        {
            public CircularProgress ProgressRing { get; }

            public Color4 RingColour
            {
                set => ProgressRing.Colour = value;
            }

            public HoldIndicator()
            {
                Size = new Vector2(indicator_size);
                Origin = Anchor.Centre;
                Alpha = 0;
                AlwaysPresent = true;

                Masking = true;
                EdgeEffect = new EdgeEffectParameters
                {
                    Type = EdgeEffectType.Shadow,
                    Offset = new Vector2(0f, 1f),
                    Radius = 3f,
                    Colour = Color4.Black.Opacity(0.25f),
                };

                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Black.Opacity(0.7f),
                    },
                    ProgressRing = new CircularProgress
                    {
                        RelativeSizeAxes = Axes.Both,
                        InnerRadius = 0.08f,
                    },
                    new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(18),
                        Icon = FontAwesome.Regular.Image,
                    },
                };
            }

            public void ShowAt(Vector2 screenSpacePosition)
            {
                Position = getPositionInParent(screenSpacePosition);
                this.ScaleTo(0.8f).ScaleTo(1, 200, Easing.OutQuint);
                this.FadeIn(fade_duration, Easing.OutQuint);
            }

            public void TrackTo(Vector2 screenSpacePosition, Vector2 parentSize)
            {
                var target = getPositionInParent(screenSpacePosition);

                // keep the indicator roughly within the bounds of the screen.
                target.X = Math.Clamp(target.X, indicator_size, parentSize.X - indicator_size);
                target.Y = Math.Clamp(target.Y, indicator_size, parentSize.Y - indicator_size);

                Position = target;
            }

            private Vector2 getPositionInParent(Vector2 screenSpacePosition)
                => Parent!.ToLocalSpace(screenSpacePosition);
        }
    }
}
