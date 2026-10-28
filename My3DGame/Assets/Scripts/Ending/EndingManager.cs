// ============================================================
// EndingManager.cs
// ------------------------------------------------------------
// Master terminal interface triggering the 3 story endings.
//
// ENDINGS:
//   (A) SHUT DOWN THE FACILITY — Purges temporal cores, seals the fracture.
//       Requires >= 2 Audio Logs collected.
//   (B) COMPLETE THE EXPERIMENT — Merges the fractured timelines permanently.
//       Requires >= 4 Audio Logs collected.
//   (C) ESCAPE INTO THE WASTELAND — Abandons the research facility.
//       Always available.
//
// FEATURES:
//   - Implements IInteractable for direct terminal interaction.
//   - Code-built atmospheric terminal UI and ending resolution screens.
//   - Automatically returns to Main Menu or reloads scene.
// ============================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using TimeFracture.Player;
using TimeFracture.World;

namespace TimeFracture.Ending
{
    public class EndingManager : MonoBehaviour, IInteractable
    {
        // ──────────────────────────────────────────────────
        // SINGLETON
        // ──────────────────────────────────────────────────

        public static EndingManager Instance { get; private set; }

        // ──────────────────────────────────────────────────
        // INSPECTOR FIELDS
        // ──────────────────────────────────────────────────

        [Header("Requirements")]
        [Tooltip("Number of audio logs required to unlock Ending A (Shutdown).")]
        [SerializeField] private int logsRequiredForShutdown = 2;

        [Tooltip("Number of audio logs required to unlock Ending B (Complete).")]
        [SerializeField] private int logsRequiredForComplete = 4;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip terminalHumClip;

        // ──────────────────────────────────────────────────
        // INTERNAL STATE
        // ──────────────────────────────────────────────────

        private bool _isTerminalOpen = false;
        private Canvas _endingCanvas;
        private GameObject _terminalUIRoot;
        private GameObject _endingScreenRoot;
        private TextMeshProUGUI _endingTitleText;
        private TextMeshProUGUI _endingNarrativeText;
        private TextMeshProUGUI _logCountText;

        // ──────────────────────────────────────────────────
        // UNITY LIFECYCLE
        // ──────────────────────────────────────────────────

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

            BuildEndingUI();
            CloseTerminal();
        }

        // ──────────────────────────────────────────────────
        // IINTERACTABLE IMPLEMENTATION
        // ──────────────────────────────────────────────────

        public string GetPrompt()
        {
            if (_isTerminalOpen) return string.Empty;
            return "Press [E] to access Master Override Terminal";
        }

        public void Interact(PlayerInteractor interactor)
        {
            OpenTerminal();
        }

        // ──────────────────────────────────────────────────
        // TERMINAL UI MANAGEMENT
        // ──────────────────────────────────────────────────

        public void OpenTerminal()
        {
            _isTerminalOpen = true;
            if (_terminalUIRoot != null) _terminalUIRoot.SetActive(true);
            if (_endingScreenRoot != null) _endingScreenRoot.SetActive(false);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            UpdateTerminalDisplay();
        }

        public void CloseTerminal()
        {
            _isTerminalOpen = false;
            if (_terminalUIRoot != null) _terminalUIRoot.SetActive(false);
            if (_endingScreenRoot != null) _endingScreenRoot.SetActive(false);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public int GetCollectedLogCount()
        {
            int count = 0;
            string[] logFlags = { "AudioLog_01", "AudioLog_02", "AudioLog_03", "AudioLog_04", "AudioLog_05", "AudioLog_06" };
            for (int i = 0; i < logFlags.Length; i++)
            {
                if (WorldState.HasFlag(logFlags[i])) count++;
            }
            return count;
        }

        private void UpdateTerminalDisplay()
        {
            int logs = GetCollectedLogCount();
            if (_logCountText != null)
            {
                _logCountText.text = $"<color=#888888>DECRYPTED RESEARCH LOGS:</color> <color=#00E5FF><b>{logs} / 6</b></color>";
            }
        }

        // ──────────────────────────────────────────────────
        // ENDING RESOLUTIONS
        // ──────────────────────────────────────────────────

        public void ChooseEndingA()
        {
            int logs = GetCollectedLogCount();
            if (logs < logsRequiredForShutdown)
            {
                Debug.LogWarning($"[EndingManager] Ending A requires {logsRequiredForShutdown} logs (Player has {logs}).");
                return;
            }

            ShowEnding(
                "ENDING A: THE CONTAINMENT PROTOCOL",
                "You initiate the emergency reactor purge sequence.\n\n" +
                "High-voltage magnetic clamps slam shut deep beneath the facility floor. " +
                "The temporal tear violently implodes into blinding blue Cherenkov radiation. " +
                "The Echo and The Residue vanish, their loops broken forever.\n\n" +
                "As the facility plunges into complete silence, you collapse against the console. " +
                "The timeline is sealed... but at the cost of burying the research forever."
            );
        }

        public void ChooseEndingB()
        {
            int logs = GetCollectedLogCount();
            if (logs < logsRequiredForComplete)
            {
                Debug.LogWarning($"[EndingManager] Ending B requires {logsRequiredForComplete} logs (Player has {logs}).");
                return;
            }

            ShowEnding(
                "ENDING B: PARADOX SYNTHESIS",
                "You align the harmonic phase emitters and execute Dr. Mercer's final equation.\n\n" +
                "The facility shudders as Past and Present merge into a permanent singularity. " +
                "Decayed desks stand beside running supercomputers. The dead walk the corridors " +
                "not as shrieking spectres, but as living statues frozen in eternal discovery.\n\n" +
                "You step out into a world where yesterday and tomorrow coexist. " +
                "Time has ceased to flow. You are the architect of eternity."
            );
        }

        public void ChooseEndingC()
        {
            ShowEnding(
                "ENDING C: THE DESERTION",
                "You reject the terminal controls, turning your back on the reactor.\n\n" +
                "Priying open the emergency ventilation hatch, you crawl into the freezing rain outside. " +
                "Behind you in the dark, the building groans as metal fatigue and temporal fractures " +
                "tear the facility apart.\n\n" +
                "You survived the night, but the anomalies remain active in the ruins. " +
                "Some truths are never meant to be solved."
            );
        }

        private void ShowEnding(string title, string narrative)
        {
            if (_terminalUIRoot != null) _terminalUIRoot.SetActive(false);
            if (_endingScreenRoot != null) _endingScreenRoot.SetActive(true);

            if (_endingTitleText != null) _endingTitleText.text = title;
            if (_endingNarrativeText != null) _endingNarrativeText.text = narrative;

            StartCoroutine(EndingCompletionRoutine());
        }

        private IEnumerator EndingCompletionRoutine()
        {
            yield return new WaitForSeconds(8.0f);

            // Return to MainMenu if scene exists in build, otherwise reload
            if (Application.CanStreamedLevelBeLoaded("MainMenu"))
            {
                SceneManager.LoadScene("MainMenu");
            }
            else
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        }

        // ──────────────────────────────────────────────────
        // CODE-BUILT UI (ZERO MANUAL SETUP)
        // ──────────────────────────────────────────────────

        private void BuildEndingUI()
        {
            if (_terminalUIRoot != null) return;

            GameObject canvasObj = new GameObject("EndingManager_Canvas");
            _endingCanvas = canvasObj.AddComponent<Canvas>();
            _endingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _endingCanvas.sortingOrder = 95;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            // ─── 1. Terminal Selection UI ───
            _terminalUIRoot = new GameObject("TerminalPanel");
            _terminalUIRoot.transform.SetParent(canvasObj.transform, false);

            Image termBg = _terminalUIRoot.AddComponent<Image>();
            termBg.color = new Color(0.04f, 0.05f, 0.08f, 0.94f);

            RectTransform termRect = _terminalUIRoot.GetComponent<RectTransform>();
            termRect.anchorMin = new Vector2(0.18f, 0.12f);
            termRect.anchorMax = new Vector2(0.82f, 0.88f);
            termRect.offsetMin = Vector2.zero;
            termRect.offsetMax = Vector2.zero;

            // Terminal Header
            GameObject hObj = new GameObject("Header");
            hObj.transform.SetParent(_terminalUIRoot.transform, false);
            TextMeshProUGUI headerTMP = hObj.AddComponent<TextMeshProUGUI>();
            headerTMP.text = "<color=#00E5FF><b>PROJECT TIME FRACTURE — MASTER OVERRIDE</b></color>";
            headerTMP.fontSize = 26f;
            headerTMP.alignment = TextAlignmentOptions.Center;

            RectTransform hRect = hObj.GetComponent<RectTransform>();
            hRect.anchorMin = new Vector2(0.05f, 0.86f);
            hRect.anchorMax = new Vector2(0.95f, 0.96f);
            hRect.offsetMin = Vector2.zero;
            hRect.offsetMax = Vector2.zero;

            // Decrypted count text
            GameObject cObj = new GameObject("CountText");
            cObj.transform.SetParent(_terminalUIRoot.transform, false);
            _logCountText = cObj.AddComponent<TextMeshProUGUI>();
            _logCountText.fontSize = 18f;
            _logCountText.alignment = TextAlignmentOptions.Center;

            RectTransform cRect = cObj.GetComponent<RectTransform>();
            cRect.anchorMin = new Vector2(0.05f, 0.78f);
            cRect.anchorMax = new Vector2(0.95f, 0.85f);
            cRect.offsetMin = Vector2.zero;
            cRect.offsetMax = Vector2.zero;

            // Options buttons
            CreateTerminalButton(_terminalUIRoot.transform, 0.55f, 0.70f,
                "OPTION A: INITIATE REACTOR SHUTDOWN (Requires >= 2 Logs)",
                new Color(0.15f, 0.35f, 0.50f, 0.9f), ChooseEndingA);

            CreateTerminalButton(_terminalUIRoot.transform, 0.38f, 0.53f,
                "OPTION B: COMPLETE THE PARADOX EXPERIMENT (Requires >= 4 Logs)",
                new Color(0.45f, 0.15f, 0.50f, 0.9f), ChooseEndingB);

            CreateTerminalButton(_terminalUIRoot.transform, 0.21f, 0.36f,
                "OPTION C: ABANDON FACILITY & ESCAPE (Always Available)",
                new Color(0.35f, 0.35f, 0.35f, 0.9f), ChooseEndingC);

            CreateTerminalButton(_terminalUIRoot.transform, 0.05f, 0.17f,
                "CANCEL & RETURN TO FACILITY",
                new Color(0.2f, 0.2f, 0.2f, 0.8f), CloseTerminal);

            // ─── 2. Ending Screen UI ───
            _endingScreenRoot = new GameObject("EndingScreen");
            _endingScreenRoot.transform.SetParent(canvasObj.transform, false);

            Image endBg = _endingScreenRoot.AddComponent<Image>();
            endBg.color = new Color(0.01f, 0.01f, 0.02f, 0.98f); // Pitch black cinematic backdrop

            RectTransform endRect = _endingScreenRoot.GetComponent<RectTransform>();
            endRect.anchorMin = Vector2.zero;
            endRect.anchorMax = Vector2.one;
            endRect.offsetMin = Vector2.zero;
            endRect.offsetMax = Vector2.zero;

            // Ending Title
            GameObject etObj = new GameObject("EndingTitle");
            etObj.transform.SetParent(_endingScreenRoot.transform, false);
            _endingTitleText = etObj.AddComponent<TextMeshProUGUI>();
            _endingTitleText.fontSize = 28f;
            _endingTitleText.color = new Color(0f, 0.9f, 1f);
            _endingTitleText.alignment = TextAlignmentOptions.Center;

            RectTransform etRect = etObj.GetComponent<RectTransform>();
            etRect.anchorMin = new Vector2(0.1f, 0.75f);
            etRect.anchorMax = new Vector2(0.9f, 0.90f);
            etRect.offsetMin = Vector2.zero;
            etRect.offsetMax = Vector2.zero;

            // Ending Narrative
            GameObject enObj = new GameObject("EndingNarrative");
            enObj.transform.SetParent(_endingScreenRoot.transform, false);
            _endingNarrativeText = enObj.AddComponent<TextMeshProUGUI>();
            _endingNarrativeText.fontSize = 20f;
            _endingNarrativeText.color = Color.white;
            _endingNarrativeText.alignment = TextAlignmentOptions.Center;
            _endingNarrativeText.textWrappingMode = TextWrappingModes.Normal;

            RectTransform enRect = enObj.GetComponent<RectTransform>();
            enRect.anchorMin = new Vector2(0.15f, 0.20f);
            enRect.anchorMax = new Vector2(0.85f, 0.70f);
            enRect.offsetMin = Vector2.zero;
            enRect.offsetMax = Vector2.zero;
        }

        private void CreateTerminalButton(Transform parent, float yMin, float yMax, string label, Color bgColor, UnityEngine.Events.UnityAction action)
        {
            GameObject btnObj = new GameObject("Btn_" + label.Substring(0, Mathf.Min(12, label.Length)));
            btnObj.transform.SetParent(parent, false);

            Image img = btnObj.AddComponent<Image>();
            img.color = bgColor;

            Button btn = btnObj.AddComponent<Button>();
            btn.onClick.AddListener(action);

            RectTransform rect = btnObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.08f, yMin);
            rect.anchorMax = new Vector2(0.92f, yMax);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = $"<b>{label}</b>";
            tmp.fontSize = 17f;
            tmp.alignment = TextAlignmentOptions.Center;

            RectTransform tRect = textObj.GetComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.zero;
        }
    }
}
