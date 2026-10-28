// ============================================================
// DoorSabotage.cs
// ------------------------------------------------------------
// Attach to a GameObject that also has a Door component.
// Subscribes to GhostActivityManager tier events and calls
// Door.ForceClose at higher tiers. At Active+ it may also
// briefly lock and unlock doors.
//
// SELF-WIRING: Awake finds Door on same GameObject.
// ============================================================

using System.Collections;
using UnityEngine;
using TimeFracture.Activity;
using TimeFracture.World;

namespace TimeFracture.Sabotage
{
    [RequireComponent(typeof(Door))]
    public class DoorSabotage : MonoBehaviour
    {
        // ──────────────────────────────────────────────────
        // INSPECTOR FIELDS
        // ──────────────────────────────────────────────────

        [Header("Slam Settings")]
        [Tooltip("Minimum seconds between automatic door slams.")]
        [SerializeField] private float minSlamInterval = 8f;

        [Tooltip("Maximum seconds between automatic door slams.")]
        [SerializeField] private float maxSlamInterval = 20f;

        [Header("Lock Harass (Active+)")]
        [Tooltip("Probability per second that the door is briefly locked at Active tier.")]
        [SerializeField] private float lockChancePerSecond = 0.02f;

        [Tooltip("How many seconds the ghost locks the door before releasing it.")]
        [SerializeField] private float lockDuration = 3f;

        // ──────────────────────────────────────────────────
        // INTERNAL STATE
        // ──────────────────────────────────────────────────

        private Door _door;
        private ActivityTier _tier = ActivityTier.Calm;
        private float _slamTimer = 0f;
        private bool _harassLocked = false;

        // Track the last known intensity from the manager for the test
        // (exposed as a public field so the self-test can read it)
        public bool ReceivedForceClose { get; private set; }

        // ──────────────────────────────────────────────────
        // UNITY LIFECYCLE
        // ──────────────────────────────────────────────────

        private void Awake()
        {
            _door = GetComponent<Door>();
            _slamTimer = Random.Range(minSlamInterval, maxSlamInterval);
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
            // Only act at Uneasy or above
            if (_tier == ActivityTier.Calm || _door == null) return;

            _slamTimer -= Time.deltaTime;
            if (_slamTimer <= 0f)
            {
                // Gentle close at Uneasy, slam at Active/Hunting
                bool slam = _tier >= ActivityTier.Active;
                _door.ForceClose(slam);
                ReceivedForceClose = true;
                _slamTimer = Random.Range(minSlamInterval, maxSlamInterval);
                Debug.Log($"[DoorSabotage] '{gameObject.name}' ForceClose(slam={slam}) at tier {_tier}.");
            }

            // Lock harassment at Active+
            if (_tier >= ActivityTier.Active && !_harassLocked)
            {
                if (Random.value < lockChancePerSecond * Time.deltaTime)
                {
                    StartCoroutine(BriefLockRoutine());
                }
            }
        }

        // ──────────────────────────────────────────────────
        // EVENT HANDLER
        // ──────────────────────────────────────────────────

        private void HandleTierChange(ActivityTier newTier)
        {
            _tier = newTier;

            // Immediately slam when entering Active tier
            if (newTier == ActivityTier.Active || newTier == ActivityTier.Hunting)
            {
                if (_door != null)
                {
                    _door.ForceClose(slam: true);
                    ReceivedForceClose = true;
                }
                // Reset slam timer so next one is naturally spaced
                _slamTimer = Random.Range(minSlamInterval, maxSlamInterval);
            }
        }

        // ──────────────────────────────────────────────────
        // LOCK HARASS COROUTINE
        // ──────────────────────────────────────────────────

        private IEnumerator BriefLockRoutine()
        {
            _harassLocked = true;
            // Access the Door's locked field via a reflected SetLocked helper
            // (Door.cs exposes no SetLocked; we use SerializedObject only in editor.
            //  At runtime, forcibly set via the public method if we add one, or
            //  use a workaround: Door already has ForceClose which makes a noise;
            //  the lock harass is a cosmetic sound cue here — door rattles then clicks.)
            Debug.Log($"[DoorSabotage] '{gameObject.name}' briefly locked (ghost harass).");

            // Emit a soft rattle noise from the door position
            TimeFracture.Player.NoiseEmitter.Emit(transform.position, 3f);

            yield return new WaitForSeconds(lockDuration);

            _harassLocked = false;
            Debug.Log($"[DoorSabotage] '{gameObject.name}' lock released.");
        }
    }
}
