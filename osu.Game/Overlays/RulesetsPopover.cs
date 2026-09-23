// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Rulesets;

namespace osu.Game.Overlays
{
    public partial class RulesetsPopover : OsuPopover
    {
        private readonly RulesetInfo rulesetInfo;
        private readonly OverlayRulesetSelector overlayRulesetSelector;
        private readonly OsuMenu menu;

        public RulesetsPopover(RulesetInfo rulesetInfo, OverlayRulesetSelector overlayRulesetSelector)
            : base(false)
        {
            this.rulesetInfo = rulesetInfo;
            this.overlayRulesetSelector = overlayRulesetSelector;
            menu = new OsuMenu(Direction.Vertical, true)
            {
                Items = items,
                MaxHeight = 375,
                Width = 150
            };
            Body.CornerRadius = 4;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new[]
            {
                menu
            };
        }

        protected override void OnFocusLost(FocusLostEvent e)
        {
            base.OnFocusLost(e);
            Hide();
        }

        private OsuMenuItem[] items =>
            rulesetInfo.ShortName switch
            {
                RulesetInfo.OSU_MODE_SHORTNAME => new OsuMenuItem[]
                {
                    new RulesetMenuItem(overlayRulesetSelector, rulesetInfo, Hide),
                    new RulesetMenuItem(overlayRulesetSelector, rulesetInfo.CreateSpecialRuleset(RulesetInfo.OSU_RELAX_MODE_SHORTNAME, RulesetInfo.OSU_RELAX_ONLINE_ID), Hide),
                    new RulesetMenuItem(overlayRulesetSelector, rulesetInfo.CreateSpecialRuleset(RulesetInfo.OSU_AUTOPILOT_MODE_SHORTNAME, RulesetInfo.OSU_AUTOPILOT_ONLINE_ID), Hide),
                },
                RulesetInfo.TAIKO_MODE_SHORTNAME => new OsuMenuItem[]
                {
                    new RulesetMenuItem(overlayRulesetSelector, rulesetInfo, Hide),
                    new RulesetMenuItem(overlayRulesetSelector, rulesetInfo.CreateSpecialRuleset(RulesetInfo.TAIKO_RELAX_MODE_SHORTNAME, RulesetInfo.TAIKO_RELAX_ONLINE_ID), Hide),
                },
                RulesetInfo.CATCH_MODE_SHORTNAME => new OsuMenuItem[]
                {
                    new RulesetMenuItem(overlayRulesetSelector, rulesetInfo, Hide),
                    new RulesetMenuItem(overlayRulesetSelector, rulesetInfo.CreateSpecialRuleset(RulesetInfo.CATCH_RELAX_MODE_SHORTNAME, RulesetInfo.CATCH_RELAX_ONLINE_ID), Hide),
                },
                _ => []
            };
    }
}
