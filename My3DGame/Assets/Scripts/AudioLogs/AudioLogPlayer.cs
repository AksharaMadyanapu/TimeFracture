// ============================================================
// AudioLogPlayer.cs
// ------------------------------------------------------------
// Manages playback of collected audio logs.
// Features:
//   - Timed TextMeshPro subtitles displayed at bottom-center.
//   - Distortion effect (AudioDistortionFilter + pitch wobble).
//   - Procedural radio tone synthesis if no audio clip is assigned.
//   - Static events for other systems to subscribe to.
//   - Self-wiring HUD Canvas and subtitle text.
// ============================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TimeFracture.Player;
using TimeFracture.Activity;

namespace TimeFracture.AudioLogs
{
    public class AudioLogPlayer : MonoBehaviour
    {
        // ──────────────────────────────────────────────────
        // SINGLETON & EVENTS
        // ──────────────────────────────────────────────────

        private static AudioLogPlayer _instance;
        public static AudioLogPlayer Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = UnityEngine.Object.FindFirstObjectByType<AudioLogPlayer>();
                    if (_instance == null)
                    {
                        GameObject obj = new GameObject("AudioLogPlayer");
                        _instance = obj.AddComponent<AudioLogPlayer>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        public static event Action<AudioLogData> OnLogStarted;
        public static event Action<AudioLogData> OnLogCompleted;

        // ──────────────────────────────────────────────────
        // INSPECTOR FIELDS
        // ──────────────────────────────────────────────────

        [Header("Audio & Distortion")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioDistortionFilter distortionFilter;

        [Tooltip("Strength of tape distortion (0 = clean, 1 = heavily distorted).")]
        [Range(0f, 1f)]
        [SerializeField] private float distortionLevel = 0.35f;

        [Tooltip("Amount of analog tape pitch wow/flutter modulation.")]
        [SerializeField] private float pitchWobbleAmount = 0.04f;

        [Header("Subtitles UI")]
        [SerializeField] private Canvas subtitleCanvas;
        [SerializeField] private TextMeshProUGUI subtitleText;

        // ──────────────────────────────────────────────────
        // INTERNAL STATE
        // ──────────────────────────────────────────────────

        private AudioLogData _currentLog;
        private Coroutine _playbackRoutine;
        private bool _isPlaying = false;
        private float _basePitch = 1.0f;
        private readonly List<AudioLogData> _collectedLogs = new List<AudioLogData>();

        // ──────────────────────────────────────────────────
        // PROPERTIES
        // ──────────────────────────────────────────────────

        public bool IsPlaying => _isPlaying;
        public AudioLogData CurrentLog => _currentLog;
        public IReadOnlyList<AudioLogData> CollectedLogs => _collectedLogs;

        // ──────────────────────────────────────────────────
        // UNITY LIFECYCLE
        // ──────────────────────────────────────────────────

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            SelfWireComponents();
            EnsureSubtitleUI();
            HideSubtitles();
        }

        private void Update()
        {
            if (_isPlaying && audioSource != null && audioSource.isPlaying)
            {
                // Subtle pitch wobble (tape flutter)
                float wobble = Mathf.Sin(Time.time * 7f) * pitchWobbleAmount;
                audioSource.pitch = _basePitch + wobble;
            }
        }

        // ──────────────────────────────────────────────────
        // PUBLIC PLAYBACK API
        // ──────────────────────────────────────────────────

        /// <summary>
        /// Registers a log as discovered and starts playback.
        /// </summary>
        public void PlayLog(AudioLogData logData)
        {
            if (logData == null) return;

            if (!_collectedLogs.Contains(logData))
            {
                _collectedLogs.Add(logData);
            }

            if (_playbackRoutine != null)
            {
                StopCoroutine(_playbackRoutine);
            }

            _playbackRoutine = StartCoroutine(PlaybackCoroutine(logData));
        }

        /// <summary>
        /// Stops current playback and clears the subtitle display.
        /// </summary>
        public void StopPlayback()
        {
            if (_playbackRoutine != null)
            {
                StopCoroutine(_playbackRoutine);
                _playbackRoutine = null;
            }

            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.pitch = 1.0f;
            }

            _isPlaying = false;
            _currentLog = null;
            HideSubtitles();
        }

        // ──────────────────────────────────────────────────
        // PLAYBACK COROUTINE
        // ──────────────────────────────────────────────────

        private IEnumerator PlaybackCoroutine(AudioLogData log)
        {
            _currentLog = log;
            _isPlaying = true;
            OnLogStarted?.Invoke(log);

            // Determine audio clip or synthesized procedural tone
            AudioClip clipToPlay = log.audioClip;
            float totalDuration = 0f;

            if (clipToPlay == null)
            {
                // Calculate duration from subtitles or default 8s
                if (log.subtitles != null && log.subtitles.Length > 0)
                {
                    float maxTime = log.subtitles[log.subtitles.Length - 1].timestamp + 4.5f;
                    totalDuration = Mathf.Max(6.0f, maxTime);
                }
                else
                {
                    totalDuration = 7.0f;
                }

                clipToPlay = GenerateDistortedStaticClip("Procedural_Log_" + log.id, totalDuration);
            }
            else
            {
                totalDuration = clipToPlay.length;
            }

            // Configure audio
            if (audioSource != null)
            {
                audioSource.clip = clipToPlay;
                audioSource.volume = 0.85f;
                audioSource.Play();
            }

            // Small noise broadcast to nearby ghosts
            NoiseEmitter.Emit(transform.position, 4.0f);

            // Raise ghost activity slightly
            GhostActivityManager.Instance?.AddActivity(5.0f);

            // Subtitle playback loop
            float playbackTimer = 0f;
            int subtitleIndex = 0;

            while (playbackTimer < totalDuration)
            {
                playbackTimer += Time.deltaTime;

                // Check for subtitle lines
                if (log.subtitles != null && subtitleIndex < log.subtitles.Length)
                {
                    if (playbackTimer >= log.subtitles[subtitleIndex].timestamp)
                    {
                        ShowSubtitle(log.title, log.subtitles[subtitleIndex].text);
                        subtitleIndex++;
                    }
                }

                yield return null;
            }

            // Finish
            HideSubtitles();
            _isPlaying = false;
            OnLogCompleted?.Invoke(log);
            _playbackRoutine = null;
        }

        // ──────────────────────────────────────────────────
        // SUBTITLE UI
        // ──────────────────────────────────────────────────

        private void ShowSubtitle(string title, string text)
        {
            if (subtitleText == null) return;
            subtitleText.text = $"<color=#00E5FF><b>[{title}]</b></color>\n<color=#FFFFFF>\"{text}\"</color>";
            subtitleText.enabled = true;
        }

        private void HideSubtitles()
        {
            if (subtitleText != null)
            {
                subtitleText.text = string.Empty;
                subtitleText.enabled = false;
            }
        }

        private void EnsureSubtitleUI()
        {
            if (subtitleText != null) return;

            GameObject canvasObj = GameObject.Find("AudioLogSubtitle_Canvas");
            if (canvasObj == null)
            {
                canvasObj = new GameObject("AudioLogSubtitle_Canvas");
                subtitleCanvas = canvasObj.AddComponent<Canvas>();
                subtitleCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                subtitleCanvas.sortingOrder = 90;

                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                canvasObj.AddComponent<GraphicRaycaster>();
            }
            else
            {
                subtitleCanvas = canvasObj.GetComponent<Canvas>();
            }

            // Subtitle Panel
            GameObject panelObj = new GameObject("SubtitlePanel");
            panelObj.transform.SetParent(canvasObj.transform, false);

            Image bg = panelObj.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.03f, 0.05f, 0.70f);

            RectTransform rect = panelObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 65f);
            rect.sizeDelta = new Vector2(850f, 90f);

            // Subtitle TMP
            GameObject textObj = new GameObject("SubtitleText");
            textObj.transform.SetParent(panelObj.transform, false);

            subtitleText = textObj.AddComponent<TextMeshProUGUI>();
            subtitleText.fontSize = 20f;
            subtitleText.alignment = TextAlignmentOptions.Center;
            subtitleText.textWrappingMode = TextWrappingModes.Normal;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(15f, 8f);
            textRect.offsetMax = new Vector2(-15f, -8f);
        }

        // ──────────────────────────────────────────────────
        // SELF-WIRING & PROCEDURAL STATIC
        // ──────────────────────────────────────────────────

        private void SelfWireComponents()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
            }
            if (audioSource != null)
            {
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0.0f; // 2D narrator / player listening
            }

            if (distortionFilter == null)
            {
                distortionFilter = GetComponent<AudioDistortionFilter>() ?? gameObject.AddComponent<AudioDistortionFilter>();
            }
            if (distortionFilter != null)
            {
                distortionFilter.distortionLevel = distortionLevel;
            }
        }

        private static AudioClip GenerateDistortedStaticClip(string name, float duration)
        {
            int sampleRate = 44100;
            int totalSamples = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            float carrierPhase = 0f;
            float carrierFreq = 380f;

            for (int i = 0; i < totalSamples; i++)
            {
                carrierPhase += 2f * Mathf.PI * carrierFreq / sampleRate;
                float tone = Mathf.Sin(carrierPhase) * 0.15f;
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.08f;
                samples[i] = tone + noise;
            }

            AudioClip clip = AudioClip.Create(name, totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
