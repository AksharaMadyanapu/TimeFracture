// ============================================================
// AudioLogPickup.cs
// ------------------------------------------------------------
// World interactive object representing an audio log (cassette/recorder).
// When looked at by the player, displays a prompt. When the player
// presses [E], collects the log, sets the WorldState flag, and auto-plays.
// ============================================================

using UnityEngine;
using TimeFracture.Player;
using TimeFracture.World;
using TimeFracture.Activity;

namespace TimeFracture.AudioLogs
{
    [RequireComponent(typeof(Collider))]
    public class AudioLogPickup : MonoBehaviour, IInteractable
    {
        // ──────────────────────────────────────────────────
        // INSPECTOR FIELDS
        // ──────────────────────────────────────────────────

        [Header("Audio Log Reference")]
        [Tooltip("The AudioLogData asset awarded by this pickup.")]
        [SerializeField] private AudioLogData logData;

        [Header("Feedback")]
        [Tooltip("Sound played upon pickup.")]
        [SerializeField] private AudioClip pickupChime;

        [Tooltip("AudioSource used to play pickup sound.")]
        [SerializeField] private AudioSource audioSource;

        [Tooltip("If true, automatically begins playing the log through AudioLogPlayer.")]
        [SerializeField] private bool autoPlayOnCollect = true;

        [Header("Visual Effects")]
        [Tooltip("Optional bob and spin idle animation.")]
        [SerializeField] private bool idleAnimation = true;

        // ──────────────────────────────────────────────────
        // INTERNAL STATE
        // ──────────────────────────────────────────────────

        private bool _isCollected = false;
        private Vector3 _startPosition;

        // ──────────────────────────────────────────────────
        // PROPERTIES
        // ──────────────────────────────────────────────────

        public AudioLogData LogData
        {
            get => logData;
            set => logData = value;
        }

        public bool IsCollected => _isCollected;

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
            if (idleAnimation && !_isCollected)
            {
                transform.Rotate(Vector3.up, 30f * Time.deltaTime, Space.World);
                float y = _startPosition.y + Mathf.Sin(Time.time * 2f) * 0.04f;
                transform.position = new Vector3(transform.position.x, y, transform.position.z);
            }
        }

        // ──────────────────────────────────────────────────
        // IINTERACTABLE IMPLEMENTATION
        // ──────────────────────────────────────────────────

        public string GetPrompt()
        {
            if (_isCollected) return string.Empty;
            string title = logData != null ? logData.title : "Audio Log";
            return $"Press [E] to listen: {title}";
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (_isCollected) return;
            _isCollected = true;

            // 1. Set WorldState flag if specified
            if (logData != null && !string.IsNullOrEmpty(logData.worldStateFlag))
            {
                WorldState.SetFlag(logData.worldStateFlag, true);
                Debug.Log($"[AudioLogPickup] Set WorldState flag '{logData.worldStateFlag}' -> true");
            }

            // 2. Play log through AudioLogPlayer
            if (autoPlayOnCollect && logData != null)
            {
                AudioLogPlayer.Instance.PlayLog(logData);
            }

            // 3. Audio feedback
            if (audioSource != null && pickupChime != null)
            {
                audioSource.PlayOneShot(pickupChime);
            }

            // 4. Disable visual components immediately
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null) renderers[i].enabled = false;
            }

            Destroy(gameObject, 0.4f);
        }

        // ──────────────────────────────────────────────────
        // SELF-WIRING HELPERS
        // ──────────────────────────────────────────────────

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
        }
    }
}
