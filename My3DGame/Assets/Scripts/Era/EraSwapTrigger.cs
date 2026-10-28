// ============================================================
// EraSwapTrigger.cs
// ------------------------------------------------------------
// An in-world interactive object (e.g. Chrono-Stabilizer machine,
// terminal, tape recorder) that triggers an era swap when used.
//
// IMPLEMENTS:
//   IInteractable (from TimeFracture.Player) — works seamlessly
//   with PlayerInteractor raycasts.
// ============================================================

using UnityEngine;
using TimeFracture.Player;

namespace TimeFracture.Era
{
    [RequireComponent(typeof(Collider))]
    public class EraSwapTrigger : MonoBehaviour, IInteractable
    {
        [Header("Prompts")]
        [Tooltip("Text shown to the player when looking at the device.")]
        [SerializeField] private string activePrompt = "Press [E] to engage Chrono-Stabilizer";

        [Tooltip("Text shown after use when triggerOnce is true.")]
        [SerializeField] private string exhaustedPrompt = "Chrono-Stabilizer core depleted";

        [Header("Trigger Settings")]
        [Tooltip("If true, this machine can only be used once.")]
        [SerializeField] private bool triggerOnce = false;

        [Tooltip("Radius of noise emitted when activated (ghosts can hear this).")]
        [SerializeField] private float noiseRadius = 12f;

        [Header("Audio")]
        [Tooltip("Optional audio source on or near the machine.")]
        [SerializeField] private AudioSource audioSource;

        [Tooltip("Optional sound effect to play on activation.")]
        [SerializeField] private AudioClip activateSound;

        // Runtime state
        private bool _hasTriggered = false;

        private void Awake()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }
        }

        // ──────────────────────────────────────────────────
        // IINTERACTABLE IMPLEMENTATION
        // ──────────────────────────────────────────────────

        public string GetPrompt()
        {
            if (triggerOnce && _hasTriggered)
            {
                return exhaustedPrompt;
            }

            return activePrompt;
        }

        public void Interact(PlayerInteractor interactor)
        {
            // If one-shot and already used, do nothing
            if (triggerOnce && _hasTriggered)
            {
                return;
            }

            _hasTriggered = true;

            // Play local audio if available
            if (audioSource != null && activateSound != null)
            {
                audioSource.PlayOneShot(activateSound);
            }

            // Emit noise at this location
            NoiseEmitter.Emit(transform.position, noiseRadius);

            // Execute the era swap
            if (EraManager.Instance != null)
            {
                EraManager.Instance.SwapEra();
            }
            else
            {
                Debug.LogWarning("[EraSwapTrigger] No EraManager found in scene!");
            }
        }
    }
}
