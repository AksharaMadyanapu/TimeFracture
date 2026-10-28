// ============================================================
// PlayerHide.cs
// ------------------------------------------------------------
// Controls player behaviour while inside a HidingSpot.
//
// WHAT IT DOES:
//   1. Smoothly transitions the player/camera into and out of lockers.
//   2. Disables PlayerController movement & CharacterController collisions.
//   3. Restricts look angles to simulate peering through locker slits.
//   4. Fades in a locker slats / vignette UI overlay.
//   5. Exposes static PlayerHide.IsHidden and OnHiddenChanged event.
//   6. Breath-Hold Mechanic: Holding [Space] reduces detection noise to 0,
//      draining a breath meter; running out causes an audible gasp!
//   7. Era-Shift Reaction: If an era swap occurs while hidden in an
//      incompatible locker, triggers a glitch and forces the player out.
// ============================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TimeFracture.Player;
using TimeFracture.Era;

namespace TimeFracture.Hiding
{
    [DisallowMultipleComponent]
    public class PlayerHide : MonoBehaviour
    {
        // ──────────────────────────────────────────────────
        // STATIC STATE & EVENTS
        // ──────────────────────────────────────────────────

        /// <summary>True while the player is currently inside a hiding spot.</summary>
        public static bool IsHidden { get; private set; }

        /// <summary>Singleton reference to the active PlayerHide component.</summary>
        public static PlayerHide Instance { get; private set; }

        /// <summary>True if the player is currently holding breath inside a locker.</summary>
        public static bool IsPlayerHoldingBreath => Instance != null && Instance.IsHoldingBreath;

        /// <summary>Fired whenever the player enters or exits a hiding spot.</summary>
        public static event Action<bool> OnHiddenChanged;

        // ──────────────────────────────────────────────────
        // INSPECTOR FIELDS
        // ──────────────────────────────────────────────────

        [Header("Components & References")]
        [Tooltip("Camera holder to animate and rotate (auto-finds if null).")]
        [SerializeField] private Transform cameraHolder;

        [Tooltip("Locker slats / darkened vignette UI canvas group.")]
        [SerializeField] private CanvasGroup slatsOverlay;

        [Tooltip("Optional breath UI bar (auto-hidden when not hiding).")]
        [SerializeField] private Slider breathBar;

        [Header("Input Keys")]
        [Tooltip("Key used to exit the hiding spot.")]
        [SerializeField] private KeyCode exitKey = KeyCode.E;

        [Tooltip("Key held to hold breath and stay silent.")]
        [SerializeField] private KeyCode holdBreathKey = KeyCode.Space;

        [Header("Breath Mechanic")]
        [Tooltip("Maximum seconds the player can hold their breath.")]
        [SerializeField] private float maxBreath = 5f;

        [Tooltip("How fast breath drains per second while holding Space.")]
        [SerializeField] private float breathDrainRate = 1f;

        [Tooltip("How fast breath recovers per second when not holding Space.")]
        [SerializeField] private float breathRegenRate = 1.5f;

        [Tooltip("Cooldown period in seconds after gasping before breath can be held again.")]
        [SerializeField] private float gaspCooldown = 2.5f;

        [Header("Noise Emissions While Hidden")]
        [Tooltip("Noise radius emitted periodically during normal breathing inside locker.")]
        [SerializeField] private float normalHiddenNoiseRadius = 1.0f;

        [Tooltip("Loud noise radius broadcast when the player gasps for air!")]
        [SerializeField] private float gaspNoiseRadius = 7.0f;

        [Tooltip("Interval in seconds between breathing noise emissions.")]
        [SerializeField] private float hiddenNoiseInterval = 1.2f;

        [Header("Look Sensitivity Inside")]
        [SerializeField] private float peepSensitivityX = 1.5f;
        [SerializeField] private float peepSensitivityY = 1.5f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip breathHoldSound;
        [SerializeField] private AudioClip gaspSound;

        // ──────────────────────────────────────────────────
        // INTERNAL STATE
        // ──────────────────────────────────────────────────

        private PlayerController _playerController;
        private PlayerLook _playerLook;
        private CharacterController _characterController;
        private HidingSpot _currentSpot;

        private bool _isTransitioning = false;
        private float _currentBreath;
        private bool _isHoldingBreath = false;
        private bool _isGasping = false;
        private float _gaspCooldownTimer = 0f;
        private float _noiseTimer = 0f;

        // Clamped peep look offsets relative to hidePoint
        private float _peepYaw = 0f;
        private float _peepPitch = 0f;

        public float CurrentBreath => _currentBreath;
        public float MaxBreath => maxBreath;
        public bool IsHoldingBreath => _isHoldingBreath;

        // ──────────────────────────────────────────────────
        // UNITY LIFECYCLE
        // ──────────────────────────────────────────────────

        private void Awake()
        {
            Instance = this;
            _playerController = GetComponent<PlayerController>();
            _playerLook = GetComponent<PlayerLook>();
            _characterController = GetComponent<CharacterController>();

            if (cameraHolder == null)
            {
                Transform cam = GetComponentInChildren<Camera>()?.transform;
                cameraHolder = cam != null ? cam.parent : transform;
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            _currentBreath = maxBreath;

            if (slatsOverlay != null)
            {
                slatsOverlay.alpha = 0f;
                slatsOverlay.gameObject.SetActive(false);
            }

            if (breathBar != null)
            {
                breathBar.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            EraManager.OnEraChanged += HandleEraChanged;
        }

        private void OnDisable()
        {
            EraManager.OnEraChanged -= HandleEraChanged;

            if (IsHidden)
            {
                ForceEject();
            }
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
            if (!IsHidden || _isTransitioning) return;

            HandleExitInput();
            HandlePeepLook();
            HandleBreathMechanic();
        }

        // ──────────────────────────────────────────────────
        // INPUT & LOOK
        // ──────────────────────────────────────────────────

        private void HandleExitInput()
        {
            if (Input.GetKeyDown(exitKey))
            {
                ExitHiding();
            }
        }

        private void HandlePeepLook()
        {
            if (_currentSpot == null || cameraHolder == null) return;

            float mouseX = Input.GetAxis("Mouse X") * peepSensitivityX;
            float mouseY = Input.GetAxis("Mouse Y") * peepSensitivityY;

            _peepYaw += mouseX;
            _peepPitch -= mouseY;

            // Clamp angles to locker opening limits
            _peepYaw = Mathf.Clamp(_peepYaw, -_currentSpot.MaxHorizontalAngle, _currentSpot.MaxHorizontalAngle);
            _peepPitch = Mathf.Clamp(_peepPitch, -_currentSpot.MaxVerticalAngle, _currentSpot.MaxVerticalAngle);

            // Apply peep rotation relative to the locker door orientation
            Quaternion baseRot = _currentSpot.HidePoint.rotation;
            cameraHolder.rotation = baseRot * Quaternion.Euler(_peepPitch, _peepYaw, 0f);
        }

        // ──────────────────────────────────────────────────
        // BREATH-HOLD MECHANIC
        // ──────────────────────────────────────────────────

        private void HandleBreathMechanic()
        {
            // Update gasp cooldown
            if (_isGasping)
            {
                _gaspCooldownTimer -= Time.deltaTime;
                if (_gaspCooldownTimer <= 0f)
                {
                    _isGasping = false;
                }
            }

            // Read breath hold input
            bool wantsToHold = Input.GetKey(holdBreathKey) && !_isGasping;

            if (wantsToHold)
            {
                if (!_isHoldingBreath)
                {
                    _isHoldingBreath = true;
                    if (audioSource != null && breathHoldSound != null)
                        audioSource.PlayOneShot(breathHoldSound);
                }

                // Drain breath
                _currentBreath -= breathDrainRate * Time.deltaTime;
                if (_currentBreath <= 0f)
                {
                    // Breath depleted — trigger involuntary gasp!
                    _currentBreath = 0f;
                    TriggerGasp();
                }
            }
            else
            {
                _isHoldingBreath = false;

                // Regenerate breath
                if (_currentBreath < maxBreath)
                {
                    _currentBreath += breathRegenRate * Time.deltaTime;
                    _currentBreath = Mathf.Min(_currentBreath, maxBreath);
                }
            }

            // Update UI Slider if present
            if (breathBar != null)
            {
                breathBar.value = _currentBreath / maxBreath;
            }

            // Periodic noise broadcast
            _noiseTimer += Time.deltaTime;
            if (_noiseTimer >= hiddenNoiseInterval)
            {
                _noiseTimer = 0f;
                float radius = _isHoldingBreath ? 0f : normalHiddenNoiseRadius;
                if (radius > 0f)
                {
                    NoiseEmitter.Emit(transform.position, radius);
                }
            }
        }

        private void TriggerGasp()
        {
            _isHoldingBreath = false;
            _isGasping = true;
            _gaspCooldownTimer = gaspCooldown;

            if (audioSource != null && gaspSound != null)
            {
                audioSource.PlayOneShot(gaspSound);
            }

            // Broadcast loud noise: ghosts right outside locker will hear!
            NoiseEmitter.Emit(transform.position, gaspNoiseRadius);
            Debug.Log("[PlayerHide] Ran out of breath! Gasped loudly!");
        }

        // ──────────────────────────────────────────────────
        // ENTER / EXIT TRANSITIONS
        // ──────────────────────────────────────────────────

        public void EnterHiding(HidingSpot spot)
        {
            if (IsHidden || _isTransitioning || spot == null) return;

            StartCoroutine(EnterRoutine(spot));
        }

        private IEnumerator EnterRoutine(HidingSpot spot)
        {
            _isTransitioning = true;
            _currentSpot = spot;
            HidingSpot.PlayerIsHiddenAt = spot;

            // Lock controls
            if (_playerController != null) _playerController.CanMove = false;
            if (_characterController != null) _characterController.enabled = false;
            if (_playerLook != null) _playerLook.enabled = false;

            // Reset peep look
            _peepYaw = 0f;
            _peepPitch = 0f;
            _currentBreath = maxBreath;
            _isHoldingBreath = false;
            _isGasping = false;

            // Show UI
            if (slatsOverlay != null)
            {
                slatsOverlay.gameObject.SetActive(true);
            }
            if (breathBar != null)
            {
                breathBar.gameObject.SetActive(true);
                breathBar.value = 1f;
            }

            // Smooth interpolation to hide point
            Vector3 startPlayerPos = transform.position;
            Quaternion startPlayerRot = transform.rotation;
            Vector3 targetPlayerPos = spot.HidePoint.position;
            Quaternion targetPlayerRot = spot.HidePoint.rotation;

            float elapsed = 0f;
            float duration = spot.EnterDuration;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

                transform.position = Vector3.Lerp(startPlayerPos, targetPlayerPos, t);
                transform.rotation = Quaternion.Slerp(startPlayerRot, targetPlayerRot, t);

                if (cameraHolder != null)
                {
                    cameraHolder.rotation = Quaternion.Slerp(startPlayerRot, targetPlayerRot, t);
                }

                if (slatsOverlay != null)
                {
                    slatsOverlay.alpha = t;
                }

                yield return null;
            }

            transform.position = targetPlayerPos;
            transform.rotation = targetPlayerRot;

            IsHidden = true;
            _isTransitioning = false;
            OnHiddenChanged?.Invoke(true);
        }

        public void ExitHiding()
        {
            if (!IsHidden || _isTransitioning) return;

            if (_currentSpot == null)
            {
                ForceEject();
                return;
            }

            StartCoroutine(ExitRoutine());
        }

        private IEnumerator ExitRoutine()
        {
            _isTransitioning = true;
            _currentSpot.PlayExitSound();

            Vector3 startPos = transform.position;
            Vector3 targetPos = _currentSpot.ExitPoint.position;
            if (targetPos.y < 0.05f) targetPos.y = 0.05f;

            float elapsed = 0f;
            float duration = _currentSpot.ExitDuration;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

                transform.position = Vector3.Lerp(startPos, targetPos, t);

                if (slatsOverlay != null)
                {
                    slatsOverlay.alpha = 1f - t;
                }

                yield return null;
            }

            // Ensure player is at exit position and physics transform is synced BEFORE enabling controller
            transform.position = targetPos;
            Physics.SyncTransforms();

            // Hide UI
            if (slatsOverlay != null)
            {
                slatsOverlay.alpha = 0f;
                slatsOverlay.gameObject.SetActive(false);
            }
            if (breathBar != null)
            {
                breathBar.gameObject.SetActive(false);
            }

            // Restore player look and body orientation towards exit forward
            if (_playerLook != null)
            {
                _playerLook.SetLookRotation(_currentSpot.ExitPoint.forward);
                _playerLook.enabled = true;
            }

            // Re-enable CharacterController & movement
            if (_characterController == null) _characterController = GetComponent<CharacterController>();
            if (_characterController != null) _characterController.enabled = true;
            if (_playerController != null) _playerController.CanMove = true;

            IsHidden = false;
            HidingSpot.PlayerIsHiddenAt = null;
            _currentSpot = null;
            _isTransitioning = false;

            OnHiddenChanged?.Invoke(false);
        }

        // ──────────────────────────────────────────────────
        // ERA SWAP REACTION
        // ──────────────────────────────────────────────────

        private void HandleEraChanged(TimeFracture.Era.Era newEra)
        {
            if (!IsHidden || _currentSpot == null) return;

            // If the locker is incompatible with the new timeline era, eject!
            if (!_currentSpot.IsAvailableInCurrentEra())
            {
                Debug.LogWarning("[PlayerHide] Era shifted while inside restricted locker! Ejecting!");
                ForceEject();
            }
        }

        /// <summary>
        /// Instantly ejects the player if a timeline shift occurs or locker is destroyed.
        /// </summary>
        public void ForceEject()
        {
            StopAllCoroutines();

            // Trigger visual glitch sting
            GlitchEvents.FireGlitch(1.0f);

            Vector3 exitPos;
            Vector3 exitForward;

            if (_currentSpot != null)
            {
                exitPos = _currentSpot.ExitPoint.position;
                exitForward = _currentSpot.ExitPoint.forward;
            }
            else
            {
                exitPos = transform.position + transform.forward * 1.5f;
                exitForward = transform.forward;
            }

            if (exitPos.y < 0.05f) exitPos.y = 0.05f;

            // Ensure player is at exit position and physics transform is synced BEFORE enabling controller
            transform.position = exitPos;
            Physics.SyncTransforms();

            if (slatsOverlay != null)
            {
                slatsOverlay.alpha = 0f;
                slatsOverlay.gameObject.SetActive(false);
            }
            if (breathBar != null)
            {
                breathBar.gameObject.SetActive(false);
            }

            if (_playerLook != null)
            {
                _playerLook.SetLookRotation(exitForward);
                _playerLook.enabled = true;
            }

            // Re-enable CharacterController and movement
            if (_characterController == null) _characterController = GetComponent<CharacterController>();
            if (_characterController != null) _characterController.enabled = true;
            if (_playerController != null) _playerController.CanMove = true;

            IsHidden = false;
            HidingSpot.PlayerIsHiddenAt = null;
            _currentSpot = null;
            _isTransitioning = false;

            OnHiddenChanged?.Invoke(false);
        }
    }
}
