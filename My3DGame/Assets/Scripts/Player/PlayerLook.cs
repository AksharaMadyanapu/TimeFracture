// ============================================================
// PlayerLook.cs
// ============================================================
// Handles first-person mouse look:
//   • Horizontal (left/right) rotation rotates the PLAYER BODY.
//   • Vertical (up/down) rotation rotates only the CAMERA HOLDER.
//
// This separation is important!  If we rotated the whole body
// up/down, the CharacterController would tilt and movement
// would be wrong.
//
// Attach this script to the PLAYER ROOT GameObject.
// ============================================================

using UnityEngine;

namespace TimeFracture.Player
{
    public class PlayerLook : MonoBehaviour
    {
        // ──────────────────────────────────────────────────
        // INSPECTOR FIELDS
        // ──────────────────────────────────────────────────

        [Header("References")]
        [Tooltip("Drag the CameraHolder (parent of Main Camera) here. " +
                 "This transform is rotated vertically.")]
        [SerializeField] private Transform cameraHolder;

        [Header("Sensitivity")]
        [Tooltip("Mouse sensitivity for left/right rotation.")]
        [SerializeField] private float sensitivityX = 2f;

        [Tooltip("Mouse sensitivity for up/down rotation.")]
        [SerializeField] private float sensitivityY = 2f;

        [Header("Vertical Clamp")]
        [Tooltip("Maximum degrees the camera can look up (negative = up in Unity).")]
        [SerializeField] private float minVerticalAngle = -85f;

        [Tooltip("Maximum degrees the camera can look down.")]
        [SerializeField] private float maxVerticalAngle = 85f;

        // ──────────────────────────────────────────────────
        // PRIVATE STATE
        // ──────────────────────────────────────────────────

        // Accumulated vertical rotation angle (stored as a plain float
        // so we can clamp it before applying it to the transform)
        private float _verticalAngle;

        // ──────────────────────────────────────────────────
        // UNITY LIFECYCLE
        // ──────────────────────────────────────────────────

        public float Sensitivity => sensitivityX;

        public void SetSensitivity(float newSensitivity)
        {
            sensitivityX = newSensitivity;
            sensitivityY = newSensitivity;
        }

        private void Start()
        {
            float saved = PlayerPrefs.GetFloat("MouseSensitivity", sensitivityX);
            SetSensitivity(saved);

            // Hide the cursor and lock it to the centre of the screen.
            // This is the standard FPS cursor setup.
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }

        private void Update()
        {
            // Read raw mouse delta this frame.
            // "Mouse X" and "Mouse Y" are built-in legacy Input axes.
            float mouseX = Input.GetAxis("Mouse X") * sensitivityX;
            float mouseY = Input.GetAxis("Mouse Y") * sensitivityY;

            // --- Horizontal rotation (player body) ---
            // Rotate the player GameObject around the world Y axis.
            // We use Space.Self so it always turns relative to the player's
            // own orientation rather than world space.
            transform.Rotate(Vector3.up * mouseX, Space.Self);

            // --- Vertical rotation (camera holder only) ---
            // Accumulate the angle.  Mouse Y axis: moving up = positive value,
            // but in Unity looking up means a NEGATIVE X rotation, so subtract.
            _verticalAngle -= mouseY;

            // Clamp so the player cannot look past straight up or straight down
            _verticalAngle = Mathf.Clamp(_verticalAngle, minVerticalAngle, maxVerticalAngle);

            // Apply the clamped angle to the camera holder's local rotation.
            // We set the full rotation each frame instead of adding, which
            // prevents floating-point drift.
            if (cameraHolder != null)
                cameraHolder.localRotation = Quaternion.Euler(_verticalAngle, 0f, 0f);
        }

        /// <summary>
        /// Explicitly aligns the player body and camera to look in a given world direction.
        /// Useful when exiting hiding spots, cutscenes, or respawns to avoid camera snapping.
        /// </summary>
        public void SetLookRotation(Vector3 forward)
        {
            if (forward.sqrMagnitude < 0.001f) return;

            Vector3 flatForward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            if (flatForward.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(flatForward, Vector3.up);
            }

            // Calculate vertical pitch (negative X is up in Unity)
            float pitch = -Vector3.SignedAngle(flatForward, forward, transform.right);
            _verticalAngle = Mathf.Clamp(pitch, minVerticalAngle, maxVerticalAngle);

            if (cameraHolder != null)
                cameraHolder.localRotation = Quaternion.Euler(_verticalAngle, 0f, 0f);
        }
    }
}
