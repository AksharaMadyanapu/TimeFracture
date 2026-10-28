// ============================================================
// MovableProp.cs
// ------------------------------------------------------------
// A small physics prop (chair, book, debris) that slightly shifts
// or tips when the player is NOT looking at it and the activity
// meter is Active or higher.
//
// CAMERA-VIEWPORT CHECK:
//   Uses Camera.main.WorldToViewportPoint to determine if the
//   prop is inside the screen rectangle [0,1]² and in front of
//   the camera. Only moves when outside that region.
//
// SELF-WIRING: uses Camera.main fallback.
// ============================================================

using System.Collections;
using UnityEngine;
using TimeFracture.Activity;

namespace TimeFracture.Sabotage
{
    public class MovableProp : MonoBehaviour
    {
        // ──────────────────────────────────────────────────
        // INSPECTOR FIELDS
        // ──────────────────────────────────────────────────

        [Header("Movement Settings")]
        [Tooltip("Maximum distance the prop shifts per nudge.")]
        [SerializeField] private float nudgeDistance = 0.12f;

        [Tooltip("Maximum rotation tip (degrees) per nudge.")]
        [SerializeField] private float nudgeTilt = 8f;

        [Tooltip("Duration of the nudge animation in seconds.")]
        [SerializeField] private float nudgeDuration = 0.35f;

        [Header("Timing")]
        [Tooltip("Minimum seconds between nudges at Active tier.")]
        [SerializeField] private float minNudgeInterval = 5f;

        [Tooltip("Maximum seconds between nudges at Active tier.")]
        [SerializeField] private float maxNudgeInterval = 15f;

        [Tooltip("Multiplier applied to interval at Hunting tier (faster movement).")]
        [SerializeField] private float huntingSpeedMultiplier = 0.4f;

        // ──────────────────────────────────────────────────
        // INTERNAL STATE
        // ──────────────────────────────────────────────────

        private ActivityTier _tier = ActivityTier.Calm;
        private float _nudgeTimer = 0f;
        private bool _isMoving = false;
        private Vector3 _basePosition;
        private Quaternion _baseRotation;

        // ──────────────────────────────────────────────────
        // UNITY LIFECYCLE
        // ──────────────────────────────────────────────────

        private void Awake()
        {
            // Remember the starting transform so we never drift too far
            _basePosition = transform.position;
            _baseRotation = transform.rotation;
            _nudgeTimer = Random.Range(minNudgeInterval, maxNudgeInterval);
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
            // Only move at Active or Hunting tier
            if (_tier < ActivityTier.Active || _isMoving) return;

            _nudgeTimer -= Time.deltaTime;
            if (_nudgeTimer <= 0f)
            {
                float interval = Random.Range(minNudgeInterval, maxNudgeInterval);
                if (_tier == ActivityTier.Hunting) interval *= huntingSpeedMultiplier;
                _nudgeTimer = interval;

                // Only nudge if player is NOT looking at this prop
                if (!IsVisibleToCamera())
                {
                    StartCoroutine(NudgeRoutine());
                }
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
        // VISIBILITY CHECK
        // ──────────────────────────────────────────────────

        /// <summary>
        /// Returns true when this prop's position is inside the camera's viewport
        /// (x and y both 0–1) AND in front of the camera (z > 0).
        /// </summary>
        private bool IsVisibleToCamera()
        {
            Camera cam = Camera.main;
            if (cam == null) return false;

            Vector3 vp = cam.WorldToViewportPoint(transform.position);

            // z < 0 means behind the camera
            if (vp.z < 0f) return false;

            // x and y in [0, 1] means inside the screen rectangle
            return vp.x >= 0f && vp.x <= 1f &&
                   vp.y >= 0f && vp.y <= 1f;
        }

        // ──────────────────────────────────────────────────
        // NUDGE COROUTINE
        // ──────────────────────────────────────────────────

        private IEnumerator NudgeRoutine()
        {
            _isMoving = true;

            // Pick a random small offset and tilt from the base transform
            Vector3 targetPos = _basePosition + new Vector3(
                Random.Range(-nudgeDistance, nudgeDistance),
                0f,
                Random.Range(-nudgeDistance, nudgeDistance));

            // Random tilt on X/Z axes (tip the prop slightly)
            Quaternion targetRot = _baseRotation *
                Quaternion.Euler(
                    Random.Range(-nudgeTilt, nudgeTilt),
                    0f,
                    Random.Range(-nudgeTilt, nudgeTilt));

            float elapsed = 0f;
            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;

            while (elapsed < nudgeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / nudgeDuration);
                transform.position = Vector3.Lerp(startPos, targetPos, t);
                transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return null;
            }

            transform.position = targetPos;
            transform.rotation = targetRot;

            // Update base so accumulated drift stays small
            _basePosition = targetPos;
            _baseRotation = targetRot;

            _isMoving = false;
        }
    }
}
