// ============================================================
// AudioManager.cs
// ------------------------------------------------------------
// Central audio controller for Time Fracture.
// Features:
//   - Auto-created singleton.
//   - Master, SFX, Music, and Voice volume channels saved in PlayerPrefs.
//   - Procedural ambient drone synthesis per era (Past clean hum, Present decayed wind).
//   - Smooth crossfade on era transitions.
// ============================================================

using System;
using UnityEngine;
using TimeFracture.Era;
using TimelineEra = TimeFracture.Era.Era;

namespace TimeFracture.Audio
{
    public class AudioManager : MonoBehaviour
    {
        // ──────────────────────────────────────────────────
        // SINGLETON & INITIALIZATION
        // ──────────────────────────────────────────────────

        public static AudioManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                GameObject obj = new GameObject("AudioManager");
                Instance = obj.AddComponent<AudioManager>();
                DontDestroyOnLoad(obj);
            }
        }

        // ──────────────────────────────────────────────────
        // VOLUME SETTINGS
        // ──────────────────────────────────────────────────

        public float MasterVolume { get; private set; } = 1.0f;
        public float SFXVolume { get; private set; } = 1.0f;
        public float MusicVolume { get; private set; } = 0.8f;
        public float VoiceVolume { get; private set; } = 1.0f;

        // ──────────────────────────────────────────────────
        // INTERNAL STATE
        // ──────────────────────────────────────────────────

        private AudioSource _pastAmbienceSource;
        private AudioSource _presentAmbienceSource;
        private TimelineEra _currentEra = TimelineEra.Past;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            LoadVolumePreferences();
            SetupAmbientSources();
        }

        private void OnEnable()
        {
            EraManager.OnEraChanged += HandleEraChanged;
        }

        private void OnDisable()
        {
            EraManager.OnEraChanged -= HandleEraChanged;
        }

        private void Update()
        {
            // Crossfade ambient loops based on active era
            float targetPastVol = (_currentEra == TimelineEra.Past ? 0.35f : 0f) * MasterVolume * MusicVolume;
            float targetPresentVol = (_currentEra == TimelineEra.Present ? 0.45f : 0f) * MasterVolume * MusicVolume;

            if (_pastAmbienceSource != null)
            {
                _pastAmbienceSource.volume = Mathf.MoveTowards(_pastAmbienceSource.volume, targetPastVol, Time.deltaTime * 0.5f);
            }
            if (_presentAmbienceSource != null)
            {
                _presentAmbienceSource.volume = Mathf.MoveTowards(_presentAmbienceSource.volume, targetPresentVol, Time.deltaTime * 0.5f);
            }
        }

        // ──────────────────────────────────────────────────
        // PUBLIC VOLUME API
        // ──────────────────────────────────────────────────

        public void SetMasterVolume(float vol)
        {
            MasterVolume = Mathf.Clamp01(vol);
            AudioListener.volume = MasterVolume;
            PlayerPrefs.SetFloat("MasterVolume", MasterVolume);
        }

        public void SetSFXVolume(float vol)
        {
            SFXVolume = Mathf.Clamp01(vol);
            PlayerPrefs.SetFloat("SFXVolume", SFXVolume);
        }

        public void SetMusicVolume(float vol)
        {
            MusicVolume = Mathf.Clamp01(vol);
            PlayerPrefs.SetFloat("MusicVolume", MusicVolume);
        }

        public void SetVoiceVolume(float vol)
        {
            VoiceVolume = Mathf.Clamp01(vol);
            PlayerPrefs.SetFloat("VoiceVolume", VoiceVolume);
        }

        private void LoadVolumePreferences()
        {
            MasterVolume = PlayerPrefs.GetFloat("MasterVolume", 1.0f);
            SFXVolume = PlayerPrefs.GetFloat("SFXVolume", 1.0f);
            MusicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.8f);
            VoiceVolume = PlayerPrefs.GetFloat("VoiceVolume", 1.0f);
            AudioListener.volume = MasterVolume;
        }

        // ──────────────────────────────────────────────────
        // PROCEDURAL AMBIENCE GENERATION
        // ──────────────────────────────────────────────────

        private void HandleEraChanged(TimelineEra newEra)
        {
            _currentEra = newEra;
        }

        private void SetupAmbientSources()
        {
            // Past Ambience: Laboratory Generator Hum (low warm drone 60Hz + 120Hz)
            _pastAmbienceSource = gameObject.AddComponent<AudioSource>();
            _pastAmbienceSource.clip = GenerateLabHumClip("Past_LabDrone", 4.0f);
            _pastAmbienceSource.loop = true;
            _pastAmbienceSource.playOnAwake = true;
            _pastAmbienceSource.volume = 0.35f;
            _pastAmbienceSource.Play();

            // Present Ambience: Decayed Ruin Wind (soft filtered turbulence)
            _presentAmbienceSource = gameObject.AddComponent<AudioSource>();
            _presentAmbienceSource.clip = GenerateWindClip("Present_RuinWind", 4.0f);
            _presentAmbienceSource.loop = true;
            _presentAmbienceSource.playOnAwake = true;
            _presentAmbienceSource.volume = 0f;
            _presentAmbienceSource.Play();
        }

        private static AudioClip GenerateLabHumClip(string name, float duration)
        {
            int sampleRate = 44100;
            int totalSamples = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float tone1 = Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.4f;
                float tone2 = Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.25f;
                float harmonic = Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.1f;
                samples[i] = tone1 + tone2 + harmonic;
            }

            AudioClip clip = AudioClip.Create(name, totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip GenerateWindClip(string name, float duration)
        {
            int sampleRate = 44100;
            int totalSamples = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            float last = 0f;
            for (int i = 0; i < totalSamples; i++)
            {
                float white = (UnityEngine.Random.value * 2f - 1f) * 0.3f;
                last = (last * 0.95f) + (white * 0.05f); // Low pass filter
                samples[i] = last;
            }

            AudioClip clip = AudioClip.Create(name, totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
