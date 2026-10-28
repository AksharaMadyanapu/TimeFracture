// ============================================================
// FlickeringLight.cs
// ------------------------------------------------------------
// Attach to any GameObject with a Light component.
// Subscribes to GhostActivityManager tier events and increases
// flicker intensity accordingly. At Active+ tier, occasional
// full blackouts occur.
//
// SELF-WIRING: Awake finds the Light automatically.
// ============================================================

using System.Collections;
using UnityEngine;
using TimeFracture.Activity;

namespace TimeFracture.Sabotage
{
    [RequireComponent(typeof(Light))]
    public class FlickeringLight : MonoBehaviour
    {
        // ──────────────────────────────────────────────────
        // INSPECTOR FIELDS
        // ──────────────────────────────────────────────────

        [Header("Base Light Settings")]
        [Tooltip("Normal light intensity when calm.")]
        [SerializeField] private float calmIntensity = 1.0f;

        [Tooltip("Minimum intensity during flicker (not during blackout).")]
        [SerializeField] private float minFlickerIntensity = 0.05f;

        [Header("Flicker Speeds (by tier)")]
        [Tooltip("How often (seconds) the light re-randomises its intensity at Uneasy tier.")]
        [SerializeField] private float uneasyFlickerInterval = 0.18f;

        [Tooltip("How often the light re-randomises at Active tier.")]
        [SerializeField] private float activeFlickerInterval = 0.07f;

        [Tooltip("How often the light re-randomises at Hunting tier.")]
        [SerializeField] private float huntingFlickerInterval = 0.04f;

        [Header("Blackout Settings")]
        [Tooltip("Probability (0–1) per second that a blackout starts at Active+.")]
        [SerializeField] private float blackoutChancePerSecond = 0.04f;

        [Tooltip("Duration of a blackout in seconds.")]
        [SerializeField] private float blackoutDuration = 0.6f;

        // ──────────────────────────────────────────────────
        // INTERNAL STATE
        // ──────────────────────────────────────────────────

        private Light _light;
        private ActivityTier _tier = ActivityTier.Calm;
        private float _flickerTimer = 0f;
        private float _currentInterval = 999f;   // effectively never flickers at Calm
        private bool _isBlackedOut = false;

        // ──────────────────────────────────────────────────
        // UNITY LIFECYCLE
        // ──────────────────────────────────────────────────

        private void Awake()
        {
            _light = GetComponent<Light>();
            if (_light != null) _light.intensity = calmIntensity;
        }

        private void OnEnable()
        {
            GhostActivityManager.OnTierChanged += HandleTierChange;
        }

        private void OnDisable()
        {
            GhostActivityManager.OnTierChanged -= HandleTierChange;
            // Restore calm intensity when disabled so nothing is permanently dark
            if (_light != null) _light.intensity = calmIntensity;
        }

        private void Update()
        {
            if (_tier == ActivityTier.Calm || _light == null) return;
            if (_isBlackedOut) return;

            _flickerTimer -= Time.deltaTime;
            if (_flickerTimer <= 0f)
            {
                // Flicker: random intensity between min and calm
                _light.intensity = Random.Range(minFlickerIntensity, calmIntensity);
                _flickerTimer = _currentInterval + Random.Range(-_currentInterval * 0.3f, _currentInterval * 0.3f);
            }

            // Random blackout chance at Active / Hunting tier
            if (_tier >= ActivityTier.Active)
            {
                if (Random.value < blackoutChancePerSecond * Time.deltaTime)
                {
                    StartCoroutine(BlackoutRoutine());
                }
            }
        }

        // ──────────────────────────────────────────────────
        // EVENT HANDLER
        // ──────────────────────────────────────────────────

        private void HandleTierChange(ActivityTier newTier)
        {
            _tier = newTier;

            switch (newTier)
            {
                case ActivityTier.Calm:
                    _currentInterval = 999f;
                    if (_light != null) _light.intensity = calmIntensity;
                    break;
                case ActivityTier.Uneasy:
                    _currentInterval = uneasyFlickerInterval;
                    break;
                case ActivityTier.Active:
                    _currentInterval = activeFlickerInterval;
                    break;
                case ActivityTier.Hunting:
                    _currentInterval = huntingFlickerInterval;
                    break;
            }
        }

        // ──────────────────────────────────────────────────
        // BLACKOUT COROUTINE
        // ──────────────────────────────────────────────────

        private IEnumerator BlackoutRoutine()
        {
            _isBlackedOut = true;
            if (_light != null) _light.intensity = 0f;
            yield return new WaitForSeconds(blackoutDuration);
            _isBlackedOut = false;
            if (_light != null) _light.intensity = calmIntensity;
        }
    }
}
