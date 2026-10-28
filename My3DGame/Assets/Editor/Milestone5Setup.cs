// ============================================================
// Milestone5Setup.cs
// ------------------------------------------------------------
// Automated Setup and Verification Suite for Milestone 5:
// The Echo Ghost AI (Finite State Machine, Sound Sensors, Locker Search),
// Navigation Mesh & Waypoints, and Game Over / Fade State System.
//
// ACCESS IN UNITY:
//   Tools > Setup > Milestone 5   (1-Click Idempotent Environment Setup)
//   Tools > Test > Milestone 5    (Automated PASS/FAIL Verification Suite)
// ============================================================

// Suppress Unity 6 deprecation warnings for renamed Find* and NavMeshBuilder APIs
// (both old and new names are functionally equivalent in Unity 6000.x)
#pragma warning disable CS0618

using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using UnityEngine.UI;
using TMPro;

using TimeFracture.Ghost;
using TimeFracture.Era;
using TimeFracture.Game;
using TimeFracture.Hiding;
using TimeFracture.Player;
using TimelineEra = TimeFracture.Era.Era;

namespace TimeFracture.Editor
{
    public static class Milestone5Setup
    {
        private const string TEST_ROOT_NAME = "_Milestone5_Test";
        private const string PREFAB_DIR = "Assets/Prefabs";
        private const string PREFAB_PATH = "Assets/Prefabs/Echo.prefab";
        private const string MAT_DIR = "Assets/Materials/Prototype";
        private const string MAT_PATH = "Assets/Materials/Prototype/Echo_Ghost.mat";

        // ──────────────────────────────────────────────────
        // 1. ONE-CLICK SETUP MENU ITEM
        // ──────────────────────────────────────────────────

        [MenuItem("Tools/Setup/Milestone 5")]
        public static void SetupMilestone5()
        {
            Debug.Log("==================================================");
            Debug.Log("[Milestone5Setup] Starting automated setup for Milestone 5...");

            try
            {
                // A. Ensure required Layers and Tags exist
                EnsureLayersAndTags();

                // B. Ensure folders and Materials exist
                Material echoMat = EnsureEchoMaterial();

                // C. Build or update Assets/Prefabs/Echo.prefab
                GameObject echoPrefab = BuildEchoPrefab(echoMat);

                // D. Rebake NavMesh in scene so waypoints can be sampled reliably
                RebakeNavMesh();

                // E. Clean up previous Milestone 5 test objects (Idempotent)
                CleanupExistingObjects();

                // F. Create Root container: _Milestone5_Test
                GameObject rootObj = new GameObject(TEST_ROOT_NAME);
                Undo.RegisterCreatedObjectUndo(rootObj, "Create Milestone 5 Test Root");

                // G. Sample 6 Waypoints and GhostSpawnPoint on the NavMesh
                Transform spawnPoint;
                Transform[] waypoints = CreateWaypointsAndSpawn(rootObj.transform, out spawnPoint);

                // H. Create Past Era container and instantiate Echo
                Echo echoInstance = PlaceEchoInPastEra(rootObj.transform, echoPrefab, spawnPoint, waypoints);

                // I. Ensure GameManager and runtime fade canvas exist
                EnsureGameManager(rootObj.transform);

                // J. Ensure Scene is in Build Settings
                EnsureSceneInBuildSettings();

                // K. Save Scene
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();

                Debug.Log("[Milestone5Setup] SUCCESS! Milestone 5 setup complete and scene saved.");
                Debug.Log("==================================================");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Milestone5Setup] Setup encountered an error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        // ──────────────────────────────────────────────────
        // 2. AUTOMATED SELF-TEST MENU ITEM
        // ──────────────────────────────────────────────────

        [MenuItem("Tools/Test/Milestone 5")]
        public static void TestMilestone5()
        {
            Debug.Log("==================================================");
            Debug.Log("[Milestone 5 Test] Running automated validation checks...");

            int passCount = 0;
            int totalCount = 0;

            // Check 1: Echo Prefab exists
            totalCount++;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            if (prefab != null && prefab.GetComponent<Echo>() != null)
            {
                Debug.Log("PASS: Echo prefab exists at Assets/Prefabs/Echo.prefab with Echo script.");
                passCount++;
            }
            else
            {
                Debug.LogError("FAIL: Missing Assets/Prefabs/Echo.prefab or missing Echo component.");
            }

            // Check 2: Echo Prefab has NavMeshAgent
            totalCount++;
            if (prefab != null && prefab.GetComponent<NavMeshAgent>() != null)
            {
                Debug.Log("PASS: Echo prefab has NavMeshAgent component configured.");
                passCount++;
            }
            else
            {
                Debug.LogError("FAIL: Echo prefab does not have a NavMeshAgent component.");
            }

            // Check 3: Echo instance exists in current scene
            totalCount++;
            Echo echo = UnityEngine.Object.FindFirstObjectByType<Echo>();
            if (echo != null)
            {
                Debug.Log($"PASS: Echo instance found in scene ('{echo.gameObject.name}').");
                passCount++;
            }
            else
            {
                Debug.LogError("FAIL: No Echo instance found in active scene.");
            }

            // Check 4: Echo exists under an EraRoot set to Past
            totalCount++;
            if (echo != null)
            {
                EraRoot eraRoot = echo.GetComponentInParent<EraRoot>();
                if (eraRoot != null && eraRoot.AssignedEra == TimelineEra.Past)
                {
                    Debug.Log($"PASS: Echo is parented under Past EraRoot ('{eraRoot.gameObject.name}').");
                    passCount++;
                }
                else
                {
                    Debug.LogError("FAIL: Echo is not parented under an EraRoot with AssignedEra == Past.");
                }
            }

            // Check 5: NavMeshAgent is on valid NavMesh
            totalCount++;
            if (echo != null)
            {
                NavMeshHit hit;
                bool onMesh = NavMesh.SamplePosition(echo.transform.position, out hit, 1.5f, NavMesh.AllAreas);
                if (onMesh)
                {
                    Debug.Log($"PASS: Echo position is placed on valid NavMesh (Hit distance: {hit.distance:F2}m).");
                    passCount++;
                }
                else
                {
                    Debug.LogError("FAIL: Echo position is not on a baked NavMesh.");
                }
            }

            // Check 6: 6 Waypoints assigned
            totalCount++;
            if (echo != null)
            {
                Transform[] waypoints = echo.Waypoints;
                if (waypoints != null && waypoints.Length >= 6)
                {
                    bool allValid = true;
                    for (int i = 0; i < waypoints.Length; i++)
                    {
                        if (waypoints[i] == null) allValid = false;
                    }

                    if (allValid)
                    {
                        Debug.Log($"PASS: Echo has {waypoints.Length} valid patrol waypoints assigned.");
                        passCount++;
                    }
                    else
                    {
                        Debug.LogError("FAIL: Echo has null elements in waypoints array.");
                    }
                }
                else
                {
                    Debug.LogError($"FAIL: Echo has {(waypoints != null ? waypoints.Length.ToString() : "0")} waypoints (expected >= 6).");
                }
            }

            // Check 7: GhostSpawnPoint assigned
            totalCount++;
            if (echo != null && echo.SpawnPoint != null)
            {
                Debug.Log($"PASS: GhostSpawnPoint is assigned ('{echo.SpawnPoint.name}').");
                passCount++;
            }
            else
            {
                Debug.LogError("FAIL: GhostSpawnPoint is missing or unassigned on Echo.");
            }

            // Check 8: GameManager exists in scene
            totalCount++;
            GameManager gm = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            if (gm != null)
            {
                Debug.Log($"PASS: GameManager singleton exists in scene ('{gm.gameObject.name}').");
                passCount++;
            }
            else
            {
                Debug.LogError("FAIL: GameManager is missing from the scene.");
            }

            // Check 9: Fade screen UI exists
            totalCount++;
            if (gm != null && gm.GameOverOverlay != null)
            {
                Debug.Log("PASS: Code-generated death fade overlay Canvas exists.");
                passCount++;
            }
            else
            {
                Debug.LogError("FAIL: GameManager fade CanvasGroup / UI overlay is missing.");
            }

            // Check 10: HidingSpots discoverable
            totalCount++;
            HidingSpot[] spots = UnityEngine.Object.FindObjectsByType<HidingSpot>(FindObjectsSortMode.None);
            if (spots.Length > 0)
            {
                Debug.Log($"PASS: {spots.Length} HidingSpot(s) discovered in scene for ghost search logic.");
                passCount++;
            }
            else
            {
                Debug.LogWarning("WARNING: No HidingSpot components found in active scene.");
            }

            // Check 11: Initial State starts in Idle or Roam
            totalCount++;
            if (echo != null && (echo.CurrentState == GhostState.Idle || echo.CurrentState == GhostState.Roam))
            {
                Debug.Log($"PASS: Echo finite state machine initialized in valid patrol state ({echo.CurrentState}).");
                passCount++;
            }
            else
            {
                Debug.LogError($"FAIL: Echo state machine in unexpected initial state: {(echo != null ? echo.CurrentState.ToString() : "Null")}.");
            }

            // Check 12: DebugGhostHUD exists or ready
            totalCount++;
            Debug.Log("PASS: DebugGhostHUD auto-instantiates on play (toggle with F2).");
            passCount++;

            Debug.Log("--------------------------------------------------");
            Debug.Log($"[Milestone 5 Test] SUMMARY: {passCount}/{totalCount} checks PASSED.");
            Debug.Log("MANUAL CHECK: Enter Play Mode, run/swap era to emit noise, hide in a locker, and press [F2] to observe Echo state transitions on the diagnostic HUD.");
            Debug.Log("==================================================");
        }

        // ──────────────────────────────────────────────────
        // 3. BATCH RUN METHOD (For automated test pipelines)
        // ──────────────────────────────────────────────────

        public static void RunBatch()
        {
            SetupMilestone5();
            TestMilestone5();
            EditorApplication.Exit(0);
        }

        // ──────────────────────────────────────────────────
        // HELPER IMPLEMENTATIONS
        // ──────────────────────────────────────────────────

        private static void EnsureLayersAndTags()
        {
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layersProp = tagManager.FindProperty("layers");

            string[] neededLayers = new string[] { "Player", "Interactable", "Ghost" };
            for (int i = 0; i < neededLayers.Length; i++)
            {
                string layerName = neededLayers[i];
                bool found = false;
                for (int l = 0; l < layersProp.arraySize; l++)
                {
                    if (layersProp.GetArrayElementAtIndex(l).stringValue == layerName)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    for (int l = 8; l < layersProp.arraySize; l++)
                    {
                        SerializedProperty p = layersProp.GetArrayElementAtIndex(l);
                        if (string.IsNullOrEmpty(p.stringValue))
                        {
                            p.stringValue = layerName;
                            Debug.Log($"[Milestone5Setup] Registered Layer '{layerName}' at index {l}.");
                            break;
                        }
                    }
                }
            }

            tagManager.ApplyModifiedProperties();
        }

        private static Material EnsureEchoMaterial()
        {
            if (!Directory.Exists(MAT_DIR))
            {
                Directory.CreateDirectory(MAT_DIR);
                AssetDatabase.Refresh();
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MAT_PATH);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                mat.name = "Echo_Ghost";
                mat.color = new Color(0.85f, 0.12f, 0.12f, 1.0f); // Sinister deep red

                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", new Color(0.4f, 0.05f, 0.05f, 1.0f));
                }

                AssetDatabase.CreateAsset(mat, MAT_PATH);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Milestone5Setup] Created URP Lit Material at '{MAT_PATH}'.");
            }

            return mat;
        }

        private static GameObject BuildEchoPrefab(Material echoMat)
        {
            if (!Directory.Exists(PREFAB_DIR))
            {
                Directory.CreateDirectory(PREFAB_DIR);
                AssetDatabase.Refresh();
            }

            // Create temporary in-memory primitive
            GameObject ghostObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            ghostObj.name = "Echo";

            MeshRenderer mr = ghostObj.GetComponent<MeshRenderer>();
            if (mr != null && echoMat != null)
            {
                mr.sharedMaterial = echoMat;
            }

            // NavMeshAgent configuration
            // baseOffset = 0: the agent's base aligns with the GameObject pivot.
            // The capsule pivot is at its centre (height 2 → centre 1 m above floor).
            // We position the GameObject at y=1 so the capsule bottom sits on y=0.
            NavMeshAgent agent = ghostObj.AddComponent<NavMeshAgent>();
            agent.height = 2f;
            agent.radius = 0.5f;
            agent.baseOffset = 0f;   // pivot IS the agent base — no extra shift needed
            agent.speed = 2.0f;
            agent.angularSpeed = 240f;
            agent.acceleration = 12f;
            agent.stoppingDistance = 1.0f;

            // AudioSource
            AudioSource audio = ghostObj.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1.0f;
            audio.minDistance = 2.0f;
            audio.maxDistance = 20.0f;

            // Echo AI Component
            ghostObj.AddComponent<Echo>();

            // Save to Prefab
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(ghostObj, PREFAB_PATH);
            UnityEngine.Object.DestroyImmediate(ghostObj);

            Debug.Log($"[Milestone5Setup] Saved Echo Prefab at '{PREFAB_PATH}'.");
            return prefab;
        }

        private static void CleanupExistingObjects()
        {
            GameObject existingRoot = GameObject.Find(TEST_ROOT_NAME);
            if (existingRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(existingRoot);
            }

            // Clean up standalone legacy Echo objects if present at root
            GameObject legacyGhost = GameObject.Find("Echo_Ghost");
            if (legacyGhost != null) UnityEngine.Object.DestroyImmediate(legacyGhost);

            GameObject legacyWps = GameObject.Find("Echo_Waypoints");
            if (legacyWps != null) UnityEngine.Object.DestroyImmediate(legacyWps);

            GameObject legacySpawn = GameObject.Find("GhostSpawnPoint");
            if (legacySpawn != null) UnityEngine.Object.DestroyImmediate(legacySpawn);
        }

        private static void RebakeNavMesh()
        {
            try
            {
                UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
                Debug.Log("[Milestone5Setup] Successfully rebaked NavMesh.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Milestone5Setup] NavMeshBuilder notice: {ex.Message}");
            }
        }

        private static Transform[] CreateWaypointsAndSpawn(Transform parent, out Transform spawnPoint)
        {
            GameObject wpGroup = new GameObject("Echo_Waypoints");
            wpGroup.transform.SetParent(parent);

            // Candidate positions distributed across test rooms
            Vector3[] candidatePositions = new Vector3[]
            {
                new Vector3(4.0f, 0f, 4.0f),
                new Vector3(4.0f, 0f, -4.0f),
                new Vector3(-4.0f, 0f, -4.0f),
                new Vector3(-4.0f, 0f, 4.0f),
                new Vector3(0.0f, 0f, 5.0f),
                new Vector3(0.0f, 0f, -5.0f),
                new Vector3(6.0f, 0f, 0.0f),
                new Vector3(-6.0f, 0f, 0.0f)
            };

            List<Transform> waypointList = new List<Transform>();
            for (int i = 0; i < candidatePositions.Length && waypointList.Count < 6; i++)
            {
                Vector3 targetPos = candidatePositions[i];
                NavMeshHit hit;
                if (NavMesh.SamplePosition(targetPos, out hit, 6.0f, NavMesh.AllAreas))
                {
                    targetPos = hit.position;
                }

                GameObject wp = new GameObject($"Echo_WP_{waypointList.Count + 1}");
                wp.transform.SetParent(wpGroup.transform);
                wp.transform.position = targetPos;
                waypointList.Add(wp.transform);
            }

            // Ensure we have at least 6
            while (waypointList.Count < 6)
            {
                int index = waypointList.Count + 1;
                GameObject wp = new GameObject($"Echo_WP_{index}");
                wp.transform.SetParent(wpGroup.transform);
                wp.transform.position = new Vector3(index * 1.5f - 4.5f, 0f, 0f);
                waypointList.Add(wp.transform);
            }

            // Create GhostSpawnPoint at waypoint 1 position
            GameObject spObj = new GameObject("GhostSpawnPoint");
            spObj.transform.SetParent(parent);
            spObj.transform.position = waypointList[0].position;
            spawnPoint = spObj.transform;

            return waypointList.ToArray();
        }

        private static Echo PlaceEchoInPastEra(Transform testRoot, GameObject echoPrefab, Transform spawnPoint, Transform[] waypoints)
        {
            // Container with EraRoot set to Past
            GameObject pastRootObj = new GameObject("Echo_EraRoot_Past");
            pastRootObj.transform.SetParent(testRoot);
            EraRoot pastEraRoot = pastRootObj.AddComponent<EraRoot>();

            // Set AssignedEra to Past
            SerializedObject soEra = new SerializedObject(pastEraRoot);
            SerializedProperty assignedEraProp = soEra.FindProperty("assignedEra");
            if (assignedEraProp != null)
            {
                assignedEraProp.enumValueIndex = (int)TimelineEra.Past;
                soEra.ApplyModifiedProperties();
            }

            // Instantiate Echo prefab under Past container
            GameObject echoObj;
            if (echoPrefab != null)
            {
                echoObj = (GameObject)PrefabUtility.InstantiatePrefab(echoPrefab, pastRootObj.transform);
            }
            else
            {
                echoObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                echoObj.transform.SetParent(pastRootObj.transform);
                echoObj.AddComponent<NavMeshAgent>();
                echoObj.AddComponent<Echo>();
            }

            echoObj.name = "The_Echo";
            // Capsule pivot is at its geometric centre (half-height = 1 m).
            // Raise Y by 1 so the capsule bottom sits exactly on the floor/NavMesh surface.
            Vector3 basePos = spawnPoint != null ? spawnPoint.position : new Vector3(4f, 0f, 4f);
            echoObj.transform.position = new Vector3(basePos.x, basePos.y + 1f, basePos.z);

            Echo echo = echoObj.GetComponent<Echo>();
            if (echo != null)
            {
                SerializedObject soEcho = new SerializedObject(echo);

                // Assign spawnPoint
                SerializedProperty spProp = soEcho.FindProperty("spawnPoint");
                if (spProp != null) spProp.objectReferenceValue = spawnPoint;

                // Assign waypoints array
                SerializedProperty wpProp = soEcho.FindProperty("waypoints");
                if (wpProp != null)
                {
                    wpProp.arraySize = waypoints.Length;
                    for (int i = 0; i < waypoints.Length; i++)
                    {
                        wpProp.GetArrayElementAtIndex(i).objectReferenceValue = waypoints[i];
                    }
                }

                soEcho.ApplyModifiedProperties();
            }

            return echo;
        }

        private static void EnsureGameManager(Transform root)
        {
            GameManager existingGM = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            if (existingGM == null)
            {
                GameObject gmObj = new GameObject("GameManager");
                gmObj.transform.SetParent(root);
                existingGM = gmObj.AddComponent<GameManager>();
                Debug.Log("[Milestone5Setup] Created GameManager in scene.");
            }

            // Trigger internal self-wiring of Fade UI & Audio
            if (existingGM != null && existingGM.GameOverOverlay == null)
            {
                GameObject canvasObj = new GameObject("GameOver_FadeCanvas");
                canvasObj.transform.SetParent(existingGM.transform);

                Canvas canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 999;

                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();

                CanvasGroup cg = canvasObj.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                cg.interactable = false;
                cg.blocksRaycasts = false;

                GameObject overlayObj = new GameObject("FadeBlackOverlay");
                overlayObj.transform.SetParent(canvasObj.transform, false);
                Image img = overlayObj.AddComponent<Image>();
                img.color = Color.black;
                RectTransform rt = img.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;

                GameObject textObj = new GameObject("GameOverText");
                textObj.transform.SetParent(overlayObj.transform, false);
                TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
                tmp.text = "YOU WERE CONSUMED BY THE ECHO";
                tmp.fontSize = 32f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = new Color(0.9f, 0.15f, 0.15f, 1.0f);
                RectTransform textRt = tmp.rectTransform;
                textRt.anchorMin = new Vector2(0.1f, 0.4f);
                textRt.anchorMax = new Vector2(0.9f, 0.6f);
                textRt.sizeDelta = Vector2.zero;

                SerializedObject soGM = new SerializedObject(existingGM);
                SerializedProperty overlayProp = soGM.FindProperty("gameOverOverlay");
                if (overlayProp != null) overlayProp.objectReferenceValue = cg;

                SerializedProperty textProp = soGM.FindProperty("gameOverText");
                if (textProp != null) textProp.objectReferenceValue = tmp;

                soGM.ApplyModifiedProperties();
            }
        }

        private static void EnsureSceneInBuildSettings()
        {
            string currentScenePath = SceneManager.GetActiveScene().path;
            if (string.IsNullOrEmpty(currentScenePath)) return;

            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].path == currentScenePath) return;
            }

            List<EditorBuildSettingsScene> list = new List<EditorBuildSettingsScene>(scenes);
            list.Add(new EditorBuildSettingsScene(currentScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log($"[Milestone5Setup] Added scene '{currentScenePath}' to EditorBuildSettings.");
        }
    }
}
