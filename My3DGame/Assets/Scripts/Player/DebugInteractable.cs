// ============================================================
// DebugInteractable.cs
// ============================================================
// A simple MonoBehaviour that implements IInteractable.
// Attach it to any GameObject in the scene to make it
// interactable.  When the player presses E while looking at it,
// it logs a message to the Unity Console.
//
// This script exists ONLY for testing Milestone 1.
// Replace or remove it in later milestones.
// ============================================================

using UnityEngine;

namespace TimeFracture.Player
{
    public class DebugInteractable : MonoBehaviour, IInteractable
    {
        // ──────────────────────────────────────────────────
        // INSPECTOR FIELDS
        // ──────────────────────────────────────────────────

        [Tooltip("Text shown in the centre-screen prompt when the player " +
                 "looks at this object.")]
        [SerializeField] private string promptMessage = "Press [E] to interact";

        [Tooltip("Message printed to the Console when the player interacts.")]
        [SerializeField] private string debugLogMessage = "DebugInteractable: Interact() called!";

        // ──────────────────────────────────────────────────
        // IInteractable IMPLEMENTATION
        // ──────────────────────────────────────────────────

        /// <summary>
        /// Returns the prompt text displayed on screen.
        /// </summary>
        public string GetPrompt()
        {
            return promptMessage;
        }

        /// <summary>
        /// Called when the player presses E while looking at this object.
        /// </summary>
        public void Interact(PlayerInteractor interactor)
        {
            // Log to the Unity Console (Window > General > Console)
            Debug.Log(debugLogMessage, this);

            // Optional: flash the object colour to give visual feedback.
            // Works only if the object has a MeshRenderer.
            var rend = GetComponent<MeshRenderer>();
            if (rend != null)
                StartCoroutine(FlashColour(rend));
        }

        // ──────────────────────────────────────────────────
        // HELPER: flash colour briefly
        // ──────────────────────────────────────────────────

        private System.Collections.IEnumerator FlashColour(MeshRenderer rend)
        {
            Color original = rend.material.color;
            rend.material.color = Color.green;
            yield return new WaitForSeconds(0.15f);
            rend.material.color = original;
        }
    }
}
