// ============================================================
// PlayerInteractor.cs
// ============================================================
// Sends a raycast from the centre of the camera every frame.
// If the ray hits a GameObject that has a component implementing
// IInteractable, it:
//   1. Shows a TextMeshPro prompt text (e.g. "Press [E] to open").
//   2. Calls Interact() on that component when the player presses E.
//
// ATTACH THIS SCRIPT TO: The Main Camera GameObject.
//
// DEPENDENCIES: TextMeshPro (free Unity package).
// ============================================================

using TMPro;
using UnityEngine;

namespace TimeFracture.Player
{
    public class PlayerInteractor : MonoBehaviour
    {
        // ──────────────────────────────────────────────────
        // INSPECTOR FIELDS
        // ──────────────────────────────────────────────────

        [Header("Raycast Settings")]
        [Tooltip("Maximum distance (metres) for the interaction raycast.")]
        [SerializeField] private float interactRange = 2.5f;

        [Tooltip("Layer mask for the raycast. Set this to the layer your " +
                 "interactable objects are on (e.g. 'Interactable') to " +
                 "avoid hitting unintended geometry.  If left as 'Everything'" +
                 " it will check all layers.")]
        [SerializeField] private LayerMask interactLayer = ~0; // ~0 = Everything

        [Header("UI Prompt")]
        [Tooltip("Drag the TextMeshPro UI Text element that shows the " +
                 "interaction prompt here (e.g. the 'InteractPrompt' object).")]
        [SerializeField] private TMP_Text promptText;

        // ──────────────────────────────────────────────────
        // PRIVATE STATE
        // ──────────────────────────────────────────────────

        // The IInteractable the player is currently looking at (can be null)
        private IInteractable _currentTarget;

        // ──────────────────────────────────────────────────
        // UNITY LIFECYCLE
        // ──────────────────────────────────────────────────

        private void Start()
        {
            // Hide prompt on startup
            if (promptText != null)
                promptText.enabled = false;
        }

        private void Update()
        {
            ScanForInteractable();
            HandleInteractInput();
        }

        // ──────────────────────────────────────────────────
        // SCAN
        // ──────────────────────────────────────────────────

        private void ScanForInteractable()
        {
            // Cast a ray from the camera's position in the direction it is facing.
            // Physics.Raycast returns true if it hits something.
            Ray ray = new Ray(transform.position, transform.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, interactRange, interactLayer,
                                QueryTriggerInteraction.Ignore))
            {
                // Try to get an IInteractable component from the hit object.
                // GetComponent returns null if no such component exists.
                IInteractable interactable = hit.collider.GetComponent<IInteractable>();

                if (interactable != null)
                {
                    _currentTarget = interactable;
                    ShowPrompt(_currentTarget.GetPrompt());
                    return; // found a target — skip the clear block below
                }
            }

            // Nothing interactable found — clear the target and hide the prompt
            _currentTarget = null;
            HidePrompt();
        }

        // ──────────────────────────────────────────────────
        // INPUT
        // ──────────────────────────────────────────────────

        private void HandleInteractInput()
        {
            if (_currentTarget == null) return;

            if (Input.GetKeyDown(KeyCode.E))
                _currentTarget.Interact(this);
        }

        // ──────────────────────────────────────────────────
        // PROMPT HELPERS
        // ──────────────────────────────────────────────────

        private void ShowPrompt(string message)
        {
            if (promptText == null) return;
            promptText.text    = message;
            promptText.enabled = true;
        }

        private void HidePrompt()
        {
            if (promptText == null) return;
            promptText.enabled = false;
        }

        // ──────────────────────────────────────────────────
        // OPTIONAL DEBUG VISUALISATION
        // ──────────────────────────────────────────────────

        // Draw the interact ray in the Scene view (not visible in Game view).
        // Green = hitting an interactable. Red = not hitting one.
        private void OnDrawGizmos()
        {
            Gizmos.color = (_currentTarget != null) ? Color.green : Color.red;
            Gizmos.DrawRay(transform.position, transform.forward * interactRange);
        }
    }
}
