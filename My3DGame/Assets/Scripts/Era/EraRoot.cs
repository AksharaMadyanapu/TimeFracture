// ============================================================
// EraRoot.cs
// ------------------------------------------------------------
// Attach this to the root parent GameObject of an era-specific layout.
//
// EXAMPLE HIERARCHY:
//   - LabRoom_Past    [EraRoot: Past]
//       ├── CleanWalls
//       ├── WorkingMachines
//       └── IntactLights
//   - LabRoom_Present [EraRoot: Present]
//       ├── RottedWalls
//       ├── DecayedRubble
//       └── BrokenLights
//
// HOW IT WORKS:
//   EraRoot registers with EraManager on Awake.
//   When EraManager switches eras, it calls SetActiveForEra(),
//   toggling gameObject.SetActive(true/false) based on whether
//   its assignedEra matches the active era.
// ============================================================

using UnityEngine;

namespace TimeFracture.Era
{
    [DisallowMultipleComponent]
    public class EraRoot : MonoBehaviour
    {
        [Header("Era Assignment")]
        [Tooltip("The era in which this room layout or object hierarchy exists.")]
        [SerializeField] private Era assignedEra = Era.Past;

        /// <summary>The era this root is designated for.</summary>
        public Era AssignedEra => assignedEra;

        private void Awake()
        {
            // Register with EraManager
            EraManager.RegisterRoot(this);
        }

        private void OnDestroy()
        {
            // Clean up registration if destroyed
            EraManager.UnregisterRoot(this);
        }

        /// <summary>
        /// Called by EraManager to activate or deactivate this hierarchy.
        /// </summary>
        public void SetActiveForEra(Era activeEra)
        {
            bool shouldBeActive = (assignedEra == activeEra);

            if (gameObject.activeSelf != shouldBeActive)
            {
                gameObject.SetActive(shouldBeActive);
            }
        }
    }
}
