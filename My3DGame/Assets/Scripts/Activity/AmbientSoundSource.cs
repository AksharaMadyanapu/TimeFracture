// ============================================================
// AmbientSoundSource.cs
// ------------------------------------------------------------
// Plays eerie ambient sounds (whisper, knock, distant footstep)
// from a random position near its transform when the activity
// tier is Uneasy or higher.
//
// ALL AUDIO IS PROCEDURALLY GENERATED with AudioClip.Create
// so no external sound assets are needed.
//
// AUDIO TYPES generated in code:
//   Whisper  - low amplitude noise with slow amplitude envelope
//   Knock    - single transient click burst
//   Footstep - rhythmic low-frequency thump pair
// ============================================================

using System.Collections;
using UnityEngine;
using TimeFracture.Activity;

namespace TimeFracture.Sabotage
{
    [RequireComponent(typeof(AudioSource))]
    public class AmbientSoundSource : MonoBehaviour
    {
        // ──────────────────────────────────────────────────
        // INSPECTOR FIELDS
        // ──────────────────────────────────────────────────

        [Header("Timing")]
        [Tooltip("Minimum seconds between sound events at Uneasy.")]
        [SerializeField] private float minInterval = 6f;

        [Tooltip("Maximum seconds between sound events at Uneasy.")]
        [SerializeField] private float maxInterval = 18f;

        [Tooltip("Interval multiplier at Active tier (shorter pauses).")]
        [SerializeField] private float activeTierMultiplier = 0.55f;

        [Tooltip("Interval multiplier at Hunting tier (frequent sounds).")]
        [SerializeField] private float huntingTierMultiplier = 0.25f;

        [Header("Spawn Radius")]
        [Tooltip("Random offset radius from this object where sound appears to come from.")]
        [SerializeField] private float spawnRadius = 4f;

        [Header("Volume")]
        [Tooltip("Peak volume of generated ambient sounds.")]
        [SerializeField] [Range(0f, 1f)] private float soundVolume = 0.55f;

        // ──────────────────────────────────────────────────
        // INTERNAL STATE
        // ──────────────────────────────────────────────────

        private AudioSource _audio;
        private ActivityTier _tier = ActivityTier.Calm;
        private float _soundTimer = 0f;

        // Generated procedural clips (cached so we don't regenerate every frame)
        private AudioClip _whisperClip;
        private AudioClip _knockClip;
        private AudioClip _footstepClip;

        // ──────────────────────────────────────────────────
        // UNITY LIFECYCLE
        // ──────────────────────────────────────────────────

        private void Awake()
        {
            _audio = GetComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 1f;   // Full 3D audio
            _audio.minDistance = 1f;
            _audio.maxDistance = 18f;
            _audio.rolloffMode = AudioRolloffMode.Logarithmic;

            // Pre-generate all clips once
            _whisperClip  = GenerateWhisperClip();
            _knockClip    = GenerateKnockClip();
            _footstepClip = GenerateFootstepClip();

            _soundTimer = Random.Range(minInterval, maxInterval);
        }

        private void OnEnable()
        {
            GhostActivityManager.OnTierChanged += HandleTierChange;
        }

        private void OnDisable()
        {
            GhostActivityManager.OnTierChanged -= HandleTierChange;
        }

        private void Update()
        {
            // Only play at Uneasy or above
            if (_tier == ActivityTier.Calm) return;

            _soundTimer -= Time.deltaTime;
            if (_soundTimer <= 0f)
            {
                PlayAmbientSound();
                ResetTimer();
            }
        }

        // ──────────────────────────────────────────────────
        // EVENT HANDLER
        // ──────────────────────────────────────────────────

        private void HandleTierChange(ActivityTier newTier)
        {
            _tier = newTier;
        }

        // ──────────────────────────────────────────────────
        // SOUND EMISSION
        // ──────────────────────────────────────────────────

        private void PlayAmbientSound()
        {
            // Pick a random clip
            AudioClip[] candidates = { _whisperClip, _knockClip, _footstepClip };
            AudioClip chosen = candidates[Random.Range(0, candidates.Length)];
            if (chosen == null) return;

            // Offset position — make it sound like it's from nearby
            Vector3 randomOffset = new Vector3(
                Random.Range(-spawnRadius, spawnRadius),
                Random.Range(-0.5f, 1.5f),
                Random.Range(-spawnRadius, spawnRadius));
            _audio.transform.position = transform.position + randomOffset;

            _audio.pitch = Random.Range(0.88f, 1.12f);
            _audio.volume = soundVolume * Random.Range(0.7f, 1.0f);
            _audio.PlayOneShot(chosen);
        }

        private void ResetTimer()
        {
            float interval = Random.Range(minInterval, maxInterval);
            switch (_tier)
            {
                case ActivityTier.Active:   interval *= activeTierMultiplier;  break;
                case ActivityTier.Hunting:  interval *= huntingTierMultiplier; break;
            }
            _soundTimer = Mathf.Max(interval, 0.5f);
        }

        // ──────────────────────────────────────────────────
        // PROCEDURAL AUDIO GENERATORS
        // ──────────────────────────────────────────────────

        // Low amplitude brownian noise with a slow volume envelope (creepy whisper)
        private static AudioClip GenerateWhisperClip()
        {
            const int sampleRate = 22050;
            const float duration  = 1.8f;
            int samples = Mathf.RoundToInt(sampleRate * duration);
            float[] data = new float[samples];

            float prev = 0f;
            for (int i = 0; i < samples; i++)
            {
                // Brownian noise: drift by small random step
                prev = Mathf.Clamp(prev + Random.Range(-0.08f, 0.08f), -1f, 1f);

                // Envelope: rises over first 20 %, sustained, fades last 40 %
                float t = (float)i / samples;
                float env = t < 0.2f ? t / 0.2f :
                            t > 0.6f ? 1f - (t - 0.6f) / 0.4f : 1f;

                data[i] = prev * env * 0.22f;   // quiet — it's a whisper
            }

            AudioClip clip = AudioClip.Create("Whisper", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // A hard transient + short decay (knock on wood / wall tap)
        private static AudioClip GenerateKnockClip()
        {
            const int sampleRate = 22050;
            const float duration  = 0.3f;
            int samples = Mathf.RoundToInt(sampleRate * duration);
            float[] data = new float[samples];

            // Decaying sine burst at low frequency (body knock ~180 Hz)
            const float freq = 180f;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-t * 18f);   // fast exponential decay
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope * 0.7f;
            }

            AudioClip clip = AudioClip.Create("Knock", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Two low-frequency thuds (footstep pair)
        private static AudioClip GenerateFootstepClip()
        {
            const int sampleRate = 22050;
            const float duration  = 0.7f;
            int samples = Mathf.RoundToInt(sampleRate * duration);
            float[] data = new float[samples];

            // First thud at t=0, second at t=0.35
            float[] thudTimes = { 0f, 0.35f };
            const float thudFreq = 80f;

            foreach (float startTime in thudTimes)
            {
                int startSample = Mathf.RoundToInt(startTime * sampleRate);
                for (int i = startSample; i < samples; i++)
                {
                    float t = (float)(i - startSample) / sampleRate;
                    if (t > 0.25f) break;   // each thud lasts 250 ms
                    float envelope = Mathf.Exp(-t * 22f);
                    data[i] += Mathf.Sin(2f * Mathf.PI * thudFreq * t) * envelope * 0.55f;
                }
            }

            AudioClip clip = AudioClip.Create("Footstep", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
