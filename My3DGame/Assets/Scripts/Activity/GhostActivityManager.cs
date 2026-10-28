// ============================================================
// GhostActivityManager.cs
// ------------------------------------------------------------
// Singleton that tracks "how agitated the ghost is" via a 0–100
// activity meter. The meter drives four tiers (Calm / Uneasy /
// Active / Hunting) and fires events that sabotage scripts
// (FlickeringLight, DoorSabotage, MovableProp, AmbientSoundSource)
// subscribe to so they escalate atmosphere automatically.
//
// SOURCES THAT RAISE ACTIVITY:
//   - GlitchEvents.OnGlitch  (era swap / temporal tear)
//   - NoiseEmitter.OnNoiseMade  (loud player noise)
//   - Proximity to any ghost  (passive raise while near ghost)
//
// DECAY:
//   Activity slowly decreases when none of the above are happening.
//
// HUNT HOOK (Milestone 6):
//   When Hunting tier is reached, GhostBase.StartHunt() is called
//   on every GhostBase in the scene that CanHunt.
//
// USAGE:
//   GhostActivityManager.Instance.AddActivity(amount);
//   GhostActivityManager.OnTierChanged += MyHandler;
// ============================================================

using System;
using UnityEngine;
using TimeFracture.Era;
using TimeFracture.Player;
using TimeFracture.Ghost;

namespace TimeFracture.Activity
{
    // ── Tier enum ────────────────────────────────────────────
    public enum ActivityTier
    {
        Calm,       // 0–29    : Normal patrol, no effects
        Uneasy,     // 30–59   : Lights flicker, creaks begin
        Active,     // 60–89   : Doors slam, props move
        Hunting     // 90–100  : Ghost enters Hunt state
    }

    [DefaultExecutionOrder(-50)]    // Run before sabotage scripts
    public class GhostActivityManager : MonoBehaviour
    {
        // ──────────────────────────────────────────────────
        // SINGLETON
        // ──────────────────────────────────────────────────

        public static GhostActivityManager Instance { get; private set; }

        /// <summary>Auto-creates the manager if missing (called before scene load).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("GhostActivityManager");
            go.AddComponent<GhostActivityManager>();
            DontDestroyOnLoad(go);
        }

        // ──────────────────────────────────────────────────
        // EVENTS  (subscribe to react to tier changes)
        // ──────────────────────────────────────────────────

        /// <summary>Fired whenever the activity tier changes. Supplies new tier.</summary>
        public static event Action<ActivityTier> OnTierChanged;

        /// <summary>Fired every frame with the current 0–100 activity value.</summary>
        public static event Action<float> OnActivityChanged;

        // ──────────────────────────────────────────────────
        // INSPECTOR TUNING
        // ──────────────────────────────────────────────────

        [Header("Tier Thresholds")]
        [Tooltip("Activity at or above this → Uneasy tier.")]
        [SerializeField] private float uneasyThreshold = 30f;

        [Tooltip("Activity at or above this → Active tier.")]
        [SerializeField] private float activeThreshold = 60f;

        [Tooltip("Activity at or above this → Hunting tier.")]
        [SerializeField] private float huntingThreshold = 90f;

        [Header("Meter Tuning")]
        [Tooltip("Activity added per era-swap glitch event.")]
        [SerializeField] private float glitchActivityBoost = 25f;

        [Tooltip("Activity added per unit of noise radius received.")]
        [SerializeField] private float noiseActivityPerRadius = 0.6f;

        [Tooltip("Maximum noise contribution per single noise event.")]
        [SerializeField] private float maxNoiseContribution = 20f;

        [Tooltip("Activity added per second while the player is within ghostProximityRange of any ghost.")]
        [SerializeField] private float proximityRatePerSecond = 4f;

        [Tooltip("Distance (metres) within which the player raises activity passively.")]
        [SerializeField] private float ghostProximityRange = 8f;

        [Tooltip("Activity lost per second when idle (no noise/glitch/proximity).")]
        [SerializeField] private float decayRatePerSecond = 3f;

        // ──────────────────────────────────────────────────
        // INTERNAL STATE
        // ──────────────────────────────────────────────────

        private float _activity = 0f;
        private ActivityTier _currentTier = ActivityTier.Calm;

        // Cache for proximity checks
        private Transform _playerTransform;
        private GhostBase[] _ghosts;
        private float _ghostCacheTimer = 0f;
        private const float GHOST_CACHE_INTERVAL = 2f;

        // ──────────────────────────────────────────────────
        // PUBLIC READ ACCESSOR
        // ──────────────────────────────────────────────────

        /// <summary>Current activity level 0–100.</summary>
        public float Activity => _activity;

        /// <summary>Current activity tier.</summary>
        public ActivityTier CurrentTier => _currentTier;

        // ──────────────────────────────────────────────────
        // UNITY LIFECYCLE
        // ──────────────────────────────────────────────────

        private void Awake()
        {
            // Singleton pattern — allow existing instance to persist
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            // Subscribe to global event channels
            GlitchEvents.OnGlitch += HandleGlitch;
            NoiseEmitter.OnNoiseMade += HandleNoise;
        }

        private void OnDisable()
        {
            GlitchEvents.OnGlitch -= HandleGlitch;
            NoiseEmitter.OnNoiseMade -= HandleNoise;
        }

        private void Update()
        {
            // Refresh ghost and player caches periodically (cheap)
            _ghostCacheTimer -= Time.deltaTime;
            if (_ghostCacheTimer <= 0f)
            {
                _ghosts = FindObjectsByType<GhostBase>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                _playerTransform = FindPlayerTransform();
                _ghostCacheTimer = GHOST_CACHE_INTERVAL;
            }

            bool nearGhost = false;

            // Proximity raise: player within range of any ghost
            if (_playerTransform != null && _ghosts != null)
            {
                foreach (GhostBase ghost in _ghosts)
                {
                    if (ghost == null) continue;
                    float dist = Vector3.Distance(_playerTransform.position, ghost.transform.position);
                    if (dist <= ghostProximityRange)
                    {
                        nearGhost = true;
                        break;
                    }
                }
            }

            if (nearGhost)
            {
                // Passive raise while near a ghost
                _activity += proximityRatePerSecond * Time.deltaTime;
            }
            else
            {
                // Slow decay when no source is active
                _activity -= decayRatePerSecond * Time.deltaTime;
            }

            // Clamp to [0, 100]
            _activity = Mathf.Clamp(_activity, 0f, 100f);

            // Fire change event for HUD
            OnActivityChanged?.Invoke(_activity);

            // Update tier and fire tier-change event if needed
            ActivityTier newTier = CalculateTier(_activity);
            if (newTier != _currentTier)
            {
                _currentTier = newTier;
                OnTierChanged?.Invoke(_currentTier);
                Debug.Log($"[GhostActivityManager] Tier changed → {_currentTier} (Activity={_activity:F1})");

                // At Hunting tier, force all capable ghosts to hunt
                if (_currentTier == ActivityTier.Hunting && _ghosts != null)
                {
                    foreach (GhostBase ghost in _ghosts)
                    {
                        if (ghost != null && ghost.CanHunt)
                        {
                            ghost.StartHunt();
                        }
                    }
                }
            }
        }

        // ──────────────────────────────────────────────────
        // PUBLIC API
        // ──────────────────────────────────────────────────

        /// <summary>
        /// Adds <paramref name="amount"/> to the activity meter (clamped to 0–100).
        /// Call this from any system that wants to agitate the ghost.
        /// </summary>
        public void AddActivity(float amount)
        {
            _activity = Mathf.Clamp(_activity + amount, 0f, 100f);
            OnActivityChanged?.Invoke(_activity);

            ActivityTier newTier = CalculateTier(_activity);
            if (newTier != _currentTier)
            {
                _currentTier = newTier;
                OnTierChanged?.Invoke(_currentTier);
                Debug.Log($"[GhostActivityManager] AddActivity({amount:F1}) → Tier {_currentTier} (Activity={_activity:F1})");

                if (_currentTier == ActivityTier.Hunting)
                {
                    if (_ghosts == null)
                        _ghosts = FindObjectsByType<GhostBase>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

                    foreach (GhostBase ghost in _ghosts)
                    {
                        if (ghost != null && ghost.CanHunt) ghost.StartHunt();
                    }
                }
            }
        }

        // ──────────────────────────────────────────────────
        // PRIVATE HELPERS
        // ──────────────────────────────────────────────────

        private void HandleGlitch(float intensity)
        {
            // Era swap always gives a strong boost (multiply by tunable)
            AddActivity(glitchActivityBoost * intensity);
        }

        private void HandleNoise(Vector3 position, float radius)
        {
            // Louder noise → more activity, capped per event
            float contribution = Mathf.Min(radius * noiseActivityPerRadius, maxNoiseContribution);
            AddActivity(contribution);
        }

        private ActivityTier CalculateTier(float value)
        {
            if (value >= huntingThreshold) return ActivityTier.Hunting;
            if (value >= activeThreshold)  return ActivityTier.Active;
            if (value >= uneasyThreshold)  return ActivityTier.Uneasy;
            return ActivityTier.Calm;
        }

        private Transform FindPlayerTransform()
        {
            // Cheap tag lookup first, then fallback
            GameObject pObj = GameObject.FindWithTag("Player");
            if (pObj != null) return pObj.transform;
            if (Camera.main != null) return Camera.main.transform.root;
            return null;
        }
    }
}
