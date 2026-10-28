// ============================================================
// GameManager.cs
// ------------------------------------------------------------
// Central session and game loop controller for Time Fracture.
//
// RESPONSIBILITIES:
//   1. Tracks game states (Playing, GameOver, Paused).
//   2. Listens to GhostBase.OnPlayerKilled to trigger the fail state.
//   3. Freezes player movement, displays game over overlay, and fades screen.
//   4. Handles scene reload or checkpoint restart.
// ============================================================

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using TimeFracture.Player;
using TimeFracture.Ghost;

namespace TimeFracture.Game
{
    public class GameManager : MonoBehaviour
    {
        // ──────────────────────────────────────────────────
        // SINGLETON
        // ──────────────────────────────────────────────────

        private static GameManager _instance;
        public static GameManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<GameManager>();
                    if (_instance == null)
                    {
                        GameObject gmObj = new GameObject("GameManager");
                        _instance = gmObj.AddComponent<GameManager>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        // ──────────────────────────────────────────────────
        // INSPECTOR FIELDS
        // ──────────────────────────────────────────────────

        [Header("Fail State UI & Effects")]
        [Tooltip("CanvasGroup for the game over screen (fades from alpha 0 to 1).")]
        [SerializeField] private CanvasGroup gameOverOverlay;
        public CanvasGroup GameOverOverlay => gameOverOverlay;

        [Tooltip("TextMeshPro text showing the death cause / message.")]
        [SerializeField] private TextMeshProUGUI gameOverText;
        public TextMeshProUGUI GameOverText => gameOverText;

        [Tooltip("Duration of the death screen fade in seconds.")]
        [SerializeField] private float fadeDuration = 1.5f;

        [Header("Restart Options")]
        [Tooltip("When true, the scene automatically reloads after delay. When false, waits for [R] key.")]
        [SerializeField] private bool autoRestart = true;

        [Tooltip("Delay in seconds after fade before reloading scene.")]
        [SerializeField] private float restartDelay = 3.0f;

        [Tooltip("Key to manually restart after death.")]
        [SerializeField] private KeyCode restartKey = KeyCode.R;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip deathSting;

        // ──────────────────────────────────────────────────
        // INTERNAL STATE
        // ──────────────────────────────────────────────────

        private bool _isGameOver = false;
        private PlayerController _playerController;

        public bool IsGameOver => _isGameOver;

        // ──────────────────────────────────────────────────
        // UNITY LIFECYCLE
        // ──────────────────────────────────────────────────

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            SelfWireGameManager();
        }

        private void Reset()
        {
            SelfWireGameManager();
        }

        private void SelfWireGameManager()
        {
            if (_playerController == null)
            {
                _playerController = FindFirstObjectByType<PlayerController>();
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
            }

            if (deathSting == null)
            {
                deathSting = GenerateDeathStingClip();
            }

            // Self-wire or code-generate Game Over UI (Rule 2 & 3)
            if (gameOverOverlay == null)
            {
                CanvasGroup foundCG = GameObject.Find("GameOver_Overlay")?.GetComponent<CanvasGroup>();
                if (foundCG != null)
                {
                    gameOverOverlay = foundCG;
                    gameOverText = foundCG.GetComponentInChildren<TextMeshProUGUI>();
                }
                else
                {
                    CreateCodeGeneratedGameOverUI();
                }
            }

            if (gameOverOverlay != null)
            {
                gameOverOverlay.alpha = 0f;
                gameOverOverlay.gameObject.SetActive(false);
            }
        }

        private void CreateCodeGeneratedGameOverUI()
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            GameObject canvasObj;
            if (canvas == null)
            {
                canvasObj = new GameObject("HUD_Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 999;
                UnityEngine.UI.CanvasScaler scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }
            else
            {
                canvasObj = canvas.gameObject;
            }

            GameObject overlayObj = new GameObject("GameOver_Overlay");
            overlayObj.transform.SetParent(canvasObj.transform, false);
            RectTransform rect = overlayObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            gameOverOverlay = overlayObj.AddComponent<CanvasGroup>();
            gameOverOverlay.alpha = 0f;
            gameOverOverlay.interactable = false;
            gameOverOverlay.blocksRaycasts = false;

            UnityEngine.UI.Image bg = overlayObj.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0.06f, 0.01f, 0.01f, 0.95f);

            GameObject textObj = new GameObject("GameOverText");
            textObj.transform.SetParent(overlayObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.sizeDelta = new Vector2(900f, 300f);

            gameOverText = textObj.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null) gameOverText.font = font;
            gameOverText.fontSize = 44;
            gameOverText.alignment = TextAlignmentOptions.Center;
            gameOverText.color = new Color(0.95f, 0.15f, 0.2f);
            gameOverText.text = "<b>TIMELINE SEVERED</b>\n<size=55%>You were consumed by The Echo.</size>";

            overlayObj.SetActive(false);
            Debug.Log("[GameManager] Auto-generated Game Over fade UI canvas.");
        }

        private static AudioClip GenerateDeathStingClip()
        {
            int sampleRate = 44100;
            int count = sampleRate; // 1 second
            float[] data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float env = 1f - (float)i / count;
                // Dissonant minor second chord tone (220Hz + 233Hz)
                data[i] = (Mathf.Sin(2 * Mathf.PI * 220f * t) * 0.4f + Mathf.Sin(2 * Mathf.PI * 233.08f * t) * 0.4f) * env;
            }
            AudioClip clip = AudioClip.Create("GameManager_DeathSting", count, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void OnEnable()
        {
            GhostBase.OnPlayerKilled += HandlePlayerKilled;
        }

        private void OnDisable()
        {
            GhostBase.OnPlayerKilled -= HandlePlayerKilled;
        }

        private void Update()
        {
            if (_isGameOver && !autoRestart && Input.GetKeyDown(restartKey))
            {
                RestartScene();
            }
        }

        // ──────────────────────────────────────────────────
        // FAIL STATE LOGIC
        // ──────────────────────────────────────────────────

        private void HandlePlayerKilled()
        {
            if (_isGameOver) return;
            _isGameOver = true;

            Debug.Log("[GameManager] Player caught! Triggering death sequence.");

            // Disable player movement
            if (_playerController == null) _playerController = FindAnyObjectByType<PlayerController>();
            if (_playerController != null)
            {
                _playerController.CanMove = false;
            }

            if (audioSource != null && deathSting != null)
            {
                audioSource.PlayOneShot(deathSting);
            }

            StartCoroutine(GameOverSequence());
        }

        private IEnumerator GameOverSequence()
        {
            if (gameOverOverlay != null)
            {
                gameOverOverlay.gameObject.SetActive(true);
                gameOverOverlay.alpha = 0f;

                if (gameOverText != null)
                {
                    gameOverText.text = "TIMELINE SEVERED\n<size=50%>You were consumed by The Echo.</size>";
                }

                float elapsed = 0f;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.deltaTime;
                    gameOverOverlay.alpha = Mathf.Clamp01(elapsed / fadeDuration);
                    yield return null;
                }
                gameOverOverlay.alpha = 1f;
            }

            if (autoRestart)
            {
                yield return new WaitForSeconds(restartDelay);
                RestartScene();
            }
            else
            {
                if (gameOverText != null)
                {
                    gameOverText.text += "\n\n<size=40%>Press [R] to reload timeline</size>";
                }
            }
        }

        /// <summary>
        /// Reloads the active scene to restore gameplay.
        /// </summary>
        public void RestartScene()
        {
            Debug.Log("[GameManager] Restarting scene...");
            Scene activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.buildIndex);
        }
    }
}
