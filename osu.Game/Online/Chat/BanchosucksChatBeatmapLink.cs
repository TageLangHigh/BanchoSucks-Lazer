// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Online.Chat
{
    /// <summary>
    /// Banchosucks: what a beatmap link in a chat message points at, for the preview card under the message.
    /// </summary>
    public readonly struct BanchosucksChatBeatmapLink : IEquatable<BanchosucksChatBeatmapLink>
    {
        /// <summary>
        /// Whether the link names a beatmap set (true) or one difficulty (false).
        /// </summary>
        public readonly bool IsSet;

        /// <summary>
        /// The online id of the set or of the difficulty.
        /// </summary>
        public readonly int Id;

        public BanchosucksChatBeatmapLink(bool isSet, int id)
        {
            IsSet = isSet;
            Id = id;
        }

        /// <summary>
        /// The first beatmap or beatmap set link of a message, if there is one.
        /// </summary>
        /// <remarks>
        /// Which links count is decided by <see cref="MessageFormatter"/> alone: links to our website
        /// (<see cref="MessageFormatter.WebsiteRootUrl"/>, e.g. banchosucks.cc/lazer/beatmapsets/1#osu/2) and
        /// osu://b/, osu://s/ and osu://dl/ links. Links to other websites are external links there and get no preview.
        /// One card per message is enough, so only the first link is used.
        /// </remarks>
        /// <param name="links">The links of a formatted message (<see cref="Message.Links"/>).</param>
        public static BanchosucksChatBeatmapLink? Find(IEnumerable<Link>? links)
        {
            if (links == null)
                return null;

            foreach (var link in links.OrderBy(l => l.Index))
            {
                if (link.Action != LinkAction.OpenBeatmap && link.Action != LinkAction.OpenBeatmapSet)
                    continue;

                // the argument is the id as text; anything else (or an id that cannot exist) is not worth a request
                if (!int.TryParse(link.Argument?.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out int id) || id <= 0)
                    continue;

                return new BanchosucksChatBeatmapLink(link.Action == LinkAction.OpenBeatmapSet, id);
            }

            return null;
        }

        /// <summary>
        /// The first beatmap or beatmap set link in a text as it was typed.
        /// </summary>
        public static BanchosucksChatBeatmapLink? Find(string text) => Find(MessageFormatter.FormatText(text).Links);

        public bool Equals(BanchosucksChatBeatmapLink other) => IsSet == other.IsSet && Id == other.Id;

        public override bool Equals(object? obj) => obj is BanchosucksChatBeatmapLink other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(IsSet, Id);

        public override string ToString() => IsSet ? $"set {Id}" : $"beatmap {Id}";
    }

    /// <summary>
    /// Banchosucks: the texts of the chat preview card, worked out from the looked-up set and the link.
    /// </summary>
    public class BanchosucksChatBeatmapPreviewInfo
    {
        public string Title { get; private init; } = string.Empty;
        public string TitleUnicode { get; private init; } = string.Empty;
        public string Artist { get; private init; } = string.Empty;
        public string ArtistUnicode { get; private init; } = string.Empty;
        public string Mapper { get; private init; } = string.Empty;

        /// <summary>
        /// The name of the linked difficulty, null for a link to the whole set (or a difficulty the set does not have).
        /// </summary>
        public string? DifficultyName { get; private init; }

        /// <summary>
        /// Lowest and highest star rating shown: the same value twice for one difficulty, the range of the set otherwise.
        /// Null when the set came without difficulties.
        /// </summary>
        public (double Min, double Max)? Stars { get; private init; }

        /// <summary>
        /// "5.21" for one difficulty, "1.80 - 6.43" for a set, empty without difficulties.
        /// </summary>
        public string StarText
        {
            get
            {
                if (Stars is not var (min, max))
                    return string.Empty;

                string format(double stars) => stars.ToString("0.00", CultureInfo.InvariantCulture);

                return Math.Abs(max - min) < 0.005 ? format(max) : $"{format(min)} - {format(max)}";
            }
        }

        public static BanchosucksChatBeatmapPreviewInfo From(APIBeatmapSet set, BanchosucksChatBeatmapLink link)
        {
            APIBeatmap? linked = link.IsSet ? null : set.Beatmaps.FirstOrDefault(b => b.OnlineID == link.Id);

            (double, double)? stars = null;

            if (linked != null)
                stars = (linked.StarRating, linked.StarRating);
            else if (set.Beatmaps.Length > 0)
                stars = (set.Beatmaps.Min(b => b.StarRating), set.Beatmaps.Max(b => b.StarRating));

            return new BanchosucksChatBeatmapPreviewInfo
            {
                Title = set.Title,
                TitleUnicode = string.IsNullOrEmpty(set.TitleUnicode) ? set.Title : set.TitleUnicode,
                Artist = set.Artist,
                ArtistUnicode = string.IsNullOrEmpty(set.ArtistUnicode) ? set.Artist : set.ArtistUnicode,
                Mapper = set.Author.Username,
                DifficultyName = linked?.DifficultyName,
                Stars = stars,
            };
        }
    }
}
