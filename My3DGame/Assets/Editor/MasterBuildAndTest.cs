// ============================================================
// MasterBuildAndTest.cs
// ------------------------------------------------------------
// Master menu command runner for Time Fracture:
//   Tools > Setup > Build Everything
//     Runs all milestone setups (5, 6, 7, 8, 9, 10) in sequential order,
//     rebakes the NavMesh, and saves the open scene.
//
//   Tools > Test > Run All
//     Runs every milestone test suite, records results, and prints:
//     "ALL PASSED" or "N FAILED: <names>".
// ============================================================

#pragma warning disable CS0618

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace TimeFracture.Editor
{
    public static class MasterBuildAndTest
    {
        // ──────────────────────────────────────────────────
        // 1. MASTER SETUP: BUILD EVERYTHING
        // ──────────────────────────────────────────────────

        [MenuItem("Tools/Setup/Build Everything", false, 0)]
        public static void BuildEverything()
        {
            Debug.Log("======================================================================");
            Debug.Log(">>> [MASTER SETUP] BUILDING EVERYTHING (MILESTONES 5, 6, 7, 8, 9, 10)...");
            Debug.Log("======================================================================");

            try
            {
                // Ensure TestScene is open
                if (System.IO.File.Exists("Assets/Scenes/TestScene.unity"))
                {
                    EditorSceneManager.OpenScene("Assets/Scenes/TestScene.unity");
                }

                // 1. Milestone 5: The Echo & NavMesh
                Debug.Log("[Master Setup] (1/6) Running Milestone 5 Setup...");
                Milestone5Setup.SetupMilestone5();

                // 2. Milestone 6: Activity Meter & Sabotage
                Debug.Log("[Master Setup] (2/6) Running Milestone 6 Setup...");
                Milestone6Setup.SetupMilestone6();

                // 3. Milestone 7: Inventory & Protective Items
                Debug.Log("[Master Setup] (3/6) Running Milestone 7 Setup...");
                Milestone7Setup.SetupMilestone7();

                // 4. Milestone 8: Audio Logs & Lore Pickups
                Debug.Log("[Master Setup] (4/6) Running Milestone 8 Setup...");
                Milestone8Setup.SetupMilestone8();

                // 5. Milestone 9: The Residue & Story Endings
                Debug.Log("[Master Setup] (5/6) Running Milestone 9 Setup...");
                Milestone9Setup.SetupMilestone9();

                // 6. Milestone 10: Polish, Visuals, and Build Configuration
                Debug.Log("[Master Setup] (6/6) Running Milestone 10 Setup...");
                Milestone10Setup.SetupMilestone10();

                // Final save
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
                AssetDatabase.SaveAssets();

                Debug.Log("======================================================================");
                Debug.Log(">>> [MASTER SETUP] ALL MILESTONE SETUPS COMPLETED & SCENE SAVED! <<<");
                Debug.Log("======================================================================");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MASTER SETUP ERROR] Failed during Build Everything: {ex.Message}\n{ex.StackTrace}");
            }
        }

        // ──────────────────────────────────────────────────
        // 2. MASTER TEST: RUN ALL
        // ──────────────────────────────────────────────────

        [MenuItem("Tools/Test/Run All", false, 0)]
        public static void RunAllTests()
        {
            Debug.Log("======================================================================");
            Debug.Log(">>> [MASTER TEST] EXECUTING ALL MILESTONE TEST SUITES (5 TO 10)... <<<");
            Debug.Log("======================================================================");

            List<string> passedTests = new List<string>();
            List<string> failedTests = new List<string>();

            RunSuite("Milestone 5", Milestone5Setup.TestMilestone5, passedTests, failedTests);
            RunSuite("Milestone 6", Milestone6Setup.TestMilestone6, passedTests, failedTests);
            RunSuite("Milestone 7", Milestone7Setup.TestMilestone7, passedTests, failedTests);
            RunSuite("Milestone 8", Milestone8Setup.TestMilestone8, passedTests, failedTests);
            RunSuite("Milestone 9", Milestone9Setup.TestMilestone9, passedTests, failedTests);
            RunSuite("Milestone 10", Milestone10Setup.TestMilestone10, passedTests, failedTests);

            Debug.Log("======================================================================");
            if (failedTests.Count == 0)
            {
                Debug.Log(">>> ALL PASSED <<<");
                Debug.Log($"All {passedTests.Count} test suites passed with zero errors.");
            }
            else
            {
                Debug.LogError($">>> {failedTests.Count} FAILED: {string.Join(", ", failedTests)} <<<");
            }
            Debug.Log("======================================================================");
        }

        private static void RunSuite(string name, Action testAction, List<string> passed, List<string> failed)
        {
            try
            {
                Debug.Log($"--- Running {name} Tests ---");
                testAction();
                passed.Add(name);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TEST SUITE FAILURE] {name} threw an unhandled exception: {ex.Message}");
                failed.Add(name);
            }
        }

        // Batchmode runner for CI/Headless invocation
        public static void RunBatch()
        {
            BuildEverything();
            RunAllTests();
        }
    }
}
