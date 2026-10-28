// ============================================================
// DebugEndingShortcuts.cs
// ------------------------------------------------------------
// Runtime debug helper:
// Pressing [F3] cycles granting audio logs (0 -> 2 -> 4 -> 6)
// through WorldState flags so all three endings can be tested quickly.
// ============================================================

using UnityEngine;
using TimeFracture.World;

namespace TimeFracture.Ending
{
    public class DebugEndingShortcuts : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInit()
        {
            if (FindFirstObjectByType<DebugEndingShortcuts>() == null)
            {
                GameObject obj = new GameObject("DebugEndingShortcuts");
                obj.AddComponent<DebugEndingShortcuts>();
                DontDestroyOnLoad(obj);
            }
        }

        private int _grantedTier = 0;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F3))
            {
                CycleLogsShortcut();
            }
        }

        public void CycleLogsShortcut()
        {
            _grantedTier = (_grantedTier + 1) % 4;
            int logsToGrant = _grantedTier switch
            {
                1 => 2, // Grants Ending A
                2 => 4, // Grants Ending B
                3 => 6, // Grants All
                _ => 0  // Reset
            };

            string[] flags = { "AudioLog_01", "AudioLog_02", "AudioLog_03", "AudioLog_04", "AudioLog_05", "AudioLog_06" };

            for (int i = 0; i < flags.Length; i++)
            {
                WorldState.SetFlag(flags[i], i < logsToGrant);
            }

            Debug.Log($"[DebugEndingShortcuts] [F3] Toggled Audio Logs: Granted {logsToGrant}/6 logs. Available Endings: " +
                      (logsToGrant >= 4 ? "A, B, C (All unlocked)" : logsToGrant >= 2 ? "A, C" : "C only"));
        }
    }
}
