// ============================================================
// NoiseEmitter.cs
// ------------------------------------------------------------
// WHY IT'S A SEPARATE FILE: The static event OnNoiseMade is
// the only way ghost AI scripts will know about player noise.
// Keeping it in its own class means PlayerController doesn't
// need to import ghost namespaces, and ghost AI scripts don't
// need a reference to PlayerController.  Decoupled = clean.
//
// HOW IT WORKS:
//   PlayerController calls NoiseEmitter.TryEmit() from Update.
//   TryEmit accumulates time and fires the static event only
//   once per emitInterval seconds, passing the current noise
//   radius.  A radius of 0 means "silent" and nothing is fired.
// ============================================================

using System;
using UnityEngine;

namespace TimeFracture.Player
{
    public class NoiseEmitter : MonoBehaviour
    {
        // --------------------------------------------------
        // Static event — anyone in the project can subscribe
        // without holding a reference to this component.
        // Usage: NoiseEmitter.OnNoiseMade += MyHandler;
        //        void MyHandler(Vector3 pos, float radius) { ... }
        // --------------------------------------------------
        public static event Action<Vector3, float> OnNoiseMade;

        // --------------------------------------------------
        // Static helper — lets ANY script (not just PlayerController)
        // fire the noise event.  World objects such as doors and
        // switches call this instead of finding the MonoBehaviour.
        //
        // Usage: NoiseEmitter.Emit(transform.position, 8f);
        // --------------------------------------------------
        public static void Emit(Vector3 worldPosition, float radius)
        {
            if (radius > 0f)
                OnNoiseMade?.Invoke(worldPosition, radius);
        }

        [Header("Noise Radii")]
        [Tooltip("Radius broadcast to ghosts while walking normally.")]
        [SerializeField] private float walkNoiseRadius = 5f;

        [Tooltip("Radius broadcast to ghosts while sprinting.")]
        [SerializeField] private float sprintNoiseRadius = 12f;

        [Tooltip("Radius broadcast to ghosts while crouching and moving.")]
        [SerializeField] private float crouchNoiseRadius = 1.5f;

        [Header("Emission Timing")]
        [Tooltip("How often (in seconds) the noise event is fired. " +
                 "Keeps the event from spamming every frame.")]
        [SerializeField] private float emitInterval = 0.25f;

        // Internal timer
        private float _timer;

        /// <summary>
        /// Call this every frame from PlayerController.
        /// Pass radius = 0 when the player is still (no event fired).
        /// </summary>
        public void TryEmit(Vector3 worldPosition, float radius)
        {
            // Accumulate time between calls
            _timer += Time.deltaTime;

            // Only fire the event on the interval, and only if making noise
            if (_timer >= emitInterval && radius > 0f)
            {
                _timer = 0f;
                // Invoke the static event — null check prevents crashes
                // when nobody is subscribed yet
                OnNoiseMade?.Invoke(worldPosition, radius);
            }

            // If radius dropped to 0 (player stopped), reset timer so the
            // next movement emits immediately rather than after a full interval
            if (radius <= 0f)
                _timer = emitInterval; // prime for next step
        }

        // --------------------------------------------------
        // Public read accessors so PlayerController can pass
        // the correct radius based on the current move state
        // --------------------------------------------------
        public float WalkRadius   => walkNoiseRadius;
        public float SprintRadius => sprintNoiseRadius;
        public float CrouchRadius => crouchNoiseRadius;
    }
}
