// ============================================================
// Inventory.cs
// ------------------------------------------------------------
// Manages consumable protective items (NoiseDecoys and Flares).
//
// FEATURES:
//   - Tracks counts for NoiseDecoy and Flare with configurable stack limits.
//   - Numeric keys [1] and [2] activate/throw items with a projectile arc.
//   - In-code auto-created TextMeshPro HUD at screen corner.
//   - Static instance & C# events (OnInventoryChanged) for clean decoupling.
//   - Starting inventory: 1 Decoy, 1 Flare (Milestone 7 spec).
// ============================================================

#pragma warning disable CS0618

using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TimeFracture.Player;

namespace TimeFracture.Items
{
    public class Inventory : MonoBehaviour
    {
        // ──────────────────────────────────────────────────
        // STATIC INSTANCE & EVENTS
        // ──────────────────────────────────────────────────

        public static Inventory Instance { get; private set; }

        /// <summary>
        /// Broadcasts whenever an item count changes (itemType, newCount).
        /// </summary>
        public static event Action<ItemType, int> OnInventoryChanged;

        // ──────────────────────────────────────────────────
        // INSPECTOR FIELDS
        // ──────────────────────────────────────────────────

        [Header("Stack Limits")]
        [Tooltip("Maximum quantity of each item type the player can carry simultaneously.")]
        [SerializeField] private int maxStackSize = 3;

        [Header("Starting Inventory (Milestone 7)")]
        [Tooltip("Starting count for Noise Decoys.")]
        [SerializeField] private int startingDecoys = 1;

        [Tooltip("Starting count for Flares.")]
        [SerializeField] private int startingFlares = 1;

        [Header("Item Prefabs")]
        [Tooltip("Prefab instantiated and thrown when pressing key [1].")]
        [SerializeField] private GameObject noiseDecoyPrefab;

        [Tooltip("Prefab instantiated and thrown when pressing key [2].")]
        [SerializeField] private GameObject flarePrefab;

        [Header("Throw Settings")]
        [Tooltip("Transform from which items are thrown (usually the player's camera).")]
        [SerializeField] private Transform throwOrigin;

        [Tooltip("Forward impulse force applied to thrown items.")]
        [SerializeField] private float throwForce = 12.0f;

        [Tooltip("Upward arc velocity applied to thrown items.")]
        [SerializeField] private float throwUpwardAngle = 2.5f;

        [Header("UI HUD (TextMeshPro)")]
        [Tooltip("TextMeshProUGUI label displaying item counts. Created in code if unassigned.")]
        [SerializeField] private TextMeshProUGUI hudText;

        [SerializeField] private Canvas hudCanvas;

        // ──────────────────────────────────────────────────
        // INTERNAL STATE
        // ──────────────────────────────────────────────────

        private int _decoyCount = 1;
        private int _flareCount = 1;

        // ──────────────────────────────────────────────────
        // PROPERTIES
        // ──────────────────────────────────────────────────

        public int DecoyCount => _decoyCount;
        public int FlareCount => _flareCount;
        public int MaxStackSize => maxStackSize;
        public Transform ThrowOrigin => throwOrigin;

        // ──────────────────────────────────────────────────
        // UNITY LIFECYCLE
        // ──────────────────────────────────────────────────

        private void Reset()
        {
            _decoyCount = startingDecoys;
            _flareCount = startingFlares;
            SelfWireReferences();
        }

        public void SetCounts(int decoys, int flares)
        {
            _decoyCount = Mathf.Clamp(decoys, 0, maxStackSize);
            _flareCount = Mathf.Clamp(flares, 0, maxStackSize);
            UpdateHUD();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            _decoyCount = startingDecoys;
            _flareCount = startingFlares;

            SelfWireReferences();
            EnsureHUD();
            UpdateHUD();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            // Key [1] = Use / Throw Noise Decoy
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            {
                UseItem(ItemType.NoiseDecoy);
            }

            // Key [2] = Use / Throw Flare
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                UseItem(ItemType.Flare);
            }
        }

        // ──────────────────────────────────────────────────
        // PUBLIC API
        // ──────────────────────────────────────────────────

        /// <summary>
        /// Returns the current quantity of the given item type.
        /// </summary>
        public int GetCount(ItemType type)
        {
            return type == ItemType.NoiseDecoy ? _decoyCount : _flareCount;
        }

        /// <summary>
        /// Checks if there is space in the inventory for more units of the given item.
        /// </summary>
        public bool CanAddItem(ItemType type)
        {
            return GetCount(type) < maxStackSize;
        }

        /// <summary>
        /// Adds items to the inventory up to the max stack size.
        /// </summary>
        /// <returns>True if at least one item was added; false if inventory was full.</returns>
        public bool AddItem(ItemType type, int amount = 1)
        {
            if (amount <= 0) return false;

            int current = GetCount(type);
            if (current >= maxStackSize)
            {
                return false;
            }

            int added = Mathf.Min(amount, maxStackSize - current);
            if (type == ItemType.NoiseDecoy)
            {
                _decoyCount += added;
                OnInventoryChanged?.Invoke(ItemType.NoiseDecoy, _decoyCount);
            }
            else
            {
                _flareCount += added;
                OnInventoryChanged?.Invoke(ItemType.Flare, _flareCount);
            }

            UpdateHUD();
            return true;
        }

        /// <summary>
        /// Uses/throws one unit of the specified item type.
        /// Decrements inventory count, spawns the item, and launches it with a projectile arc.
        /// </summary>
        /// <returns>True if item was successfully consumed and thrown.</returns>
        public bool UseItem(ItemType type)
        {
            if (GetCount(type) <= 0)
            {
                return false;
            }

            // Decrement count
            if (type == ItemType.NoiseDecoy)
            {
                _decoyCount--;
                OnInventoryChanged?.Invoke(ItemType.NoiseDecoy, _decoyCount);
            }
            else
            {
                _flareCount--;
                OnInventoryChanged?.Invoke(ItemType.Flare, _flareCount);
            }

            // Spawn and throw projectile
            SpawnAndThrowItem(type);

            UpdateHUD();
            return true;
        }

        // ──────────────────────────────────────────────────
        // ITEM THROWING MECHANICS
        // ──────────────────────────────────────────────────

        private void SpawnAndThrowItem(ItemType type)
        {
            GameObject prefab = type == ItemType.NoiseDecoy ? noiseDecoyPrefab : flarePrefab;
            if (prefab == null)
            {
                Debug.LogWarning($"[Inventory] Cannot throw {type}: Prefab is unassigned!");
                return;
            }

            // Determine origin and forward trajectory
            Vector3 originPos = transform.position + Vector3.up * 1.5f;
            Vector3 forwardDir = transform.forward;

            if (throwOrigin != null)
            {
                originPos = throwOrigin.position;
                forwardDir = throwOrigin.forward;
            }
            else if (Camera.main != null)
            {
                originPos = Camera.main.transform.position;
                forwardDir = Camera.main.transform.forward;
            }

            // Spawn slightly in front of camera to avoid clipping player collider
            Vector3 spawnPos = originPos + forwardDir * 0.45f;
            Quaternion spawnRot = Quaternion.LookRotation(forwardDir);

            GameObject spawned = Instantiate(prefab, spawnPos, spawnRot);

            // Calculate projectile trajectory with upward arc
            Vector3 throwVelocity = forwardDir * throwForce + Vector3.up * throwUpwardAngle;

            if (type == ItemType.NoiseDecoy)
            {
                NoiseDecoy decoyComp = spawned.GetComponent<NoiseDecoy>();
                if (decoyComp != null)
                {
                    decoyComp.Launch(throwVelocity);
                }
                else
                {
                    Rigidbody rb = spawned.GetComponent<Rigidbody>();
                    if (rb != null) rb.linearVelocity = throwVelocity;
                }
            }
            else
            {
                Flare flareComp = spawned.GetComponent<Flare>();
                if (flareComp != null)
                {
                    flareComp.Launch(throwVelocity);
                }
                else
                {
                    Rigidbody rb = spawned.GetComponent<Rigidbody>();
                    if (rb != null) rb.linearVelocity = throwVelocity;
                }
            }
        }

        // ──────────────────────────────────────────────────
        // HUD CREATION & REFRESH (CODE-ONLY ZERO SETUP)
        // ──────────────────────────────────────────────────

        public void UpdateHUD()
        {
            if (hudText == null) return;

            string decoyColor = _decoyCount > 0 ? "00E5FF" : "666666";
            string flareColor = _flareCount > 0 ? "FFA726" : "666666";

            hudText.text =
                $"<size=11><color=#8E99A2>ITEMS (KEYS 1 & 2)</color></size>\n" +
                $"<color=#{decoyColor}><b>[1] Decoy:</b> {_decoyCount}/{maxStackSize}</color>\n" +
                $"<color=#{flareColor}><b>[2] Flare:</b> {_flareCount}/{maxStackSize}</color>";
        }

        public void EnsureHUD()
        {
            if (hudText != null) return;

            // 1. Find or create HUD Canvas
            GameObject canvasObj = GameObject.Find("InventoryHUD_Canvas");
            if (canvasObj == null)
            {
                canvasObj = new GameObject("InventoryHUD_Canvas");
                hudCanvas = canvasObj.AddComponent<Canvas>();
                hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                hudCanvas.sortingOrder = 50;

                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                canvasObj.AddComponent<GraphicRaycaster>();
            }
            else
            {
                hudCanvas = canvasObj.GetComponent<Canvas>();
            }

            // 2. Create Panel Container
            GameObject panelObj = new GameObject("InventoryPanel");
            panelObj.transform.SetParent(canvasObj.transform, false);

            Image bgImage = panelObj.AddComponent<Image>();
            bgImage.color = new Color(0.04f, 0.05f, 0.07f, 0.75f); // Deep frosted dark slate

            RectTransform panelRect = panelObj.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 0f); // Bottom-left corner
            panelRect.anchorMax = new Vector2(0f, 0f);
            panelRect.pivot = new Vector2(0f, 0f);
            panelRect.anchoredPosition = new Vector2(24f, 24f);
            panelRect.sizeDelta = new Vector2(210f, 85f);

            // 3. Create TextMeshProUGUI element
            GameObject textObj = new GameObject("InventoryText");
            textObj.transform.SetParent(panelObj.transform, false);

            hudText = textObj.AddComponent<TextMeshProUGUI>();
            hudText.fontSize = 17f;
            hudText.alignment = TextAlignmentOptions.MidlineLeft;
            hudText.textWrappingMode = TextWrappingModes.NoWrap;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = new Vector2(14f, 8f);
            textRect.offsetMax = new Vector2(-10f, -8f);

            UpdateHUD();
        }

        // ──────────────────────────────────────────────────
        // SELF-WIRING HELPERS
        // ──────────────────────────────────────────────────

        private void SelfWireReferences()
        {
            if (throwOrigin == null)
            {
                if (Camera.main != null)
                {
                    throwOrigin = Camera.main.transform;
                }
                else
                {
                    PlayerInteractor interactor = UnityEngine.Object.FindFirstObjectByType<PlayerInteractor>();
                    if (interactor != null) throwOrigin = interactor.transform;
                }
            }
        }
    }
}
