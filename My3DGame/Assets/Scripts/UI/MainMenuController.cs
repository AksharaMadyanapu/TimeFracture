// ============================================================
// MainMenuController.cs
// ------------------------------------------------------------
// Controller for the MainMenu scene.
// Builds the menu UI in code on startup if not present:
//   - Start Game: loads TestScene
//   - Settings: Sensitivity, Master volume, SFX volume
//   - Quit Game
// Unlocks and shows cursor for desktop navigation.
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using TimeFracture.Audio;

namespace TimeFracture.UI
{
    public class MainMenuController : MonoBehaviour
    {
        [Header("Scene Configuration")]
        [Tooltip("Name of the playable gameplay scene to load.")]
        [SerializeField] private string gameSceneName = "TestScene";

        private GameObject _settingsPanel;
        private Slider _sensSlider;
        private Slider _volSlider;

        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            BuildMainMenuUI();
        }

        public void StartGame()
        {
            if (Application.CanStreamedLevelBeLoaded(gameSceneName))
            {
                SceneManager.LoadScene(gameSceneName);
            }
            else
            {
                SceneManager.LoadScene(1);
            }
        }

        public void ToggleSettings()
        {
            if (_settingsPanel != null)
            {
                _settingsPanel.SetActive(!_settingsPanel.activeSelf);
            }
        }

        public void QuitGame()
        {
            Debug.Log("[MainMenu] Quitting application.");
            Application.Quit();
        }

        private void BuildMainMenuUI()
        {
            if (GameObject.Find("MainMenu_Canvas") != null) return;

            GameObject canvasObj = new GameObject("MainMenu_Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            // Fullscreen Backdrop
            GameObject bgObj = new GameObject("Backdrop");
            bgObj.transform.SetParent(canvasObj.transform, false);
            Image bg = bgObj.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.03f, 0.04f, 1.0f);

            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // Title
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(canvasObj.transform, false);
            TextMeshProUGUI title = titleObj.AddComponent<TextMeshProUGUI>();
            title.text = "<color=#00E5FF><b>TIME FRACTURE</b></color>\n<size=20><color=#888888>A 3D Psychological Survival Horror Experience</color></size>";
            title.fontSize = 44f;
            title.alignment = TextAlignmentOptions.Center;

            RectTransform tRect = titleObj.GetComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0.1f, 0.70f);
            tRect.anchorMax = new Vector2(0.9f, 0.90f);
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.zero;

            // Buttons Container
            GameObject btnGroup = new GameObject("ButtonsContainer");
            btnGroup.transform.SetParent(canvasObj.transform, false);
            VerticalLayoutGroup vlg = btnGroup.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 16f;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;

            RectTransform bRect = btnGroup.GetComponent<RectTransform>();
            bRect.anchorMin = new Vector2(0.38f, 0.32f);
            bRect.anchorMax = new Vector2(0.62f, 0.65f);
            bRect.offsetMin = Vector2.zero;
            bRect.offsetMax = Vector2.zero;

            CreateMenuButton(btnGroup.transform, "ENTER THE FACILITY", StartGame, new Color(0.10f, 0.35f, 0.45f));
            CreateMenuButton(btnGroup.transform, "SETTINGS", ToggleSettings, new Color(0.14f, 0.17f, 0.22f));
            CreateMenuButton(btnGroup.transform, "QUIT EXPERIMENT", QuitGame, new Color(0.30f, 0.12f, 0.12f));

            // Settings Sub-Panel
            _settingsPanel = new GameObject("SettingsPanel");
            _settingsPanel.transform.SetParent(canvasObj.transform, false);

            Image sBg = _settingsPanel.AddComponent<Image>();
            sBg.color = new Color(0.06f, 0.08f, 0.12f, 0.95f);

            RectTransform sRect = _settingsPanel.GetComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0.30f, 0.08f);
            sRect.anchorMax = new Vector2(0.70f, 0.28f);
            sRect.offsetMin = Vector2.zero;
            sRect.offsetMax = Vector2.zero;

            _sensSlider = CreateSlider(_settingsPanel.transform, "Mouse Sensitivity", 0.5f, 5.0f, 2.0f, 0.55f, (val) =>
            {
                PlayerPrefs.SetFloat("MouseSensitivity", val);
            });

            _volSlider = CreateSlider(_settingsPanel.transform, "Master Volume", 0.0f, 1.0f, 1.0f, 0.15f, (val) =>
            {
                PlayerPrefs.SetFloat("MasterVolume", val);
                AudioListener.volume = val;
            });

            _settingsPanel.SetActive(false);
        }

        private Slider CreateSlider(Transform parent, string labelText, float min, float max, float defVal, float yNorm, UnityEngine.Events.UnityAction<float> onValChanged)
        {
            GameObject container = new GameObject($"Slider_{labelText}");
            container.transform.SetParent(parent, false);

            RectTransform cRect = container.AddComponent<RectTransform>();
            cRect.anchorMin = new Vector2(0.05f, yNorm);
            cRect.anchorMax = new Vector2(0.95f, yNorm + 0.35f);
            cRect.offsetMin = Vector2.zero;
            cRect.offsetMax = Vector2.zero;

            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(container.transform, false);
            TextMeshProUGUI label = labelObj.AddComponent<TextMeshProUGUI>();
            label.text = labelText;
            label.fontSize = 15f;
            label.color = Color.white;

            RectTransform lRect = labelObj.GetComponent<RectTransform>();
            lRect.anchorMin = new Vector2(0f, 0.5f);
            lRect.anchorMax = new Vector2(0.4f, 1f);
            lRect.offsetMin = Vector2.zero;
            lRect.offsetMax = Vector2.zero;

            GameObject sliderObj = new GameObject("Slider");
            sliderObj.transform.SetParent(container.transform, false);
            Slider slider = sliderObj.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = PlayerPrefs.GetFloat(labelText.Replace(" ", ""), defVal);
            slider.onValueChanged.AddListener(onValChanged);

            RectTransform slRect = sliderObj.GetComponent<RectTransform>();
            slRect.anchorMin = new Vector2(0.42f, 0.5f);
            slRect.anchorMax = new Vector2(0.98f, 1f);
            slRect.offsetMin = Vector2.zero;
            slRect.offsetMax = Vector2.zero;

            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(sliderObj.transform, false);
            Image bg = bgObj.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0.25f);
            bgRect.anchorMax = new Vector2(1f, 0.75f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            return slider;
        }

        private void CreateMenuButton(Transform parent, string label, UnityEngine.Events.UnityAction action, Color bgColor)
        {
            GameObject btnObj = new GameObject($"Btn_{label}");
            btnObj.transform.SetParent(parent, false);

            Image img = btnObj.AddComponent<Image>();
            img.color = bgColor;

            Button btn = btnObj.AddComponent<Button>();
            btn.onClick.AddListener(action);

            LayoutElement le = btnObj.AddComponent<LayoutElement>();
            le.minHeight = 52f;
            le.preferredHeight = 52f;

            GameObject textObj = new GameObject("Label");
            textObj.transform.SetParent(btnObj.transform, false);
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = $"<b>{label}</b>";
            tmp.fontSize = 19f;
            tmp.alignment = TextAlignmentOptions.Center;

            RectTransform tRect = textObj.GetComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.zero;
        }
    }
}
