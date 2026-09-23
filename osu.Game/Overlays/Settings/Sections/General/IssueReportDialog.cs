// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Overlays.Dialog;

namespace osu.Game.Overlays.Settings.Sections.General
{
    public partial class IssueReportDialog : PopupDialog
    {
        public IssueReportDialog(Action onConfirm)
        {
            HeaderText = GeneralSettingsStrings.IssueReportDialogHeader;
            BodyText = GeneralSettingsStrings.IssueReportDialogText;

            Icon = FontAwesome.Regular.Lightbulb;

            Buttons = new PopupDialogButton[]
            {
                new PopupDialogOkButton
                {
                    Text = DialogStrings.Confirm,
                    Action = onConfirm,
                },
                new PopupDialogCancelButton
                {
                    Text = DialogStrings.Cancel,
                },
            };
        }
    }
}
