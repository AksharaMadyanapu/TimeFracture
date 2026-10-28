// ============================================================
// WorldState.cs
// ============================================================
// A static class that stores named boolean flags.
// Think of it as a simple, global game-state dictionary.
//
// WHO USES IT:
//   - Switch.cs  → calls SetFlag("UnlockDoor", true) when flipped.
//   - Door.cs    → subscribes to OnFlagChanged; unlocks itself when
//                  its requiredFlag is set.
//   - EraManager (Milestone 3) → can use flags to track era state.
//
// WHY STATIC:
//   Any script can call WorldState.SetFlag without needing a scene
//   reference.  This keeps systems decoupled.
//
// STUDENT NOTE — Dictionary<TKey, TValue>:
//   A Dictionary maps a "key" (string name) to a "value" (bool).
//   HasFlag("MyKey") returns true if the key exists AND is true.
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace TimeFracture.World
{
    public static class WorldState
    {
        // --------------------------------------------------
        // Internal flag store.
        // Key   = flag name (e.g. "LeverPulled", "DoorUnlocked")
        // Value = true / false
        // --------------------------------------------------
        private static readonly Dictionary<string, bool> _flags =
            new Dictionary<string, bool>();

        // --------------------------------------------------
        // Event fired whenever a flag changes.
        // Subscribers receive:  string flagName, bool newValue
        //
        // Usage:
        //   WorldState.OnFlagChanged += (name, value) => { ... };
        // --------------------------------------------------
        public static event Action<string, bool> OnFlagChanged;

        // --------------------------------------------------
        // SET: Creates or overwrites a flag.
        // --------------------------------------------------
        public static void SetFlag(string flagName, bool value)
        {
            if (string.IsNullOrEmpty(flagName))
            {
                Debug.LogWarning("[WorldState] SetFlag called with empty name.");
                return;
            }

            // Only fire the event if the value actually changed
            _flags.TryGetValue(flagName, out bool previous);

            _flags[flagName] = value;

            if (value != previous)
                OnFlagChanged?.Invoke(flagName, value);
        }

        // --------------------------------------------------
        // HAS: Returns true if the flag exists and is true.
        // --------------------------------------------------
        public static bool HasFlag(string flagName)
        {
            if (string.IsNullOrEmpty(flagName)) return false;
            return _flags.TryGetValue(flagName, out bool v) && v;
        }

        // --------------------------------------------------
        // CLEAR: Removes a flag from the dictionary entirely.
        // (Equivalent to calling SetFlag(name, false) but
        // also cleans up memory in long sessions.)
        // --------------------------------------------------
        public static void ClearFlag(string flagName)
        {
            if (_flags.Remove(flagName))
                OnFlagChanged?.Invoke(flagName, false);
        }

        // --------------------------------------------------
        // RESET ALL: call this when loading a new scene or
        // restarting the game so flags don't persist.
        // --------------------------------------------------
        public static void ResetAll()
        {
            // Copy keys so we can iterate while clearing
            var keys = new List<string>(_flags.Keys);
            _flags.Clear();
            foreach (var k in keys)
                OnFlagChanged?.Invoke(k, false);
        }

#if UNITY_EDITOR
        // --------------------------------------------------
        // Editor-only helper: print all flags to the Console.
        // Call it from another script with WorldState.DebugPrint().
        // --------------------------------------------------
        public static void DebugPrint()
        {
            if (_flags.Count == 0)
            {
                Debug.Log("[WorldState] No flags set.");
                return;
            }
            var sb = new System.Text.StringBuilder("[WorldState] Current flags:\n");
            foreach (var pair in _flags)
                sb.AppendLine($"  {pair.Key} = {pair.Value}");
            Debug.Log(sb.ToString());
        }
#endif
    }
}
