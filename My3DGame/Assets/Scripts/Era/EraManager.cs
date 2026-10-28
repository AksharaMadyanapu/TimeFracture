// ============================================================
// EraManager.cs
// ------------------------------------------------------------
// Central controller for the timeline system (Past / Present).
//
// RESPONSIBILITIES:
//   1. Tracks current era (Past or Present).
//   2. Fires static event OnEraChanged so audio, lighting, and
//      ghost systems react immediately.
//   3. Controls active EraRoot GameObjects across the facility.
//   4. Emits noise so ghosts in hearing range react to timeline tears.
//
// STUDENT NOTE — Singletons & Static Events:
//   - EraManager.Instance allows easy script access for SwapEra().
//   - EraManager.OnEraChanged is a C# static event. Any script can
//     subscribe without having an inspector reference to EraManager.
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using TimeFracture.Player;

namespace TimeFracture.Era
{
    public enum Era
    {
        Past,
        Present
    }

    public class EraManager : MonoBehaviour
    {
        // ──────────────────────────────────────────────────
        // SINGLETON INSTANCE & STATIC EVENT
        // ──────────────────────────────────────────────────

        public static EraManager Instance { get; private set; }

        /// <summary>
        /// Fired whenever the timeline shifts between Past and Present.
        /// Any system (lighting, ghosts, UI, audio) can subscribe to this.
        /// </summary>
        public static event Action<Era> OnEraChanged;

        // ──────────────────────────────────────────────────
        // INSPECTOR FIELDS
        // ──────────────────────────────────────────────────

        [Header("Starting State")]
        [Tooltip("The era active when the scene begins.")]
        [SerializeField] private Era initialEra = Era.Past;

        [Header("Audio & Noise")]
        [Tooltip("Radius of noise broadcast to ghosts when an era swap occurs.")]
        [SerializeField] private float swapNoiseRadius = 15f;

        [Header("Testing & Debug")]
        [Tooltip("When true, pressing the debug key instantly triggers an era swap.")]
        [SerializeField] private bool enableDebugKey = true;

        [Tooltip("Key to trigger an era swap during play testing.")]
        [SerializeField] private KeyCode debugKey = KeyCode.T;

        // ──────────────────────────────────────────────────
        // RUNTIME STATE
        // ──────────────────────────────────────────────────

        private Era _currentEra;

        /// <summary>The currently active timeline era.</summary>
        public Era CurrentEra => _currentEra;

        // Registered EraRoot objects (Past / Present room versions)
        private static readonly List<EraRoot> _registeredRoots = new List<EraRoot>();

        // ──────────────────────────────────────────────────
        // ROOT REGISTRATION
        // ──────────────────────────────────────────────────

        /// <summary>
        /// Registers an EraRoot to be controlled by the timeline manager.
        /// Called automatically in EraRoot.Awake().
        /// </summary>
        public static void RegisterRoot(EraRoot root)
        {
            if (root != null && !_registeredRoots.Contains(root))
            {
                _registeredRoots.Add(root);

                // If EraManager is already alive, immediately set initial visibility
                if (Instance != null)
                {
                    root.SetActiveForEra(Instance.CurrentEra);
                }
            }
        }

        /// <summary>
        /// Unregisters an EraRoot when destroyed or unloaded.
        /// </summary>
        public static void UnregisterRoot(EraRoot root)
        {
            if (root != null)
            {
                _registeredRoots.Remove(root);
            }
        }

        // ──────────────────────────────────────────────────
        // UNITY LIFECYCLE
        // ──────────────────────────────────────────────────

        private void Awake()
        {
            // Singleton pattern enforcement
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _currentEra = initialEra;

            // Pre-discover any EraRoots present in the scene, including inactive ones
            EraRoot[] sceneRoots = FindObjectsByType<EraRoot>(FindObjectsInactive.Include);
            foreach (var root in sceneRoots)
            {
                RegisterRoot(root);
            }
        }

        private void Start()
        {
            // Initialize room activation state for the initial era
            UpdateRoots();
        }

        private void Update()
        {
            // Quick testing shortcut
            if (enableDebugKey && Input.GetKeyDown(debugKey))
            {
                SwapEra();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                _registeredRoots.Clear();
            }
        }

        // ──────────────────────────────────────────────────
        // CORE ERA SWAPPING
        // ──────────────────────────────────────────────────

        /// <summary>
        /// Toggles between Past and Present, activates matching EraRoots,
        /// broadcasts OnEraChanged, and emits a loud noise event for ghosts.
        /// </summary>
        public void SwapEra()
        {
            _currentEra = (_currentEra == Era.Past) ? Era.Present : Era.Past;

            // Update room active states
            UpdateRoots();

            // Notify all subscribers (GlitchEffect, Lighting, Ghost Spawners, UI)
            OnEraChanged?.Invoke(_currentEra);

            // Emit noise so nearby ghosts react to the timeline rupture
            NoiseEmitter.Emit(transform.position, swapNoiseRadius);

            Debug.Log($"[EraManager] Timeline shifted to: {_currentEra}");
        }

        /// <summary>
        /// Sets a specific era explicitly (e.g. for narrative scripted triggers).
        /// </summary>
        public void SetEra(Era targetEra)
        {
            if (_currentEra == targetEra) return;

            _currentEra = targetEra;
            UpdateRoots();
            OnEraChanged?.Invoke(_currentEra);
            NoiseEmitter.Emit(transform.position, swapNoiseRadius);
        }

        /// <summary>
        /// Activates only the EraRoots matching the current era.
        /// </summary>
        public void UpdateRoots()
        {
            // Clean up any destroyed references
            _registeredRoots.RemoveAll(r => r == null);

            for (int i = 0; i < _registeredRoots.Count; i++)
            {
                _registeredRoots[i].SetActiveForEra(_currentEra);
            }
        }
    }
}
