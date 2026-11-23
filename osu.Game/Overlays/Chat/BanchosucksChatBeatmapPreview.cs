// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Configuration;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Chat;

namespace osu.Game.Overlays.Chat
{
    /// <summary>
    /// Banchosucks: the place under a chat message's text where a beatmap preview card appears when the message
    /// links to a beatmap or beatmap set (<see cref="BanchosucksChatBeatmapLink"/>).
    /// </summary>
    /// <remarks>
    /// Takes no room until a card is there. The set is looked up through <see cref="BanchosucksChatBeatmapLookup"/>,
    /// and only once the line has been on screen for a moment: lines above the visible part of the chat (older
    /// messages) are never looked up unless the player scrolls to them, and lines rushing past while the chat
    /// scrolls to its end are not either. Only the chat overlay's own lines get previews; the narrow chat displays
    /// of multiplayer and the tournament client use subclasses of <see cref="ChatLine"/> and are left alone.
    /// </remarks>
    public partial class BanchosucksChatBeatmapPreview : CompositeDrawable
    {
        /// <summary>
        /// How long a line has to stay on screen before its link is looked up, in milliseconds.
        /// </summary>
        public const double VISIBLE_TIME_BEFORE_LOOKUP = 500;

        private readonly Drawable line;
        private readonly bool allowed;

        [Resolved]
        private BanchosucksChatBeatmapLookup? lookup { get; set; }

        [Resolved]
        private OsuConfigManager? config { get; set; }

        private readonly Bindable<bool> enabled = new BindableBool(true);

        private BanchosucksChatBeatmapLink? link;
        private Action<APIBeatmapSet>? pendingCallback;
        private bool requested;
        private double? visibleSince;

        /// <summary>
        /// The card currently shown, if any (for tests).
        /// </summary>
        public BanchosucksChatBeatmapCard? Card { get; private set; }

        /// <summary>
        /// The link of the current message that would get a preview, if any (for tests).
        /// </summary>
        public BanchosucksChatBeatmapLink? Link => link;

        /// <param name="line">The chat line this belongs to; its position decides whether the message is on screen.</param>
        public BanchosucksChatBeatmapPreview(Drawable line)
        {
            this.line = line;
            allowed = line.GetType() == typeof(ChatLine);

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

            // the cards take their colours from the overlay they sit in; a chat line outside of an overlay has none
            if (parent.Get<OverlayColourProvider>() == null)
                dependencies.Cache(new OverlayColourProvider(OverlayColourScheme.Pink));

            return dependencies;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            config?.BindWith(OsuSetting.BanchosucksChatBeatmapPreviews, enabled);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            enabled.BindValueChanged(e =>
            {
                // switched off: cards go away at once. switched on: lines on screen get theirs through Update.
                if (!e.NewValue)
                    reset();
            });
        }

        /// <summary>
        /// Tells the preview which message its line shows now.
        /// </summary>
        public void SetMessage(Message message)
        {
            // an echo of the player's own message is replaced by the real one in a moment
            var newLink = allowed && message is not LocalEchoMessage ? BanchosucksChatBeatmapLink.Find(message.Links) : null;

            if (Nullable.Equals(newLink, link))
                return;

            // the old lookup is withdrawn before the link changes
            reset();
            link = newLink;
        }

        private void reset()
        {
            cancelPending();

            requested = false;
            visibleSince = null;
            Card = null;
            ClearInternal();
        }

        private void cancelPending()
        {
            if (pendingCallback != null && link != null)
                lookup?.Cancel(link.Value, pendingCallback);

            pendingCallback = null;
        }

        protected override void Update()
        {
            base.Update();

            if (link == null || requested || lookup == null || !enabled.Value)
                return;

            // on screen = inside what the chat's scroll container shows. a freshly added line has no place yet,
            // which the waiting time below covers.
            if (!line.IsPresent || !line.ComputeMaskingBounds().IntersectsWith(line.ScreenSpaceDrawQuad.AABBFloat))
            {
                visibleSince = null;
                return;
            }

            visibleSince ??= Time.Current;

            if (Time.Current - visibleSince.Value < VISIBLE_TIME_BEFORE_LOOKUP)
                return;

            requested = true;

            var requestedLink = link.Value;

            pendingCallback = set =>
            {
                pendingCallback = null;

                if (Nullable.Equals(link, requestedLink) && enabled.Value)
                    show(set, requestedLink);
            };

            lookup.Lookup(requestedLink, pendingCallback);
        }

        private void show(APIBeatmapSet set, BanchosucksChatBeatmapLink shownLink)
        {
            LoadComponentAsync(new BanchosucksChatBeatmapCard(set, shownLink), card =>
            {
                if (!Nullable.Equals(link, shownLink) || !enabled.Value)
                    return;

                Card = card;
                InternalChild = new Container
                {
                    AutoSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Top = 3, Bottom = 4 },
                    Child = card,
                };
            });
        }

        protected override void Dispose(bool isDisposing)
        {
            cancelPending();
            base.Dispose(isDisposing);
        }
    }
}
