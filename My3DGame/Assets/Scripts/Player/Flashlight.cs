// ============================================================
// Flashlight.cs
// ============================================================
// Toggles a Spot Light on/off with the F key.
// Adds a subtle "handheld sway" by slowly lagging the light's
// rotation behind the camera using SmoothDamp.
//
// SETUP: Attach this script to the CAMERA (or any child of it).
// The Spot Light should be a child of the Camera as well.
//
// HOW SWAY WORKS:
//   Every frame we record where the camera is looking (a
//   direction vector).  The flashlight "catches up" to that
//   direction over time via Vector3.SmoothDamp, which gives
//   a natural lag/inertia feel.
// ============================================================

using UnityEngine;

namespace TimeFracture.Player
{
    public class Flashlight : MonoBehaviour
    {
        // ──────────────────────────────────────────────────
        // INSPECTOR FIELDS
        // ──────────────────────────────────────────────────

        [Header("References")]
        [Tooltip("Drag the Spot Light (child of the Camera) here.")]
        [SerializeField] private Light spotLight;

        [Header("Toggle")]
        [Tooltip("Should the flashlight start turned on?")]
        [SerializeField] private bool startOn = true;

        [Header("Sway Settings")]
        [Tooltip("How quickly the flashlight follows the camera. " +
                 "Lower = more lag/sway. Higher = snappier.  " +
                 "Try values between 0.05 and 0.3.")]
        [SerializeField] private float swaySmoothing = 0.12f;

        [Tooltip("Maximum angle (degrees) the flashlight can deviate " +
                 "from the camera's forward direction.")]
        [SerializeField] private float maxSwayAngle = 5f;

        // ──────────────────────────────────────────────────
        // PRIVATE STATE
        // ──────────────────────────────────────────────────

        // The smoothed forward direction the flashlight is currently pointing
        private Vector3 _currentSwayDirection;

        // Required by Vector3.SmoothDamp — holds the current velocity
        private Vector3 _swayVelocity;

        // ──────────────────────────────────────────────────
        // UNITY LIFECYCLE
        // ──────────────────────────────────────────────────

        private void Start()
        {
            if (spotLight == null)
            {
                Debug.LogWarning("[Flashlight] No Spot Light assigned! " +
                                 "Drag a Light component into the spotLight field.", this);
                return;
            }

            // Initialise sway direction so there is no snap on the first frame
            _currentSwayDirection = transform.forward;

            // Apply starting state
            spotLight.enabled = startOn;
        }

        private void Update()
        {
            HandleToggle();
            HandleSway();
        }

        // ──────────────────────────────────────────────────
        // TOGGLE
        // ──────────────────────────────────────────────────

        private void HandleToggle()
        {
            if (Input.GetKeyDown(KeyCode.F) && spotLight != null)
                spotLight.enabled = !spotLight.enabled;
        }

        // ──────────────────────────────────────────────────
        // SWAY
        // ──────────────────────────────────────────────────

        private void HandleSway()
        {
            if (spotLight == null) return;

            // Smoothly move _currentSwayDirection toward the camera's
            // actual forward direction (transform.forward = the camera's Z axis)
            _currentSwayDirection = Vector3.SmoothDamp(
                _currentSwayDirection,      // from
                transform.forward,          // to (target = camera forward)
                ref _swayVelocity,          // velocity state (modified by SmoothDamp)
                swaySmoothing               // smooth time in seconds
            );

            // Clamp: don't let the sway deviate too far from camera forward
            // by using Vector3.RotateTowards with a maximum angle
            Vector3 clampedDirection = Vector3.RotateTowards(
                transform.forward,
                _currentSwayDirection,
                maxSwayAngle * Mathf.Deg2Rad,   // max angle in radians
                0f                              // no magnitude change
            );

            // Point the flashlight in the sway direction
            spotLight.transform.rotation = Quaternion.LookRotation(clampedDirection);
        }
    }
}
