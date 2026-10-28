// ============================================================
// DebugGhostHUD.cs
// ------------------------------------------------------------
// Lightweight on-screen diagnostic HUD for ghost AI verification.
//
// FEATURES:
//   1. Toggled with [F2] at runtime.
//   2. Auto-created on scene load if missing.
//   3. Displays: Current Era, Echo State, Distance to Player,
//      Player Hiding Status, Breath-Hold Status, Recent Noises.
// ============================================================

using UnityEngine;
using TimeFracture.Era;
using TimeFracture.Player;
using TimeFracture.Hiding;

namespace TimeFracture.Ghost
{
    public class DebugGhostHUD : MonoBehaviour
    {
        private bool _showHUD = true;
        private Echo _echo;
        private GUIStyle _boxStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _stateStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (FindFirstObjectByType<DebugGhostHUD>() == null)
            {
                GameObject hudObj = new GameObject("DebugGhostHUD");
                hudObj.AddComponent<DebugGhostHUD>();
                DontDestroyOnLoad(hudObj);
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F2))
            {
                _showHUD = !_showHUD;
            }

            if (_echo == null)
            {
                _echo = FindFirstObjectByType<Echo>();
            }
        }

        private void OnGUI()
        {
            if (!_showHUD) return;

            InitStyles();

            GUILayout.BeginArea(new Rect(15, 15, 340, 240), _boxStyle);
            GUILayout.Label("<b>THE ECHO — DEBUG HUD [F2 to toggle]</b>", _headerStyle);
            GUILayout.Space(6);

            // 1. Current Era
            string eraName = EraManager.Instance != null ? EraManager.Instance.CurrentEra.ToString() : "Unknown";
            Color eraColor = (EraManager.Instance != null && EraManager.Instance.CurrentEra == TimeFracture.Era.Era.Past)
                ? new Color(0.4f, 1f, 0.4f)
                : new Color(0.7f, 0.7f, 0.7f);
            DrawField("Timeline Era", eraName, eraColor);

            // 2. Ghost Presence & State
            if (_echo != null)
            {
                string stateName = _echo.CurrentState.ToString();
                Color stateColor;
                switch (_echo.CurrentState)
                {
                    case GhostState.Hunt:
                        stateColor = new Color(1f, 0.2f, 0.2f); // Red
                        break;
                    case GhostState.Investigate:
                        stateColor = new Color(1f, 0.85f, 0.1f); // Yellow
                        break;
                    case GhostState.Search:
                        stateColor = new Color(1f, 0.5f, 0.1f); // Orange
                        break;
                    default:
                        stateColor = new Color(0.3f, 0.8f, 1f); // Cyan
                        break;
                }

                DrawField("Echo State", stateName, stateColor);

                // Distance to player
                Transform pTrans = Camera.main != null ? Camera.main.transform.root : null;
                if (pTrans != null)
                {
                    float dist = Vector3.Distance(_echo.transform.position, pTrans.position);
                    DrawField("Distance to Player", $"{dist:F1} m", dist < 5f ? Color.red : Color.white);
                }
            }
            else
            {
                DrawField("The Echo", "Not Found in Scene", Color.red);
            }

            // 3. Player Stealth Status
            bool isHidden = PlayerHide.IsHidden;
            bool isHoldingBreath = PlayerHide.IsPlayerHoldingBreath;

            DrawField("Player Hidden", isHidden ? "YES (In Locker)" : "NO (Exposed)", isHidden ? Color.green : Color.yellow);
            DrawField("Holding Breath [Space]", isHoldingBreath ? "YES (Silent)" : "NO (Breathing)", isHoldingBreath ? Color.cyan : Color.white);

            GUILayout.EndArea();
        }

        private void DrawField(string label, string value, Color valColor)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label + ":", _labelStyle, GUILayout.Width(170));
            Color old = GUI.color;
            GUI.color = valColor;
            GUILayout.Label(value, _stateStyle);
            GUI.color = old;
            GUILayout.EndHorizontal();
        }

        private void InitStyles()
        {
            if (_boxStyle == null)
            {
                Texture2D darkBg = new Texture2D(1, 1);
                darkBg.SetPixel(0, 0, new Color(0.05f, 0.05f, 0.08f, 0.88f));
                darkBg.Apply();

                _boxStyle = new GUIStyle(GUI.skin.box);
                _boxStyle.normal.background = darkBg;
                _boxStyle.padding = new RectOffset(12, 12, 10, 10);

                _headerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                _headerStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);

                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Normal
                };
                _labelStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);

                _stateStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold
                };
            }
        }
    }
}
