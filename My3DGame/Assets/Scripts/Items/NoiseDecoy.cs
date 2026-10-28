// ============================================================
// NoiseDecoy.cs
// ------------------------------------------------------------
// Protective throwable sound-lure item.
// Thrown with a projectile arc; on impact (or timeout fallback)
// begins emitting repeating sound pulses via NoiseEmitter.Emit
// for ~8 seconds.
//
// DESIGN & GHOST BEHAVIOUR:
//   - Specifically counters The Echo (Milestone 5).
//   - Broadcasts noise within hearing range (default 8.5m).
//   - Causes The Echo to leave patrol and switch to Investigate
//     at the decoy's impact position, buying the player time
//     to slip past or reach a hiding spot.
// ============================================================

#pragma warning disable CS0618 // Rigidbody.velocity / linearVelocity compatibility

using UnityEngine;
using TimeFracture.Player;

namespace TimeFracture.Items
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class NoiseDecoy : MonoBehaviour
    {
        // ──────────────────────────────────────────────────
        // INSPECTOR FIELDS
        // ──────────────────────────────────────────────────

        [Header("Noise Emission")]
        [Tooltip("Noise radius broadcast to ghost AI. Set to 8.5m so it lures The Echo into Investigate state.")]
        [SerializeField] private float noiseRadius = 8.5f;

        [Tooltip("Total duration (in seconds) that the decoy repeats noise pulses after activation.")]
        [SerializeField] private float activeDuration = 8.0f;

        [Tooltip("Interval (in seconds) between repeated noise pulses.")]
        [SerializeField] private float pulseInterval = 0.8f;

        [Tooltip("Fallback seconds after launch to activate automatically if no collision occurs.")]
        [SerializeField] private float fallbackActivationTime = 1.5f;

        [Header("Components & Feedback")]
        [Tooltip("Light component that flashes with each audio pulse.")]
        [SerializeField] private Light pulseLight;

        [Tooltip("Renderer whose emissive material flashes.")]
        [SerializeField] private Renderer emissiveRenderer;

        [Tooltip("AudioSource that plays each electronic chirp/beep.")]
        [SerializeField] private AudioSource audioSource;

        [Tooltip("Audio clip played with each noise pulse.")]
        [SerializeField] private AudioClip beepClip;

        // ──────────────────────────────────────────────────
        // INTERNAL STATE
        // ──────────────────────────────────────────────────

        private Rigidbody _rigidbody;
        private bool _isLaunched = false;
        private bool _isEmitting = false;
        private float _timeSinceLaunch = 0f;
        private float _emissionTimer = 0f;
        private float _pulseTimer = 0f;
        private Material _instancedMat;
        private Color _baseEmissionColor = new Color(0f, 0.8f, 1f); // Electric cyan

        // ──────────────────────────────────────────────────
        // PROPERTIES
        // ──────────────────────────────────────────────────

        public bool IsEmitting => _isEmitting;
        public float NoiseRadius => noiseRadius;
        public float TimeRemaining => Mathf.Max(0f, activeDuration - _emissionTimer);

        // ──────────────────────────────────────────────────
        // UNITY LIFECYCLE
        // ──────────────────────────────────────────────────

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            SelfWireComponents();
        }

        private void Update()
        {
            // Fallback activation if thrown high or landing softly
            if (_isLaunched && !_isEmitting)
            {
                _timeSinceLaunch += Time.deltaTime;
                if (_timeSinceLaunch >= fallbackActivationTime)
                {
                    ActivateEmission();
                }
            }

            if (!_isEmitting) return;

            // Emission duration tracking
            _emissionTimer += Time.deltaTime;
            _pulseTimer += Time.deltaTime;

            if (_pulseTimer >= pulseInterval)
            {
                _pulseTimer = 0f;
                EmitPulse();
            }

            // Visual decay/flash fade
            if (pulseLight != null && pulseLight.intensity > 0f)
            {
                pulseLight.intensity = Mathf.Max(0f, pulseLight.intensity - Time.deltaTime * 6f);
            }

            // Expiration
            if (_emissionTimer >= activeDuration)
            {
                FinishAndDestroy();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!_isEmitting)
            {
                ActivateEmission();
            }
        }

        // ──────────────────────────────────────────────────
        // PUBLIC API
        // ──────────────────────────────────────────────────

        /// <summary>
        /// Launches the decoy with projectile velocity.
        /// </summary>
        public void Launch(Vector3 velocity)
        {
            if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody>();
            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = velocity;
                _rigidbody.angularVelocity = Random.insideUnitSphere * 8f;
            }
            _isLaunched = true;
            _timeSinceLaunch = 0f;
        }

        /// <summary>
        /// Activates the repeating noise loop immediately.
        /// </summary>
        public void ActivateEmission()
        {
            if (_isEmitting) return;
            _isEmitting = true;
            _emissionTimer = 0f;
            _pulseTimer = pulseInterval; // Trigger first pulse right away
            EmitPulse();
        }

        /// <summary>
        /// Forces an immediate synchronous noise pulse. Ideal for automated testing.
        /// </summary>
        public void TriggerEmissionImmediate()
        {
            EmitPulse();
        }

        // ──────────────────────────────────────────────────
        // PULSE & FEEDBACK
        // ──────────────────────────────────────────────────

        private void EmitPulse()
        {
            // 1. Emit noise via static event for ghost AI
            NoiseEmitter.Emit(transform.position, noiseRadius);

            // 2. Play audio beep
            if (audioSource != null && beepClip != null)
            {
                audioSource.PlayOneShot(beepClip, 0.85f);
            }

            // 3. Flash light
            if (pulseLight != null)
            {
                pulseLight.intensity = 3.5f;
            }

            // 4. Flash emission
            if (_instancedMat != null && _instancedMat.HasProperty("_EmissionColor"))
            {
                _instancedMat.SetColor("_EmissionColor", _baseEmissionColor * 2.0f);
            }
        }

        private void FinishAndDestroy()
        {
            _isEmitting = false;
            if (pulseLight != null) pulseLight.enabled = false;
            if (_instancedMat != null && _instancedMat.HasProperty("_EmissionColor"))
            {
                _instancedMat.SetColor("_EmissionColor", Color.black);
            }

            Destroy(gameObject, 0.25f);
        }

        // ──────────────────────────────────────────────────
        // SELF-WIRING HELPERS (ZERO MANUAL SETUP)
        // ──────────────────────────────────────────────────

        private void SelfWireComponents()
        {
            // Audio Source & Procedural Beep
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
            }

            if (beepClip == null)
            {
                beepClip = CreateProceduralBeepClip("Decoy_Beep", 1200f, 0.12f);
            }

            // Light
            if (pulseLight == null)
            {
                pulseLight = GetComponentInChildren<Light>();
                if (pulseLight == null)
                {
                    GameObject lightObj = new GameObject("DecoyLight");
                    lightObj.transform.SetParent(transform, false);
                    pulseLight = lightObj.AddComponent<Light>();
                    pulseLight.type = LightType.Point;
                    pulseLight.range = 7f;
                    pulseLight.intensity = 0f;
                    pulseLight.color = _baseEmissionColor;
                }
            }

            // Renderer material
            if (emissiveRenderer == null)
            {
                emissiveRenderer = GetComponent<Renderer>();
            }
            if (emissiveRenderer != null)
            {
                _instancedMat = emissiveRenderer.material;
            }
        }

        /// <summary>
        /// Generates a high-pitch procedural electronic chirp clip.
        /// </summary>
        private static AudioClip CreateProceduralBeepClip(string name, float frequency, float duration)
        {
            int sampleRate = 44100;
            int totalSamples = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = 1f - ((float)i / totalSamples); // Fast decay
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.45f;
            }

            AudioClip clip = AudioClip.Create(name, totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnDestroy()
        {
            if (_instancedMat != null)
            {
                Destroy(_instancedMat);
            }
        }
    }
}
