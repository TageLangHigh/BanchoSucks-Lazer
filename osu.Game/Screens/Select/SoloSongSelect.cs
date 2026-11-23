// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Graphics.UserInterface;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Banchosucks.Clicker;
using osu.Game.Screens.Banchosucks.Practice;
using osu.Game.Screens.Banchosucks.Warmup;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Footer;
using osu.Game.Screens.Play;
using osu.Game.Users;
using osu.Game.Utils;
using WebCommonStrings = osu.Game.Resources.Localisation.Web.CommonStrings;

namespace osu.Game.Screens.Select
{
    public partial class SoloSongSelect : SongSelect
    {
        public override bool AllowLegacyFooterSkinning => true;

        protected override UserActivity InitialActivity => new UserActivity.ChoosingBeatmap();

        private PlayerLoader? playerLoader;
        private IReadOnlyList<Mod>? modsAtGameplayStart;

        [Resolved]
        private BeatmapSetOverlay? beatmapOverlay { get; set; }

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        [Resolved]
        private IDialogOverlay? dialogOverlay { get; set; }

        [Resolved]
        private OsuGame? game { get; set; }

        private Sample? sampleConfirmSelection { get; set; }

        // Banchosucks: the warm-up series (Options > Warm-up, or the key binding). Cached so the results screen pushed from here can offer the next map.
        [Cached]
        private readonly BanchosucksWarmupSeries banchosucksWarmupSeries = new BanchosucksWarmupSeries();

        private BanchosucksWarmupController banchosucksWarmup = null!;

        internal BanchosucksWarmupController BanchosucksWarmup => banchosucksWarmup;

        private bool banchosucksStartingWarmup;

        [BackgroundDependencyLoader]
        private void load(AudioManager audio)
        {
            sampleConfirmSelection = audio.Samples.Get(@"SongSelect/confirm-selection");

            AddInternal(new SongSelectTouchInputDetector());

            // Banchosucks: picks and runs the warm-up maps; starts them the same way a click on the map does.
            AddInternal(banchosucksWarmup = new BanchosucksWarmupController(banchosucksWarmupSeries, beatmap =>
            {
                banchosucksStartingWarmup = true;

                try
                {
                    SelectAndRun(beatmap, OnStart);
                }
                finally
                {
                    banchosucksStartingWarmup = false;
                }

                return playerLoader != null;
            }));
        }

        /// <summary>
        /// Banchosucks: starts a warm-up series: three local maps a bit below the player's level, played one after another.
        /// </summary>
        public void BanchosucksStartWarmup()
        {
            if (this.IsCurrentScreen())
                banchosucksWarmup.Start();
        }

        // Banchosucks: the warm-up has a rebindable action without a default key.
        public override bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Action == GlobalAction.BanchosucksWarmup && !e.Repeat && this.IsCurrentScreen())
            {
                BanchosucksStartWarmup();
                return true;
            }

            return base.OnPressed(e);
        }

        public override IEnumerable<OsuMenuItem> GetForwardActions(BeatmapInfo beatmap)
        {
            yield return new OsuMenuItem(ButtonSystemStrings.Play.ToSentence(), MenuItemType.Highlighted, () => SelectAndRun(beatmap, OnStart)) { Icon = FontAwesome.Solid.Check };
            yield return new OsuMenuItem(ButtonSystemStrings.Edit.ToSentence(), MenuItemType.Standard, () => Edit(beatmap)) { Icon = FontAwesome.Solid.PencilAlt };
            // Banchosucks: practise a section of this difficulty in a loop; start, end and speed are picked on the next screen.
            yield return new OsuMenuItem(ClickerStrings.Text("Practise section", "Abschnitt üben"), MenuItemType.Standard,
                () => SelectAndRun(beatmap, () => this.Push(new BanchosucksPracticeSetupScreen()))) { Icon = FontAwesome.Solid.Redo };

            yield return new OsuMenuItemSpacer();

            if (beatmap.OnlineID > 0)
            {
                yield return new OsuMenuItem(CommonStrings.Details, MenuItemType.Standard, () => beatmapOverlay?.FetchAndShowBeatmap(beatmap.OnlineID));

                if (beatmap.GetOnlineURL(api, Ruleset.Value) is string url)
                    yield return new OsuMenuItem(CommonStrings.CopyLink, MenuItemType.Standard, () => game?.CopyToClipboard(url));

                yield return new OsuMenuItemSpacer();
            }

            foreach (var i in CreateCollectionMenuActions(beatmap))
                yield return i;

            if (beatmap.LastPlayed == null)
                yield return new OsuMenuItem(SongSelectStrings.MarkAsPlayed, MenuItemType.Standard, () => beatmaps.MarkPlayed(beatmap)) { Icon = FontAwesome.Solid.TimesCircle };
            else
                yield return new OsuMenuItem(SongSelectStrings.RemoveFromPlayed, MenuItemType.Standard, () => beatmaps.MarkNotPlayed(beatmap)) { Icon = FontAwesome.Solid.TimesCircle };

            yield return new OsuMenuItem(SongSelectStrings.ClearAllLocalScores, MenuItemType.Destructive, () => dialogOverlay?.Push(new BeatmapClearScoresDialog(beatmap)))
            {
                Icon = FontAwesome.Solid.Eraser
            };

            if (beatmaps.CanHide(beatmap))
                yield return new OsuMenuItem(WebCommonStrings.ButtonsHide.ToSentence(), MenuItemType.Standard, () => beatmaps.Hide(beatmap));
        }

        protected override void OnStart()
        {
            if (playerLoader != null) return;

            modsAtGameplayStart = Mods.Value.Select(m => m.DeepClone()).ToArray();

            // Ctrl+Enter should start map with autoplay enabled.
            // Banchosucks: not for a warm-up map, which may be started by a key binding that contains Ctrl.
            if (!banchosucksStartingWarmup && GetContainingInputManager()?.CurrentState?.Keyboard.ControlPressed == true)
            {
                var autoInstance = getAutoplayMod();

                if (autoInstance == null)
                {
                    notifications?.Post(new SimpleNotification
                    {
                        Text = NotificationsStrings.NoAutoplayMod
                    });
                    return;
                }

                var mods = Mods.Value.Append(autoInstance).ToArray();

                if (!ModUtils.CheckCompatibleSet(mods, out var invalid))
                    mods = mods.Except(invalid).Append(autoInstance).ToArray();

                Mods.Value = mods;
            }

            sampleConfirmSelection?.Play();

            this.Push(playerLoader = new PlayerLoader(createPlayer));

            Player createPlayer()
            {
                Player player;

                var replayGeneratingMod = Mods.Value.OfType<ICreateReplayData>().FirstOrDefault();

                if (replayGeneratingMod != null)
                {
                    player = new ReplayPlayer(replayGeneratingMod.CreateScoreFromReplayData);
                }
                else
                {
                    player = new SoloPlayer();
                }

                return player;
            }
        }

        public void Edit(BeatmapInfo beatmap)
        {
            if (!this.IsCurrentScreen())
                return;

            SelectAndRun(beatmap, () => this.Push(new EditorLoader()));
        }

        public override void OnResuming(ScreenTransitionEvent e)
        {
            base.OnResuming(e);
            revertMods();

            // Banchosucks: back at song select without asking for the next map means the warm-up series was left.
            banchosucksWarmup.OnSongSelectResumed();
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            if (base.OnExiting(e))
                return true;

            // Banchosucks: leaving song select ends a warm-up series.
            banchosucksWarmup.Stop();

            revertMods();
            return false;
        }

        private ModAutoplay? getAutoplayMod() => Ruleset.Value.CreateInstance().GetAutoplayMod();

        private void revertMods()
        {
            if (playerLoader == null) return;

            Mods.Value = modsAtGameplayStart;
            playerLoader = null;
        }

        private partial class PlayerLoader : Play.PlayerLoader
        {
            public PlayerLoader(Func<Player> createPlayer)
                : base(createPlayer)
            {
                FooterContent.Value = ScreenFooterContent.Argon();
            }
        }
    }
}
