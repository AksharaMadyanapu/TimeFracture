// ============================================================
// ItemPickup.cs
// ------------------------------------------------------------
// World pickup object that implements IInteractable.
// When looked at by the player, displays a prompt. When the player
// presses [E], adds the item to the Inventory and removes itself.
//
// ATTACH TO: Any pickup GameObject placed in the world.
// NOTE: GameObject must have a Collider and be on the 'Interactable' layer.
// ============================================================

using UnityEngine;
using TimeFracture.Player;

namespace TimeFracture.Items
{
    [RequireComponent(typeof(Collider))]
    public class ItemPickup : MonoBehaviour, IInteractable
    {
        // ──────────────────────────────────────────────────
        // INSPECTOR FIELDS
        // ──────────────────────────────────────────────────

        [Header("Item Configuration")]
        [Tooltip("The type of item this pickup grants.")]
        [SerializeField] private ItemType itemType = ItemType.NoiseDecoy;

        [Tooltip("How many units of the item this pickup awards.")]
        [SerializeField] private int amount = 1;

        [Tooltip("Display name used in interaction prompts.")]
        [SerializeField] private string itemName = "Noise Decoy";

        [Header("Feedback")]
        [Tooltip("Optional sound played when item is picked up.")]
        [SerializeField] private AudioClip pickupSound;

        [Tooltip("AudioSource component used to play pickup feedback.")]
        [SerializeField] private AudioSource audioSource;

        [Header("Idle Animation (Optional)")]
        [Tooltip("If true, gently bobs and rotates in place.")]
        [SerializeField] private bool idleBobbing = true;

        // ──────────────────────────────────────────────────
        // INTERNAL STATE
        // ──────────────────────────────────────────────────

        private Vector3 _startPosition;
        private bool _isCollected = false;

        // ──────────────────────────────────────────────────
        // PROPERTIES
        // ──────────────────────────────────────────────────

        public ItemType ItemType => itemType;
        public int Amount => amount;
        public string ItemName => itemName;

        // ──────────────────────────────────────────────────
        // UNITY LIFECYCLE
        // ──────────────────────────────────────────────────

        private void Awake()
        {
            _startPosition = transform.position;
            SelfWireComponents();
        }

        private void Update()
        {
            if (idleBobbing && !_isCollected)
            {
                // Slow rotation
                transform.Rotate(Vector3.up, 40f * Time.deltaTime, Space.World);
                // Subtle vertical bob
                float newY = _startPosition.y + Mathf.Sin(Time.time * 2.5f) * 0.05f;
                transform.position = new Vector3(transform.position.x, newY, transform.position.z);
            }
        }

        // ──────────────────────────────────────────────────
        // IINTERACTABLE IMPLEMENTATION
        // ──────────────────────────────────────────────────

        /// <summary>
        /// Returns the prompt text shown in the centre-screen HUD when looking at this object.
        /// </summary>
        public string GetPrompt()
        {
            if (_isCollected) return string.Empty;

            Inventory inv = Inventory.Instance;
            if (inv == null)
            {
                inv = Object.FindFirstObjectByType<Inventory>();
            }

            if (inv != null && !inv.CanAddItem(itemType))
            {
                return $"[Full] Cannot carry more {itemName}s";
            }

            return $"Press [E] to pick up {itemName}";
        }

        /// <summary>
        /// Called when the player presses [E] while looking at this object.
        /// </summary>
        public void Interact(PlayerInteractor interactor)
        {
            if (_isCollected) return;

            Inventory inv = Inventory.Instance;
            if (inv == null)
            {
                inv = Object.FindFirstObjectByType<Inventory>();
            }

            if (inv == null)
            {
                Debug.LogWarning("[ItemPickup] Cannot collect item: Inventory not found in scene!");
                return;
            }

            // Try to add item to inventory
            if (inv.AddItem(itemType, amount))
            {
                _isCollected = true;
                PlayPickupFeedback();

                // Disable colliders and renderers immediately so it cannot be interacted with again
                Collider col = GetComponent<Collider>();
                if (col != null) col.enabled = false;

                Renderer[] renderers = GetComponentsInChildren<Renderer>();
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null) renderers[i].enabled = false;
                }

                // Destroy after audio finishes
                float destroyDelay = (pickupSound != null) ? pickupSound.length : 0.35f;
                Destroy(gameObject, destroyDelay);
            }
            else
            {
                Debug.Log($"[ItemPickup] Inventory is already full for {itemName}.");
            }
        }

        // ──────────────────────────────────────────────────
        // FEEDBACK & SELF-WIRING
        // ──────────────────────────────────────────────────

        private void PlayPickupFeedback()
        {
            if (audioSource != null && pickupSound != null)
            {
                audioSource.PlayOneShot(pickupSound, 0.7f);
            }
            else
            {
                // Play procedural chime at camera location
                AudioClip chime = CreateProceduralChime();
                if (chime != null && Camera.main != null)
                {
                    AudioSource.PlayClipAtPoint(chime, Camera.main.transform.position, 0.5f);
                }
            }
        }

        private void SelfWireComponents()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
            }
            if (audioSource != null)
            {
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1.0f;
            }

            // Auto-detect item name if default
            if (string.IsNullOrEmpty(itemName))
            {
                itemName = itemType == ItemType.NoiseDecoy ? "Noise Decoy" : "Flare";
            }
        }

        private static AudioClip CreateProceduralChime()
        {
            int sampleRate = 44100;
            float duration = 0.25f;
            int totalSamples = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = 1f - ((float)i / totalSamples);
                // Two harmonized sine waves (880Hz + 1320Hz)
                samples[i] = (Mathf.Sin(2f * Mathf.PI * 880f * t) * 0.6f +
                              Mathf.Sin(2f * Mathf.PI * 1320f * t) * 0.4f) * envelope * 0.35f;
            }

            AudioClip clip = AudioClip.Create("Pickup_Chime", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
