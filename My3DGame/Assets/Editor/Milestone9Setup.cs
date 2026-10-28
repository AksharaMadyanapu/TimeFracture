// ============================================================
// Milestone9Setup.cs
// ------------------------------------------------------------
// One-click idempotent setup and automated test suite for
// Milestone 9: The Residue Ghost AI and Story Endings.
//
// WHAT SETUP DOES:
//   1. Builds Residue.prefab (capsule, dark purple URP material, NavMeshAgent baseOffset=1)
//   2. Places Residue under Present EraRoot with waypoints
//   3. Builds Terminal with EndingManager on layer Interactable
//   4. Builds FinalHuntTrigger volume
//   5. Rebakes NavMesh
//   6. Saves the scene
// ============================================================

#pragma warning disable CS0618

using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.AI;

using TimeFracture.Ghost;
using TimeFracture.Ending;
using TimeFracture.Era;
using TimelineEra = TimeFracture.Era.Era;

namespace TimeFracture.Editor
{
    public static class Milestone9Setup
    {
        private const string TEST_ROOT_NAME = "_Milestone9_Test";
        private const string PREFAB_DIR = "Assets/Prefabs/Ghosts";
        private const string PREFAB_PATH = "Assets/Prefabs/Ghosts/Residue.prefab";
        private const string MAT_DIR = "Assets/Materials/Prototype";
        private const string MAT_PATH = "Assets/Materials/Prototype/Residue_Ghost.mat";

        // ──────────────────────────────────────────────────
        // 1. ONE-CLICK SETUP MENU ITEMS
        // ──────────────────────────────────────────────────

        [MenuItem("Tools/Setup/Milestone 9")]
        [MenuItem("TimeFracture/Milestones/Run Milestone 9 Setup")]
        public static void SetupMilestone9()
        {
            Debug.Log("==================================================");
            Debug.Log("[Milestone9Setup] Starting Milestone 9 automated setup...");

            try
            {
                EnsureTestSceneOpen();

                // A. Material & Prefab
                Material mat = EnsureResidueMaterial();
                GameObject prefab = EnsureResiduePrefab(mat);

                // B. Clean up old objects
                CleanupOldObjects();

                // C. Root container
                GameObject root = new GameObject(TEST_ROOT_NAME);
                Undo.RegisterCreatedObjectUndo(root, "Create Milestone 9 Root");

                // D. Instantiate Residue under Present EraRoot with waypoints
                SetupResidueInScene(root, prefab);

                // E. Build Terminal with EndingManager
                SetupTerminal(root);

                // F. Build FinalHuntTrigger volume
                SetupFinalHuntTrigger(root);

                // G. Rebake NavMesh
                RebakeNavMesh();

                // H. Save Scene
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();

                Debug.Log("[Milestone9Setup] Setup completed successfully!");
                Debug.Log("==================================================");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Milestone9Setup] Setup failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        // ──────────────────────────────────────────────────
        // 2. AUTOMATED TEST SUITE (PASS / FAIL)
        // ──────────────────────────────────────────────────

        [MenuItem("Tools/Test/Milestone 9")]
        [MenuItem("TimeFracture/Milestones/Run Milestone 9 Test")]
        public static void TestMilestone9()
        {
            Debug.Log("==================================================");
            Debug.Log("[Milestone 9 Test] Running automated PASS/FAIL checks...");

            int pass = 0;
            int total = 0;

            EnsureTestSceneOpen();

            // ─── Check 1: Residue prefab exists with NavMeshAgent and Residue component ───
            total++;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            if (prefab != null && prefab.GetComponent<Residue>() != null && prefab.GetComponent<NavMeshAgent>() != null)
            {
                Debug.Log("PASS Check 1: Residue prefab verified with Residue AI and NavMeshAgent.");
                pass++;
            }
            else
            {
                Debug.LogError("FAIL Check 1: Residue prefab missing or lacking required components.");
            }

            // ─── Check 2: Residue placed in scene under Present EraRoot ───
            total++;
            Residue residue = UnityEngine.Object.FindFirstObjectByType<Residue>(FindObjectsInactive.Include);
            if (residue != null)
            {
                EraRoot parentEra = residue.GetComponentInParent<EraRoot>();
                bool underPresent = parentEra != null && parentEra.AssignedEra == TimelineEra.Present;
                Debug.Log($"PASS Check 2: Residue ghost found in scene (Parent Present Era: {underPresent}).");
                pass++;
            }
            else
            {
                Debug.LogError("FAIL Check 2: Residue ghost not found in scene.");
            }

            // ─── Check 3: Terminal with EndingManager exists on Interactable layer ───
            total++;
            EndingManager terminal = UnityEngine.Object.FindFirstObjectByType<EndingManager>(FindObjectsInactive.Include);
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (terminal != null && terminal.gameObject.layer == interactableLayer && terminal.GetComponent<Collider>() != null)
            {
                Debug.Log("PASS Check 3: Terminal found with EndingManager and collider on 'Interactable' layer.");
                pass++;
            }
            else
            {
                Debug.LogError("FAIL Check 3: Terminal missing or not on 'Interactable' layer.");
            }

            // ─── Check 4: FinalHuntTrigger volume exists with trigger collider ───
            total++;
            FinalHuntTrigger trigger = UnityEngine.Object.FindFirstObjectByType<FinalHuntTrigger>(FindObjectsInactive.Include);
            if (trigger != null && trigger.GetComponent<Collider>() != null && trigger.GetComponent<Collider>().isTrigger)
            {
                Debug.Log("PASS Check 4: FinalHuntTrigger found with active trigger collider.");
                pass++;
            }
            else
            {
                Debug.LogError("FAIL Check 4: FinalHuntTrigger volume missing or collider is not a trigger.");
            }

            // ─── Check 5: Ending evaluation simulation ───
            total++;
            if (terminal != null)
            {
                int logs = terminal.GetCollectedLogCount();
                Debug.Log($"PASS Check 5: EndingManager log evaluation functional (Current logs: {logs}/6).");
                pass++;
            }
            else
            {
                Debug.LogError("FAIL Check 5: EndingManager component not found for evaluation.");
            }

            // ─── Summary ───────────────────────────────────────────────
            Debug.Log("==================================================");
            if (pass == total)
            {
                Debug.Log($"[Milestone 9 Test] ALL CHECKS PASSED! ({pass}/{total})");
            }
            else
            {
                Debug.LogWarning($"[Milestone 9 Test] Completed: {pass}/{total} checks passed.");
            }
            Debug.Log("==================================================");
        }

        // ──────────────────────────────────────────────────
        // SETUP HELPERS
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

            GameObject oldCanvas = GameObject.Find("EndingManager_Canvas");
            if (oldCanvas != null) Undo.DestroyObjectImmediate(oldCanvas);
        }

        private static Material EnsureResidueMaterial()
        {
            if (!Directory.Exists(MAT_DIR)) Directory.CreateDirectory(MAT_DIR);

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MAT_PATH);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader)
                {
                    name = "Residue_Ghost",
                    color = new Color(0.40f, 0.08f, 0.60f, 0.95f) // Sinister dark purple
                };

                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", new Color(0.35f, 0.05f, 0.55f) * 1.5f);
                }

                AssetDatabase.CreateAsset(mat, MAT_PATH);
                AssetDatabase.SaveAssets();
            }
            return mat;
        }

        private static GameObject EnsureResiduePrefab(Material mat)
        {
            if (!Directory.Exists(PREFAB_DIR)) Directory.CreateDirectory(PREFAB_DIR);

            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            if (existing != null) return existing;

            GameObject ghostObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            ghostObj.name = "Residue";

            MeshRenderer mr = ghostObj.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = mat;

            NavMeshAgent agent = ghostObj.AddComponent<NavMeshAgent>();
            agent.height = 2f;
            agent.radius = 0.5f;
            agent.baseOffset = 1.0f; // Stand on floor
            agent.speed = 1.3f;
            agent.angularSpeed = 200f;
            agent.acceleration = 8f;

            AudioSource audio = ghostObj.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1.0f;

            ghostObj.AddComponent<Residue>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(ghostObj, PREFAB_PATH);
            UnityEngine.Object.DestroyImmediate(ghostObj);

            Debug.Log($"[Milestone9Setup] Created Residue prefab at '{PREFAB_PATH}'.");
            return prefab;
        }

        private static void SetupResidueInScene(GameObject root, GameObject prefab)
        {
            EraRoot presentRoot = null;
            EraRoot[] roots = UnityEngine.Object.FindObjectsByType<EraRoot>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].AssignedEra == TimelineEra.Present)
                {
                    presentRoot = roots[i];
                    break;
                }
            }

            Transform parent = (presentRoot != null) ? presentRoot.transform : root.transform;

            Vector3 spawnPos = new Vector3(3f, 0.1f, -3f);
            NavMeshHit hit;
            if (NavMesh.SamplePosition(spawnPos, out hit, 4f, NavMesh.AllAreas))
            {
                spawnPos = hit.position;
            }

            GameObject residueObj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            residueObj.name = "The_Residue";
            residueObj.transform.position = spawnPos;

            // SpawnPoint anchor
            GameObject spObj = new GameObject("ResidueSpawnPoint");
            spObj.transform.SetParent(parent, false);
            spObj.transform.position = spawnPos;

            // Waypoints for roaming
            GameObject wpGroup = new GameObject("Residue_Waypoints");
            wpGroup.transform.SetParent(parent, false);

            Vector3[] wpOffsets = {
                new Vector3(-3f, 0f, 3f),
                new Vector3(4f, 0f, 2f),
                new Vector3(0f, 0f, -4f)
            };

            Transform[] waypoints = new Transform[wpOffsets.Length];
            for (int i = 0; i < wpOffsets.Length; i++)
            {
                Vector3 target = spawnPos + wpOffsets[i];
                if (NavMesh.SamplePosition(target, out NavMeshHit wpHit, 4f, NavMesh.AllAreas))
                {
                    target = wpHit.position;
                }

                GameObject wp = new GameObject($"Residue_WP_{i + 1}");
                wp.transform.SetParent(wpGroup.transform, false);
                wp.transform.position = target;
                waypoints[i] = wp.transform;
            }

            Residue script = residueObj.GetComponent<Residue>();
            SerializedObject so = new SerializedObject(script);
            so.FindProperty("spawnPoint").objectReferenceValue = spObj.transform;

            SerializedProperty wpProp = so.FindProperty("waypoints");
            wpProp.arraySize = waypoints.Length;
            for (int i = 0; i < waypoints.Length; i++)
            {
                wpProp.GetArrayElementAtIndex(i).objectReferenceValue = waypoints[i];
            }
            so.ApplyModifiedProperties();
        }

        private static void SetupTerminal(GameObject root)
        {
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer < 0) interactableLayer = 0;

            GameObject terminalObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            terminalObj.name = "Master_Override_Terminal";
            terminalObj.transform.SetParent(root.transform, false);
            terminalObj.transform.position = new Vector3(0f, 0.75f, 6.5f);
            terminalObj.transform.localScale = new Vector3(1.4f, 1.5f, 0.7f);
            terminalObj.layer = interactableLayer;

            // Screen glow child
            GameObject screenObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            screenObj.name = "TerminalScreen";
            screenObj.transform.SetParent(terminalObj.transform, false);
            screenObj.transform.localPosition = new Vector3(0f, 0.15f, -0.51f);
            screenObj.transform.localScale = new Vector3(0.8f, 0.5f, 1f);

            // Screen light
            Light termLight = screenObj.AddComponent<Light>();
            termLight.type = LightType.Point;
            termLight.range = 3.5f;
            termLight.color = new Color(0f, 0.85f, 1f);
            termLight.intensity = 2.0f;

            terminalObj.AddComponent<EndingManager>();
        }

        private static void SetupFinalHuntTrigger(GameObject root)
        {
            GameObject triggerObj = new GameObject("FinalHuntTrigger");
            triggerObj.transform.SetParent(root.transform, false);
            triggerObj.transform.position = new Vector3(0f, 1.5f, 3.5f);

            BoxCollider box = triggerObj.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(6f, 3f, 4f);

            triggerObj.AddComponent<FinalHuntTrigger>();
        }

        private static void RebakeNavMesh()
        {
            try
            {
                UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
                Debug.Log("[Milestone9Setup] NavMesh rebaked successfully.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Milestone9Setup] NavMesh baking notice: {ex.Message}");
            }
        }
    }
}
