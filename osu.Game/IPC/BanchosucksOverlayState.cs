// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;

namespace osu.Game.IPC
{
    /// <summary>
    /// Banchosucks: everything the overlay data source knows about the game at one moment, as plain values.
    /// Filled by <see cref="BanchosucksOverlayDataSource"/> on the update thread, never changed afterwards,
    /// turned into tosu's v2 JSON by <see cref="BanchosucksOverlayJson"/>.
    /// </summary>
    /// <remarks>
    /// Deliberately absent, whatever an overlay might want: password, access tokens, e-mail address, IP address,
    /// country, chat contents and local file paths.
    /// </remarks>
    public sealed class BanchosucksOverlayState
    {
        /// <summary>
        /// The screen the player is on, in tosu's numbering (see <see cref="BanchosucksOverlayGameState"/>).
        /// </summary>
        public BanchosucksOverlayGameState Game = BanchosucksOverlayGameState.Menu;

        /// <summary>
        /// The client version, for <c>settings.client.version</c>.
        /// </summary>
        public string ClientVersion = string.Empty;

        /// <summary>
        /// Milliseconds since the game started, in steps of a second.
        /// </summary>
        public long SessionPlayTime;

        /// <summary>
        /// Plays started since the game started.
        /// </summary>
        public int SessionPlayCount;

        /// <summary>
        /// The ruleset selected in the game (0 osu, 1 taiko, 2 catch, 3 mania).
        /// </summary>
        public int RulesetId;

        public readonly SettingsState Settings = new SettingsState();
        public readonly ProfileState Profile = new ProfileState();
        public readonly BeatmapState Beatmap = new BeatmapState();
        public readonly PlayState Play = new PlayState();
        public readonly ResultsState Results = new ResultsState();

        public sealed class SettingsState
        {
            public bool InterfaceVisible = true;
            public bool Fullscreen;
            public int WindowWidth;
            public int WindowHeight;
            public string SkinName = string.Empty;

            /// <summary>
            /// 0 to 100.
            /// </summary>
            public double VolumeMaster;

            public double VolumeMusic;
            public double VolumeEffect;

            /// <summary>
            /// The global audio offset in milliseconds.
            /// </summary>
            public double AudioOffset;

            /// <summary>
            /// Background dim, 0 to 100.
            /// </summary>
            public double BackgroundDim;

            public bool Storyboard;
            public double CursorSize;
        }

        public sealed class ProfileState
        {
            public bool Online;

            /// <summary>
            /// What the player is doing, in the numbering of bancho's status (0 idle, 2 playing, 3 editing, 6 watching, 11 lobby).
            /// </summary>
            public int ActivityNumber;

            public string ActivityName = "idle";

            public int Id;
            public string Name = string.Empty;
            public long RankedScore;
            public double Level;

            /// <summary>
            /// 0 to 100.
            /// </summary>
            public double Accuracy;

            public double PP;
            public int PlayCount;
            public int GlobalRank;
        }

        public sealed class BeatmapState
        {
            public bool IsKiai;
            public bool IsBreak;
            public bool IsConvert;

            /// <summary>
            /// Position of the song in milliseconds.
            /// </summary>
            public double TimeLive;

            public double TimeFirstObject;
            public double TimeLastObject;

            /// <summary>
            /// Length of the audio file in milliseconds.
            /// </summary>
            public double TimeMp3Length;

            /// <summary>
            /// Ranked status in tosu's numbering (see <see cref="BanchosucksOverlayJson.RankedStatusName"/>).
            /// </summary>
            public int StatusNumber;

            public string Checksum = string.Empty;
            public int Id;
            public int SetId;

            /// <summary>
            /// The ruleset the beatmap was made for.
            /// </summary>
            public int RulesetId;

            public string Artist = string.Empty;
            public string ArtistUnicode = string.Empty;
            public string Title = string.Empty;
            public string TitleUnicode = string.Empty;
            public string Mapper = string.Empty;
            public string Version = string.Empty;

            /// <summary>
            /// Star rating with the selected mods.
            /// </summary>
            public double Stars;

            /// <summary>
            /// Star rating of the part played so far; equal to <see cref="Stars"/> outside gameplay.
            /// </summary>
            public double StarsLive;

            /// <summary>
            /// pp of a perfect play with the selected mods (<c>performance.accuracy.100</c>).
            /// </summary>
            public double MaxPP;

            public double ApproachRate;
            public double ApproachRateConverted;
            public double CircleSize;
            public double CircleSizeConverted;
            public double OverallDifficulty;
            public double OverallDifficultyConverted;
            public double DrainRate;
            public double DrainRateConverted;

            public double BpmRealtime;
            public double BpmCommon;
            public double BpmMin;
            public double BpmMax;

            public int Circles;
            public int Sliders;
            public int Spinners;
            public int Holds;
            public int Objects;
            public int MaxCombo;
        }

        public sealed class ModsState
        {
            /// <summary>
            /// The stable mod bitmask, as far as the mods have one.
            /// </summary>
            public int Number;

            public string[] Acronyms = Array.Empty<string>();

            /// <summary>
            /// Speed of the song (1.5 for Double Time).
            /// </summary>
            public double Rate = 1;
        }

        public sealed class HitsState
        {
            public int Great;
            public int Geki;
            public int Ok;
            public int Katu;
            public int Meh;
            public int Miss;
            public int SliderEndHits;
            public int SmallTickHits;
            public int LargeTickHits;
        }

        public sealed class PerformanceState
        {
            public double Total;
            public double Aim;
            public double Speed;
            public double Accuracy;
            public double Difficulty;
            public double Flashlight;
        }

        public sealed class PlayState
        {
            /// <summary>
            /// Whether a play (or replay) is on screen. Without one, everything else in here keeps its default.
            /// </summary>
            public bool Active;

            public string PlayerName = string.Empty;
            public int RulesetId;
            public long Score;

            /// <summary>
            /// 0 to 100.
            /// </summary>
            public double Accuracy = 100;

            /// <summary>
            /// 0 to 1.
            /// </summary>
            public double Health;

            public readonly HitsState Hits = new HitsState();
            public int Combo;
            public int MaxCombo;
            public ModsState Mods = new ModsState();

            /// <summary>
            /// "XH", "X", "SH", "S", "A", "B", "C", "D" or "F"; empty without a play.
            /// </summary>
            public string Rank = string.Empty;

            public PerformanceState PP = new PerformanceState();
            public PerformanceState PPIfFullCombo = new PerformanceState();

            /// <summary>
            /// The highest pp value reached during this play.
            /// </summary>
            public double PPMaxThisPlay;

            public double UnstableRate;
        }

        public sealed class ResultsState
        {
            /// <summary>
            /// Whether a results screen with a score is on screen.
            /// </summary>
            public bool Active;

            public long ScoreId;
            public string PlayerName = string.Empty;
            public int RulesetId;
            public long Score;

            /// <summary>
            /// 0 to 100.
            /// </summary>
            public double Accuracy;

            public readonly HitsState Hits = new HitsState();
            public ModsState Mods = new ModsState();
            public int MaxCombo;
            public string Rank = string.Empty;
            public double PP;

            /// <summary>
            /// ISO 8601, empty without a score.
            /// </summary>
            public string CreatedAt = string.Empty;
        }
    }

    /// <summary>
    /// Banchosucks: tosu's <c>state.number</c> (the screen numbers of osu!stable). Only the ones this client reports.
    /// </summary>
    public enum BanchosucksOverlayGameState
    {
        Menu = 0,
        Edit = 1,
        Play = 2,
        SelectPlay = 5,
        ResultScreen = 7,
        Lobby = 11,
    }
}
