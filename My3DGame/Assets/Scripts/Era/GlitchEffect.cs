// ============================================================
// GlitchEffect.cs
// ------------------------------------------------------------
// Simulates a violent temporal rupture when the era swaps.
//
// WHAT IT DOES:
//   1. Listens to EraManager.OnEraChanged.
//   2. Freezes player movement for ~0.3 seconds at the rupture start.
//   3. Animates URP Post-Processing Volume overrides via Coroutine:
//      - Chromatic Aberration (colour fringing / RGB split)
//      - Lens Distortion (fisheye warp)
//      - Film Grain (analogue noise)
//      - Vignette (darkened edges)
//   4. Plays an audio hook through an AudioSource.
//   5. Broadcasts GlitchEvents.OnGlitch(1f) to alert future ghost AI.
//
// STUDENT NOTE — URP Post-Processing via Script:
//   A URP Volume controls visual effects. By obtaining references
//   to VolumeComponent overrides, we can animate their intensities
//   frame-by-frame inside an IEnumerator coroutine.
// ============================================================

using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TimeFracture.Player;

namespace TimeFracture.Era
{
    public class GlitchEffect : MonoBehaviour
    {
        // ──────────────────────────────────────────────────
        // INSPECTOR FIELDS
        // ──────────────────────────────────────────────────

        [Header("Volume Reference")]
        [Tooltip("The URP Volume component to animate. If unassigned, searches this GameObject.")]
        [SerializeField] private Volume volume;

        [Header("Timing")]
        [Tooltip("Total duration of the glitch animation in seconds (1.0 to 1.5s).")]
        [SerializeField] private float glitchDuration = 1.2f;

        [Tooltip("Duration in seconds at the start of the glitch where player movement is frozen.")]
        [SerializeField] private float playerFreezeDuration = 0.3f;

        [Header("Glitch Intensities")]
        [Tooltip("Peak Chromatic Aberration intensity (0 to 1).")]
        [SerializeField] private float maxChromaticAberration = 1.0f;

        [Tooltip("Peak Lens Distortion intensity (-1 to 1). Negative causes barrel pinch.")]
        [SerializeField] private float maxLensDistortion = -0.45f;

        [Tooltip("Peak Film Grain intensity (0 to 1).")]
        [SerializeField] private float maxFilmGrain = 1.0f;

        [Tooltip("Peak Vignette intensity (0 to 1).")]
        [SerializeField] private float maxVignette = 0.5f;

        [Header("Audio")]
        [Tooltip("AudioSource to play the temporal glitch sound effect.")]
        [SerializeField] private AudioSource audioSource;

        [Tooltip("Optional sound clip for the glitch sting. If null, plays existing AudioSource clip.")]
        [SerializeField] private AudioClip glitchSound;

        [Header("Player Reference")]
        [Tooltip("PlayerController reference. If null, auto-finds in scene.")]
        [SerializeField] private PlayerController playerController;

        // ──────────────────────────────────────────────────
        // INTERNAL STATE & OVERRIDES
        // ──────────────────────────────────────────────────

        private ChromaticAberration _chromaticAberration;
        private LensDistortion _lensDistortion;
        private FilmGrain _filmGrain;
        private Vignette _vignette;

        private Coroutine _glitchCoroutine;

        // ──────────────────────────────────────────────────
        // INITIALIZATION
        // ──────────────────────────────────────────────────

        private void Awake()
        {
            if (volume == null)
            {
                volume = GetComponent<Volume>();
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (playerController == null)
            {
                playerController = FindAnyObjectByType<PlayerController>();
            }

            SetupVolumeOverrides();
        }

        private void OnEnable()
        {
            // Subscribe to era change event
            EraManager.OnEraChanged += HandleEraChanged;
        }

        private void OnDisable()
        {
            // Unsubscribe to avoid memory leaks
            EraManager.OnEraChanged -= HandleEraChanged;

            // Ensure player movement is restored if disabled mid-glitch
            if (playerController != null)
            {
                playerController.CanMove = true;
            }

            ResetOverrides();
        }

        /// <summary>
        /// Retrieves or adds the necessary URP volume components to the profile.
        /// </summary>
        private void SetupVolumeOverrides()
        {
            if (volume == null)
            {
                Debug.LogWarning("[GlitchEffect] No Volume assigned! Post-processing glitch will be skipped.");
                return;
            }

            // Ensure we are working with a runtime profile instance
            VolumeProfile profile = volume.profile;

            if (!profile.TryGet(out _chromaticAberration))
                _chromaticAberration = profile.Add<ChromaticAberration>(true);

            if (!profile.TryGet(out _lensDistortion))
                _lensDistortion = profile.Add<LensDistortion>(true);

            if (!profile.TryGet(out _filmGrain))
                _filmGrain = profile.Add<FilmGrain>(true);

            if (!profile.TryGet(out _vignette))
                _vignette = profile.Add<Vignette>(true);

            // Enable override states
            _chromaticAberration.intensity.overrideState = true;
            _lensDistortion.intensity.overrideState = true;
            _filmGrain.intensity.overrideState = true;
            _vignette.intensity.overrideState = true;

            ResetOverrides();
        }

        // ──────────────────────────────────────────────────
        // EVENT HANDLER & COROUTINE
        // ──────────────────────────────────────────────────

        private void HandleEraChanged(Era newEra)
        {
            if (_glitchCoroutine != null)
            {
                StopCoroutine(_glitchCoroutine);
            }

            _glitchCoroutine = StartCoroutine(GlitchRoutine());
        }

        private IEnumerator GlitchRoutine()
        {
            // 1. Notify future ghost AI systems of high temporal agitation
            GlitchEvents.FireGlitch(1.0f);

            // 2. Play glitch sound hook
            if (audioSource != null)
            {
                audioSource.pitch = Random.Range(0.92f, 1.08f);
                if (glitchSound != null)
                {
                    audioSource.PlayOneShot(glitchSound);
                }
                else if (audioSource.clip != null)
                {
                    audioSource.Play();
                }
            }

            // 3. Freeze player movement for initial disorientation
            if (playerController == null)
            {
                playerController = FindAnyObjectByType<PlayerController>();
            }

            if (playerController != null)
            {
                playerController.CanMove = false;
            }

            bool playerRestored = false;
            float elapsed = 0f;

            // 4. Animate glitch distortion
            while (elapsed < glitchDuration)
            {
                elapsed += Time.deltaTime;

                // Check if freeze window has passed
                if (!playerRestored && elapsed >= playerFreezeDuration)
                {
                    if (playerController != null)
                    {
                        playerController.CanMove = true;
                    }
                    playerRestored = true;
                }

                // Calculate intensity curve:
                // Peak during freeze duration, then smooth decaying curve with jitter
                float currentIntensity;
                if (elapsed <= playerFreezeDuration)
                {
                    // Rapid initial spike with violent jitter
                    float jitter = Random.Range(0.85f, 1.0f);
                    currentIntensity = jitter;
                }
                else
                {
                    // Decay phase (from 1.0 down to 0.0)
                    float decayTime = glitchDuration - playerFreezeDuration;
                    float progress = (elapsed - playerFreezeDuration) / decayTime;
                    float decay = 1f - progress;

                    // Add small electric flicker during decay
                    float flicker = (Random.value > 0.8f) ? 0.2f : 0f;
                    currentIntensity = Mathf.Clamp01((decay * decay) + flicker);
                }

                ApplyOverrides(currentIntensity);

                yield return null;
            }

            // 5. Cleanup and guarantee safe state
            if (playerController != null)
            {
                playerController.CanMove = true;
            }

            ResetOverrides();
            _glitchCoroutine = null;
        }

        private void ApplyOverrides(float intensity)
        {
            if (_chromaticAberration != null)
                _chromaticAberration.intensity.value = maxChromaticAberration * intensity;

            if (_lensDistortion != null)
                _lensDistortion.intensity.value = maxLensDistortion * intensity;

            if (_filmGrain != null)
                _filmGrain.intensity.value = maxFilmGrain * intensity;

            if (_vignette != null)
                _vignette.intensity.value = maxVignette * intensity;
        }

        private void ResetOverrides()
        {
            if (_chromaticAberration != null)
                _chromaticAberration.intensity.value = 0f;

            if (_lensDistortion != null)
                _lensDistortion.intensity.value = 0f;

            if (_filmGrain != null)
                _filmGrain.intensity.value = 0f;

            if (_vignette != null)
                _vignette.intensity.value = 0f;
        }
    }
}
