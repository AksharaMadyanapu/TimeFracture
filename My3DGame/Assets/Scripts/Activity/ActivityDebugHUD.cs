// ============================================================
// ActivityDebugHUD.cs
// ------------------------------------------------------------
// Lightweight on-screen HUD showing the Ghost Activity Meter.
//
// FEATURES:
//   1. Toggle with [F1] at runtime (F2 = Echo HUD, F1 = Activity HUD).
//   2. Auto-created via RuntimeInitializeOnLoadMethod.
//   3. Draws a coloured bar (green → yellow → orange → red) and
//      the current tier name using Unity's immediate-mode GUI.
// ============================================================

using UnityEngine;
using TimeFracture.Activity;

namespace TimeFracture.Activity
{
    public class ActivityDebugHUD : MonoBehaviour
    {
        // ──────────────────────────────────────────────────
        // AUTO-BOOTSTRAP
        // ──────────────────────────────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (FindFirstObjectByType<ActivityDebugHUD>() == null)
            {
                GameObject go = new GameObject("ActivityDebugHUD");
                go.AddComponent<ActivityDebugHUD>();
                DontDestroyOnLoad(go);
            }
        }

        // ──────────────────────────────────────────────────
        // INTERNAL STATE
        // ──────────────────────────────────────────────────

        private bool _show = true;          // default ON so player sees it immediately
        private float _activity = 0f;
        private ActivityTier _tier = ActivityTier.Calm;

        private GUIStyle _boxStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _tierStyle;
        private bool _stylesInit = false;

        // ──────────────────────────────────────────────────
        // UNITY LIFECYCLE
        // ──────────────────────────────────────────────────

        private void OnEnable()
        {
            GhostActivityManager.OnActivityChanged += OnActivityChanged;
            GhostActivityManager.OnTierChanged     += OnTierChanged;
        }

        private void OnDisable()
        {
            GhostActivityManager.OnActivityChanged -= OnActivityChanged;
            GhostActivityManager.OnTierChanged     -= OnTierChanged;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
                _show = !_show;
        }

        // ──────────────────────────────────────────────────
        // EVENT HANDLERS
        // ──────────────────────────────────────────────────

        private void OnActivityChanged(float value)  => _activity = value;
        private void OnTierChanged(ActivityTier tier) => _tier = tier;

        // ──────────────────────────────────────────────────
        // GUI RENDERING
        // ──────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!_show) return;

            InitStyles();

            // Panel anchored to top-right below the Echo HUD (which is at y=15)
            float panelW = 280f;
            float panelH = 100f;
            float x = Screen.width  - panelW - 15f;
            float y = 270f;   // sit below the Echo HUD (240 + 15 + 15 gap)

            GUI.Box(new Rect(x, y, panelW, panelH), GUIContent.none, _boxStyle);

            // Title
            GUI.Label(new Rect(x + 8, y + 6, panelW - 16, 22),
                "<b>GHOST ACTIVITY  [F1 toggle]</b>", _labelStyle);

            // Tier name coloured text
            Color tierColor = TierColor(_tier);
            string tierStr = _tier.ToString().ToUpper();
            GUI.color = tierColor;
            GUI.Label(new Rect(x + 8, y + 26, panelW - 16, 22),
                $"<b>{tierStr}</b>", _tierStyle);
            GUI.color = Color.white;

            // Background track
            GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
            GUI.DrawTexture(new Rect(x + 8, y + 52, panelW - 16, 18), Texture2D.whiteTexture);

            // Filled bar
            float fillW = (panelW - 16f) * Mathf.Clamp01(_activity / 100f);
            GUI.color = tierColor;
            if (fillW > 0f)
                GUI.DrawTexture(new Rect(x + 8, y + 52, fillW, 18), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Numeric label inside bar
            GUI.Label(new Rect(x + 8, y + 51, panelW - 16, 20),
                $"  {_activity:F0} / 100", _labelStyle);

            // Tier threshold tick marks
            DrawTick(x + 8, y + 52, panelW - 16, 30f / 100f);  // Uneasy
            DrawTick(x + 8, y + 52, panelW - 16, 60f / 100f);  // Active
            DrawTick(x + 8, y + 52, panelW - 16, 90f / 100f);  // Hunting
        }

        // ──────────────────────────────────────────────────
        // HELPERS
        // ──────────────────────────────────────────────────

        private void DrawTick(float barX, float barY, float barW, float frac)
        {
            float tx = barX + barW * frac;
            GUI.color = new Color(1f, 1f, 1f, 0.5f);
            GUI.DrawTexture(new Rect(tx - 1f, barY, 2f, 18f), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private static Color TierColor(ActivityTier tier)
        {
            switch (tier)
            {
                case ActivityTier.Uneasy:  return new Color(1f, 0.85f, 0.1f);  // Yellow
                case ActivityTier.Active:  return new Color(1f, 0.45f, 0.1f);  // Orange
                case ActivityTier.Hunting: return new Color(1f, 0.15f, 0.15f); // Red
                default:                   return new Color(0.3f, 0.9f, 0.3f); // Green
            }
        }

        private void InitStyles()
        {
            if (_stylesInit) return;
            _stylesInit = true;

            _boxStyle = new GUIStyle(GUI.skin.box);
            _boxStyle.normal.background = MakeTex(new Color(0.05f, 0.05f, 0.1f, 0.82f));
            _boxStyle.border = new RectOffset(4, 4, 4, 4);

            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.fontSize    = 12;
            _labelStyle.richText    = true;
            _labelStyle.normal.textColor = Color.white;

            _tierStyle = new GUIStyle(_labelStyle);
            _tierStyle.fontSize = 14;
        }

        private static Texture2D MakeTex(Color c)
        {
            Texture2D t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }
    }
}
