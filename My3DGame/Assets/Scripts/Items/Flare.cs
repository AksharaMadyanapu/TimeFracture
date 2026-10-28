// ============================================================
// Flare.cs
// ------------------------------------------------------------
// Protective throwable/placeable item.
// Creates a bright point-light radius for ~20 seconds and
// maintains a protective FlareZone against darkness and ghosts
// (specifically designed to counter The Residue in Milestone 9).
//
// FEATURES:
//   - Rigidbody projectile physics
//   - Bright warm point light with subtle atmospheric flicker
//   - Self-wiring procedural hissing/burning sound if no clip assigned
//   - Automatic fade-out and cleanup after burn duration
// ============================================================

#pragma warning disable CS0618 // Rigidbody.velocity / linearVelocity compatibility

using UnityEngine;

namespace TimeFracture.Items
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(FlareZone))]
    public class Flare : MonoBehaviour
    {
        // ──────────────────────────────────────────────────
        // INSPECTOR FIELDS
        // ──────────────────────────────────────────────────

        [Header("Burn Settings")]
        [Tooltip("How long (in seconds) the flare remains burning and active.")]
        [SerializeField] private float burnDuration = 20.0f;

        [Tooltip("Seconds before burn completion when the light starts dimming.")]
        [SerializeField] private float fadeOutDuration = 2.5f;

        [Header("Components")]
        [Tooltip("Point light representing the flare flame.")]
        [SerializeField] private Light flareLight;

        [Tooltip("AudioSource playing the flare hissing/burning sound.")]
        [SerializeField] private AudioSource audioSource;

        [Tooltip("Optional audio clip for flare ignition and burning.")]
        [SerializeField] private AudioClip burnClip;

        // ──────────────────────────────────────────────────
        // INTERNAL STATE
        // ──────────────────────────────────────────────────

        private Rigidbody _rigidbody;
        private FlareZone _flareZone;
        private float _timeRemaining;
        private float _initialLightIntensity = 4.0f;
        private bool _isBurning = true;

        // ──────────────────────────────────────────────────
        // PROPERTIES
        // ──────────────────────────────────────────────────

        public bool IsBurning => _isBurning;
        public float TimeRemaining => _timeRemaining;

        // ──────────────────────────────────────────────────
        // UNITY LIFECYCLE
        // ──────────────────────────────────────────────────

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _flareZone = GetComponent<FlareZone>();
            _timeRemaining = burnDuration;

            SelfWireComponents();
        }

        private void Start()
        {
            if (flareLight != null)
            {
                _initialLightIntensity = flareLight.intensity;
            }

            StartBurnAudio();
        }

        private void Update()
        {
            if (!_isBurning) return;

            _timeRemaining -= Time.deltaTime;

            // Subtle atmospheric flame flicker
            if (flareLight != null && _timeRemaining > fadeOutDuration)
            {
                float noise = Mathf.PerlinNoise(Time.time * 8.0f, 0f) * 0.4f + 0.8f;
                flareLight.intensity = _initialLightIntensity * noise;
            }
            // Smooth fade out near expiration
            else if (flareLight != null && _timeRemaining <= fadeOutDuration)
            {
                float t = Mathf.Clamp01(_timeRemaining / fadeOutDuration);
                flareLight.intensity = _initialLightIntensity * t;
            }

            // Expiration
            if (_timeRemaining <= 0f)
            {
                Extinguish();
            }
        }

        // ──────────────────────────────────────────────────
        // PUBLIC API
        // ──────────────────────────────────────────────────

        /// <summary>
        /// Launches the flare forward with the given initial velocity.
        /// </summary>
        public void Launch(Vector3 velocity)
        {
            if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody>();
            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = velocity;
                // Add slight random spin for natural arc feel
                _rigidbody.angularVelocity = Random.insideUnitSphere * 4f;
            }
        }

        /// <summary>
        /// Extinguishes the flare, disables the light and FlareZone, and cleans up.
        /// </summary>
        public void Extinguish()
        {
            if (!_isBurning) return;
            _isBurning = false;

            if (flareLight != null) flareLight.enabled = false;
            if (_flareZone != null) _flareZone.enabled = false;
            if (audioSource != null) audioSource.Stop();

            Destroy(gameObject, 0.2f);
        }

        // ──────────────────────────────────────────────────
        // SELF-WIRING HELPERS (ZERO MANUAL SETUP)
        // ──────────────────────────────────────────────────

        private void SelfWireComponents()
        {
            // Point Light
            if (flareLight == null)
            {
                flareLight = GetComponentInChildren<Light>();
                if (flareLight == null)
                {
                    GameObject lightObj = new GameObject("FlareLight");
                    lightObj.transform.SetParent(transform, false);
                    flareLight = lightObj.AddComponent<Light>();
                    flareLight.type = LightType.Point;
                    flareLight.range = 10f;
                    flareLight.intensity = 4.5f;
                    flareLight.color = new Color(1.0f, 0.45f, 0.15f); // Rich warm flame orange
                }
            }

            // Audio Source & Procedural Hiss
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
            }
            if (audioSource != null)
            {
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1.0f;
                audioSource.minDistance = 2f;
                audioSource.maxDistance = 20f;
                audioSource.loop = true;
            }
        }

        private void StartBurnAudio()
        {
            if (audioSource == null) return;

            if (burnClip == null)
            {
                burnClip = CreateProceduralHissClip("Flare_Hiss", 1.5f);
            }

            audioSource.clip = burnClip;
            audioSource.volume = 0.4f;
            audioSource.Play();
        }

        /// <summary>
        /// Generates procedural white/pink noise hiss so the flare has realistic burning audio
        /// without requiring imported audio assets.
        /// </summary>
        private static AudioClip CreateProceduralHissClip(string name, float duration)
        {
            int sampleRate = 44100;
            int totalSamples = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            float lastSample = 0f;
            for (int i = 0; i < totalSamples; i++)
            {
                // Soft pink noise filter
                float white = (Random.value * 2f - 1f) * 0.25f;
                lastSample = (lastSample + white) * 0.5f;
                samples[i] = lastSample;
            }

            AudioClip clip = AudioClip.Create(name, totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
