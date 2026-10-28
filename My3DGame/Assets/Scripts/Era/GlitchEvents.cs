// ============================================================
// GlitchEvents.cs
// ------------------------------------------------------------
// Decoupled static event channel for glitch and temporal distortions.
//
// WHO USES IT:
//   - GlitchEffect.cs fires OnGlitch when an era swap occurs.
//   - Future Ghost AI / Activity Meter (Milestone 5 & 6) will
//     subscribe to this to trigger ghost agitation, light flickers,
//     and hunt behavior without needing direct references.
//
// WHY STATIC:
//   Zero coupling. Any system can trigger or listen to glitch
//   events without holding scene references.
// ============================================================

using System;
using UnityEngine;

namespace TimeFracture.Era
{
    public static class GlitchEvents
    {
        /// <summary>
        /// Fired whenever a timeline glitch or temporal shift occurs.
        /// Float parameter represents intensity (0.0 to 1.0).
        /// </summary>
        public static event Action<float> OnGlitch;

        /// <summary>
        /// Fires the OnGlitch event with the specified intensity.
        /// </summary>
        /// <param name="intensity">Intensity normalized between 0.0 and 1.0.</param>
        public static void FireGlitch(float intensity)
        {
            intensity = Mathf.Clamp01(intensity);
            OnGlitch?.Invoke(intensity);
        }
    }
}
