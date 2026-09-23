// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Linq;
using JetBrains.Annotations;
using osu.Game.Extensions;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using Realms;

namespace osu.Game.Rulesets
{
    [MapTo("Ruleset")]
    public class RulesetInfo : RealmObject, IEquatable<RulesetInfo>, IComparable<RulesetInfo>, IRulesetInfo
    {
        public const string OSU_MODE_SHORTNAME = "osu";
        public const string TAIKO_MODE_SHORTNAME = "taiko";
        public const string CATCH_MODE_SHORTNAME = "fruits";

        // https://github.com/GooGuTeam/g0v0-server/blob/main/README.en.md#supported-rulesets
        public const string OSU_RELAX_MODE_SHORTNAME = "osurx";
        public const string OSU_AUTOPILOT_MODE_SHORTNAME = "osuap";
        public const string TAIKO_RELAX_MODE_SHORTNAME = "taikorx";
        public const string CATCH_RELAX_MODE_SHORTNAME = "fruitsrx";

        public const int OSU_RELAX_ONLINE_ID = 4;
        public const int OSU_AUTOPILOT_ONLINE_ID = 5;
        public const int TAIKO_RELAX_ONLINE_ID = 6;
        public const int CATCH_RELAX_ONLINE_ID = 7;

        [PrimaryKey]
        public string ShortName { get; set; } = string.Empty;

        [Indexed]
        public int OnlineID { get; set; } = -1;

        public string Name { get; set; } = string.Empty;

        public string InstantiationInfo { get; set; } = string.Empty;

        /// <summary>
        /// Stores the last applied <see cref="DifficultyCalculator.Version"/>
        /// </summary>
        public int LastAppliedDifficultyVersion { get; set; }

        public RulesetInfo(string shortName, string name, string instantiationInfo, int onlineID)
        {
            ShortName = shortName;
            Name = name;
            InstantiationInfo = instantiationInfo;
            OnlineID = onlineID;
        }

        [UsedImplicitly]
        public RulesetInfo()
        {
        }

        public bool Available { get; set; }

        public bool Equals(RulesetInfo? other)
        {
            if (ReferenceEquals(this, other)) return true;
            if (other == null) return false;

            return ShortName == other.ShortName;
        }

        public bool Equals(IRulesetInfo? other) => other is RulesetInfo r && Equals(r);

        public int CompareTo(RulesetInfo? other)
        {
            if (OnlineID >= 0 && other?.OnlineID >= 0)
                return OnlineID.CompareTo(other.OnlineID);

            // Official rulesets are always given precedence for the time being.
            if (OnlineID >= 0)
                return -1;
            if (other?.OnlineID >= 0)
                return 1;

            return string.Compare(ShortName, other?.ShortName, StringComparison.Ordinal);
        }

        public int CompareTo(IRulesetInfo? other)
        {
            if (!(other is RulesetInfo ruleset))
                throw new ArgumentException($@"Object is not of type {nameof(RulesetInfo)}.", nameof(other));

            return CompareTo(ruleset);
        }

        public override int GetHashCode()
        {
            // Importantly, ignore the underlying realm hash code, as it will usually not match.
            var hashCode = new HashCode();
            // ReSharper disable once NonReadonlyMemberInGetHashCode
            hashCode.Add(ShortName);
            return hashCode.ToHashCode();
        }

        public override string ToString() => Name;

        public RulesetInfo Clone() => new RulesetInfo
        {
            OnlineID = OnlineID,
            Name = Name,
            ShortName = ShortName,
            InstantiationInfo = InstantiationInfo,
            Available = Available,
            LastAppliedDifficultyVersion = LastAppliedDifficultyVersion,
        };

        public Ruleset CreateInstance()
        {
            if (!Available)
                throw new RulesetLoadException(@"Ruleset not available");

            var type = Type.GetType(InstantiationInfo);

            if (type == null)
                throw new RulesetLoadException(@"Type lookup failure");

            var ruleset = Activator.CreateInstance(type) as Ruleset;

            if (ruleset == null)
                throw new RulesetLoadException(@"Instantiation failure");

            // overwrite the pre-populated RulesetInfo with a potentially database attached copy.
            // TODO: figure if we still want/need this after switching to realm.
            // ruleset.RulesetInfo = this;

            return ruleset;
        }

        public RulesetInfo CreateSpecialRuleset(string newShortName, int onlineId)
        {
            string suffix = newShortName[^2..].ToUpperInvariant();

            var newRuleset = Clone();
            newRuleset.OnlineID = onlineId;
            newRuleset.ShortName = newShortName;
            newRuleset.Name = $"{newRuleset.Name} ({suffix})";
            return newRuleset;
        }

        public RulesetInfo? CreateSpecialRulesetByScore(ScoreInfo score)
        {
            if (!score.Ruleset.HasSpecialRuleset()) { return null; }

            return score.Ruleset.ShortName switch
            {
                OSU_MODE_SHORTNAME when score.Mods.OfType<ModRelax>().Any() => CreateSpecialRuleset(OSU_RELAX_MODE_SHORTNAME, OSU_RELAX_ONLINE_ID),
                OSU_MODE_SHORTNAME when score.APIMods.Any(m => m.Acronym == "AP") => CreateSpecialRuleset(OSU_AUTOPILOT_MODE_SHORTNAME, OSU_AUTOPILOT_ONLINE_ID),
                TAIKO_MODE_SHORTNAME when score.Mods.OfType<ModRelax>().Any() => CreateSpecialRuleset(TAIKO_RELAX_MODE_SHORTNAME, TAIKO_RELAX_ONLINE_ID),
                CATCH_MODE_SHORTNAME when score.Mods.OfType<ModRelax>().Any() => CreateSpecialRuleset(CATCH_RELAX_MODE_SHORTNAME, CATCH_RELAX_ONLINE_ID),
                _ => null
            };
        }

        public RulesetInfo CreateNormalRuleset()
        {
            string baseShortName = ShortName.Length > 4 ? ShortName[..^2] : ShortName;

            var newRuleset = Clone();
            newRuleset.OnlineID = OnlineID switch
            {
                OSU_RELAX_ONLINE_ID or OSU_AUTOPILOT_ONLINE_ID => 0,
                TAIKO_RELAX_ONLINE_ID => 1,
                CATCH_RELAX_ONLINE_ID => 2,
                _ => OnlineID,
            };
            newRuleset.ShortName = baseShortName;
            newRuleset.Name = newRuleset.Name.Contains('(')
                ? newRuleset.Name[..newRuleset.Name.LastIndexOf(" (", StringComparison.Ordinal)]
                : newRuleset.Name;
            return newRuleset;
        }
    }
}
