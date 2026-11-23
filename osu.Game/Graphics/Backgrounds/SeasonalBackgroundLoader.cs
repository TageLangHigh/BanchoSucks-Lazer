// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

#nullable disable

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Framework.Utils;
using osu.Game.Configuration;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Graphics.Backgrounds
{
    public partial class SeasonalBackgroundLoader : Component
    {
        /// <summary>
        /// Fired when background should be changed due to receiving backgrounds from API
        /// or when the user setting is changed (as it might require unloading the seasonal background).
        /// </summary>
        public event Action SeasonalBackgroundChanged;

        [Resolved]
        private IAPIProvider api { get; set; }

        private Bindable<SeasonalBackgroundMode> seasonalBackgroundMode;
        private Bindable<bool> pinnedSeasonalBackground;
        private Bindable<int> pinnedSeasonalBackgroundIndex;
        private Bindable<APISeasonalBackgrounds> seasonalBackgrounds;

        private int current;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, SessionStatics sessionStatics)
        {
            seasonalBackgroundMode = config.GetBindable<SeasonalBackgroundMode>(OsuSetting.SeasonalBackgroundMode);
            seasonalBackgroundMode.BindValueChanged(_ => SeasonalBackgroundChanged?.Invoke());

            pinnedSeasonalBackground = config.GetBindable<bool>(OsuSetting.PinnedSeasonalBackground);
            pinnedSeasonalBackground.BindValueChanged(_ =>
            {
                if (seasonalBackgroundMode.Value != SeasonalBackgroundMode.Never)
                    SeasonalBackgroundChanged?.Invoke();
            });

            pinnedSeasonalBackgroundIndex = config.GetBindable<int>(OsuSetting.PinnedSeasonalBackgroundIndex);
            pinnedSeasonalBackgroundIndex.BindValueChanged(_ =>
            {
                if (seasonalBackgroundMode.Value != SeasonalBackgroundMode.Never && pinnedSeasonalBackground.Value)
                    SeasonalBackgroundChanged?.Invoke();
            });

            seasonalBackgrounds = sessionStatics.GetBindable<APISeasonalBackgrounds>(Static.SeasonalBackgrounds);
            seasonalBackgrounds.BindValueChanged(_ =>
            {
                if (shouldShowSeasonal)
                    SeasonalBackgroundChanged?.Invoke();
            });

            fetchSeasonalBackgrounds();
        }

        private void fetchSeasonalBackgrounds()
        {
            if (seasonalBackgrounds.Value != null)
                return;

            var request = new GetSeasonalBackgroundsRequest();
            request.Success += response =>
            {
                seasonalBackgrounds.Value = response;
                current = RNG.Next(0, response.Backgrounds?.Count ?? 0);
            };

            api.PerformAsync(request);
        }

        /// <summary>
        /// Pick the seasonal background which should be displayed.
        /// </summary>
        /// <returns>
        /// The background to display, or <c>null</c> if the current settings do not enable seasonal backgrounds.
        /// </returns>
        public SeasonalBackground LoadNextBackground()
        {
            if (!shouldShowSeasonal)
                return null;

            var backgrounds = seasonalBackgrounds.Value.Backgrounds;

            // a pinned index replaces the rotation with a single image.
            int index = pinnedSeasonalBackground.Value ? pinnedSeasonalBackgroundIndex.Value : -1;

            if (index < 0 || index >= backgrounds.Count)
                index = current = (current + 1) % backgrounds.Count;

            return new SeasonalBackground(backgrounds[index].Url);
        }

        private bool shouldShowSeasonal => seasonalBackgroundMode.Value switch
        {
            SeasonalBackgroundMode.Never => false,
            SeasonalBackgroundMode.Sometimes when !isInSeason => false,
            _ => seasonalBackgrounds.Value?.Backgrounds?.Any() == true,
        };

        private bool isInSeason => seasonalBackgrounds.Value != null && DateTimeOffset.Now < seasonalBackgrounds.Value.EndDate;
    }

    [LongRunningLoad]
    public partial class SeasonalBackground : Background
    {
        /// <summary>
        /// The bundled texture displayed in place of the remote image when the latter could not be loaded.
        /// </summary>
        public const string FALLBACK_TEXTURE_NAME = @"Backgrounds/bg1";

        /// <summary>
        /// The remote URL this background's texture is loaded from.
        /// </summary>
        public string Url { get; }

        /// <summary>
        /// <see cref="Url"/> if the displayed texture was actually retrieved from it, or <c>null</c> if
        /// <see cref="FALLBACK_TEXTURE_NAME"/> is being displayed instead.
        /// </summary>
        public string LoadedUrl { get; private set; }

        public SeasonalBackground(string url)
        {
            Url = url;
        }

        [BackgroundDependencyLoader]
        private void load(LargeTextureStore textures)
        {
            Texture texture = textures.Get(Url);

            if (texture != null)
            {
                Sprite.Texture = texture;
                LoadedUrl = Url;
                return;
            }

            Sprite.Texture = textures.Get(FALLBACK_TEXTURE_NAME);
        }

        public override bool Equals(Background other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;

            return other.GetType() == GetType() && ((SeasonalBackground)other).Url == Url;
        }
    }
}
