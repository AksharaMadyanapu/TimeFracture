// ============================================================
// EraLinkedObject.cs
// ------------------------------------------------------------
// Demonstrates cross-era cause-and-effect using WorldState flags.
//
// HOW IT WORKS:
//   1. An action taken in the Past (e.g. moving a crate, clearing a barrier,
//      or flipping a switch) sets a WorldState flag.
//   2. An EraLinkedObject in the Present (or Past) listens to that flag
//      and modifies its own state (disappears, appears, or moves aside).
//   3. It can also act as an IInteractable itself (e.g. a barrier the player
//      interacts with directly in the Past to set the flag).
//
// EXAMPLE:
//   In Past: Player looks at barrier -> "Press [E] to move heavy barrier".
//            Player moves it -> Sets flag "BarrierCleared" = true.
//   In Present: Present door or obstacle checks "BarrierCleared" ->
//               Present door unlocks or blockage disappears!
// ============================================================

using System.Collections;
using UnityEngine;
using TimeFracture.Player;
using TimeFracture.World;

namespace TimeFracture.Era
{
    public enum LinkAction
    {
        [Tooltip("Deactivates this GameObject when the flag is true (e.g. rubble/barrier disappears in Present).")]
        DeactivateWhenFlagTrue,

        [Tooltip("Activates this GameObject when the flag is true (e.g. power restored or walkway appears).")]
        ActivateWhenFlagTrue,

        [Tooltip("Smoothly moves this object to local offset when the flag is true (e.g. sliding barrier).")]
        MoveToOffsetWhenFlagTrue
    }

    public class EraLinkedObject : MonoBehaviour, IInteractable
    {
        // ──────────────────────────────────────────────────
        // INSPECTOR FIELDS
        // ──────────────────────────────────────────────────

        [Header("WorldState Flag Link")]
        [Tooltip("The flag name in WorldState to monitor / set (e.g. 'BarrierMovedInPast').")]
        [SerializeField] private string flagName = "BarrierMovedInPast";

        [Tooltip("What happens to this object when the flag is true.")]
        [SerializeField] private LinkAction actionWhenFlagTrue = LinkAction.DeactivateWhenFlagTrue;

        [Tooltip("Local offset to move to when using MoveToOffsetWhenFlagTrue.")]
        [SerializeField] private Vector3 targetLocalOffset = new Vector3(0f, 0f, 2f);

        [Tooltip("Speed of the smooth move transition.")]
        [SerializeField] private float moveSpeed = 3f;

        [Header("Direct Interaction (Optional)")]
        [Tooltip("If true, the player can press E directly on this object to set the flag (e.g. the Past barrier).")]
        [SerializeField] private bool canInteract = false;

        [Tooltip("Interaction prompt before being used.")]
        [SerializeField] private string promptText = "Press [E] to move heavy barrier";

        [Tooltip("Interaction prompt after being used.")]
        [SerializeField] private string consumedPrompt = "Barrier has already been moved";

        [Tooltip("Noise radius emitted when interacted with.")]
        [SerializeField] private float noiseRadius = 8f;

        [Header("Audio (Optional)")]
        [Tooltip("Sound played when this object changes state.")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip actionSound;

        // ──────────────────────────────────────────────────
        // INTERNAL STATE
        // ──────────────────────────────────────────────────

        private Vector3 _initialLocalPosition;
        private Vector3 _targetLocalPosition;
        private Coroutine _moveCoroutine;
        private bool _isInteracted = false;

        private void Awake()
        {
            _initialLocalPosition = transform.localPosition;
            _targetLocalPosition = _initialLocalPosition + targetLocalOffset;

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }
        }

        private void OnEnable()
        {
            // Subscribe to WorldState changes
            WorldState.OnFlagChanged += HandleFlagChanged;

            // Immediately apply the current WorldState when enabled (e.g. when room activates)
            bool isFlagSet = WorldState.HasFlag(flagName);
            ApplyState(isFlagSet, immediate: true);
        }

        private void OnDisable()
        {
            WorldState.OnFlagChanged -= HandleFlagChanged;

            if (_moveCoroutine != null)
            {
                StopCoroutine(_moveCoroutine);
                _moveCoroutine = null;
            }
        }

        // ──────────────────────────────────────────────────
        // FLAG HANDLING
        // ──────────────────────────────────────────────────

        private void HandleFlagChanged(string changedFlag, bool newValue)
        {
            if (changedFlag == flagName)
            {
                ApplyState(newValue, immediate: false);
            }
        }

        private void ApplyState(bool flagActive, bool immediate)
        {
            _isInteracted = flagActive;

            switch (actionWhenFlagTrue)
            {
                case LinkAction.DeactivateWhenFlagTrue:
                    gameObject.SetActive(!flagActive);
                    break;

                case LinkAction.ActivateWhenFlagTrue:
                    gameObject.SetActive(flagActive);
                    break;

                case LinkAction.MoveToOffsetWhenFlagTrue:
                    Vector3 dest = flagActive ? _targetLocalPosition : _initialLocalPosition;
                    if (immediate)
                    {
                        transform.localPosition = dest;
                    }
                    else
                    {
                        if (_moveCoroutine != null) StopCoroutine(_moveCoroutine);
                        _moveCoroutine = StartCoroutine(MoveRoutine(dest));
                    }
                    break;
            }

            if (!immediate && audioSource != null && actionSound != null)
            {
                audioSource.PlayOneShot(actionSound);
            }
        }

        private IEnumerator MoveRoutine(Vector3 destination)
        {
            while (Vector3.Distance(transform.localPosition, destination) > 0.01f)
            {
                transform.localPosition = Vector3.MoveTowards(
                    transform.localPosition,
                    destination,
                    moveSpeed * Time.deltaTime
                );
                yield return null;
            }
            transform.localPosition = destination;
            _moveCoroutine = null;
        }

        // ──────────────────────────────────────────────────
        // IINTERACTABLE IMPLEMENTATION
        // ──────────────────────────────────────────────────

        public string GetPrompt()
        {
            if (!canInteract) return string.Empty;

            return _isInteracted ? consumedPrompt : promptText;
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (!canInteract || _isInteracted) return;

            // Set the WorldState flag so other objects and eras react
            WorldState.SetFlag(flagName, true);

            // Alert nearby ghosts
            NoiseEmitter.Emit(transform.position, noiseRadius);

            Debug.Log($"[EraLinkedObject] Interacted with {gameObject.name}. Set flag '{flagName}' to true.");
        }
    }
}
