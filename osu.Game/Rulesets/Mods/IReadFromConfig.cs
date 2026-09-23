// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Game.Configuration;

namespace osu.Game.Rulesets.Mods
{
    /// <summary>
    /// An interface for mods that require reading access to the g0v0! configuration.
    /// </summary>
    public interface IReadFromConfig
    {
        void ReadFromConfig(OsuConfigManager config);
    }
}
