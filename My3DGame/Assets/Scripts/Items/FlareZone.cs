// ============================================================
// FlareZone.cs
// ------------------------------------------------------------
// Defines a safe/protected zone created by an active Flare.
// Maintains a static registry of all active zones so ghosts
// (e.g. The Residue, or future systems) can query whether any
// point in the world is currently shielded by protective light.
//
// STATIC API:
//   FlareZone.IsPointProtected(Vector3 point) -> bool
//   FlareZone.ActiveZones -> IReadOnlyList<FlareZone>
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace TimeFracture.Items
{
    public class FlareZone : MonoBehaviour
    {
        // ──────────────────────────────────────────────────
        // STATIC REGISTRY
        // ──────────────────────────────────────────────────

        private static readonly List<FlareZone> s_ActiveZones = new List<FlareZone>();

        /// <summary>
        /// Read-only collection of all currently active flare protection zones.
        /// </summary>
        public static IReadOnlyList<FlareZone> ActiveZones => s_ActiveZones;

        /// <summary>
        /// Checks whether the specified world position is inside the protection
        /// radius of ANY active flare zone in the scene.
        /// </summary>
        /// <param name="worldPosition">Point in world coordinates to test.</param>
        /// <returns>True if protected by at least one flare zone; otherwise false.</returns>
        public static bool IsPointProtected(Vector3 worldPosition)
        {
            for (int i = 0; i < s_ActiveZones.Count; i++)
            {
                FlareZone zone = s_ActiveZones[i];
                if (zone != null && zone.IsActive && zone.Contains(worldPosition))
                {
                    return true;
                }
            }
            return false;
        }

        // ──────────────────────────────────────────────────
        // INSPECTOR FIELDS
        // ──────────────────────────────────────────────────

        [Header("Protection Parameters")]
        [Tooltip("Radius (in metres) around this flare that provides protection.")]
        [SerializeField] private float protectionRadius = 6.0f;

        // ──────────────────────────────────────────────────
        // PROPERTIES
        // ──────────────────────────────────────────────────

        public float ProtectionRadius
        {
            get => protectionRadius;
            set => protectionRadius = value;
        }

        public bool IsActive => enabled && gameObject.activeInHierarchy;

        // ──────────────────────────────────────────────────
        // UNITY LIFECYCLE
        // ──────────────────────────────────────────────────

        private void OnEnable()
        {
            if (!s_ActiveZones.Contains(this))
            {
                s_ActiveZones.Add(this);
            }
        }

        private void OnDisable()
        {
            s_ActiveZones.Remove(this);
        }

        private void OnDestroy()
        {
            s_ActiveZones.Remove(this);
        }

        // ──────────────────────────────────────────────────
        // PUBLIC METHODS
        // ──────────────────────────────────────────────────

        /// <summary>
        /// Tests if a specific world position falls within this flare's protection radius.
        /// </summary>
        public bool Contains(Vector3 worldPosition)
        {
            float distanceSqr = (transform.position - worldPosition).sqrMagnitude;
            return distanceSqr <= (protectionRadius * protectionRadius);
        }

        // ──────────────────────────────────────────────────
        // GIZMOS (VISUAL DEBUGGING)
        // ──────────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.7f, 0.1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, protectionRadius);
        }
    }
}
