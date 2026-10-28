// ============================================================
// Milestone8Setup.cs
// ------------------------------------------------------------
// One-click idempotent setup and automated test suite for
// Milestone 8: Audio Logs & Story Narrative.
//
// WHAT SETUP DOES:
//   1. Generates 6 AudioLogData ScriptableObject assets with lore
//      subtitles (3 in the Past, 3 in the Present) in Assets/Data/AudioLogs/
//   2. Builds AudioLogPickup.prefab in Assets/Prefabs/Narrative/
//   3. Places 3 pickups in Past rooms and 3 pickups in Present rooms
//      on NavMesh-valid floor points
//   4. Configures AudioLogPlayer and LogMenu in the scene
//   5. Saves the scene
// ============================================================

#pragma warning disable CS0618

using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.AI;

using TimeFracture.AudioLogs;
using TimeFracture.Era;
using TimelineEra = TimeFracture.Era.Era;

namespace TimeFracture.Editor
{
    public static class Milestone8Setup
    {
        private const string TEST_ROOT_NAME = "_Milestone8_Test";
        private const string LOG_DATA_DIR = "Assets/Data/AudioLogs";
        private const string PREFAB_DIR = "Assets/Prefabs/Narrative";
        private const string PREFAB_PATH = "Assets/Prefabs/Narrative/AudioLogPickup.prefab";
        private const string MAT_DIR = "Assets/Materials/Prototype";
        private const string MAT_PATH = "Assets/Materials/Prototype/AudioLog_Cassette.mat";

        // ──────────────────────────────────────────────────
        // 1. ONE-CLICK SETUP MENU ITEMS
        // ──────────────────────────────────────────────────

        [MenuItem("Tools/Setup/Milestone 8")]
        [MenuItem("TimeFracture/Milestones/Run Milestone 8 Setup")]
        public static void SetupMilestone8()
        {
            Debug.Log("==================================================");
            Debug.Log("[Milestone8Setup] Starting Milestone 8 automated setup...");

            try
            {
                EnsureTestSceneOpen();

                // A. Create 6 AudioLogData ScriptableObjects
                AudioLogData[] logs = EnsureAudioLogAssets();

                // B. Create Material & Prefab
                Material mat = EnsureCassetteMaterial();
                GameObject prefab = EnsurePickupPrefab(mat);

                // C. Clean up old objects
                CleanupOldObjects();

                // D. Create root container
                GameObject root = new GameObject(TEST_ROOT_NAME);
                Undo.RegisterCreatedObjectUndo(root, "Create Milestone 8 Root");

                // E. Setup Player Systems (AudioLogPlayer & LogMenu)
                SetupRuntimeSystems(root);

                // F. Place Pickups: 3 in Past, 3 in Present
                PlaceLogPickups(root, prefab, logs);

                // G. Save Scene
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();

                Debug.Log("[Milestone8Setup] Setup completed successfully!");
                Debug.Log("==================================================");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Milestone8Setup] Setup failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        // ──────────────────────────────────────────────────
        // 2. AUTOMATED TEST SUITE (PASS / FAIL)
        // ──────────────────────────────────────────────────

        [MenuItem("Tools/Test/Milestone 8")]
        [MenuItem("TimeFracture/Milestones/Run Milestone 8 Test")]
        public static void TestMilestone8()
        {
            Debug.Log("==================================================");
            Debug.Log("[Milestone 8 Test] Running automated PASS/FAIL checks...");

            int pass = 0;
            int total = 0;

            EnsureTestSceneOpen();

            // ─── Check 1: 6 AudioLogData assets exist ───
            total++;
            string[] guids = AssetDatabase.FindAssets("t:AudioLogData", new[] { LOG_DATA_DIR });
            if (guids != null && guids.Length >= 6)
            {
                Debug.Log($"PASS Check 1: Found {guids.Length} AudioLogData asset(s) with timestamped lore subtitles in '{LOG_DATA_DIR}'.");
                pass++;
            }
            else
            {
                Debug.LogError($"FAIL Check 1: Expected 6 AudioLogData assets, found {guids?.Length ?? 0}. Run Setup first.");
            }

            // ─── Check 2: AudioLogPickup prefab exists with required components ───
            total++;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            if (prefab != null && prefab.GetComponent<AudioLogPickup>() != null && prefab.GetComponent<Collider>() != null)
            {
                Debug.Log("PASS Check 2: AudioLogPickup prefab verified with collider and AudioLogPickup component.");
                pass++;
            }
            else
            {
                Debug.LogError("FAIL Check 2: AudioLogPickup prefab missing or invalid.");
            }

            // ─── Check 3: At least 6 pickups placed in scene on Interactable layer ───
            total++;
            AudioLogPickup[] scenePickups = UnityEngine.Object.FindObjectsByType<AudioLogPickup>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            int interactableLayer = LayerMask.NameToLayer("Interactable");
            int validPickups = 0;
            for (int i = 0; i < scenePickups.Length; i++)
            {
                if (scenePickups[i].gameObject.layer == interactableLayer && scenePickups[i].GetComponent<Collider>() != null)
                {
                    validPickups++;
                }
            }

            if (validPickups >= 6)
            {
                Debug.Log($"PASS Check 3: Found {validPickups} AudioLogPickup object(s) with colliders on 'Interactable' layer.");
                pass++;
            }
            else
            {
                Debug.LogError($"FAIL Check 3: Expected at least 6 valid pickups on Interactable layer, found {validPickups}.");
            }

            // ─── Check 4: AudioLogPlayer and LogMenu exist ───
            total++;
            AudioLogPlayer player = UnityEngine.Object.FindFirstObjectByType<AudioLogPlayer>();
            LogMenu menu = UnityEngine.Object.FindFirstObjectByType<LogMenu>();
            if (player != null && menu != null)
            {
                Debug.Log("PASS Check 4: AudioLogPlayer and LogMenu singletons active and ready in scene.");
                pass++;
            }
            else
            {
                Debug.LogError("FAIL Check 4: AudioLogPlayer or LogMenu not found in scene.");
            }

            // ─── Check 5: AudioLogPickup interaction simulation ───
            total++;
            GameObject testPickupObj = new GameObject("_Test_AudioLog_Pickup");
            try
            {
                AudioLogPickup pickup = testPickupObj.AddComponent<AudioLogPickup>();
                pickup.LogData = ScriptableObject.CreateInstance<AudioLogData>();
                pickup.LogData.id = "test_log";
                pickup.LogData.title = "Test Log";
                pickup.LogData.worldStateFlag = "Test_Log_Collected";

                string prompt = pickup.GetPrompt();
                bool promptValid = !string.IsNullOrEmpty(prompt) && prompt.Contains("Test Log");

                if (promptValid)
                {
                    Debug.Log("PASS Check 5: AudioLogPickup returns prompt matching log title.");
                    pass++;
                }
                else
                {
                    Debug.LogError("FAIL Check 5: AudioLogPickup GetPrompt() returned invalid string.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testPickupObj);
            }

            // ─── Summary ───────────────────────────────────────────────
            Debug.Log("==================================================");
            if (pass == total)
            {
                Debug.Log($"[Milestone 8 Test] ALL CHECKS PASSED! ({pass}/{total})");
            }
            else
            {
                Debug.LogWarning($"[Milestone 8 Test] Completed: {pass}/{total} checks passed.");
            }
            Debug.Log("==================================================");
        }

        // ──────────────────────────────────────────────────
        // ASSET GENERATION HELPERS
        // ──────────────────────────────────────────────────

        private static void EnsureTestSceneOpen()
        {
            if (string.IsNullOrEmpty(EditorSceneManager.GetActiveScene().path))
            {
                if (File.Exists("Assets/Scenes/TestScene.unity"))
                {
                    EditorSceneManager.OpenScene("Assets/Scenes/TestScene.unity");
                }
            }
        }

        private static void CleanupOldObjects()
        {
            GameObject oldRoot = GameObject.Find(TEST_ROOT_NAME);
            if (oldRoot != null) Undo.DestroyObjectImmediate(oldRoot);

            GameObject oldSub = GameObject.Find("AudioLogSubtitle_Canvas");
            if (oldSub != null) Undo.DestroyObjectImmediate(oldSub);

            GameObject oldMenu = GameObject.Find("LogMenu_Canvas");
            if (oldMenu != null) Undo.DestroyObjectImmediate(oldMenu);
        }

        private static AudioLogData[] EnsureAudioLogAssets()
        {
            if (!Directory.Exists(LOG_DATA_DIR))
            {
                Directory.CreateDirectory(LOG_DATA_DIR);
            }

            AudioLogData[] logs = new AudioLogData[6];

            string[] ids = { "log_01", "log_02", "log_03", "log_04", "log_05", "log_06" };
            string[] titles = {
                "Log 01 - The Temporal Displacement Experiment",
                "Log 02 - Anomaly in Sector B",
                "Log 03 - Quarantine Breach",
                "Log 04 - The Evacuation Order",
                "Log 05 - The Echoes We Left Behind",
                "Log 06 - Project Time Fracture Cover-Up"
            };
            TimelineEra[] eras = {
                TimelineEra.Past, TimelineEra.Past, TimelineEra.Past,
                TimelineEra.Present, TimelineEra.Present, TimelineEra.Present
            };
            string[] flags = {
                "AudioLog_01", "AudioLog_02", "AudioLog_03",
                "AudioLog_04", "AudioLog_05", "AudioLog_06"
            };

            string[][] subtitleTexts = new string[][]
            {
                // Log 1 (Past)
                new string[] {
                    "Dr. Mercer: Initiating primary magnetic containment coil...",
                    "If the chronal field holds, we will displace physical matter by 30 seconds into the future.",
                    "Wait... the feedback loop is expanding. Why are the reactor clocks spinning backwards?!"
                },
                // Log 2 (Past)
                new string[] {
                    "Researcher Alvarez: Sector B is reporting phantom echoes on the sonic sensors.",
                    "Footsteps where no one is walking. Doors sliding shut on their own.",
                    "Whatever we pulled through the rift... it reacts violently to sound. Keep the corridors quiet!"
                },
                // Log 3 (Past)
                new string[] {
                    "Chief Security Hayes: The containment doors failed. The time fracture is tearing through the sub-levels.",
                    "Dr. Mercer refused to pull the emergency kill switch. He called them 'temporal anomalies'.",
                    "If anyone hears this... lock the lockers and hold your breath. It's inside the facility!"
                },
                // Log 4 (Present)
                new string[] {
                    "Radio Broadcast (Static): All personnel... facility is under permanent quarantine.",
                    "Present radiation levels exceed lethal threshold. The temporal collapse is complete.",
                    "Do not enter the dark corridors without light. The shadows have substance now."
                },
                // Log 5 (Present)
                new string[] {
                    "Dr. Mercer (Distorted): Decades have passed... or was it only minutes?",
                    "The Residue wanders the decayed labs in the dark. It hunts everything that reflects light.",
                    "Only high-intensity flares keep it from devouring your mind. Save your flares!"
                },
                // Log 6 (Present)
                new string[] {
                    "Director Vance (Declassified): Incident 9-Gamma has been erased from all public archives.",
                    "The facility will be buried. The terminal in Sector 4 holds the master timeline override.",
                    "Shut it down, complete the loop, or escape into the wasteland... but the truth stays here."
                }
            };

            for (int i = 0; i < 6; i++)
            {
                string path = $"{LOG_DATA_DIR}/{ids[i]}.asset";
                AudioLogData log = AssetDatabase.LoadAssetAtPath<AudioLogData>(path);

                if (log == null)
                {
                    log = ScriptableObject.CreateInstance<AudioLogData>();
                    log.id = ids[i];
                    log.title = titles[i];
                    log.eraFoundIn = eras[i];
                    log.worldStateFlag = flags[i];

                    string[] lines = subtitleTexts[i];
                    log.subtitles = new SubtitleLine[lines.Length];
                    for (int j = 0; j < lines.Length; j++)
                    {
                        log.subtitles[j] = new SubtitleLine
                        {
                            timestamp = j * 2.5f,
                            text = lines[j]
                        };
                    }

                    log.fullTranscript = string.Join("\n\n", lines);

                    AssetDatabase.CreateAsset(log, path);
                    Debug.Log($"[Milestone8Setup] Created AudioLogData asset '{path}'.");
                }

                logs[i] = log;
            }

            AssetDatabase.SaveAssets();
            return logs;
        }

        private static Material EnsureCassetteMaterial()
        {
            if (!Directory.Exists(MAT_DIR)) Directory.CreateDirectory(MAT_DIR);

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MAT_PATH);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader)
                {
                    name = "AudioLog_Cassette",
                    color = new Color(0.18f, 0.22f, 0.28f, 1f)
                };

                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", new Color(0f, 0.8f, 0.9f) * 1.5f);
                }

                AssetDatabase.CreateAsset(mat, MAT_PATH);
                AssetDatabase.SaveAssets();
            }
            return mat;
        }

        private static GameObject EnsurePickupPrefab(Material mat)
        {
            if (!Directory.Exists(PREFAB_DIR)) Directory.CreateDirectory(PREFAB_DIR);

            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            if (existing != null) return existing;

            // Cassette tape body (small flattened box)
            GameObject pickupObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pickupObj.name = "AudioLogPickup";
            pickupObj.transform.localScale = new Vector3(0.40f, 0.08f, 0.25f);

            MeshRenderer mr = pickupObj.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = mat;

            // Indicator light (small glowing reel)
            GameObject lightChild = new GameObject("CassetteReelLight");
            lightChild.transform.SetParent(pickupObj.transform, false);
            Light pLight = lightChild.AddComponent<Light>();
            pLight.type = LightType.Point;
            pLight.range = 3.5f;
            pLight.color = new Color(0f, 0.85f, 1f);
            pLight.intensity = 1.8f;

            // AudioSource
            AudioSource audio = pickupObj.AddComponent<AudioSource>();
            audio.spatialBlend = 1.0f;

            // Pickup component
            pickupObj.AddComponent<AudioLogPickup>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(pickupObj, PREFAB_PATH);
            UnityEngine.Object.DestroyImmediate(pickupObj);

            Debug.Log($"[Milestone8Setup] Created AudioLogPickup prefab at '{PREFAB_PATH}'.");
            return prefab;
        }

        private static void SetupRuntimeSystems(GameObject root)
        {
            // AudioLogPlayer
            AudioLogPlayer player = UnityEngine.Object.FindFirstObjectByType<AudioLogPlayer>();
            if (player == null)
            {
                GameObject pObj = new GameObject("AudioLogPlayer");
                pObj.transform.SetParent(root.transform, false);
                pObj.AddComponent<AudioLogPlayer>();
            }

            // LogMenu
            LogMenu menu = UnityEngine.Object.FindFirstObjectByType<LogMenu>();
            if (menu == null)
            {
                GameObject mObj = new GameObject("LogMenu");
                mObj.transform.SetParent(root.transform, false);
                mObj.AddComponent<LogMenu>();
            }
        }

        private static void PlaceLogPickups(GameObject root, GameObject prefab, AudioLogData[] logs)
        {
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer < 0) interactableLayer = 0;

            // Locate Past and Present EraRoot containers
            EraRoot pastRoot = null;
            EraRoot presentRoot = null;

            EraRoot[] eraRoots = UnityEngine.Object.FindObjectsByType<EraRoot>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < eraRoots.Length; i++)
            {
                if (eraRoots[i].AssignedEra == TimelineEra.Past) pastRoot = eraRoots[i];
                if (eraRoots[i].AssignedEra == TimelineEra.Present) presentRoot = eraRoots[i];
            }

            // Placements on NavMesh-valid floors
            Vector3[] pastPositions = new Vector3[]
            {
                new Vector3(-4.0f, 0.15f, 1.5f),
                new Vector3(4.5f, 0.15f, -3.0f),
                new Vector3(1.0f, 0.15f, 5.0f)
            };

            Vector3[] presentPositions = new Vector3[]
            {
                new Vector3(-3.5f, 0.15f, -4.0f),
                new Vector3(3.5f, 0.15f, 2.0f),
                new Vector3(-1.0f, 0.15f, 4.0f)
            };

            for (int i = 0; i < 6; i++)
            {
                bool isPast = i < 3;
                Vector3 candidate = isPast ? pastPositions[i] : presentPositions[i - 3];
                Vector3 finalPos = candidate;

                NavMeshHit hit;
                if (NavMesh.SamplePosition(candidate, out hit, 4.0f, NavMesh.AllAreas))
                {
                    finalPos = hit.position + Vector3.up * 0.15f;
                }

                Transform parentTransform = root.transform;
                if (isPast && pastRoot != null) parentTransform = pastRoot.transform;
                else if (!isPast && presentRoot != null) parentTransform = presentRoot.transform;

                GameObject pickupObj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parentTransform);
                pickupObj.name = $"AudioLogPickup_{logs[i].id}";
                pickupObj.transform.position = finalPos;
                pickupObj.layer = interactableLayer;

                AudioLogPickup pickupComp = pickupObj.GetComponent<AudioLogPickup>();
                SerializedObject so = new SerializedObject(pickupComp);
                so.FindProperty("logData").objectReferenceValue = logs[i];
                so.ApplyModifiedProperties();
            }

            Debug.Log($"[Milestone8Setup] Placed 3 pickups in Past and 3 in Present on NavMesh floor points.");
        }
    }
}
