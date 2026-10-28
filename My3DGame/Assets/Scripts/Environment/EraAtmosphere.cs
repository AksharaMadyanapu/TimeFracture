// ============================================================
// EraAtmosphere.cs
// ------------------------------------------------------------
// Visual and atmospheric environment controller.
// Features:
//   - Era-specific lighting, fog color and density.
//   - URP Volume profiles created in code per era (Past cool/clean, Present dark/desaturated).
//   - Procedural heartbeat audio synthesis whose pulse rate accelerates with the activity meter.
//   - Subtle atmospheric camera bob.
// ============================================================

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TimeFracture.Era;
using TimeFracture.Activity;
using TimelineEra = TimeFracture.Era.Era;

namespace TimeFracture.Environment
{
    public class EraAtmosphere : MonoBehaviour
    {
        // ──────────────────────────────────────────────────
        // SINGLETON
        // ──────────────────────────────────────────────────

        public static EraAtmosphere Instance { get; private set; }

        // ──────────────────────────────────────────────────
        // INSPECTOR FIELDS
        // ──────────────────────────────────────────────────

        [Header("Atmosphere Volumes")]
        [SerializeField] private Volume eraVolume;

        [Header("Heartbeat System")]
        [Tooltip("AudioSource playing procedural heartbeat.")]
        [SerializeField] private AudioSource heartbeatAudioSource;

        [Tooltip("Minimum heartbeat BPM during calm state.")]
        [SerializeField] private float minBPM = 60f;

        [Tooltip("Maximum heartbeat BPM during hunting tier.")]
        [SerializeField] private float maxBPM = 145f;

        [Header("Subtle Camera Bob")]
        [SerializeField] private Transform cameraHolder;
        [SerializeField] private float bobSpeed = 6.0f;
        [SerializeField] private float bobAmount = 0.035f;

        // ──────────────────────────────────────────────────
        // INTERNAL STATE
        // ──────────────────────────────────────────────────

        private VolumeProfile _pastProfile;
        private VolumeProfile _presentProfile;
        private float _heartbeatTimer = 0f;
        private AudioClip _heartbeatClip;
        private Vector3 _originalCamLocalPos;

        // ──────────────────────────────────────────────────
        // UNITY LIFECYCLE
        // ──────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(this);
                return;
            }

            SelfWireComponents();
            CreateVolumeProfiles();
        }

        private void OnEnable()
        {
            EraManager.OnEraChanged += HandleEraChanged;
        }

        private void OnDisable()
        {
            EraManager.OnEraChanged -= HandleEraChanged;
        }

        private void Start()
        {
            if (cameraHolder != null)
            {
                _originalCamLocalPos = cameraHolder.localPosition;
            }

            ApplyAtmosphere(EraManager.Instance != null ? EraManager.Instance.CurrentEra : TimelineEra.Past);
        }

        private void Update()
        {
            UpdateHeartbeat();
            UpdateCameraBob();
        }

        // ──────────────────────────────────────────────────
        // ERA TRANSITION
        // ──────────────────────────────────────────────────

        private void HandleEraChanged(TimelineEra newEra)
        {
            ApplyAtmosphere(newEra);
        }

        private void ApplyAtmosphere(TimelineEra era)
        {
            // 1. Fog adjustments
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;

            if (era == TimelineEra.Past)
            {
                // Past: Clean, clinical, faint cool cyan haze
                RenderSettings.fogColor = new Color(0.12f, 0.18f, 0.22f);
                RenderSettings.fogDensity = 0.025f;
                if (eraVolume != null && _pastProfile != null) eraVolume.profile = _pastProfile;
            }
            else
            {
                // Present: Heavy dark decayed fog, ominous amber/crimson undertone
                RenderSettings.fogColor = new Color(0.04f, 0.03f, 0.03f);
                RenderSettings.fogDensity = 0.055f;
                if (eraVolume != null && _presentProfile != null) eraVolume.profile = _presentProfile;
            }
        }

        // ──────────────────────────────────────────────────
        // DYNAMIC HEARTBEAT (TIED TO ACTIVITY METER)
        // ──────────────────────────────────────────────────

        private void UpdateHeartbeat()
        {
            float activity = GhostActivityManager.Instance != null
                ? GhostActivityManager.Instance.Activity
                : 0f;

            // Normalize activity (0 to 100)
            float t = Mathf.Clamp01(activity / 100f);
            float currentBPM = Mathf.Lerp(minBPM, maxBPM, t);
            float interval = 60f / currentBPM;

            _heartbeatTimer += Time.deltaTime;
            if (_heartbeatTimer >= interval)
            {
                _heartbeatTimer = 0f;
                PlayHeartbeatPulse(t);
            }
        }

        private void PlayHeartbeatPulse(float intensity)
        {
            if (heartbeatAudioSource == null || _heartbeatClip == null) return;

            // Heartbeat volume rises with tension
            float volume = Mathf.Lerp(0.15f, 0.85f, intensity);
            heartbeatAudioSource.pitch = Mathf.Lerp(0.9f, 1.25f, intensity);
            heartbeatAudioSource.PlayOneShot(_heartbeatClip, volume);
        }

        // ──────────────────────────────────────────────────
        // SUBTLE CAMERA BOB
        // ──────────────────────────────────────────────────

        private void UpdateCameraBob()
        {
            if (cameraHolder == null) return;

            // Check if player is moving
            float moveX = Input.GetAxis("Horizontal");
            float moveZ = Input.GetAxis("Vertical");
            bool isMoving = (Mathf.Abs(moveX) > 0.1f || Mathf.Abs(moveZ) > 0.1f);

            if (isMoving)
            {
                float bobY = Mathf.Sin(Time.time * bobSpeed) * bobAmount;
                cameraHolder.localPosition = new Vector3(_originalCamLocalPos.x, _originalCamLocalPos.y + bobY, _originalCamLocalPos.z);
            }
            else
            {
                cameraHolder.localPosition = Vector3.Lerp(cameraHolder.localPosition, _originalCamLocalPos, Time.deltaTime * 5f);
            }
        }

        // ──────────────────────────────────────────────────
        // SELF-WIRING & PROCEDURAL CREATION
        // ──────────────────────────────────────────────────

        private void SelfWireComponents()
        {
            if (eraVolume == null)
            {
                eraVolume = GetComponent<Volume>() ?? gameObject.AddComponent<Volume>();
                eraVolume.isGlobal = true;
                eraVolume.priority = 10;
            }

            if (heartbeatAudioSource == null)
            {
                heartbeatAudioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
            }
            if (heartbeatAudioSource != null)
            {
                heartbeatAudioSource.playOnAwake = false;
                heartbeatAudioSource.spatialBlend = 0.0f; // 2D player body sound
            }

            if (_heartbeatClip == null)
            {
                _heartbeatClip = CreateProceduralHeartbeatClip("Procedural_Heartbeat", 0.35f);
            }

            if (cameraHolder == null && Camera.main != null)
            {
                cameraHolder = Camera.main.transform.parent != null ? Camera.main.transform.parent : Camera.main.transform;
            }
        }

        private void CreateVolumeProfiles()
        {
            // Past Profile (Cool, clean, subtle bloom)
            _pastProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            _pastProfile.name = "Past_Atmosphere_Profile";

            ColorAdjustments pastColor = _pastProfile.Add<ColorAdjustments>(true);
            pastColor.colorFilter.Override(new Color(0.85f, 0.95f, 1.0f)); // Subtle cool blue tint
            pastColor.contrast.Override(15f);

            Bloom pastBloom = _pastProfile.Add<Bloom>(true);
            pastBloom.intensity.Override(0.5f);

            // Present Profile (Dark, decaying, desaturated, vignette)
            _presentProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            _presentProfile.name = "Present_Atmosphere_Profile";

            ColorAdjustments presentColor = _presentProfile.Add<ColorAdjustments>(true);
            presentColor.colorFilter.Override(new Color(0.70f, 0.65f, 0.60f));
            presentColor.saturation.Override(-35f); // Desaturated horror look
            presentColor.contrast.Override(30f);

            Vignette presentVignette = _presentProfile.Add<Vignette>(true);
            presentVignette.intensity.Override(0.42f);
            presentVignette.smoothness.Override(0.45f);
        }

        private static AudioClip CreateProceduralHeartbeatClip(string name, float duration)
        {
            int sampleRate = 44100;
            int totalSamples = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                // Double thump "lub-dub" waveform
                float thump1 = Mathf.Sin(2f * Mathf.PI * 45f * t) * Mathf.Exp(-t * 18f);
                float t2 = Mathf.Max(0f, t - 0.12f);
                float thump2 = Mathf.Sin(2f * Mathf.PI * 55f * t2) * Mathf.Exp(-t2 * 22f);

                samples[i] = (thump1 + thump2 * 0.7f) * 0.75f;
            }

            AudioClip clip = AudioClip.Create(name, totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
