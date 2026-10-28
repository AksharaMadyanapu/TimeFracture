// ============================================================
// WorldBillboardLabel.cs
// ------------------------------------------------------------
// Floating 3D world-space text label (TextMeshPro) that:
//   1. Always turns to face the player camera (billboarding).
//   2. Fades / hides when further than maxDistance (default 6m)
//      so the screen doesn't get cluttered.
//   3. Automatically updates its label and color if linked to
//      a WorldState flag (e.g. "DOOR (Locked)" -> "DOOR (Unlocked)").
// ============================================================

using UnityEngine;
using TMPro;

namespace TimeFracture.World
{
    [RequireComponent(typeof(TextMeshPro))]
    public class WorldBillboardLabel : MonoBehaviour
    {
        [Header("Label Content")]
        [Tooltip("The text displayed on this floating label.")]
        [SerializeField] private string baseText = "INTERACTABLE";

        [Tooltip("Maximum distance in metres before this label hides.")]
        [SerializeField] private float maxDistance = 6f;

        [Tooltip("Default color when not flag-driven.")]
        [SerializeField] private Color normalColor = Color.white;

        [Header("WorldState Dynamic State (Optional)")]
        [Tooltip("Optional WorldState flag name to monitor (e.g. 'BarrierMovedInPast').")]
        [SerializeField] private string linkedWorldStateFlag = "";

        [Tooltip("Text shown when the flag is false (e.g. door locked).")]
        [SerializeField] private string lockedText = "DOOR (Locked)";

        [Tooltip("Text shown when the flag is true (e.g. door unlocked).")]
        [SerializeField] private string unlockedText = "DOOR (Unlocked)";

        private TextMeshPro _tmp;
        private Transform _camTransform;

        public string BaseText
        {
            get => baseText;
            set
            {
                baseText = value;
                if (_tmp != null && string.IsNullOrEmpty(linkedWorldStateFlag))
                    _tmp.text = baseText;
            }
        }

        public string LinkedFlag
        {
            get => linkedWorldStateFlag;
            set => linkedWorldStateFlag = value;
        }

        private void Awake()
        {
            _tmp = GetComponent<TextMeshPro>();
            _tmp.alignment = TextAlignmentOptions.Center;
            _tmp.fontSize = 3.5f;
            _tmp.color = normalColor;
            _tmp.text = baseText;
        }

        private void Start()
        {
            UpdateCameraRef();
            UpdateLabelContent();
        }

        private void LateUpdate()
        {
            if (_camTransform == null || !_camTransform.gameObject.activeInHierarchy)
            {
                UpdateCameraRef();
                if (_camTransform == null) return;
            }

            // Distance check: hide when further than maxDistance
            float dist = Vector3.Distance(transform.position, _camTransform.position);
            bool inRange = dist <= maxDistance;

            if (_tmp.enabled != inRange)
            {
                _tmp.enabled = inRange;
            }

            if (!inRange) return;

            // Billboard: align rotation to match camera facing direction
            transform.forward = _camTransform.forward;

            // Dynamic text update
            UpdateLabelContent();
        }

        private void UpdateCameraRef()
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                _camTransform = mainCam.transform;
            }
        }

        private void UpdateLabelContent()
        {
            if (_tmp == null) return;

            if (!string.IsNullOrEmpty(linkedWorldStateFlag))
            {
                bool flagActive = WorldState.HasFlag(linkedWorldStateFlag);
                _tmp.text = flagActive ? unlockedText : lockedText;
                _tmp.color = flagActive ? new Color(0.35f, 1f, 0.45f) : new Color(1f, 0.35f, 0.35f);
            }
            else
            {
                _tmp.text = baseText;
                _tmp.color = normalColor;
            }
        }
    }
}
