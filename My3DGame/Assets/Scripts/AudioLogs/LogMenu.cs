// ============================================================
// LogMenu.cs
// ------------------------------------------------------------
// Replay and review interface for discovered audio logs.
// Pressing [Tab] toggles the code-built UI menu, unlocks the cursor,
// and lists all collected logs with a replay button and transcript.
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TimeFracture.Player;

namespace TimeFracture.AudioLogs
{
    public class LogMenu : MonoBehaviour
    {
        // ──────────────────────────────────────────────────
        // SINGLETON
        // ──────────────────────────────────────────────────

        public static LogMenu Instance { get; private set; }

        // ──────────────────────────────────────────────────
        // INSPECTOR FIELDS
        // ──────────────────────────────────────────────────

        [Header("Menu Configuration")]
        [Tooltip("Key used to toggle the log menu.")]
        [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

        [Header("UI Elements (Auto-Built in Code)")]
        [SerializeField] private GameObject menuRoot;
        [SerializeField] private Transform listContainer;
        [SerializeField] private TextMeshProUGUI transcriptTitleText;
        [SerializeField] private TextMeshProUGUI transcriptBodyText;

        // ──────────────────────────────────────────────────
        // INTERNAL STATE
        // ──────────────────────────────────────────────────

        private bool _isOpen = false;
        private Canvas _menuCanvas;
        private CursorLockMode _previousLockMode;
        private bool _previousCursorVisible;

        public bool IsOpen => _isOpen;

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

            BuildMenuUI();
            SetMenuOpen(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                SetMenuOpen(!_isOpen);
            }
        }

        // ──────────────────────────────────────────────────
        // PUBLIC API
        // ──────────────────────────────────────────────────

        public void SetMenuOpen(bool open)
        {
            _isOpen = open;

            if (menuRoot != null)
            {
                menuRoot.SetActive(_isOpen);
            }

            if (_isOpen)
            {
                _previousLockMode = Cursor.lockState;
                _previousCursorVisible = Cursor.visible;

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                RefreshLogList();
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        // ──────────────────────────────────────────────────
        // UI BUILDING (CODE-ONLY ZERO MANUAL SETUP)
        // ──────────────────────────────────────────────────

        private void BuildMenuUI()
        {
            if (menuRoot != null) return;

            // 1. Canvas
            GameObject canvasObj = new GameObject("LogMenu_Canvas");
            _menuCanvas = canvasObj.AddComponent<Canvas>();
            _menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _menuCanvas.sortingOrder = 80;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            // 2. Main Background Panel
            menuRoot = new GameObject("LogMenu_Panel");
            menuRoot.transform.SetParent(canvasObj.transform, false);

            Image panelBg = menuRoot.AddComponent<Image>();
            panelBg.color = new Color(0.03f, 0.04f, 0.06f, 0.92f); // Dark tactical terminal slate

            RectTransform rootRect = menuRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.12f, 0.10f);
            rootRect.anchorMax = new Vector2(0.88f, 0.90f);
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            // Header Title
            GameObject titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(menuRoot.transform, false);
            TextMeshProUGUI titleTMP = titleObj.AddComponent<TextMeshProUGUI>();
            titleTMP.text = "<color=#00E5FF><b>ARCHIVE LOGS</b></color>  <size=15><color=#888888>[TAB to Close]</color></size>";
            titleTMP.fontSize = 24f;

            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.04f, 0.90f);
            titleRect.anchorMax = new Vector2(0.96f, 0.98f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            // Left Side: Log List Container
            GameObject listObj = new GameObject("ListContainer");
            listObj.transform.SetParent(menuRoot.transform, false);
            VerticalLayoutGroup vlg = listObj.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10f;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;

            RectTransform listRect = listObj.GetComponent<RectTransform>();
            listRect.anchorMin = new Vector2(0.04f, 0.06f);
            listRect.anchorMax = new Vector2(0.42f, 0.88f);
            listRect.offsetMin = Vector2.zero;
            listRect.offsetMax = Vector2.zero;
            listContainer = listObj.transform;

            // Right Side: Transcript Box
            GameObject transcriptObj = new GameObject("TranscriptPanel");
            transcriptObj.transform.SetParent(menuRoot.transform, false);
            Image tBg = transcriptObj.AddComponent<Image>();
            tBg.color = new Color(0.06f, 0.08f, 0.12f, 0.65f);

            RectTransform tRect = transcriptObj.GetComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0.46f, 0.06f);
            tRect.anchorMax = new Vector2(0.96f, 0.88f);
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.zero;

            // Transcript Title
            GameObject tTitleObj = new GameObject("TranscriptTitle");
            tTitleObj.transform.SetParent(transcriptObj.transform, false);
            transcriptTitleText = tTitleObj.AddComponent<TextMeshProUGUI>();
            transcriptTitleText.fontSize = 20f;
            transcriptTitleText.color = new Color(0f, 0.9f, 1f);

            RectTransform ttRect = tTitleObj.GetComponent<RectTransform>();
            ttRect.anchorMin = new Vector2(0.04f, 0.88f);
            ttRect.anchorMax = new Vector2(0.96f, 0.98f);
            ttRect.offsetMin = Vector2.zero;
            ttRect.offsetMax = Vector2.zero;

            // Transcript Body
            GameObject tBodyObj = new GameObject("TranscriptBody");
            tBodyObj.transform.SetParent(transcriptObj.transform, false);
            transcriptBodyText = tBodyObj.AddComponent<TextMeshProUGUI>();
            transcriptBodyText.fontSize = 16f;
            transcriptBodyText.color = Color.white;
            transcriptBodyText.textWrappingMode = TextWrappingModes.Normal;

            RectTransform tbRect = tBodyObj.GetComponent<RectTransform>();
            tbRect.anchorMin = new Vector2(0.04f, 0.04f);
            tbRect.anchorMax = new Vector2(0.96f, 0.86f);
            tbRect.offsetMin = Vector2.zero;
            tbRect.offsetMax = Vector2.zero;
        }

        private void RefreshLogList()
        {
            if (listContainer == null) return;

            // Clear old entry buttons
            for (int i = listContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(listContainer.GetChild(i).gameObject);
            }

            IReadOnlyList<AudioLogData> logs = AudioLogPlayer.Instance != null
                ? AudioLogPlayer.Instance.CollectedLogs
                : new List<AudioLogData>();

            if (logs.Count == 0)
            {
                GameObject emptyMsg = new GameObject("EmptyMsg");
                emptyMsg.transform.SetParent(listContainer, false);
                TextMeshProUGUI msg = emptyMsg.AddComponent<TextMeshProUGUI>();
                msg.text = "<color=#666666><i>No audio recordings discovered yet.\nExplore the facility to find cassette logs.</i></color>";
                msg.fontSize = 16f;

                if (transcriptTitleText != null) transcriptTitleText.text = "No Log Selected";
                if (transcriptBodyText != null) transcriptBodyText.text = "Select a discovered recording on the left to review its transcript and replay audio.";
                return;
            }

            // Create button for each log
            for (int i = 0; i < logs.Count; i++)
            {
                AudioLogData log = logs[i];
                CreateLogButton(log);
            }

            // Default display to first log
            if (logs.Count > 0)
            {
                ShowTranscript(logs[0]);
            }
        }

        private void CreateLogButton(AudioLogData log)
        {
            GameObject btnObj = new GameObject($"LogEntry_{log.id}");
            btnObj.transform.SetParent(listContainer, false);

            Image img = btnObj.AddComponent<Image>();
            img.color = new Color(0.10f, 0.14f, 0.20f, 0.8f);

            Button btn = btnObj.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.highlightedColor = new Color(0.18f, 0.25f, 0.35f, 1f);
            cb.pressedColor = new Color(0f, 0.6f, 0.8f, 1f);
            btn.colors = cb;

            LayoutElement le = btnObj.AddComponent<LayoutElement>();
            le.minHeight = 44f;
            le.preferredHeight = 44f;

            GameObject textObj = new GameObject("Label");
            textObj.transform.SetParent(btnObj.transform, false);
            TextMeshProUGUI label = textObj.AddComponent<TextMeshProUGUI>();
            label.text = $"▶  {log.title}  <color=#888888>({log.eraFoundIn})</color>";
            label.fontSize = 16f;
            label.alignment = TextAlignmentOptions.MidlineLeft;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 4f);
            textRect.offsetMax = new Vector2(-12f, -4f);

            btn.onClick.AddListener(() =>
            {
                ShowTranscript(log);
                AudioLogPlayer.Instance?.PlayLog(log);
            });
        }

        private void ShowTranscript(AudioLogData log)
        {
            if (log == null) return;
            if (transcriptTitleText != null) transcriptTitleText.text = log.title + $" [{log.eraFoundIn} Era]";
            if (transcriptBodyText != null)
            {
                string text = !string.IsNullOrEmpty(log.fullTranscript)
                    ? log.fullTranscript
                    : BuildTranscriptFromSubtitles(log);
                transcriptBodyText.text = text;
            }
        }

        private string BuildTranscriptFromSubtitles(AudioLogData log)
        {
            if (log.subtitles == null || log.subtitles.Length == 0) return "(No transcription available)";
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < log.subtitles.Length; i++)
            {
                sb.AppendLine($"• \"{log.subtitles[i].text}\"");
            }
            return sb.ToString();
        }
    }
}
