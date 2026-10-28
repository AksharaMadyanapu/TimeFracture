// ============================================================
// AudioLogData.cs
// ------------------------------------------------------------
// ScriptableObject asset defining a narrative audio log in Time Fracture.
// Contains audio, timed subtitles, era origin, and WorldState flag.
// ============================================================

using System;
using UnityEngine;
using TimelineEra = TimeFracture.Era.Era;

namespace TimeFracture.AudioLogs
{
    [System.Serializable]
    public struct SubtitleLine
    {
        [Tooltip("Seconds after audio start when this subtitle appears.")]
        public float timestamp;

        [Tooltip("Subtitle text displayed on screen.")]
        [TextArea(2, 4)]
        public string text;
    }

    [CreateAssetMenu(fileName = "NewAudioLog", menuName = "TimeFracture/AudioLogData")]
    public class AudioLogData : ScriptableObject
    {
        [Header("Identification")]
        [Tooltip("Unique string identifier (e.g. 'log_01_reactor').")]
        public string id = "log_01";

        [Tooltip("Display title shown in prompts, log menu, and HUD.")]
        public string title = "Audio Log #1";

        [Header("Audio & Subtitles")]
        [Tooltip("Audio clip for playback. If unassigned, procedural static is synthesized.")]
        public AudioClip audioClip;

        [Tooltip("List of timed subtitle lines.")]
        public SubtitleLine[] subtitles;

        [Header("Lore & State")]
        [Tooltip("The facility era where this recording was made or discovered.")]
        public TimelineEra eraFoundIn = TimelineEra.Past;

        [Tooltip("WorldState flag key set to true when this log is collected.")]
        public string worldStateFlag = "AudioLog_01";

        [Tooltip("Full text transcript of this audio log for the replay menu.")]
        [TextArea(4, 8)]
        public string fullTranscript = "";
    }
}
