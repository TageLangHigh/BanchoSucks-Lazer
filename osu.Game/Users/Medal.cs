// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

#nullable disable

using System;

namespace osu.Game.Users
{
    public class Medal
    {
        public string Name { get; set; }
        public string InternalName { get; set; }

        public string ImageUrl => InternalName.StartsWith("g0v0_", StringComparison.Ordinal)
            ? $@"https://lazer-data.g0v0.top/medals/{InternalName}@2x.png"
            : $@"https://s.ppy.sh/images/medals-client/{InternalName}@2x.png";

        public string Description { get; set; }
    }
}
