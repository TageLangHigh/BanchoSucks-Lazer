// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.IO;
using osu.Framework.Extensions;

namespace osu.Game.Rulesets
{
    public class RulesetHashCache
    {
        public readonly Dictionary<string, string> RulesetsHashes = new Dictionary<string, string>();

        public RulesetHashCache(RulesetStore store)
        {
            foreach (var rulesetInfo in store.AvailableRulesets)
            {
                if (rulesetInfo.OnlineID >= 0 && rulesetInfo.OnlineID <= 3)
                    continue;

                Ruleset instance = rulesetInfo.CreateInstance();
                using var str = File.OpenRead(instance.GetType().Assembly.Location);
                RulesetsHashes[instance.ShortName] = str.ComputeMD5Hash();
            }
        }

        public string? GetHash(string shortName)
        {
            RulesetsHashes.TryGetValue(shortName, out string? hash);
            return hash;
        }

        public string? GetHash(RulesetInfo rulesetInfo) => GetHash(rulesetInfo.ShortName);

        public string? GetHash(Ruleset ruleset) => GetHash(ruleset.ShortName);
    }
}
