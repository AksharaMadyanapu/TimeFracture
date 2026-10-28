// ============================================================
// BuildTestScene.cs
// ------------------------------------------------------------
// Unity Editor utility to automatically construct and configure
// the complete playable test scene for Time Fracture (Milestones 1–4).
//
// ACCESS IN UNITY:
//   Top Menu: Tools > Build Time Fracture Test Scene
// ============================================================

using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using TMPro;
using UnityEngine.AI;

using TimeFracture.Player;
using TimeFracture.World;
using TimeFracture.Era;
using TimeFracture.Hiding;
using TimeFracture.Ghost;
using TimeFracture.Game;

namespace TimeFracture.Editor
{
    public static class BuildTestScene
    {
        private const string SCENE_DIR = "Assets/Scenes";
        private const string SCENE_PATH = "Assets/Scenes/TestScene.unity";
        private const string SETTINGS_DIR = "Assets/Settings";
        private const string PROFILE_PATH = "Assets/Settings/Glitch_TestProfile.asset";
        private const string MAT_DIR = "Assets/Materials/Prototype";

        // Material Palette
        private static Material matDoor;
        private static Material matLocker;
        private static Material matLockerTrim;
        private static Material matBarrier;
        private static Material matSwitch;
        private static Material matChronoBase;
        private static Material matChronoGlow;
        private static Material matFloor;
        private static Material matWallPast;
        private static Material matWallPresent;
        private static Material matMetalDark;
        private static Material matGhostEcho;

        private static TMP_FontAsset defaultFont;

        [MenuItem("Tools/Build Time Fracture Test Scene")]
        public static void GenerateScene()
        {
            Debug.Log("==================================================");
            Debug.Log("[BuildTestScene] Starting automated scene generation...");

            bool hasErrors = false;

            // 1. Configure Project Layers
            TryExecuteStep(1, "Configure Project Layers", () =>
            {
                EnsureLayers();
            }, ref hasErrors);

            // 2. Verify Active Input Handling
            TryExecuteStep(2, "Check Active Input Handling", () =>
            {
                CheckInputHandling();
            }, ref hasErrors);

            // 3. Open or Create TestScene.unity
            TryExecuteStep(3, "Open / Create TestScene", () =>
            {
                EnsureSceneReady();
            }, ref hasErrors);

            // 4. Clean previous objects for safe re-run
            TryExecuteStep(4, "Clean Old Scene Objects", () =>
            {
                CleanExistingObjects();
            }, ref hasErrors);

            // 5. Build Material Palette in Assets/Materials/Prototype/
            TryExecuteStep(5, "Build Prototype Materials", () =>
            {
                BuildMaterialPalette();
            }, ref hasErrors);

            // 6. Build Environment Base & Shared Floor
            TryExecuteStep(6, "Build Environment Base", () =>
            {
                BuildEnvironmentBase();
            }, ref hasErrors);

            // 7. Build UI Canvas
            TextMeshProUGUI promptTMP = null;
            CanvasGroup slatsCG = null;
            Slider breathSlider = null;
            TryExecuteStep(7, "Build HUD Canvas & Prompts", () =>
            {
                BuildHUD(out promptTMP, out slatsCG, out breathSlider);
            }, ref hasErrors);

            // 8. Build Era Systems (EraManager + GlitchEffect + URP Profile)
            EraManager eraManager = null;
            GlitchEffect glitchEffect = null;
            TryExecuteStep(8, "Build Era Systems", () =>
            {
                BuildEraSystems(out eraManager, out glitchEffect);
            }, ref hasErrors);

            // 9. Build Player
            PlayerController playerController = null;
            TryExecuteStep(9, "Build Player & Camera", () =>
            {
                BuildPlayer(promptTMP, slatsCG, breathSlider, out playerController);
            }, ref hasErrors);

            // Link Player to GlitchEffect
            if (glitchEffect != null && playerController != null)
            {
                SerializedObject soGlitch = new SerializedObject(glitchEffect);
                SafeSetObjectProperty(soGlitch, "playerController", playerController, "GlitchEffect -> PlayerController");
                soGlitch.ApplyModifiedProperties();
            }

            // 10. Build Room_Past (Clean, Bright, Movable Barrier, Past Locker)
            TryExecuteStep(10, "Build Room_Past", () =>
            {
                BuildRoomPast();
            }, ref hasErrors);

            // 11. Build Room_Present (Decayed, Dim, Locked Door, Present Locker)
            TryExecuteStep(11, "Build Room_Present", () =>
            {
                BuildRoomPresent();
            }, ref hasErrors);

            // 12. Build Central Swap Machine (ChronoStabilizer)
            TryExecuteStep(12, "Build Chrono-Stabilizer Machine", () =>
            {
                BuildChronoStabilizer();
            }, ref hasErrors);

            // 13. Build Wall Switch (Milestone 2 Switch interactable)
            TryExecuteStep(13, "Build Wall Switch", () =>
            {
                BuildWallSwitch();
            }, ref hasErrors);

            // 14. Build Crouch Test Area
            TryExecuteStep(14, "Build Crouch Test Area", () =>
            {
                BuildCrouchTestArea();
            }, ref hasErrors);

            // 15. Build The Echo (Ghost #1)
            TryExecuteStep(15, "Build The Echo Ghost (Milestone 5)", () =>
            {
                BuildEchoGhost();
            }, ref hasErrors);

            // 16. Save Scene
            TryExecuteStep(16, "Save Scene", () =>
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
            }, ref hasErrors);

            Debug.Log("==================================================");
            if (!hasErrors)
            {
                Debug.Log("[BuildTestScene] SUCCESS! 'Assets/Scenes/TestScene.unity' built and saved with 0 errors.");
                Debug.Log("[BuildTestScene] Press PLAY in Unity to test all 4 Milestones!");
                EditorUtility.DisplayDialog(
                    "Time Fracture Test Scene Built",
                    "Successfully generated 'TestScene.unity' with distinct colored materials and 3D floating labels!\n\n" +
                    "• DOOR: Brown with handle (updates locked status)\n" +
                    "• LOCKERS: Dark green with vents (Past & Present)\n" +
                    "• BARRIER: Industrial Orange (slides aside with [E])\n" +
                    "• SWITCH: Yellow wall lever\n" +
                    "• SWAP MACHINE: Glowing purple/magenta temporal core\n" +
                    "• Floating 3D labels billboard to camera within 6m\n\n" +
                    "Press PLAY to test!",
                    "Let's Go!"
                );
            }
            else
            {
                Debug.LogError("[BuildTestScene] Scene generation finished with errors. Check Console for details.");
            }
            Debug.Log("==================================================");
        }

        private static void TryExecuteStep(int stepNum, string stepName, Action action, ref bool hasErrors)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                hasErrors = true;
                Debug.LogError($"[BuildTestScene] ❌ FAILED at Step {stepNum} ({stepName}): {ex.Message}\n{ex.StackTrace}");
            }
        }

        // ──────────────────────────────────────────────────
        // 1. LAYERS (Player=6, Interactable=7)
        // ──────────────────────────────────────────────────

        private static void EnsureLayers()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) return;

            SerializedObject tagManager = new SerializedObject(assets[0]);
            SerializedProperty layersProp = tagManager.FindProperty("layers");
            if (layersProp == null) return;

            SetLayerAtIndex(layersProp, 6, "Player");
            SetLayerAtIndex(layersProp, 7, "Interactable");

            tagManager.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log("[BuildTestScene] Step 1: Verified Layers (6: Player, 7: Interactable).");
        }

        private static void SetLayerAtIndex(SerializedProperty layers, int index, string layerName)
        {
            if (index < layers.arraySize)
            {
                SerializedProperty elem = layers.GetArrayElementAtIndex(index);
                if (elem.stringValue != layerName)
                {
                    elem.stringValue = layerName;
                    Debug.Log($"[BuildTestScene] Assigned Layer {index} = '{layerName}'.");
                }
            }
        }

        // ──────────────────────────────────────────────────
        // 2. INPUT HANDLING CHECK
        // ──────────────────────────────────────────────────

        private static void CheckInputHandling()
        {
            string settingsPath = "ProjectSettings/ProjectSettings.asset";
            bool onDiskSet = false;

            try
            {
                if (File.Exists(settingsPath))
                {
                    string text = File.ReadAllText(settingsPath);
                    if (System.Text.RegularExpressions.Regex.IsMatch(text, @"activeInputHandler:\s*[02]"))
                    {
                        text = System.Text.RegularExpressions.Regex.Replace(text, @"activeInputHandler:\s*[02]", "activeInputHandler: 1");
                        File.WriteAllText(settingsPath, text);
                        onDiskSet = true;
                        Debug.Log("[BuildTestScene] Set 'activeInputHandler: 1' (Both) in ProjectSettings.asset on disk.");
                    }
                    else if (System.Text.RegularExpressions.Regex.IsMatch(text, @"activeInputHandler:\s*1"))
                    {
                        onDiskSet = true;
                    }
                }

                // Also apply via SerializedObject if available
                var assets = AssetDatabase.LoadAllAssetsAtPath(settingsPath);
                if (assets != null && assets.Length > 0)
                {
                    SerializedObject projectSettings = new SerializedObject(assets[0]);
                    SerializedProperty inputHandlerProp = projectSettings.FindProperty("activeInputHandler");
                    if (inputHandlerProp != null && inputHandlerProp.intValue != 1)
                    {
                        inputHandlerProp.intValue = 1; // Both
                        projectSettings.ApplyModifiedProperties();
                        AssetDatabase.SaveAssets();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BuildTestScene] Note while verifying input handler: {ex.Message}");
            }

            if (onDiskSet)
            {
                Debug.Log("[BuildTestScene] Step 2: Active Input Handling locked to 'Both' (Legacy + New Input).");
            }
            else
            {
                Debug.LogWarning("[BuildTestScene] ⚠️ WARNING: Could not automatically set Active Input Handling to 'Both'. Please manually verify in: Edit > Project Settings > Player > Other Settings > Active Input Handling = 'Both' (requires Editor restart).");
            }
        }

        // ──────────────────────────────────────────────────
        // 3. SCENE SETUP
        // ──────────────────────────────────────────────────

        private static void EnsureSceneReady()
        {
            if (!AssetDatabase.IsValidFolder(SCENE_DIR))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }

            if (File.Exists(SCENE_PATH))
            {
                EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            }
            else
            {
                Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(newScene, SCENE_PATH);
            }

            Debug.Log($"[BuildTestScene] Step 3: Opened scene '{SCENE_PATH}'.");
        }

        // ──────────────────────────────────────────────────
        // 4. CLEANUP FOR RE-RUN REPEATABILITY
        // ──────────────────────────────────────────────────

        private static void CleanExistingObjects()
        {
            Scene activeScene = EditorSceneManager.GetActiveScene();
            string[] namesToDelete = {
                "Main Camera",
                "Directional Light",
                "Global Volume",
                "Player",
                "HUD_Canvas",
                "EventSystem",
                "Era_Systems",
                "Environment_Base",
                "Room_Past",
                "Room_Present",
                "ChronoStabilizer",
                "Wall_Switch",
                "Crouch_Test_Area",
                "Echo_Ghost",
                "Echo_Waypoints",
                "GameManager"
            };

            int deletedCount = 0;

            // 1. Check all root objects (includes inactive ones like Room_Present!)
            GameObject[] rootObjects = activeScene.GetRootGameObjects();
            foreach (GameObject root in rootObjects)
            {
                if (root == null) continue;
                foreach (string name in namesToDelete)
                {
                    if (root.name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                        root.name.StartsWith(name + " ", StringComparison.OrdinalIgnoreCase) ||
                        root.name.StartsWith(name + "(", StringComparison.OrdinalIgnoreCase))
                    {
                        Undo.DestroyObjectImmediate(root);
                        deletedCount++;
                        break;
                    }
                }
            }

            // 2. Extra safety pass across all scene GameObjects (active or inactive)
            var allSceneObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var obj in allSceneObjects)
            {
                if (obj == null) continue;
                if (obj.scene != activeScene || obj.transform.parent != null) continue;

                foreach (string name in namesToDelete)
                {
                    if (obj.name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                        obj.name.StartsWith(name + " ", StringComparison.OrdinalIgnoreCase) ||
                        obj.name.StartsWith(name + "(", StringComparison.OrdinalIgnoreCase))
                    {
                        Undo.DestroyObjectImmediate(obj);
                        deletedCount++;
                        break;
                    }
                }
            }

            Debug.Log($"[BuildTestScene] Step 4: Cleaned {deletedCount} old/duplicate objects (including inactive Room_Present).");
        }

        // ──────────────────────────────────────────────────
        // 5. MATERIAL PALETTE (Assets/Materials/Prototype/)
        // ──────────────────────────────────────────────────

        private static void BuildMaterialPalette()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");
            if (!AssetDatabase.IsValidFolder(MAT_DIR))
                AssetDatabase.CreateFolder("Assets/Materials", "Prototype");

            matDoor         = GetOrCreateMaterial("Mat_Door", new Color(0.46f, 0.25f, 0.12f), smoothness: 0.25f);
            matLocker       = GetOrCreateMaterial("Mat_Locker", new Color(0.12f, 0.38f, 0.18f), smoothness: 0.4f);
            matLockerTrim   = GetOrCreateMaterial("Mat_Locker_DarkTrim", new Color(0.06f, 0.10f, 0.07f), smoothness: 0.2f);
            matBarrier      = GetOrCreateMaterial("Mat_Barrier", new Color(0.96f, 0.46f, 0.05f), smoothness: 0.35f);
            matSwitch       = GetOrCreateMaterial("Mat_Switch", new Color(0.95f, 0.85f, 0.12f), smoothness: 0.45f);
            matChronoBase   = GetOrCreateMaterial("Mat_Chrono_Base", new Color(0.16f, 0.15f, 0.22f), smoothness: 0.5f);
            matChronoGlow   = GetOrCreateMaterial("Mat_Chrono_Glow", new Color(0.65f, 0.15f, 0.85f), emission: new Color(0.90f, 0.20f, 1.0f) * 2.8f, smoothness: 0.6f);
            matFloor        = GetOrCreateMaterial("Mat_Floor", new Color(0.20f, 0.20f, 0.23f), smoothness: 0.15f);
            matWallPast     = GetOrCreateMaterial("Mat_Wall_Past", new Color(0.92f, 0.90f, 0.84f), smoothness: 0.2f); // Warm cream/white
            matWallPresent  = GetOrCreateMaterial("Mat_Wall_Present", new Color(0.28f, 0.24f, 0.22f), smoothness: 0.1f); // Dark desaturated dirty
            matMetalDark    = GetOrCreateMaterial("Mat_Metal_Dark", new Color(0.12f, 0.12f, 0.14f), smoothness: 0.6f);
            matGhostEcho    = GetOrCreateMaterial("Mat_Ghost_Echo", new Color(0.95f, 0.15f, 0.20f), emission: new Color(0.95f, 0.10f, 0.15f) * 1.8f, smoothness: 0.35f);

            AssetDatabase.SaveAssets();
            Debug.Log("[BuildTestScene] Step 5: Created prototype materials in Assets/Materials/Prototype/.");
        }

        private static Material GetOrCreateMaterial(string name, Color baseColor, Color? emission = null, float smoothness = 0.3f)
        {
            string path = $"{MAT_DIR}/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }

            mat.color = baseColor;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);

            if (emission.HasValue && emission.Value != Color.black)
            {
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emission.Value);
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ──────────────────────────────────────────────────
        // 6. ENVIRONMENT BASE & FLOOR
        // ──────────────────────────────────────────────────

        private static void BuildEnvironmentBase()
        {
            GameObject env = new GameObject("Environment_Base");

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(env.transform);
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(3f, 1f, 3f);
#pragma warning disable CS0618
            GameObjectUtility.SetStaticEditorFlags(floor, StaticEditorFlags.NavigationStatic);
#pragma warning restore CS0618

            GameObject sun = new GameObject("Directional Light");
            sun.transform.SetParent(env.transform);
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light sunLight = sun.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.intensity = 0.35f;
            sunLight.color = new Color(0.9f, 0.95f, 1f);

            Debug.Log("[BuildTestScene] Step 6: Created Floor and base directional light.");
        }

        // ──────────────────────────────────────────────────
        // 7. UI CANVAS & PROMPTS
        // ──────────────────────────────────────────────────

        private static void BuildHUD(out TextMeshProUGUI promptTMP, out CanvasGroup slatsCG, out Slider breathSlider)
        {
            GameObject canvasObj = new GameObject("HUD_Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasObj.AddComponent<GraphicRaycaster>();

            if (UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            defaultFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (defaultFont == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
                if (guids.Length > 0)
                    defaultFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            // Prompt
            GameObject promptObj = new GameObject("InteractPrompt");
            promptObj.transform.SetParent(canvasObj.transform, false);
            RectTransform promptRect = promptObj.AddComponent<RectTransform>();
            promptRect.anchorMin = new Vector2(0.5f, 0.5f);
            promptRect.anchorMax = new Vector2(0.5f, 0.5f);
            promptRect.pivot = new Vector2(0.5f, 0.5f);
            promptRect.anchoredPosition = new Vector2(0f, -70f);
            promptRect.sizeDelta = new Vector2(800f, 60f);

            promptTMP = promptObj.AddComponent<TextMeshProUGUI>();
            if (defaultFont != null) promptTMP.font = defaultFont;
            promptTMP.text = "Press [E] to interact";
            promptTMP.fontSize = 26;
            promptTMP.alignment = TextAlignmentOptions.Center;
            promptTMP.color = Color.white;
            promptTMP.enabled = false;

            // Controls Help
            GameObject helpObj = new GameObject("ControlsHelp");
            helpObj.transform.SetParent(canvasObj.transform, false);
            RectTransform helpRect = helpObj.AddComponent<RectTransform>();
            helpRect.anchorMin = new Vector2(0f, 1f);
            helpRect.anchorMax = new Vector2(0f, 1f);
            helpRect.pivot = new Vector2(0f, 1f);
            helpRect.anchoredPosition = new Vector2(25f, -25f);
            helpRect.sizeDelta = new Vector2(400f, 220f);

            TextMeshProUGUI helpTMP = helpObj.AddComponent<TextMeshProUGUI>();
            if (defaultFont != null) helpTMP.font = defaultFont;
            helpTMP.fontSize = 17;
            helpTMP.lineSpacing = 10f;
            helpTMP.color = new Color(1f, 1f, 1f, 0.85f);
            helpTMP.text = "<b>TIME FRACTURE - CONTROLS</b>\n" +
                           "• <b>WASD</b>: Walk\n" +
                           "• <b>Left Shift</b>: Sprint\n" +
                           "• <b>Left Ctrl</b>: Crouch (Toggle)\n" +
                           "• <b>F</b>: Flashlight\n" +
                           "• <b>E</b>: Interact / Exit Locker\n" +
                           "• <b>Space</b>: Hold Breath (when hidden)\n" +
                           "• <b>T</b>: Swap Timeline (Debug Key)";

            // Locker Slats Overlay
            GameObject slatsObj = new GameObject("LockerSlats_Overlay");
            slatsObj.transform.SetParent(canvasObj.transform, false);
            RectTransform slatsRect = slatsObj.AddComponent<RectTransform>();
            slatsRect.anchorMin = Vector2.zero;
            slatsRect.anchorMax = Vector2.one;
            slatsRect.offsetMin = Vector2.zero;
            slatsRect.offsetMax = Vector2.zero;

            slatsCG = slatsObj.AddComponent<CanvasGroup>();
            slatsCG.alpha = 0f;
            slatsCG.interactable = false;
            slatsCG.blocksRaycasts = false;
            slatsObj.SetActive(false);

            Image bg = slatsObj.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.02f, 0.03f, 0.85f);

            for (int i = -1; i <= 1; i++)
            {
                GameObject bar = new GameObject($"SlatBar_{i + 1}");
                bar.transform.SetParent(slatsObj.transform, false);
                RectTransform bRect = bar.AddComponent<RectTransform>();
                bRect.anchorMin = new Vector2(0f, 0.5f);
                bRect.anchorMax = new Vector2(1f, 0.5f);
                bRect.sizeDelta = new Vector2(0f, 110f);
                bRect.anchoredPosition = new Vector2(0f, i * 220f);
                Image bImg = bar.AddComponent<Image>();
                bImg.color = new Color(0.01f, 0.01f, 0.01f, 0.95f);
            }

            // Breath Bar
            GameObject breathObj = new GameObject("BreathBar");
            breathObj.transform.SetParent(canvasObj.transform, false);
            RectTransform bBarRect = breathObj.AddComponent<RectTransform>();
            bBarRect.anchorMin = new Vector2(0.5f, 0f);
            bBarRect.anchorMax = new Vector2(0.5f, 0f);
            bBarRect.pivot = new Vector2(0.5f, 0f);
            bBarRect.anchoredPosition = new Vector2(0f, 50f);
            bBarRect.sizeDelta = new Vector2(240f, 16f);

            breathSlider = breathObj.AddComponent<Slider>();
            breathSlider.minValue = 0f;
            breathSlider.maxValue = 1f;
            breathSlider.value = 1f;

            GameObject sliderBg = new GameObject("Background");
            sliderBg.transform.SetParent(breathObj.transform, false);
            RectTransform sBgRect = sliderBg.AddComponent<RectTransform>();
            sBgRect.anchorMin = Vector2.zero;
            sBgRect.anchorMax = Vector2.one;
            sBgRect.sizeDelta = Vector2.zero;
            Image sBgImg = sliderBg.AddComponent<Image>();
            sBgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.6f);

            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(breathObj.transform, false);
            RectTransform fAreaRect = fillArea.AddComponent<RectTransform>();
            fAreaRect.anchorMin = Vector2.zero;
            fAreaRect.anchorMax = Vector2.one;
            fAreaRect.sizeDelta = Vector2.zero;

            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            RectTransform fRect = fill.AddComponent<RectTransform>();
            fRect.anchorMin = Vector2.zero;
            fRect.anchorMax = Vector2.one;
            fRect.sizeDelta = Vector2.zero;
            Image fImg = fill.AddComponent<Image>();
            fImg.color = new Color(0.2f, 0.8f, 1f, 0.85f);

            breathSlider.fillRect = fRect;
            breathSlider.targetGraphic = fImg;
            breathObj.SetActive(false);

            // Game Over Overlay
            GameObject goObj = new GameObject("GameOver_Overlay");
            goObj.transform.SetParent(canvasObj.transform, false);
            RectTransform goRect = goObj.AddComponent<RectTransform>();
            goRect.anchorMin = Vector2.zero;
            goRect.anchorMax = Vector2.one;
            goRect.offsetMin = Vector2.zero;
            goRect.offsetMax = Vector2.zero;

            CanvasGroup goCG = goObj.AddComponent<CanvasGroup>();
            goCG.alpha = 0f;
            goCG.interactable = false;
            goCG.blocksRaycasts = false;

            Image goBg = goObj.AddComponent<Image>();
            goBg.color = new Color(0.06f, 0.01f, 0.01f, 0.95f);

            GameObject goTextObj = new GameObject("GameOverText");
            goTextObj.transform.SetParent(goObj.transform, false);
            RectTransform goTextRect = goTextObj.AddComponent<RectTransform>();
            goTextRect.anchorMin = new Vector2(0.5f, 0.5f);
            goTextRect.anchorMax = new Vector2(0.5f, 0.5f);
            goTextRect.pivot = new Vector2(0.5f, 0.5f);
            goTextRect.sizeDelta = new Vector2(900f, 300f);

            TextMeshProUGUI goTMP = goTextObj.AddComponent<TextMeshProUGUI>();
            if (defaultFont != null) goTMP.font = defaultFont;
            goTMP.fontSize = 42;
            goTMP.alignment = TextAlignmentOptions.Center;
            goTMP.color = new Color(0.95f, 0.2f, 0.2f);
            goTMP.text = "<b>TIMELINE SEVERED</b>\n<size=55%>You were consumed by The Echo.</size>";

            goObj.SetActive(false);

            // GameManager
            GameManager gm = canvasObj.AddComponent<GameManager>();
            SerializedObject soGM = new SerializedObject(gm);
            SafeSetObjectProperty(soGM, "gameOverOverlay", goCG, "GameManager");
            SafeSetObjectProperty(soGM, "gameOverText", goTMP, "GameManager");
            SafeSetFloatProperty(soGM, "fadeDuration", 1.5f, "GameManager");
            SafeSetBoolProperty(soGM, "autoRestart", true, "GameManager");
            SafeSetFloatProperty(soGM, "restartDelay", 3.0f, "GameManager");
            soGM.ApplyModifiedProperties();

            Debug.Log("[BuildTestScene] Step 7: Built HUD Canvas with prompts, controls, slats, breath bar, and GameManager overlay.");
        }

        // ──────────────────────────────────────────────────
        // 8. ERA SYSTEMS & URP VOLUME
        // ──────────────────────────────────────────────────

        private static void BuildEraSystems(out EraManager eraManager, out GlitchEffect glitchEffect)
        {
            if (!AssetDatabase.IsValidFolder(SETTINGS_DIR))
                AssetDatabase.CreateFolder("Assets", "Settings");

            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PROFILE_PATH);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, PROFILE_PATH);
            }

            if (!profile.Has<ChromaticAberration>()) profile.Add<ChromaticAberration>(true);
            if (!profile.Has<LensDistortion>()) profile.Add<LensDistortion>(true);
            if (!profile.Has<FilmGrain>()) profile.Add<FilmGrain>(true);
            if (!profile.Has<Vignette>()) profile.Add<Vignette>(true);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            GameObject eraObj = new GameObject("Era_Systems");
            eraManager = eraObj.AddComponent<EraManager>();

            SerializedObject soEra = new SerializedObject(eraManager);
            SafeSetEnumProperty(soEra, "initialEra", 0, "EraManager");
            SafeSetBoolProperty(soEra, "enableDebugKey", true, "EraManager");
            SafeSetKeyCodeProperty(soEra, "debugKey", KeyCode.T, "EraManager");
            soEra.ApplyModifiedProperties();

            Volume vol = eraObj.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.weight = 1f;
            vol.sharedProfile = profile;

            AudioSource eraAudio = eraObj.AddComponent<AudioSource>();
            eraAudio.playOnAwake = false;

            glitchEffect = eraObj.AddComponent<GlitchEffect>();
            SerializedObject soGlitch = new SerializedObject(glitchEffect);
            SafeSetObjectProperty(soGlitch, "volume", vol, "GlitchEffect");
            SafeSetObjectProperty(soGlitch, "audioSource", eraAudio, "GlitchEffect");
            SafeSetFloatProperty(soGlitch, "glitchDuration", 1.2f, "GlitchEffect");
            SafeSetFloatProperty(soGlitch, "playerFreezeDuration", 0.3f, "GlitchEffect");
            soGlitch.ApplyModifiedProperties();

            Debug.Log("[BuildTestScene] Step 8: Created Era_Systems (EraManager debugKey=T + GlitchEffect + URP Profile).");
        }

        // ──────────────────────────────────────────────────
        // 9. PLAYER
        // ──────────────────────────────────────────────────

        private static void BuildPlayer(TextMeshProUGUI promptTMP, CanvasGroup slatsCG, Slider breathSlider, out PlayerController playerController)
        {
            GameObject player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 0.05f, -6f);

            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer < 0) playerLayer = 6;
            player.layer = playerLayer;

            CharacterController cc = player.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.radius = 0.45f;
            cc.center = new Vector3(0f, 1f, 0f);

            AudioSource pAudio = player.AddComponent<AudioSource>();
            pAudio.playOnAwake = false;

            player.AddComponent<NoiseEmitter>();
            playerController = player.AddComponent<PlayerController>();
            PlayerLook playerLook = player.AddComponent<PlayerLook>();
            PlayerHide playerHide = player.AddComponent<PlayerHide>();

            GameObject camHolder = new GameObject("CameraHolder");
            camHolder.transform.SetParent(player.transform, false);
            camHolder.transform.localPosition = new Vector3(0f, 1.6f, 0f);

            GameObject camObj = new GameObject("Main Camera");
            camObj.transform.SetParent(camHolder.transform, false);
            camObj.transform.localPosition = Vector3.zero;
            camObj.transform.localRotation = Quaternion.identity;
            camObj.tag = "MainCamera";

            Camera cam = camObj.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            camObj.AddComponent<AudioListener>();

            var camData = camObj.GetComponent<UniversalAdditionalCameraData>();
            if (camData == null) camData = camObj.AddComponent<UniversalAdditionalCameraData>();
            if (camData != null) camData.renderPostProcessing = true;

            PlayerInteractor interactor = camObj.AddComponent<PlayerInteractor>();
            SerializedObject soInteractor = new SerializedObject(interactor);
            SafeSetObjectProperty(soInteractor, "promptText", promptTMP, "PlayerInteractor");
            SafeSetFloatProperty(soInteractor, "interactRange", 3.2f, "PlayerInteractor");
            SafeSetIntProperty(soInteractor, "interactLayer", ~0, "PlayerInteractor");
            soInteractor.ApplyModifiedProperties();

            // Flashlight
            GameObject flashObj = new GameObject("Flashlight");
            flashObj.transform.SetParent(camObj.transform, false);
            flashObj.transform.localPosition = new Vector3(0.2f, -0.2f, 0.3f);
            flashObj.transform.localRotation = Quaternion.identity;

            Light flashLight = flashObj.AddComponent<Light>();
            flashLight.type = LightType.Spot;
            flashLight.range = 18f;
            flashLight.spotAngle = 55f;
            flashLight.intensity = 2.5f;
            flashLight.color = new Color(1f, 0.98f, 0.9f);

            Flashlight flashScript = flashObj.AddComponent<Flashlight>();
            SerializedObject soFlash = new SerializedObject(flashScript);
            SafeSetObjectProperty(soFlash, "spotLight", flashLight, "Flashlight");
            soFlash.ApplyModifiedProperties();

            // Wire up Player references
            SerializedObject soPC = new SerializedObject(playerController);
            SafeSetObjectProperty(soPC, "cameraHolder", camHolder.transform, "PlayerController");
            SafeSetObjectProperty(soPC, "footstepAudioSource", pAudio, "PlayerController");
            soPC.ApplyModifiedProperties();

            SerializedObject soPL = new SerializedObject(playerLook);
            SafeSetObjectProperty(soPL, "cameraHolder", camHolder.transform, "PlayerLook");
            soPL.ApplyModifiedProperties();

            SerializedObject soPH = new SerializedObject(playerHide);
            SafeSetObjectProperty(soPH, "cameraHolder", camHolder.transform, "PlayerHide");
            SafeSetObjectProperty(soPH, "slatsOverlay", slatsCG, "PlayerHide");
            SafeSetObjectProperty(soPH, "breathBar", breathSlider, "PlayerHide");
            soPH.ApplyModifiedProperties();

            Debug.Log("[BuildTestScene] Step 9: Built Player at (0, 0, -6) with all components linked.");
        }

        // ──────────────────────────────────────────────────
        // 10. ROOM_PAST (Clean, Warm Tint, Movable Barrier, Past Locker)
        // ──────────────────────────────────────────────────

        private static void BuildRoomPast()
        {
            GameObject room = new GameObject("Room_Past");
            EraRoot root = room.AddComponent<EraRoot>();
            SerializedObject soRoot = new SerializedObject(root);
            SafeSetEnumProperty(soRoot, "assignedEra", 0, "Room_Past (EraRoot)");
            soRoot.ApplyModifiedProperties();

            // Bright Point Light
            GameObject light1 = new GameObject("LabLight_1");
            light1.transform.SetParent(room.transform);
            light1.transform.position = new Vector3(0f, 3.5f, 0f);
            Light l1 = light1.AddComponent<Light>();
            l1.type = LightType.Point;
            l1.range = 16f;
            l1.intensity = 20f;
            l1.color = new Color(1f, 0.96f, 0.88f); // Warm laboratory glow

            BuildRoomWalls(room.transform, matWallPast);

            int interactLayer = LayerMask.NameToLayer("Interactable");
            if (interactLayer < 0) interactLayer = 7;

            // Movable Barrier (Industrial Orange)
            GameObject barrierRoot = new GameObject("Past_Barrier");
            barrierRoot.transform.SetParent(room.transform);
            barrierRoot.transform.position = new Vector3(0f, 0f, 3f);
            barrierRoot.layer = interactLayer;

            // Collider on root
            BoxCollider bCol = barrierRoot.AddComponent<BoxCollider>();
            bCol.center = new Vector3(0f, 1.25f, 0f);
            bCol.size = new Vector3(2.6f, 2.5f, 0.6f);

            // Barrier Crossbeam
            GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = "Barrier_Beam";
            beam.transform.SetParent(barrierRoot.transform, false);
            beam.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            beam.transform.localScale = new Vector3(2.6f, 0.9f, 0.45f);
            beam.layer = interactLayer;
            beam.GetComponent<MeshRenderer>().sharedMaterial = matBarrier;
            UnityEngine.Object.DestroyImmediate(beam.GetComponent<Collider>());

            // Barrier Legs
            GameObject legL = GameObject.CreatePrimitive(PrimitiveType.Cube);
            legL.name = "Leg_Left";
            legL.transform.SetParent(barrierRoot.transform, false);
            legL.transform.localPosition = new Vector3(-1f, 0.65f, 0f);
            legL.transform.localScale = new Vector3(0.35f, 1.3f, 0.4f);
            legL.layer = interactLayer;
            legL.GetComponent<MeshRenderer>().sharedMaterial = matMetalDark;
            UnityEngine.Object.DestroyImmediate(legL.GetComponent<Collider>());

            GameObject legR = GameObject.CreatePrimitive(PrimitiveType.Cube);
            legR.name = "Leg_Right";
            legR.transform.SetParent(barrierRoot.transform, false);
            legR.transform.localPosition = new Vector3(1f, 0.65f, 0f);
            legR.transform.localScale = new Vector3(0.35f, 1.3f, 0.4f);
            legR.layer = interactLayer;
            legR.GetComponent<MeshRenderer>().sharedMaterial = matMetalDark;
            UnityEngine.Object.DestroyImmediate(legR.GetComponent<Collider>());

            EraLinkedObject linkedObj = barrierRoot.AddComponent<EraLinkedObject>();
            SerializedObject soBarrier = new SerializedObject(linkedObj);
            SafeSetStringProperty(soBarrier, "flagName", "BarrierMovedInPast", "Past_Barrier");
            SafeSetEnumProperty(soBarrier, "actionWhenFlagTrue", (int)LinkAction.MoveToOffsetWhenFlagTrue, "Past_Barrier");
            SafeSetVector3Property(soBarrier, "targetLocalOffset", new Vector3(3.2f, 0f, 0f), "Past_Barrier");
            SafeSetBoolProperty(soBarrier, "canInteract", true, "Past_Barrier");
            SafeSetStringProperty(soBarrier, "promptText", "Press [E] to move heavy barrier", "Past_Barrier");
            SafeSetStringProperty(soBarrier, "consumedPrompt", "Barrier has been moved aside", "Past_Barrier");
            soBarrier.ApplyModifiedProperties();

            // Floating Label for Barrier
            AddFloatingLabel(barrierRoot, "BARRIER [E to Move]", new Vector3(0f, 2.7f, 0f), color: new Color(1f, 0.6f, 0.1f));

            // Past Locker (Dark Green with vents)
            BuildDetailedLocker(room.transform, "Locker_Past", new Vector3(-4f, 0f, 0f), EraRestriction.PastOnly, interactLayer, "LOCKER (Past)");

            Debug.Log("[BuildTestScene] Step 10: Built Room_Past with warm tint, orange barrier, and Past locker.");
        }

        // ──────────────────────────────────────────────────
        // 11. ROOM_PRESENT (Decayed, Dim, Brown Door, Present Locker)
        // ──────────────────────────────────────────────────

        private static void BuildRoomPresent()
        {
            GameObject room = new GameObject("Room_Present");
            EraRoot root = room.AddComponent<EraRoot>();
            SerializedObject soRoot = new SerializedObject(root);
            SafeSetEnumProperty(soRoot, "assignedEra", 1, "Room_Present (EraRoot)");
            soRoot.ApplyModifiedProperties();

            // Dim Eerie Light
            GameObject light1 = new GameObject("DecayedLight_1");
            light1.transform.SetParent(room.transform);
            light1.transform.position = new Vector3(0f, 3.5f, 0f);
            Light l1 = light1.AddComponent<Light>();
            l1.type = LightType.Point;
            l1.range = 11f;
            l1.intensity = 4f;
            l1.color = new Color(1f, 0.35f, 0.2f); // Sickly amber-red

            BuildRoomWalls(room.transform, matWallPresent);

            int interactLayer = LayerMask.NameToLayer("Interactable");
            if (interactLayer < 0) interactLayer = 7;

            // Doorway Frame
            GameObject frameL = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frameL.name = "Frame_Left";
            frameL.transform.SetParent(room.transform);
            frameL.transform.position = new Vector3(-1.3f, 1.25f, 3f);
            frameL.transform.localScale = new Vector3(0.2f, 2.5f, 0.3f);
            frameL.GetComponent<MeshRenderer>().sharedMaterial = matMetalDark;

            GameObject frameR = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frameR.name = "Frame_Right";
            frameR.transform.SetParent(room.transform);
            frameR.transform.position = new Vector3(1.3f, 1.25f, 3f);
            frameR.transform.localScale = new Vector3(0.2f, 2.5f, 0.3f);
            frameR.GetComponent<MeshRenderer>().sharedMaterial = matMetalDark;

            GameObject frameTop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frameTop.name = "Frame_Top";
            frameTop.transform.SetParent(room.transform);
            frameTop.transform.position = new Vector3(0f, 2.45f, 3f);
            frameTop.transform.localScale = new Vector3(2.8f, 0.2f, 0.3f);
            frameTop.GetComponent<MeshRenderer>().sharedMaterial = matMetalDark;

            // Hinged Door (Brown wood with metal handle)
            GameObject doorHinge = new GameObject("Present_DoorHinge");
            doorHinge.transform.SetParent(room.transform);
            doorHinge.transform.position = new Vector3(-1.2f, 1.25f, 3f);
            doorHinge.layer = interactLayer;

            BoxCollider doorCol = doorHinge.AddComponent<BoxCollider>();
            doorCol.center = new Vector3(1.2f, 0f, 0f);
            doorCol.size = new Vector3(2.4f, 2.4f, 0.25f);

            GameObject doorMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            doorMesh.name = "Door_Panel";
            doorMesh.transform.SetParent(doorHinge.transform, false);
            doorMesh.transform.localPosition = new Vector3(1.2f, 0f, 0f);
            doorMesh.transform.localScale = new Vector3(2.35f, 2.35f, 0.08f);
            doorMesh.layer = interactLayer;
            doorMesh.GetComponent<MeshRenderer>().sharedMaterial = matDoor;
            UnityEngine.Object.DestroyImmediate(doorMesh.GetComponent<Collider>());

            // Metal Door Handle
            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handle.name = "Door_Handle";
            handle.transform.SetParent(doorMesh.transform, false);
            handle.transform.localPosition = new Vector3(0.38f, 0f, 0.08f);
            handle.transform.localScale = new Vector3(0.08f, 0.18f, 0.14f);
            handle.layer = interactLayer;
            handle.GetComponent<MeshRenderer>().sharedMaterial = matMetalDark;
            UnityEngine.Object.DestroyImmediate(handle.GetComponent<Collider>());

            Door doorScript = doorHinge.AddComponent<Door>();
            SerializedObject soDoor = new SerializedObject(doorScript);
            SafeSetBoolProperty(soDoor, "locked", true, "Present_DoorHinge");
            SafeSetStringProperty(soDoor, "requiredFlag", "BarrierMovedInPast", "Present_DoorHinge");
            SafeSetFloatProperty(soDoor, "openAngle", 90f, "Present_DoorHinge");
            SafeSetStringProperty(soDoor, "lockedPrompt", "Door jammed by debris (Locked)", "Present_DoorHinge");
            soDoor.ApplyModifiedProperties();

            // Floating Label for Door (auto-updates when flag changes!)
            AddFloatingLabel(doorHinge, "DOOR", new Vector3(1.2f, 1.5f, 0f), linkedFlag: "BarrierMovedInPast", lockedText: "DOOR (Locked)", unlockedText: "DOOR (Unlocked)");

            // Present Locker (Dark Green with vents)
            BuildDetailedLocker(room.transform, "Locker_Present", new Vector3(4f, 0f, 0f), EraRestriction.PresentOnly, interactLayer, "LOCKER (Present)");

            room.SetActive(false);

            Debug.Log("[BuildTestScene] Step 11: Built Room_Present with dim lighting, brown door with handle, and Present locker.");
        }

        // ──────────────────────────────────────────────────
        // 12. CHRONO-STABILIZER (Glowing Purple/Magenta Machine)
        // ──────────────────────────────────────────────────

        private static void BuildChronoStabilizer()
        {
            int interactLayer = LayerMask.NameToLayer("Interactable");
            if (interactLayer < 0) interactLayer = 7;

            GameObject machine = new GameObject("ChronoStabilizer");
            machine.transform.position = new Vector3(0f, 0f, -2.5f);
            machine.layer = interactLayer;

            BoxCollider mCol = machine.AddComponent<BoxCollider>();
            mCol.center = new Vector3(0f, 0.8f, 0f);
            mCol.size = new Vector3(1.3f, 1.6f, 1.3f);

            // Dark Tech Pedestal Base
            GameObject basePedestal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            basePedestal.name = "Pedestal_Base";
            basePedestal.transform.SetParent(machine.transform, false);
            basePedestal.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            basePedestal.transform.localScale = new Vector3(1.3f, 0.8f, 1.3f);
            basePedestal.layer = interactLayer;
            basePedestal.GetComponent<MeshRenderer>().sharedMaterial = matChronoBase;
            UnityEngine.Object.DestroyImmediate(basePedestal.GetComponent<Collider>());

            // Glowing Temporal Core (Purple / Magenta Emission)
            GameObject glowCore = GameObject.CreatePrimitive(PrimitiveType.Cube);
            glowCore.name = "Temporal_Core_Glow";
            glowCore.transform.SetParent(machine.transform, false);
            glowCore.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            glowCore.transform.localScale = new Vector3(0.7f, 0.6f, 0.7f);
            glowCore.layer = interactLayer;
            glowCore.GetComponent<MeshRenderer>().sharedMaterial = matChronoGlow;
            UnityEngine.Object.DestroyImmediate(glowCore.GetComponent<Collider>());

            // Local Point Light matching the core emission
            GameObject pLight = new GameObject("CoreLight");
            pLight.transform.SetParent(machine.transform, false);
            pLight.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            Light cl = pLight.AddComponent<Light>();
            cl.type = LightType.Point;
            cl.range = 6f;
            cl.intensity = 4.5f;
            cl.color = new Color(0.85f, 0.2f, 1.0f);

            // Terminal Screen
            GameObject screen = GameObject.CreatePrimitive(PrimitiveType.Cube);
            screen.name = "ConsoleScreen";
            screen.transform.SetParent(machine.transform, false);
            screen.transform.localPosition = new Vector3(0f, 0.95f, 0.62f);
            screen.transform.localScale = new Vector3(0.6f, 0.35f, 0.1f);
            screen.layer = interactLayer;
            screen.GetComponent<MeshRenderer>().sharedMaterial = matChronoGlow;
            UnityEngine.Object.DestroyImmediate(screen.GetComponent<Collider>());

            EraSwapTrigger trigger = machine.AddComponent<EraSwapTrigger>();
            SerializedObject soTrig = new SerializedObject(trigger);
            SafeSetStringProperty(soTrig, "activePrompt", "Press [E] to engage Chrono-Stabilizer", "ChronoStabilizer");
            SafeSetBoolProperty(soTrig, "triggerOnce", false, "ChronoStabilizer");
            SafeSetFloatProperty(soTrig, "noiseRadius", 12f, "ChronoStabilizer");
            soTrig.ApplyModifiedProperties();

            // Floating 3D Label
            AddFloatingLabel(machine, "SWAP MACHINE [T / E]", new Vector3(0f, 1.8f, 0f), color: new Color(0.9f, 0.4f, 1.0f));

            Debug.Log("[BuildTestScene] Step 12: Built Chrono-Stabilizer with glowing purple/magenta core.");
        }

        // ──────────────────────────────────────────────────
        // 13. WALL SWITCH (Yellow Interactive Switch)
        // ──────────────────────────────────────────────────

        private static void BuildWallSwitch()
        {
            int interactLayer = LayerMask.NameToLayer("Interactable");
            if (interactLayer < 0) interactLayer = 7;

            GameObject switchObj = new GameObject("Wall_Switch");
            switchObj.transform.position = new Vector3(5f, 1.5f, 2.85f);
            switchObj.layer = interactLayer;

            BoxCollider sCol = switchObj.AddComponent<BoxCollider>();
            sCol.center = Vector3.zero;
            sCol.size = new Vector3(0.5f, 0.7f, 0.25f);

            // Yellow Housing Box
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Switch_Box";
            box.transform.SetParent(switchObj.transform, false);
            box.transform.localPosition = Vector3.zero;
            box.transform.localScale = new Vector3(0.45f, 0.65f, 0.12f);
            box.layer = interactLayer;
            box.GetComponent<MeshRenderer>().sharedMaterial = matSwitch;
            UnityEngine.Object.DestroyImmediate(box.GetComponent<Collider>());

            // Switch Toggle Lever
            GameObject lever = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lever.name = "Switch_Lever";
            lever.transform.SetParent(switchObj.transform, false);
            lever.transform.localPosition = new Vector3(0f, 0.05f, 0.1f);
            lever.transform.localScale = new Vector3(0.12f, 0.25f, 0.12f);
            lever.layer = interactLayer;
            lever.GetComponent<MeshRenderer>().sharedMaterial = matMetalDark;
            UnityEngine.Object.DestroyImmediate(lever.GetComponent<Collider>());

            Switch switchScript = switchObj.AddComponent<Switch>();
            SerializedObject soSwitch = new SerializedObject(switchScript);
            SafeSetObjectProperty(soSwitch, "movingPart", lever.transform, "Wall_Switch");
            SafeSetStringProperty(soSwitch, "flagToSet", "BarrierMovedInPast", "Wall_Switch");
            SafeSetBoolProperty(soSwitch, "toggleState", true, "Wall_Switch");
            SafeSetStringProperty(soSwitch, "promptOff", "Press [E] to flip bypass switch", "Wall_Switch");
            SafeSetStringProperty(soSwitch, "promptOn", "Press [E] to reset bypass switch", "Wall_Switch");
            soSwitch.ApplyModifiedProperties();

            // Floating Label
            AddFloatingLabel(switchObj, "SWITCH [E to Flip]", new Vector3(0f, 0.65f, 0f), color: Color.yellow);

            Debug.Log("[BuildTestScene] Step 13: Built yellow Wall Switch at (5, 1.5, 2.85).");
        }

        // ──────────────────────────────────────────────────
        // 14. CROUCH TEST AREA
        // ──────────────────────────────────────────────────

        private static void BuildCrouchTestArea()
        {
            GameObject crouchArea = new GameObject("Crouch_Test_Area");

            // Low Ceiling slab (height = 1.6m, player cannot stand here!)
            GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.name = "LowCeiling_Slab";
            ceiling.transform.SetParent(crouchArea.transform);
            ceiling.transform.position = new Vector3(6f, 1.6f, -6f);
            ceiling.transform.localScale = new Vector3(4f, 0.2f, 4f);
            ceiling.GetComponent<MeshRenderer>().sharedMaterial = matMetalDark;

            int interactLayer = LayerMask.NameToLayer("Interactable");
            if (interactLayer < 0) interactLayer = 7;

            GameObject debugCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            debugCube.name = "DebugCube_UnderCeiling";
            debugCube.transform.SetParent(crouchArea.transform);
            debugCube.transform.position = new Vector3(6f, 0.4f, -6f);
            debugCube.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
            debugCube.layer = interactLayer;
            debugCube.GetComponent<MeshRenderer>().sharedMaterial = matSwitch;
            debugCube.AddComponent<DebugInteractable>();

            AddFloatingLabel(debugCube, "CROUCH TEST (Ctrl)", new Vector3(0f, 0.7f, 0f), color: Color.cyan);

            Debug.Log("[BuildTestScene] Step 14: Built Crouch Test area with low ceiling slab.");
        }

        // ──────────────────────────────────────────────────
        // 15. THE ECHO (Ghost #1 - Milestone 5)
        // ──────────────────────────────────────────────────

        private static void BuildEchoGhost()
        {
            // Waypoints parent
            GameObject wpParent = new GameObject("Echo_Waypoints");
            Vector3[] wpPositions = new Vector3[]
            {
                new Vector3(4f, 0f, 4f),
                new Vector3(4f, 0f, -4f),
                new Vector3(-4f, 0f, -4f),
                new Vector3(-4f, 0f, 4f)
            };

            Transform[] waypoints = new Transform[wpPositions.Length];
            for (int i = 0; i < wpPositions.Length; i++)
            {
                GameObject wp = new GameObject($"Echo_WP_{i + 1}");
                wp.transform.SetParent(wpParent.transform);
                wp.transform.position = wpPositions[i];
                waypoints[i] = wp.transform;
            }

            // Echo Ghost Root (Red-tinted Capsule placeholder)
            GameObject ghost = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            ghost.name = "Echo_Ghost";
            ghost.transform.position = new Vector3(4f, 1f, 4f);
            ghost.GetComponent<MeshRenderer>().sharedMaterial = matGhostEcho;

            // NavMeshAgent
            NavMeshAgent agent = ghost.AddComponent<NavMeshAgent>();
            agent.height = 2f;
            agent.radius = 0.5f;
            agent.speed = 2.0f;
            agent.angularSpeed = 240f;
            agent.acceleration = 12f;
            agent.stoppingDistance = 1.0f;

            // AudioSource
            AudioSource aSource = ghost.AddComponent<AudioSource>();
            aSource.playOnAwake = false;
            aSource.spatialBlend = 1.0f;
            aSource.minDistance = 2f;
            aSource.maxDistance = 20f;

            // Echo component
            Echo echoScript = ghost.AddComponent<Echo>();
            SerializedObject soEcho = new SerializedObject(echoScript);
            SafeSetObjectProperty(soEcho, "spawnPoint", waypoints[0], "Echo");
            SafeSetFloatProperty(soEcho, "hearingRadius", 16.0f, "Echo");
            SafeSetFloatProperty(soEcho, "loudNoiseThreshold", 10.0f, "Echo");
            SafeSetFloatProperty(soEcho, "repeatedNoiseWindow", 5.0f, "Echo");
            SafeSetFloatProperty(soEcho, "roamSpeed", 2.0f, "Echo");
            SafeSetFloatProperty(soEcho, "investigateSpeed", 3.2f, "Echo");
            SafeSetFloatProperty(soEcho, "huntSpeed", 5.0f, "Echo");
            SafeSetFloatProperty(soEcho, "searchSpeed", 2.4f, "Echo");
            SafeSetFloatProperty(soEcho, "cooldownSpeed", 1.5f, "Echo");
            SafeSetFloatProperty(soEcho, "catchChanceWhileBreathing", 0.90f, "Echo");
            SafeSetFloatProperty(soEcho, "catchChanceWhileHoldingBreath", 0.15f, "Echo");

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

            // Floating Label
            AddFloatingLabel(ghost, "THE ECHO (Ghost #1)", new Vector3(0f, 2.3f, 0f), color: new Color(1f, 0.25f, 0.25f));

            Debug.Log("[BuildTestScene] Step 15: Built The Echo ghost (capsule placeholder, NavMeshAgent, waypoints).");
        }

        // ──────────────────────────────────────────────────
        // HELPER: Build Detailed Locker
        // ──────────────────────────────────────────────────

        private static void BuildDetailedLocker(Transform parent, string name, Vector3 pos, EraRestriction restriction, int layer, string labelText)
        {
            GameObject locker = new GameObject(name);
            locker.transform.SetParent(parent);
            locker.transform.position = pos;
            locker.layer = layer;

            BoxCollider lCol = locker.AddComponent<BoxCollider>();
            lCol.center = new Vector3(0f, 1.2f, 0f);
            lCol.size = new Vector3(1f, 2.4f, 0.9f);

            // Main Dark Green Body
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Locker_Body";
            body.transform.SetParent(locker.transform, false);
            body.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            body.transform.localScale = new Vector3(0.95f, 2.38f, 0.85f);
            body.layer = layer;
            body.GetComponent<MeshRenderer>().sharedMaterial = matLocker;
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());

            // Vent Louvers Dark Strip
            GameObject vent = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vent.name = "Locker_VentStrip";
            vent.transform.SetParent(locker.transform, false);
            vent.transform.localPosition = new Vector3(0f, 1.85f, 0.44f);
            vent.transform.localScale = new Vector3(0.55f, 0.28f, 0.04f);
            vent.layer = layer;
            vent.GetComponent<MeshRenderer>().sharedMaterial = matLockerTrim;
            UnityEngine.Object.DestroyImmediate(vent.GetComponent<Collider>());

            // Small Handle
            GameObject lHandle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lHandle.name = "Locker_Handle";
            lHandle.transform.SetParent(locker.transform, false);
            lHandle.transform.localPosition = new Vector3(0.35f, 1.15f, 0.45f);
            lHandle.transform.localScale = new Vector3(0.05f, 0.2f, 0.06f);
            lHandle.layer = layer;
            lHandle.GetComponent<MeshRenderer>().sharedMaterial = matMetalDark;
            UnityEngine.Object.DestroyImmediate(lHandle.GetComponent<Collider>());

            // Inside Anchor
            GameObject hidePt = new GameObject("HidePoint");
            hidePt.transform.SetParent(locker.transform, false);
            hidePt.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            hidePt.transform.localRotation = Quaternion.identity;

            // Outside Anchor
            GameObject exitPt = new GameObject("ExitPoint");
            exitPt.transform.SetParent(locker.transform, false);
            exitPt.transform.localPosition = new Vector3(0f, 0f, 1.35f);
            exitPt.transform.localRotation = Quaternion.identity;

            HidingSpot spot = locker.AddComponent<HidingSpot>();
            SerializedObject soSpot = new SerializedObject(spot);
            SafeSetObjectProperty(soSpot, "hidePoint", hidePt.transform, name);
            SafeSetObjectProperty(soSpot, "exitPoint", exitPt.transform, name);
            SafeSetEnumProperty(soSpot, "eraRestriction", (int)restriction, name);
            SafeSetFloatProperty(soSpot, "enterDuration", 0.5f, name);
            SafeSetFloatProperty(soSpot, "exitDuration", 0.5f, name);
            SafeSetFloatProperty(soSpot, "maxHorizontalAngle", 35f, name);
            SafeSetFloatProperty(soSpot, "maxVerticalAngle", 20f, name);
            SafeSetStringProperty(soSpot, "enterPrompt", $"Press [E] to hide in {name}", name);
            soSpot.ApplyModifiedProperties();

            // Floating Label
            AddFloatingLabel(locker, labelText, new Vector3(0f, 2.65f, 0f), color: new Color(0.3f, 1f, 0.5f));
        }

        // ──────────────────────────────────────────────────
        // HELPER: Build Wall Box
        // ──────────────────────────────────────────────────

        private static void BuildRoomWalls(Transform parent, Material wallMat)
        {
            CreateWall(parent, "Wall_North", new Vector3(0f, 2.5f, 10f), new Vector3(20f, 5f, 0.5f), wallMat);
            CreateWall(parent, "Wall_South", new Vector3(0f, 2.5f, -10f), new Vector3(20f, 5f, 0.5f), wallMat);
            CreateWall(parent, "Wall_East", new Vector3(10f, 2.5f, 0f), new Vector3(0.5f, 5f, 20f), wallMat);
            CreateWall(parent, "Wall_West", new Vector3(-10f, 2.5f, 0f), new Vector3(0.5f, 5f, 20f), wallMat);
        }

        private static void CreateWall(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent);
            wall.transform.position = pos;
            wall.transform.localScale = scale;
            if (mat != null) wall.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        // ──────────────────────────────────────────────────
        // HELPER: Add Floating 3D Billboard Label
        // ──────────────────────────────────────────────────

        private static void AddFloatingLabel(GameObject parent, string baseText, Vector3 localOffset, string linkedFlag = "", string lockedText = "", string unlockedText = "", Color? color = null)
        {
            GameObject labelObj = new GameObject("Label_Billboard");
            labelObj.transform.SetParent(parent.transform, false);
            labelObj.transform.localPosition = localOffset;

            TextMeshPro tmp = labelObj.AddComponent<TextMeshPro>();
            if (defaultFont != null) tmp.font = defaultFont;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 3.8f;
            tmp.color = color ?? Color.white;
            tmp.text = baseText;

            WorldBillboardLabel billboard = labelObj.AddComponent<WorldBillboardLabel>();
            billboard.BaseText = baseText;
            if (!string.IsNullOrEmpty(linkedFlag))
            {
                SerializedObject soB = new SerializedObject(billboard);
                SafeSetStringProperty(soB, "linkedWorldStateFlag", linkedFlag, "Label");
                SafeSetStringProperty(soB, "lockedText", lockedText, "Label");
                SafeSetStringProperty(soB, "unlockedText", unlockedText, "Label");
                soB.ApplyModifiedProperties();
            }
        }

        // ──────────────────────────────────────────────────
        // SAFE PROPERTY SETTERS
        // ──────────────────────────────────────────────────

        private static bool SafeSetObjectProperty(SerializedObject so, string propertyName, UnityEngine.Object value, string context)
        {
            if (so == null) return false;
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogError($"[BuildTestScene] Missing property '{propertyName}' on {so.targetObject?.GetType().Name} ({context})!");
                return false;
            }
            prop.objectReferenceValue = value;
            return true;
        }

        private static bool SafeSetFloatProperty(SerializedObject so, string propertyName, float value, string context)
        {
            if (so == null) return false;
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogError($"[BuildTestScene] Missing float property '{propertyName}' on {so.targetObject?.GetType().Name} ({context})!");
                return false;
            }
            prop.floatValue = value;
            return true;
        }

        private static bool SafeSetBoolProperty(SerializedObject so, string propertyName, bool value, string context)
        {
            if (so == null) return false;
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogError($"[BuildTestScene] Missing bool property '{propertyName}' on {so.targetObject?.GetType().Name} ({context})!");
                return false;
            }
            prop.boolValue = value;
            return true;
        }

        private static bool SafeSetIntProperty(SerializedObject so, string propertyName, int value, string context)
        {
            if (so == null) return false;
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogError($"[BuildTestScene] Missing int property '{propertyName}' on {so.targetObject?.GetType().Name} ({context})!");
                return false;
            }
            prop.intValue = value;
            return true;
        }

        private static bool SafeSetStringProperty(SerializedObject so, string propertyName, string value, string context)
        {
            if (so == null) return false;
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogError($"[BuildTestScene] Missing string property '{propertyName}' on {so.targetObject?.GetType().Name} ({context})!");
                return false;
            }
            prop.stringValue = value;
            return true;
        }

        private static bool SafeSetEnumProperty(SerializedObject so, string propertyName, int enumValueIndex, string context)
        {
            if (so == null) return false;
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogError($"[BuildTestScene] Missing enum property '{propertyName}' on {so.targetObject?.GetType().Name} ({context})!");
                return false;
            }
            prop.enumValueIndex = enumValueIndex;
            return true;
        }

        private static bool SafeSetVector3Property(SerializedObject so, string propertyName, Vector3 value, string context)
        {
            if (so == null) return false;
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogError($"[BuildTestScene] Missing Vector3 property '{propertyName}' on {so.targetObject?.GetType().Name} ({context})!");
                return false;
            }
            prop.vector3Value = value;
            return true;
        }

        private static bool SafeSetKeyCodeProperty(SerializedObject so, string propertyName, KeyCode key, string context)
        {
            if (so == null) return false;
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogError($"[BuildTestScene] Missing KeyCode property '{propertyName}' on {so.targetObject?.GetType().Name} ({context})!");
                return false;
            }

            // KeyCode enum index in Unity SerializedProperty requires array index lookup
            string keyName = key.ToString();
            int index = Array.IndexOf(prop.enumNames, keyName);
            if (index >= 0)
            {
                prop.enumValueIndex = index;
            }
            else
            {
                prop.intValue = (int)key;
            }
            return true;
        }
    }
}
