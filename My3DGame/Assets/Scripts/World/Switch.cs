// ============================================================
// Switch.cs
// ============================================================
// A toggleable switch that the player can interact with.
// It fires two UnityEvents (one for ON, one for OFF) so you
// can connect it to any other script in the Inspector without
// writing code.
//
// COMMON USES:
//   - Set a WorldState flag to unlock a door.
//   - Toggle a Light component on/off.
//   - Enable/disable a GameObject.
//
// HOW UnityEvents WORK:
//   In the Inspector, expand the OnSwitchOn / OnSwitchOff
//   sections.  Click the '+' button to add an action.
//   Drag a target object, select a method or property, done.
//   No code needed for simple connections.
//
// ATTACH TO: Any GameObject (a wall panel, lever, box, etc.).
//   Add a Collider too (so raycasts can hit it).
// ============================================================

using UnityEngine;
using UnityEngine.Events;
using TimeFracture.Player;

namespace TimeFracture.World
{
    public class Switch : MonoBehaviour, IInteractable
    {
        // ──────────────────────────────────────────────────
        // INSPECTOR FIELDS
        // ──────────────────────────────────────────────────

        [Header("State")]
        [Tooltip("Starting state of the switch.")]
        [SerializeField] private bool isOn = false;

        [Header("Prompts")]
        [Tooltip("Prompt shown when the switch is currently OFF.")]
        [SerializeField] private string turnOnPrompt = "Press [E] to activate";

        [Tooltip("Prompt shown when the switch is currently ON.")]
        [SerializeField] private string turnOffPrompt = "Press [E] to deactivate";

        [Header("World Flag (optional)")]
        [Tooltip("When this switch is turned ON, set this WorldState flag. " +
                 "Leave empty to skip. Example: 'LabSwitchOn'")]
        [SerializeField] private string setFlagOnActivate = "";

        [Tooltip("When this switch is turned OFF, clear the flag above.")]
        [SerializeField] private bool clearFlagOnDeactivate = true;

        [Header("Events")]
        [Tooltip("Fired when the switch is turned ON. " +
                 "Connect lights, doors, sounds, etc. here.")]
        [SerializeField] private UnityEvent onSwitchOn;

        [Tooltip("Fired when the switch is turned OFF.")]
        [SerializeField] private UnityEvent onSwitchOff;

        [Header("Visual Feedback (optional)")]
        [Tooltip("A Renderer whose material colour changes to show state. " +
                 "Leave empty to skip.")]
        [SerializeField] private Renderer indicatorRenderer;

        [Tooltip("Colour shown on the indicator when the switch is ON.")]
        [SerializeField] private Color onColour = Color.green;

        [Tooltip("Colour shown on the indicator when the switch is OFF.")]
        [SerializeField] private Color offColour = Color.red;

        // ──────────────────────────────────────────────────
        // UNITY LIFECYCLE
        // ──────────────────────────────────────────────────

        private void Start()
        {
            // Apply the starting colour so the indicator matches the
            // initial isOn value without requiring an interaction first
            ApplyIndicatorColour();
        }

        // ──────────────────────────────────────────────────
        // IInteractable IMPLEMENTATION
        // ──────────────────────────────────────────────────

        public string GetPrompt()
        {
            return isOn ? turnOffPrompt : turnOnPrompt;
        }

        public void Interact(PlayerInteractor interactor)
        {
            // Toggle the state
            isOn = !isOn;

            if (isOn)
            {
                // --- Turned ON ---

                // Set the WorldState flag (if configured)
                if (!string.IsNullOrEmpty(setFlagOnActivate))
                    WorldState.SetFlag(setFlagOnActivate, true);

                // Fire the UnityEvent (connects to door, light, etc. in Inspector)
                onSwitchOn?.Invoke();

                Debug.Log($"[Switch] '{gameObject.name}' turned ON. " +
                          $"Flag: '{setFlagOnActivate}'");
            }
            else
            {
                // --- Turned OFF ---

                // Clear the flag if the option is enabled
                if (!string.IsNullOrEmpty(setFlagOnActivate) && clearFlagOnDeactivate)
                    WorldState.ClearFlag(setFlagOnActivate);

                // Fire the OFF event
                onSwitchOff?.Invoke();

                Debug.Log($"[Switch] '{gameObject.name}' turned OFF.");
            }

            // Update the visual indicator
            ApplyIndicatorColour();
        }

        // ──────────────────────────────────────────────────
        // HELPERS
        // ──────────────────────────────────────────────────

        private void ApplyIndicatorColour()
        {
            if (indicatorRenderer == null) return;

            // Note: this creates a material instance (expected behaviour;
            // you'll see "(Instance)" in the Inspector — that is fine).
            indicatorRenderer.material.color = isOn ? onColour : offColour;
        }

        // Public accessor so other scripts can read the state without a reference
        // to the concrete Switch type (only needed in rare edge cases).
        public bool IsOn => isOn;
    }
}
