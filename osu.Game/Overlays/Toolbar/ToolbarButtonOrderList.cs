// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Game.Graphics.Containers;
using osuTK;

namespace osu.Game.Overlays.Toolbar
{
    /// <summary>
    /// A rearrangeable list of the toolbar's optional icon buttons, identified by their stable identifier.
    /// </summary>
    public partial class ToolbarButtonOrderList : OsuRearrangeableListContainer<string>
    {
        /// <summary>
        /// How long an item takes to slide into the position it was swapped into.
        /// </summary>
        public const double SWAP_DURATION = 300;

        /// <summary>
        /// Retrieves the button backing an identifier, or <c>null</c> if it is not available (yet).
        /// </summary>
        public Func<string, ToolbarButton?>? ButtonLookup;

        /// <summary>
        /// Whether the icon with the given identifier is currently hidden by the user.
        /// </summary>
        public Func<string, bool>? IsHidden;

        /// <summary>
        /// Invoked when the user changes whether an icon is displayed in the toolbar.
        /// </summary>
        public Action<string, bool>? HiddenChanged;

        /// <summary>
        /// Invoked after the user rearranged the items.
        /// </summary>
        public Action? OrderChanged;

        /// <summary>
        /// The position every displayed item was at when the swaps so far were last taken into account.
        /// </summary>
        private readonly Dictionary<string, int> previousIndices = new Dictionary<string, int>();

        public ToolbarButtonOrderList()
        {
            ListContainer.Spacing = new Vector2(0, 3);
        }

        protected override OsuRearrangeableListItem<string> CreateOsuDrawable(string item)
            => new ToolbarButtonOrderListItem(item, ButtonLookup, IsHidden, HiddenChanged);

        /// <summary>
        /// Replaces the displayed items, without invoking <see cref="OrderChanged"/>.
        /// </summary>
        public void SetItems(IEnumerable<string> ids)
        {
            // items are added asynchronously, so an animated layout would have them all slide in from the top of
            // the list. only the swaps the user makes by dragging are worth animating, so it is switched on then.
            ListContainer.LayoutDuration = 0;

            Items.Clear();
            Items.AddRange(ids);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // only commit an arrangement once the user has let go of the item they were dragging.
            DragActive.BindValueChanged(active =>
            {
                if (active.NewValue)
                {
                    // the layout is animated from here on, so that a swap reads as the items sliding past each
                    // other rather than the list instantly showing a different order. the easing is deliberately
                    // gentler than the OutQuint used elsewhere, as a front-loaded curve makes the slide too quick
                    // to follow. it is not animated before this point so that the items added when the list is
                    // populated appear in place instead of flying in from the top of the list.
                    ListContainer.LayoutDuration = (float)SWAP_DURATION;
                    ListContainer.LayoutEasing = Easing.OutCubic;

                    captureIndices();
                }
                else
                    OrderChanged?.Invoke();
            });
        }

        protected override void OnItemsChanged()
        {
            base.OnItemsChanged();

            if (!DragActive.Value)
                return;

            for (int i = 0; i < Items.Count; i++)
            {
                string id = Items[i];

                // the item being dragged is already the obvious one, so only the items it pushed aside are
                // called out. they are the ones the swap has to be read from.
                if (ItemMap.TryGetValue(id, out var drawable)
                    && drawable is ToolbarButtonOrderListItem row
                    && !row.IsDragged
                    && previousIndices.TryGetValue(id, out int previous)
                    && previous != i)
                    row.PlaySwapAnimation();
            }

            captureIndices();
        }

        private void captureIndices()
        {
            previousIndices.Clear();

            for (int i = 0; i < Items.Count; i++)
                previousIndices[Items[i]] = i;
        }
    }
}
