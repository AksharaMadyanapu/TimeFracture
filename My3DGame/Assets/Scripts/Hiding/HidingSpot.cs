// ============================================================
// HidingSpot.cs
// ------------------------------------------------------------
// Represents an interactive hiding place (locker, under-desk, cabinet).
//
// RESPONSIBILITIES:
//   1. Defines hide and exit positions/rotations.
//   2. Implements IInteractable so PlayerInteractor can raycast and enter.
//   3. Enforces era restrictions (Any, PastOnly, PresentOnly).
//   4. Exposes static HidingSpot.PlayerIsHiddenAt so ghosts (Milestone 5)
//      can check and search hiding locations.
// ============================================================

using UnityEngine;
using TimeFracture.Player;
using TimeFracture.Era;

namespace TimeFracture.Hiding
{
    public enum EraRestriction
    {
        [Tooltip("Available in all timelines.")]
        Any,

        [Tooltip("Only functional in the Past laboratory.")]
        PastOnly,

        [Tooltip("Only functional in the decayed Present facility.")]
        PresentOnly
    }

    [RequireComponent(typeof(Collider))]
    public class HidingSpot : MonoBehaviour, IInteractable
    {
        // ──────────────────────────────────────────────────
        // STATIC GHOST ACCESSIBILITY HOOK
        // ──────────────────────────────────────────────────

        /// <summary>
        /// The hiding spot the player is currently inside (null if not hiding).
        /// Ghost AI will inspect this in Milestone 5 to check or search lockers.
        /// </summary>
        public static HidingSpot PlayerIsHiddenAt { get; internal set; }

        /// <summary>
        /// Global list of all active hiding spots in the scene for ghost AI search queries.
        /// </summary>
        public static readonly System.Collections.Generic.List<HidingSpot> AllSpots = new System.Collections.Generic.List<HidingSpot>();

        // ──────────────────────────────────────────────────
        // INSPECTOR FIELDS
        // ──────────────────────────────────────────────────

        [Header("Anchor Points")]
        [Tooltip("Where the player's camera sits inside the hiding spot.")]
        [SerializeField] private Transform hidePoint;

        [Tooltip("Where the player is deposited when exiting.")]
        [SerializeField] private Transform exitPoint;

        [Header("Era Availability")]
        [Tooltip("Which era this hiding spot is usable in.")]
        [SerializeField] private EraRestriction eraRestriction = EraRestriction.Any;

        [Header("Transition Durations")]
        [Tooltip("Seconds taken to enter the hiding spot.")]
        [SerializeField] private float enterDuration = 0.6f;

        [Tooltip("Seconds taken to exit the hiding spot.")]
        [SerializeField] private float exitDuration = 0.5f;

        [Header("Peep Look Angles")]
        [Tooltip("Maximum horizontal look angle (degrees left/right) while inside.")]
        [SerializeField] private float maxHorizontalAngle = 35f;

        [Tooltip("Maximum vertical look angle (degrees up/down) while inside.")]
        [SerializeField] private float maxVerticalAngle = 20f;

        [Header("Prompts")]
        [Tooltip("Interaction prompt to enter.")]
        [SerializeField] private string enterPrompt = "Press [E] to hide in locker";

        [Tooltip("Interaction prompt to exit.")]
        [SerializeField] private string exitPrompt = "Press [E] to exit locker";

        [Tooltip("Message shown if trying to use in the wrong era.")]
        [SerializeField] private string invalidEraPrompt = "Locker is damaged / unavailable";

        [Header("Audio")]
        [Tooltip("Sound played when entering the spot (e.g. metal door creak & close).")]
        [SerializeField] private AudioClip enterSound;

        [Tooltip("Sound played when exiting the spot (e.g. metal door push open).")]
        [SerializeField] private AudioClip exitSound;

        [Tooltip("AudioSource to play door sounds on.")]
        [SerializeField] private AudioSource audioSource;

        // ──────────────────────────────────────────────────
        // PROPERTIES
        // ──────────────────────────────────────────────────

        public Transform HidePoint => hidePoint != null ? hidePoint : transform;
        public Transform ExitPoint => exitPoint != null ? exitPoint : transform;
        public float EnterDuration => enterDuration;
        public float ExitDuration => exitDuration;
        public float MaxHorizontalAngle => maxHorizontalAngle;
        public float MaxVerticalAngle => maxVerticalAngle;
        public EraRestriction Restriction => eraRestriction;

        private void Awake()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            // Create default anchors if unassigned
            if (hidePoint == null)
            {
                GameObject hp = new GameObject("HidePoint");
                hp.transform.SetParent(transform);
                hp.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                hp.transform.localRotation = Quaternion.identity;
                hidePoint = hp.transform;
            }

            if (exitPoint == null)
            {
                GameObject ep = new GameObject("ExitPoint");
                ep.transform.SetParent(transform);
                ep.transform.localPosition = new Vector3(0f, 0f, 1.2f);
                ep.transform.localRotation = Quaternion.identity;
                exitPoint = ep.transform;
            }
        }

        private void OnEnable()
        {
            if (!AllSpots.Contains(this))
            {
                AllSpots.Add(this);
            }
        }

        private void OnDisable()
        {
            AllSpots.Remove(this);
        }

        // ──────────────────────────────────────────────────
        // ERA VALIDATION
        // ──────────────────────────────────────────────────

        /// <summary>
        /// Returns true if this spot is usable in the currently active era.
        /// </summary>
        public bool IsAvailableInCurrentEra()
        {
            if (eraRestriction == EraRestriction.Any) return true;

            if (EraManager.Instance == null) return true;

            TimeFracture.Era.Era current = EraManager.Instance.CurrentEra;

            if (eraRestriction == EraRestriction.PastOnly && current == TimeFracture.Era.Era.Past) return true;
            if (eraRestriction == EraRestriction.PresentOnly && current == TimeFracture.Era.Era.Present) return true;

            return false;
        }

        // ──────────────────────────────────────────────────
        // IINTERACTABLE IMPLEMENTATION
        // ──────────────────────────────────────────────────

        public string GetPrompt()
        {
            // If the player is already hidden in this spot
            if (PlayerIsHiddenAt == this)
            {
                return exitPrompt;
            }

            // If player is hidden elsewhere, show nothing
            if (PlayerIsHiddenAt != null)
            {
                return string.Empty;
            }

            // Check era restriction
            if (!IsAvailableInCurrentEra())
            {
                return invalidEraPrompt;
            }

            return enterPrompt;
        }

        public void Interact(PlayerInteractor interactor)
        {
            PlayerHide playerHide = interactor.GetComponentInParent<PlayerHide>();
            if (playerHide == null)
            {
                playerHide = FindAnyObjectByType<PlayerHide>();
            }

            if (playerHide == null)
            {
                Debug.LogWarning("[HidingSpot] No PlayerHide component found on the player!");
                return;
            }

            // If currently hiding in this spot, exit
            if (PlayerIsHiddenAt == this)
            {
                playerHide.ExitHiding();
                PlayExitSound();
                return;
            }

            // Cannot enter if already hiding in another spot
            if (PlayerIsHiddenAt != null) return;

            // Validate era
            if (!IsAvailableInCurrentEra())
            {
                Debug.Log($"[HidingSpot] Cannot hide here: restricted to {eraRestriction}");
                return;
            }

            // Enter spot
            PlayEnterSound();
            playerHide.EnterHiding(this);
        }

        // ──────────────────────────────────────────────────
        // AUDIO HELPERS
        // ──────────────────────────────────────────────────

        public void PlayEnterSound()
        {
            if (audioSource != null && enterSound != null)
                audioSource.PlayOneShot(enterSound);
        }

        public void PlayExitSound()
        {
            if (audioSource != null && exitSound != null)
                audioSource.PlayOneShot(exitSound);
        }

#if UNITY_EDITOR
        // Visualize the hide and exit points in the Editor
        private void OnDrawGizmosSelected()
        {
            if (hidePoint != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(hidePoint.position, 0.25f);
                Gizmos.DrawRay(hidePoint.position, hidePoint.forward * 0.7f);
            }

            if (exitPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(exitPoint.position, new Vector3(0.6f, 1.8f, 0.6f));
                Gizmos.DrawRay(exitPoint.position, exitPoint.forward * 0.7f);
            }
        }
#endif
    }
}
