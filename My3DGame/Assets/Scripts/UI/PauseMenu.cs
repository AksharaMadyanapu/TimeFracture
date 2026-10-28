// ============================================================
// PauseMenu.cs
// ------------------------------------------------------------
// In-game pause and settings interface.
// Pressing [Esc] pauses the game (Time.timeScale = 0), unlocks the cursor,
// and presents:
//   - Resume Game
//   - Settings: Mouse Sensitivity slider, Volume sliders (Master, SFX, Music)
//   - Return to Main Menu / Restart Scene
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using TimeFracture.Player;
using TimeFracture.Audio;

namespace TimeFracture.UI
{
    public class PauseMenu : MonoBehaviour
    {
        // ──────────────────────────────────────────────────
        // SINGLETON
        // ──────────────────────────────────────────────────

        public static PauseMenu Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private KeyCode pauseKey = KeyCode.Escape;

        private bool _isPaused = false;
        private GameObject _pauseRoot;
        private GameObject _settingsSubPanel;
        private Canvas _pauseCanvas;
        private Slider _sensSlider;
        private Slider _masterVolSlider;
        private Slider _sfxVolSlider;

        public bool IsPaused => _isPaused;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            BuildPauseUI();
            SetPaused(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(pauseKey))
            {
                SetPaused(!_isPaused);
            }
        }

        public void SetPaused(bool paused)
        {
            _isPaused = paused;
            Time.timeScale = _isPaused ? 0f : 1f;

            if (_pauseRoot != null)
            {
                _pauseRoot.SetActive(_isPaused);
                if (_settingsSubPanel != null) _settingsSubPanel.SetActive(false);
            }

            if (_isPaused)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                RefreshSettingsValues();
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        public void ResumeGame()
        {
            SetPaused(false);
        }

        public void ReturnToMainMenu()
        {
            Time.timeScale = 1f;
            if (Application.CanStreamedLevelBeLoaded("MainMenu"))
            {
                SceneManager.LoadScene("MainMenu");
            }
            else
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        }

        public void ToggleSettingsPanel()
        {
            if (_settingsSubPanel != null)
            {
                _settingsSubPanel.SetActive(!_settingsSubPanel.activeSelf);
            }
        }

        private void RefreshSettingsValues()
        {
            if (_sensSlider != null)
            {
                _sensSlider.value = PlayerPrefs.GetFloat("MouseSensitivity", 2.0f);
            }
            if (_masterVolSlider != null)
            {
                _masterVolSlider.value = PlayerPrefs.GetFloat("MasterVolume", 1.0f);
            }
            if (_sfxVolSlider != null)
            {
                _sfxVolSlider.value = PlayerPrefs.GetFloat("SFXVolume", 1.0f);
            }
        }

        // ──────────────────────────────────────────────────
        // CODE-BUILT UI
        // ──────────────────────────────────────────────────

        private void BuildPauseUI()
        {
            if (_pauseRoot != null) return;

            GameObject canvasObj = new GameObject("PauseMenu_Canvas");
            _pauseCanvas = canvasObj.AddComponent<Canvas>();
            _pauseCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _pauseCanvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            // Pause Root Panel
            _pauseRoot = new GameObject("PausePanel");
            _pauseRoot.transform.SetParent(canvasObj.transform, false);

            Image bg = _pauseRoot.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.03f, 0.05f, 0.88f);

            RectTransform rootRect = _pauseRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            // Title
            GameObject titleObj = new GameObject("PauseTitle");
            titleObj.transform.SetParent(_pauseRoot.transform, false);
            TextMeshProUGUI title = titleObj.AddComponent<TextMeshProUGUI>();
            title.text = "<color=#00E5FF><b>TIME FRACTURE — PAUSED</b></color>";
            title.fontSize = 32f;
            title.alignment = TextAlignmentOptions.Center;

            RectTransform tRect = titleObj.GetComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0.2f, 0.80f);
            tRect.anchorMax = new Vector2(0.8f, 0.90f);
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.zero;

            // Buttons Container
            GameObject btnGroup = new GameObject("ButtonsGroup");
            btnGroup.transform.SetParent(_pauseRoot.transform, false);
            VerticalLayoutGroup vlg = btnGroup.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 15f;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;

            RectTransform bgRect = btnGroup.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0.35f, 0.40f);
            bgRect.anchorMax = new Vector2(0.65f, 0.75f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            CreateMenuButton(btnGroup.transform, "RESUME GAME", ResumeGame, new Color(0.12f, 0.28f, 0.40f));
            CreateMenuButton(btnGroup.transform, "SETTINGS", ToggleSettingsPanel, new Color(0.18f, 0.20f, 0.25f));
            CreateMenuButton(btnGroup.transform, "RELOAD CHECKPOINT", () => { Time.timeScale = 1f; SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); }, new Color(0.18f, 0.20f, 0.25f));
            CreateMenuButton(btnGroup.transform, "RETURN TO MENU", ReturnToMainMenu, new Color(0.35f, 0.15f, 0.15f));

            // Settings Sub-Panel
            BuildSettingsSubPanel(_pauseRoot.transform);
        }

        private void BuildSettingsSubPanel(Transform parent)
        {
            _settingsSubPanel = new GameObject("SettingsSubPanel");
            _settingsSubPanel.transform.SetParent(parent, false);

            Image panelBg = _settingsSubPanel.AddComponent<Image>();
            panelBg.color = new Color(0.06f, 0.08f, 0.12f, 0.96f);

            RectTransform sRect = _settingsSubPanel.GetComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0.25f, 0.08f);
            sRect.anchorMax = new Vector2(0.75f, 0.38f);
            sRect.offsetMin = Vector2.zero;
            sRect.offsetMax = Vector2.zero;

            // Sensitivity Slider
            _sensSlider = CreateSlider(_settingsSubPanel.transform, "Mouse Sensitivity", 0.5f, 5.0f, 2.0f, 0.65f, (val) =>
            {
                PlayerPrefs.SetFloat("MouseSensitivity", val);
                PlayerLook pl = UnityEngine.Object.FindFirstObjectByType<PlayerLook>();
                if (pl != null) pl.SetSensitivity(val);
            });

            // Master Volume Slider
            _masterVolSlider = CreateSlider(_settingsSubPanel.transform, "Master Volume", 0.0f, 1.0f, 1.0f, 0.35f, (val) =>
            {
                AudioManager.Instance?.SetMasterVolume(val);
            });

            // SFX Volume Slider
            _sfxVolSlider = CreateSlider(_settingsSubPanel.transform, "SFX Volume", 0.0f, 1.0f, 1.0f, 0.05f, (val) =>
            {
                AudioManager.Instance?.SetSFXVolume(val);
            });

            _settingsSubPanel.SetActive(false);
        }

        private Slider CreateSlider(Transform parent, string labelText, float min, float max, float defVal, float yNorm, UnityEngine.Events.UnityAction<float> onValChanged)
        {
            GameObject container = new GameObject($"Slider_{labelText}");
            container.transform.SetParent(parent, false);

            RectTransform cRect = container.AddComponent<RectTransform>();
            cRect.anchorMin = new Vector2(0.05f, yNorm);
            cRect.anchorMax = new Vector2(0.95f, yNorm + 0.25f);
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

            // Unity UI Slider
            GameObject sliderObj = new GameObject("Slider");
            sliderObj.transform.SetParent(container.transform, false);
            Slider slider = sliderObj.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = defVal;
            slider.onValueChanged.AddListener(onValChanged);

            RectTransform slRect = sliderObj.GetComponent<RectTransform>();
            slRect.anchorMin = new Vector2(0.42f, 0.5f);
            slRect.anchorMax = new Vector2(0.98f, 1f);
            slRect.offsetMin = Vector2.zero;
            slRect.offsetMax = Vector2.zero;

            // Slider Background
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
            le.minHeight = 48f;
            le.preferredHeight = 48f;

            GameObject textObj = new GameObject("Label");
            textObj.transform.SetParent(btnObj.transform, false);
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = $"<b>{label}</b>";
            tmp.fontSize = 18f;
            tmp.alignment = TextAlignmentOptions.Center;

            RectTransform tRect = textObj.GetComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.zero;
        }
    }
}
