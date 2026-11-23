// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Scoring;
using osu.Game.Screens.Banchosucks.Clicker;
using osuTK;

namespace osu.Game.Screens.Ranking
{
    /// <summary>
    /// Banchosucks: renders the shown score to a video with <see cref="BanchosucksReplayRenderer"/>, straight from the results screen.
    /// Only there for osu!standard scores whose replay is stored locally or on the server.
    /// </summary>
    public partial class BanchosucksRenderVideoButton : GrayButton
    {
        public readonly Bindable<ScoreInfo?> Score = new Bindable<ScoreInfo?>();

        private readonly Bindable<DownloadState> state = new Bindable<DownloadState>();

        private ScoreDownloadTracker? downloadTracker;

        [Resolved]
        private BanchosucksReplayRenderer? renderer { get; set; }

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        public BanchosucksRenderVideoButton()
            : base(FontAwesome.Solid.Video)
        {
            Size = new Vector2(75, 30);
            TooltipText = BanchosucksReplayRenderer.RenderActionText;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Action = () =>
            {
                if (renderer == null || Score.Value == null)
                    return;

                // a score that was just played has its replay in the database, but not in the object the screen holds
                var score = renderer.ResolveLocal(Score.Value);

                if (BanchosucksReplayRenderer.CanRender(score))
                    renderer.Render(score);
                else
                {
                    // the score is on this PC, but without a replay file (and none on the server either)
                    notifications?.Post(new SimpleNotification
                    {
                        Text = ClickerStrings.Text("There is no replay for this score, so it cannot be rendered.", "Zu diesem Score gibt es kein Replay, er kann nicht gerendert werden."),
                        Icon = FontAwesome.Solid.ExclamationTriangle,
                    });
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Score.BindValueChanged(score =>
            {
                downloadTracker?.RemoveAndDisposeImmediately();
                downloadTracker = null;
                state.SetDefault();

                // only watch the database for scores that could be rendered at all
                if (renderer != null && score.NewValue != null && BanchosucksReplayRenderer.CanRenderRuleset(score.NewValue.Ruleset))
                {
                    AddInternal(downloadTracker = new ScoreDownloadTracker(score.NewValue)
                    {
                        State = { BindTarget = state }
                    });
                }

                updateState();
            }, true);

            state.BindValueChanged(_ => updateState(), true);
            FinishTransforms(true);
        }

        /// <summary>
        /// Whether the button is offered for the current score (for tests).
        /// </summary>
        public bool Available { get; private set; }

        private void updateState()
        {
            var score = Score.Value;

            Available = renderer != null
                        && score != null
                        && BanchosucksReplayRenderer.CanRenderRuleset(score.Ruleset)
                        && (state.Value == DownloadState.LocallyAvailable || score.Files.Count > 0 || (score.OnlineID > 0 && score.HasOnlineReplay));

            // an invisible button takes no room in the button row
            Enabled.Value = Available;
            this.FadeTo(Available ? 1 : 0, 200);
        }
    }
}
