#nullable disable

using System;
using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Audio;
using osu.Game.Online.API;
using osu.Game.Skinning;

namespace osu.Game.Screens.Menu
{
    /// <summary>
    /// Simply fade into the main menu. Optional welcome voice. Nothing more.
    /// </summary>
    public partial class IntroSimple : IntroScreen
    {
        private const int fade_duration = 1000;

        private SkinnableSound skinnableWelcome;

        public IntroSimple([CanBeNull] Func<MainMenu> createNextScreen = null)
            : base(createNextScreen)
        {
        }

        [BackgroundDependencyLoader]
        private void load(IAPIProvider api)
        {
            if (MenuVoice.Value && api.LocalUser.Value.IsSupporter)
                AddInternal(skinnableWelcome = new SkinnableSound(new SampleInfo(@"Intro/Welcome/welcome")));
        }

        protected override void LogoArriving(OsuLogo logo, bool resuming)
        {
            base.LogoArriving(logo, resuming);

            if (resuming)
                return;

            PrepareMenuLoad();

            // There is a chance that the intro timed out before being displayed, and this scheduled callback could
            // happen during the outro rather than intro.
            // In such a scenario, we don't want to play the intro sample, nor attempt to start the intro track
            // (that may have already been since disposed by MusicController).
            if (DidLoadMenu)
                return;

            skinnableWelcome?.Play();

            StartTrack();

            Scheduler.AddDelayed(() =>
            {
                const float fade_in_time = 200;

                logo.ScaleTo(1);
                logo.FadeIn(fade_in_time);

                FadeInBackground(fade_in_time);

                LoadMenu();
            }, fade_duration);
        }
    }
}
