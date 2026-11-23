// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace osu.Game.Overlays.Toolbar
{
    /// <summary>
    /// The user-configurable layout of the toolbar's optional icon buttons, i.e. their display order
    /// and which of them the user has chosen to hide.
    /// </summary>
    /// <remarks>
    /// The identifiers used here are deliberately decoupled from the button types,
    /// so renaming or moving a button implementation will not invalidate user configuration.
    /// Hidden state stored here only covers buttons hidden by the user directly; buttons hidden
    /// automatically because there's no room for them are derived at runtime and never persisted.
    /// </remarks>
    public class ToolbarButtonLayout
    {
        public const string RANKINGS = @"rankings";
        public const string BEATMAP_LISTING = @"beatmap_listing";
        public const string CHAT = @"chat";
        public const string SOCIAL = @"social";

        /// <summary>
        /// Every button which the user is allowed to hide and reorder, in its default order.
        /// </summary>
        public static readonly string[] DEFAULT_ORDER = [RANKINGS, BEATMAP_LISTING, CHAT, SOCIAL];

        /// <summary>
        /// The width a single one of these buttons occupies in the toolbar.
        /// They are all icon-only, so they all share the same width.
        /// </summary>
        public const float BUTTON_WIDTH = Toolbar.HEIGHT;

        /// <summary>
        /// The display order of every button in <see cref="DEFAULT_ORDER"/>, including hidden ones.
        /// </summary>
        public List<string> Order { get; set; } = new List<string>();

        /// <summary>
        /// The buttons the user has explicitly hidden.
        /// </summary>
        public HashSet<string> Hidden { get; set; } = new HashSet<string>();

        public bool IsHidden(string id) => Hidden.Contains(id);

        public void SetHidden(string id, bool hidden)
        {
            if (hidden)
                Hidden.Add(id);
            else
                Hidden.Remove(id);
        }

        /// <summary>
        /// Removes unknown and duplicated identifiers from <see cref="Order"/> (appending any missing
        /// known identifier at the end), and drops unknown identifiers from <see cref="Hidden"/>.
        /// </summary>
        public void Normalise()
        {
            var seen = new HashSet<string>();
            var normalisedOrder = Order.Where(id => DEFAULT_ORDER.Contains(id) && seen.Add(id)).ToList();
            normalisedOrder.AddRange(DEFAULT_ORDER.Where(seen.Add));

            Order = normalisedOrder;
            Hidden.RemoveWhere(id => !DEFAULT_ORDER.Contains(id));
        }

        public ToolbarButtonLayout Clone()
        {
            Normalise();

            return new ToolbarButtonLayout
            {
                Order = new List<string>(Order),
                Hidden = new HashSet<string>(Hidden),
            };
        }

        public static ToolbarButtonLayout CreateDefault() => new ToolbarButtonLayout
        {
            Order = DEFAULT_ORDER.ToList(),
            Hidden = new HashSet<string>(),
        };

        /// <summary>
        /// Parses a layout from its serialised form, falling back to the default layout
        /// if <paramref name="raw"/> is empty or cannot be parsed.
        /// </summary>
        public static ToolbarButtonLayout Parse(string? raw)
        {
            if (!string.IsNullOrEmpty(raw))
            {
                try
                {
                    // null members are ignored so that a hand-edited configuration cannot replace a
                    // member which is required for normalisation (such as the order) with null.
                    var parsed = JsonConvert.DeserializeObject<ToolbarButtonLayout>(raw, new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                    });

                    if (parsed != null)
                    {
                        parsed.Normalise();
                        return parsed;
                    }
                }
                catch (Exception)
                {
                    // malformed configuration (or one written by a future/older version) - fall back to defaults.
                }
            }

            return CreateDefault();
        }

        public string Serialize()
        {
            Normalise();
            return JsonConvert.SerializeObject(this);
        }
    }
}
