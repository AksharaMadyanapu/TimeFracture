// ============================================================
// FinalHuntTrigger.cs
// ------------------------------------------------------------
// Escalating sequence triggered when the player approaches the master terminal.
// - Locks GhostActivityManager into the hunting tier.
// - Initiates unstable era swapping on an automated timer.
// - Accelerates ghost aggression for maximum dramatic tension.
// ============================================================

using System.Collections;
using UnityEngine;
using TimeFracture.Activity;
using TimeFracture.Era;
using TimelineEra = TimeFracture.Era.Era;

namespace TimeFracture.Ending
{
    [RequireComponent(typeof(Collider))]
    public class FinalHuntTrigger : MonoBehaviour
    {
        [Header("Final Hunt Parameters")]
        [Tooltip("Interval (in seconds) between unstable glitch era swaps.")]
        [SerializeField] private float eraSwapInterval = 12.0f;

        [Tooltip("Ghost activity level forced during the final hunt.")]
        [SerializeField] private float forcedActivityLevel = 100f;

        private bool _isTriggered = false;
        private Coroutine _unstableSwapRoutine;

        private void Awake()
        {
            Collider col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isTriggered) return;
            if (other.CompareTag("Player") || other.GetComponent<TimeFracture.Player.PlayerController>() != null)
            {
                TriggerFinalHuntSequence();
            }
        }

        public void TriggerFinalHuntSequence()
        {
            if (_isTriggered) return;
            _isTriggered = true;

            Debug.Log("[FinalHuntTrigger] Final hunt initiated! Ghost activity locked to maximum; timelines collapsing!");

            // 1. Force activity meter to max
            GhostActivityManager.Instance?.AddActivity(forcedActivityLevel);

            // 2. Start unstable era swap loop
            _unstableSwapRoutine = StartCoroutine(UnstableEraLoop());
        }

        private IEnumerator UnstableEraLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(eraSwapInterval);

                if (EraManager.Instance != null)
                {
                    TimelineEra nextEra = EraManager.Instance.CurrentEra == TimelineEra.Past
                        ? TimelineEra.Present
                        : TimelineEra.Past;

                    EraManager.Instance.SwapEra();
                    GhostActivityManager.Instance?.AddActivity(20f);
                }
            }
        }

        private void OnDisable()
        {
            if (_unstableSwapRoutine != null)
            {
                StopCoroutine(_unstableSwapRoutine);
                _unstableSwapRoutine = null;
            }
        }
    }
}
