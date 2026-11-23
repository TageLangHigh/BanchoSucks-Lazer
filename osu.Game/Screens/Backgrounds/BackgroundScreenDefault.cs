// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

#nullable disable

using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Framework.Threading;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Skinning;

namespace osu.Game.Screens.Backgrounds
{
    public partial class BackgroundScreenDefault : BackgroundScreen
    {
        private int currentDisplay;
        private const int background_count = 8;
        private Bindable<Skin> skin;
        private Bindable<BackgroundSource> source;
        private readonly SeasonalBackgroundLoader seasonalBackgroundLoader = new SeasonalBackgroundLoader();

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; }

        [Resolved]
        private GameHost gameHost { get; set; }

        protected virtual bool AllowStoryboardBackground => true;

        protected virtual string SkinBackgroundLookupName => @"menu-background";

        [BackgroundDependencyLoader]
        private void load(SkinManager skinManager, OsuConfigManager config)
        {
            skin = skinManager.CurrentSkin.GetBoundCopy();
            source = config.GetBindable<BackgroundSource>(OsuSetting.MenuBackgroundSource);

            AddInternal(seasonalBackgroundLoader);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            skin.ValueChanged += _ => Scheduler.AddOnce(next);
            source.ValueChanged += _ => Scheduler.AddOnce(next);
            beatmap.ValueChanged += _ => Scheduler.AddOnce(next);

            seasonalBackgroundLoader.SeasonalBackgroundChanged += () => Scheduler.AddOnce(next);

            currentDisplay = RNG.Next(0, background_count);
            Next();

            // helper function required for AddOnce usage.
            void next() => Next();
        }

        private ScheduledDelegate storyboardUnloadDelegate;

        public override void OnSuspending(ScreenTransitionEvent e)
        {
            if (CurrentBackground is BeatmapBackgroundWithStoryboard storyboardBackground)
                storyboardUnloadDelegate = gameHost.UpdateThread.Scheduler.AddDelayed(storyboardBackground.UnloadStoryboard, TRANSITION_LENGTH);

            base.OnSuspending(e);
        }

        public override void OnResuming(ScreenTransitionEvent e)
        {
            if (CurrentBackground is BeatmapBackgroundWithStoryboard storyboardBackground)
            {
                if (storyboardUnloadDelegate?.Completed == false)
                    storyboardUnloadDelegate.Cancel();
                else
                    storyboardBackground.LoadStoryboard();

                storyboardUnloadDelegate = null;
            }

            base.OnResuming(e);
        }

        private ScheduledDelegate nextTask;
        private CancellationTokenSource cancellationTokenSource;

        /// <summary>
        /// The background which has been queued for display but has not been displayed yet.
        /// </summary>
        private Background pendingBackground;

        /// <summary>
        /// Request loading the next background.
        /// </summary>
        /// <returns>Whether a new background was queued for load. May return false if the current background is still valid.</returns>
        public virtual bool Next()
        {
            var nextBackground = createBackground();

            // in the case that the background hasn't changed, we want to avoid cancelling any tasks that could still be loading.
            if (nextBackground == CurrentBackground)
            {
                // the latest choice is the one already on screen, so anything still queued for load is stale
                // (and would otherwise replace it, contradicting the saved selection).
                if (pendingBackground != null)
                {
                    nextTask?.Cancel();
                    cancellationTokenSource?.Cancel();
                    pendingBackground = null;
                }

                return false;
            }

            Logger.Log(@"🌅 Global background change queued");

            cancellationTokenSource?.Cancel();
            cancellationTokenSource = new CancellationTokenSource();

            nextTask?.Cancel();
            pendingBackground = nextBackground;

            nextTask = Scheduler.AddDelayed(() =>
            {
                Logger.Log(@"🌅 Global background loading");
                LoadComponentAsync(nextBackground, displayNext, cancellationTokenSource.Token);
            }, 500);

            return true;
        }

        private void displayNext(Background newBackground)
        {
            pendingBackground = null;

            CurrentBackground?.FadeOut(800, Easing.OutQuint);
            CurrentBackground?.Expire();

            AddInternal(CurrentBackground = newBackground);
            currentDisplay++;
        }

        private Background createBackground()
        {
            // seasonal backgrounds take highest priority, with a pinned one replacing the rotation.
            Background newBackground = seasonalBackgroundLoader.LoadNextBackground();

            if (newBackground == null)
            {
                switch (source.Value)
                {
                    case BackgroundSource.Beatmap:
                    case BackgroundSource.BeatmapWithStoryboard:
                    {
                        if (source.Value == BackgroundSource.BeatmapWithStoryboard && AllowStoryboardBackground)
                            newBackground = new BeatmapBackgroundWithStoryboard(beatmap.Value, getBackgroundTextureName());
                        newBackground ??= new BeatmapBackground(beatmap.Value, getBackgroundTextureName());

                        break;
                    }

                    case BackgroundSource.Skin:
                        switch (skin.Value)
                        {
                            case TrianglesSkin:
                            case ArgonSkin:
                            case DefaultLegacySkin:
                                // default skins should use the default background rotation, which won't be the case if a SkinBackground is created for them.
                                break;

                            default:
                                newBackground = new SkinBackground(skin.Value, getBackgroundTextureName(), SkinBackgroundLookupName);
                                break;
                        }

                        break;
                }
            }

            // this method is called in many cases where the background might not necessarily need to change.
            // if an equivalent background is currently being shown, we don't want to load it again.
            if (newBackground?.Equals(CurrentBackground) == true)
                return CurrentBackground;

            newBackground ??= new Background(getBackgroundTextureName());
            newBackground.Depth = currentDisplay;

            return newBackground;
        }

        private string getBackgroundTextureName() => $@"Menu/menu-background-{currentDisplay % background_count + 1}";

        /// <summary>
        /// The background currently being displayed, if any.
        /// </summary>
        public Background CurrentBackground { get; private set; }
    }
}
