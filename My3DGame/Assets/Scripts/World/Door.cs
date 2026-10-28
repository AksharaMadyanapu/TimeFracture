// ============================================================
// Door.cs
// ============================================================
// A door that opens and closes with a smooth hinge rotation
// when the player interacts with it (E key).
//
// FEATURES:
//   - Smooth hinge animation (Quaternion.Lerp).
//   - locked bool + requiredFlag string: if a flag is set in
//     WorldState, the door auto-unlocks and stays unlocked.
//   - Opening, closing, and slamming broadcast noise via the
//     static NoiseEmitter.Emit() helper.
//   - ForceClose(bool slam): called by future sabotage systems
//     to slam the door shut without player input.
//
// SETUP:
//   The GameObject this script sits on is the HINGE (pivot).
//   Place it at the hinge edge of the door, not the centre.
//   The visual door mesh should be a CHILD of this hinge object.
//
//   Example hierarchy:
//     DoorHinge      ← this script lives here
//       └── DoorMesh ← the actual door visual (cube or model)
//
// ============================================================

using System.Collections;
using TimeFracture.Player;
using UnityEngine;

namespace TimeFracture.World
{
    public class Door : MonoBehaviour, IInteractable
    {
        // ──────────────────────────────────────────────────
        // INSPECTOR FIELDS
        // ──────────────────────────────────────────────────

        [Header("Hinge Settings")]
        [Tooltip("How many degrees the door rotates when fully open. " +
                 "Positive = opens counter-clockwise (away from player " +
                 "if they approach from the front). Try 90.")]
        [SerializeField] private float openAngle = 90f;

        [Tooltip("Which axis the door rotates around. Y = vertical hinge " +
                 "(standard door). Leave as (0,1,0) for most doors.")]
        [SerializeField] private Vector3 hingeAxis = Vector3.up;

        [Tooltip("How many seconds the door takes to fully open or close.")]
        [SerializeField] private float animationDuration = 0.6f;

        // ──────────────────────────────────────────────────
        [Header("Lock Settings")]
        [Tooltip("If true, the door starts locked and cannot be opened " +
                 "by the player until unlocked.")]
        [SerializeField] private bool locked = false;

        [Tooltip("Name of a WorldState flag that unlocks this door when set. " +
                 "Leave empty if no flag is required. " +
                 "Example: 'LabSwitchOn'")]
        [SerializeField] private string requiredFlag = "";

        [Tooltip("Prompt shown when the player looks at a LOCKED door.")]
        [SerializeField] private string lockedPrompt = "Locked";

        [Tooltip("Prompt shown when the door is closed and unlocked.")]
        [SerializeField] private string openPrompt = "Press [E] to open";

        [Tooltip("Prompt shown when the door is open.")]
        [SerializeField] private string closePrompt = "Press [E] to close";

        // ──────────────────────────────────────────────────
        [Header("Noise")]
        [Tooltip("Noise radius emitted when the door opens or closes normally.")]
        [SerializeField] private float openCloseNoiseRadius = 6f;

        [Tooltip("Noise radius emitted when the door is slammed (ForceClose).")]
        [SerializeField] private float slamNoiseRadius = 12f;

        // ──────────────────────────────────────────────────
        // PRIVATE STATE
        // ──────────────────────────────────────────────────

        private bool _isOpen;
        private bool _isAnimating;

        // Cached local Euler angles at spawn → used as the "closed" rotation
        private Quaternion _closedRotation;
        private Quaternion _openRotation;

        // ──────────────────────────────────────────────────
        // UNITY LIFECYCLE
        // ──────────────────────────────────────────────────

        private void Awake()
        {
            // Record the starting rotation as the "closed" state
            _closedRotation = transform.localRotation;

            // Pre-calculate the open rotation from the hinge axis + angle
            _openRotation = Quaternion.AngleAxis(openAngle, hingeAxis) * _closedRotation;
        }

        private void OnEnable()
        {
            // Subscribe to WorldState flag changes so the door can
            // auto-unlock when the required flag is set.
            WorldState.OnFlagChanged += OnFlagChanged;
        }

        private void OnDisable()
        {
            // Always unsubscribe to prevent memory leaks (stale references)
            WorldState.OnFlagChanged -= OnFlagChanged;
        }

        // Called by WorldState whenever ANY flag changes
        private void OnFlagChanged(string flagName, bool value)
        {
            // Only care about our specific required flag
            if (string.IsNullOrEmpty(requiredFlag)) return;
            if (flagName != requiredFlag) return;

            if (value && locked)
            {
                // Flag was set → unlock this door
                locked = false;
                Debug.Log($"[Door] '{gameObject.name}' unlocked by flag '{flagName}'.");
            }
        }

        // ──────────────────────────────────────────────────
        // IInteractable IMPLEMENTATION
        // ──────────────────────────────────────────────────

        public string GetPrompt()
        {
            if (locked) return lockedPrompt;
            return _isOpen ? closePrompt : openPrompt;
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (locked)
            {
                // Could play a "rattle" sound here in a future milestone
                Debug.Log($"[Door] '{gameObject.name}' is locked.");
                return;
            }

            if (_isAnimating) return; // ignore input mid-swing

            if (_isOpen)
                StartCoroutine(AnimateDoor(_openRotation, _closedRotation, slam: false));
            else
                StartCoroutine(AnimateDoor(_closedRotation, _openRotation, slam: false));
        }

        // ──────────────────────────────────────────────────
        // PUBLIC API (used by future sabotage system)
        // ──────────────────────────────────────────────────

        /// <summary>
        /// Forcibly slams or gently closes the door.
        /// Called by the ghost-activity sabotage system in Milestone 6.
        /// </summary>
        /// <param name="slam">True = slam (loud noise). False = gentle close.</param>
        public void ForceClose(bool slam)
        {
            if (_isAnimating)
            {
                // Stop the current animation immediately before starting new one
                StopAllCoroutines();
                _isAnimating = false;
            }

            float duration = slam ? animationDuration * 0.2f : animationDuration;
            StartCoroutine(AnimateDoor(transform.localRotation, _closedRotation,
                                       forceSlam: slam, durationOverride: duration));
        }

        // ──────────────────────────────────────────────────
        // ANIMATION
        // ──────────────────────────────────────────────────

        /// <summary>
        /// Coroutine that smoothly rotates the door from <paramref name="from"/>
        /// to <paramref name="to"/> over <see cref="animationDuration"/> seconds.
        /// </summary>
        private IEnumerator AnimateDoor(Quaternion from, Quaternion to,
                                        bool slam = false,
                                        bool forceSlam = false,
                                        float durationOverride = -1f)
        {
            _isAnimating = true;

            float duration  = durationOverride > 0f ? durationOverride : animationDuration;
            float elapsed   = 0f;
            bool  willOpen  = (to == _openRotation);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                // t goes from 0 to 1 over the duration
                float t = Mathf.Clamp01(elapsed / duration);

                // SmoothStep makes the animation ease in and ease out (nicer feel)
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                transform.localRotation = Quaternion.Lerp(from, to, smoothT);
                yield return null; // wait one frame
            }

            // Snap to exact target to avoid floating-point drift
            transform.localRotation = to;

            // Update state
            _isOpen      = willOpen;
            _isAnimating = false;

            // Emit noise at the end of the swing
            float radius = (forceSlam || slam) ? slamNoiseRadius : openCloseNoiseRadius;
            NoiseEmitter.Emit(transform.position, radius);
        }

        // ──────────────────────────────────────────────────
        // EDITOR GIZMO
        // ──────────────────────────────────────────────────

        // Draw a small arc in the Scene view showing the open angle
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            // Draw a line pointing in the hinge axis direction
            Gizmos.DrawRay(transform.position, transform.TransformDirection(hingeAxis) * 0.5f);
        }
    }
}
