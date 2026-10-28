// ============================================================
// PlayerController.cs
// ------------------------------------------------------------
// Handles: WASD movement, gravity, sprint + stamina, crouch.
// Requires: CharacterController on the same GameObject,
//           NoiseEmitter on the same GameObject,
//           and an AudioSource (optional, for footsteps).
//
// STUDENT NOTE — CharacterController vs Rigidbody:
//   CharacterController is Unity's built-in "capsule that
//   walks" component.  It handles collision with walls and
//   stairs automatically.  We drive it manually with Move()
//   so we have full control over gravity and speed.
// ============================================================
using UnityEngine;
using TimeFracture.Hiding;

namespace TimeFracture.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(NoiseEmitter))]
    public class PlayerController : MonoBehaviour
    {
        // ──────────────────────────────────────────────────
        // INSPECTOR FIELDS
        // ──────────────────────────────────────────────────

        [Header("Movement")]
        [Tooltip("Normal walking speed in metres per second.")]
        [SerializeField] private float walkSpeed = 4f;

        [Tooltip("Speed while sprinting.")]
        [SerializeField] private float sprintSpeed = 8f;

        [Tooltip("Speed while crouching.")]
        [SerializeField] private float crouchSpeed = 2f;

        [Tooltip("Gravity acceleration (positive = downward). " +
                 "Unity default gravity is 9.81.")]
        [SerializeField] private float gravity = 15f;

        // ──────────────────────────────────────────────────
        [Header("Stamina")]
        [Tooltip("Maximum stamina.")]
        [SerializeField] private float maxStamina = 100f;

        [Tooltip("How much stamina is drained per second while sprinting.")]
        [SerializeField] private float staminaDrainRate = 20f;

        [Tooltip("How much stamina regenerates per second when not sprinting.")]
        [SerializeField] private float staminaRegenRate = 15f;

        [Tooltip("Player cannot start sprinting again until stamina " +
                 "reaches this value.  Prevents 'flicker' sprinting.")]
        [SerializeField] private float sprintResumeThreshold = 25f;

        // ──────────────────────────────────────────────────
        [Header("Crouch")]
        [Tooltip("When true, Ctrl TOGGLES crouch on/off. " +
                 "When false, crouch is held (hold Ctrl to stay crouched).")]
        [SerializeField] private bool crouchIsToggle = true;

        [Tooltip("CharacterController height when standing.")]
        [SerializeField] private float standingHeight = 2f;

        [Tooltip("CharacterController height when crouched.")]
        [SerializeField] private float crouchingHeight = 1f;

        [Tooltip("How quickly the camera and controller transition " +
                 "between stand and crouch heights (lerp speed).")]
        [SerializeField] private float crouchTransitionSpeed = 8f;

        [Tooltip("Radius of the sphere cast upward to check for a ceiling. " +
                 "Should be slightly smaller than the CharacterController radius.")]
        [SerializeField] private float ceilingCheckRadius = 0.25f;

        // ──────────────────────────────────────────────────
        [Header("Camera Reference")]
        [Tooltip("Drag the CameraHolder GameObject here. " +
                 "Its local Y position is moved during crouch.")]
        [SerializeField] private Transform cameraHolder;

        [Tooltip("Local Y position of the camera holder when standing.")]
        [SerializeField] private float cameraStandY = 0.75f;   // relative to player root

        [Tooltip("Local Y position of the camera holder when crouched.")]
        [SerializeField] private float cameraCrouchY = 0.1f;

        // ──────────────────────────────────────────────────
        [Header("Footstep Audio (optional)")]
        [Tooltip("AudioSource used for footstep sounds. " +
                 "Leave the clip fields empty if you have no audio files yet.")]
        [SerializeField] private AudioSource footstepAudioSource;

        [Tooltip("Seconds between footstep sounds at walking speed.")]
        [SerializeField] private float footstepInterval = 0.5f;

        // ──────────────────────────────────────────────────
        // PRIVATE STATE
        // ──────────────────────────────────────────────────

        private CharacterController _cc;
        private NoiseEmitter        _noise;

        private float   _verticalVelocity;   // gravity accumulates here
        private float   _currentStamina;
        private bool    _sprintLocked;       // true when stamina is 0; must regen to threshold
        private bool    _isCrouching;
        private float   _targetHeight;       // lerp target for CC height
        private float   _targetCameraY;      // lerp target for camera Y
        private float   _footstepTimer;

        // ──────────────────────────────────────────────────
        // PUBLIC READ-ONLY PROPERTIES
        // (other scripts can read state without tight coupling)
        // ──────────────────────────────────────────────────

        /// <summary>True while the player is crouching.</summary>
        public bool IsCrouching => _isCrouching;

        /// <summary>Current stamina value (0–maxStamina).</summary>
        public float Stamina => _currentStamina;

        /// <summary>True while the player is actively sprinting.</summary>
        public bool IsSprinting { get; private set; }

        /// <summary>
        /// When false, movement input is ignored (e.g. during an era swap glitch freeze or hiding).
        /// Gravity is still applied so the player stays grounded.
        /// </summary>
        public bool CanMove { get; set; } = true;

        // ──────────────────────────────────────────────────
        // UNITY LIFECYCLE
        // ──────────────────────────────────────────────────

        private void Awake()
        {
            _cc    = GetComponent<CharacterController>();
            _noise = GetComponent<NoiseEmitter>();

            // Initialise to full stamina
            _currentStamina = maxStamina;

            // Set initial CC dimensions
            _cc.height = standingHeight;
            _targetHeight  = standingHeight;
            _targetCameraY = cameraStandY;
        }

        private void Update()
        {
            HandleCrouch();
            HandleMovement();
            HandleFootsteps();
        }

        // ──────────────────────────────────────────────────
        // MOVEMENT
        // ──────────────────────────────────────────────────

        private void HandleMovement()
        {
            // Skip all movement, gravity, and Move() calls when the CharacterController is disabled,
            // when CanMove is false, or when the player is hiding.
            if (_cc == null || !_cc.enabled || !CanMove || PlayerHide.IsHidden)
            {
                _verticalVelocity = 0f;
                IsSprinting = false;
                HandleStamina(false, false);
                return;
            }

            // --- Read WASD axes ---
            float h = CanMove ? Input.GetAxis("Horizontal") : 0f;   // A/D
            float v = CanMove ? Input.GetAxis("Vertical") : 0f;     // W/S

            // Build a move direction relative to where the player is facing.
            // transform.right and transform.forward are world-space vectors
            // pointing in the player's local X and Z directions.
            Vector3 moveDir = transform.right * h + transform.forward * v;

            // Clamp diagonal speed (without this, moving diagonally is ~41 % faster)
            if (moveDir.magnitude > 1f)
                moveDir.Normalize();

            // --- Determine speed ---
            bool wantsToSprint = CanMove && Input.GetKey(KeyCode.LeftShift) && !_isCrouching;
            HandleStamina(wantsToSprint, moveDir.magnitude > 0.01f);

            float speed;
            if (_isCrouching)
                speed = crouchSpeed;
            else if (IsSprinting)
                speed = sprintSpeed;
            else
                speed = walkSpeed;

            // --- Apply gravity ---
            if (_cc.isGrounded)
            {
                // Keep a small downward velocity so isGrounded stays true
                _verticalVelocity = -2f;
            }
            else
            {
                // Accumulate gravity over time (v = a*t)
                _verticalVelocity -= gravity * Time.deltaTime;
            }

            // Combine horizontal movement with gravity
            Vector3 velocity = moveDir * speed;
            velocity.y = _verticalVelocity;

            // Move the CharacterController
            _cc.Move(velocity * Time.deltaTime);

            // --- Noise emission ---
            float noiseRadius = 0f;
            bool isMoving = moveDir.magnitude > 0.01f;

            if (isMoving)
            {
                if (_isCrouching)
                    noiseRadius = _noise.CrouchRadius;
                else if (IsSprinting)
                    noiseRadius = _noise.SprintRadius;
                else
                    noiseRadius = _noise.WalkRadius;
            }

            _noise.TryEmit(transform.position, noiseRadius);

            // --- Smooth crouch height transitions ---
            // Only adjust CharacterController height and center if the controller is enabled
            if (_cc != null && _cc.enabled)
            {
                _cc.height = Mathf.Lerp(_cc.height, _targetHeight, Time.deltaTime * crouchTransitionSpeed);
                _cc.center = new Vector3(0f, _cc.height * 0.5f, 0f);
            }

            // Lerp camera holder Y position
            if (cameraHolder != null)
            {
                Vector3 localPos = cameraHolder.localPosition;
                localPos.y = Mathf.Lerp(localPos.y, _targetCameraY, Time.deltaTime * crouchTransitionSpeed);
                cameraHolder.localPosition = localPos;
            }
        }

        // ──────────────────────────────────────────────────
        // STAMINA
        // ──────────────────────────────────────────────────

        private void HandleStamina(bool wantsToSprint, bool isMoving)
        {
            bool canSprint = isMoving && wantsToSprint;

            if (canSprint && !_sprintLocked)
            {
                // Drain stamina while actually sprinting
                _currentStamina -= staminaDrainRate * Time.deltaTime;

                if (_currentStamina <= 0f)
                {
                    // Ran out — lock sprint until threshold is recovered
                    _currentStamina = 0f;
                    _sprintLocked   = true;
                    IsSprinting     = false;
                }
                else
                {
                    IsSprinting = true;
                }
            }
            else
            {
                // Not sprinting — regenerate
                IsSprinting = false;
                _currentStamina += staminaRegenRate * Time.deltaTime;
                _currentStamina  = Mathf.Min(_currentStamina, maxStamina);

                // Unlock sprint once threshold is reached
                if (_sprintLocked && _currentStamina >= sprintResumeThreshold)
                    _sprintLocked = false;
            }
        }

        // ──────────────────────────────────────────────────
        // CROUCH
        // ──────────────────────────────────────────────────

        private void HandleCrouch()
        {
            // Skip crouch input when controller is disabled, CanMove is false, or hiding
            if (_cc == null || !_cc.enabled || !CanMove || PlayerHide.IsHidden)
                return;

            if (crouchIsToggle)
            {
                // TOGGLE MODE: one press = crouch, next press = stand
                if (Input.GetKeyDown(KeyCode.LeftControl))
                {
                    if (_isCrouching)
                        TryStandUp();
                    else
                        Crouch();
                }
            }
            else
            {
                // HOLD MODE: hold Ctrl = crouch, release = stand
                if (Input.GetKey(KeyCode.LeftControl))
                {
                    if (!_isCrouching) Crouch();
                }
                else
                {
                    if (_isCrouching) TryStandUp();
                }
            }
        }

        private void Crouch()
        {
            if (_cc == null || !_cc.enabled) return;
            _isCrouching   = true;
            _targetHeight  = crouchingHeight;
            _targetCameraY = cameraCrouchY;
        }

        private void TryStandUp()
        {
            if (_cc == null || !_cc.enabled) return;

            // Cast a sphere above the player's head to check for a ceiling.
            // The sphere is placed at the top of the crouched capsule and
            // travels upward by the difference between standing and crouching heights.
            Vector3 topOfCrouchedCapsule = transform.position +
                                           Vector3.up * (_cc.height - ceilingCheckRadius);

            float checkDistance = standingHeight - crouchingHeight;

            // ~LayerMask.GetMask("Player") means "all layers EXCEPT Player".
            // The ~ operator flips every bit in the integer, turning the Player
            // layer mask into a mask that matches everything else.
            int ceilingMask = ~LayerMask.GetMask("Player");

            bool ceilingAbove = Physics.SphereCast(
                topOfCrouchedCapsule,
                ceilingCheckRadius,
                Vector3.up,
                out _,                   // we don't need the hit info
                checkDistance,
                ceilingMask,
                QueryTriggerInteraction.Ignore
            );

            // Only stand up if the space is clear
            if (!ceilingAbove)
            {
                _isCrouching   = false;
                _targetHeight  = standingHeight;
                _targetCameraY = cameraStandY;
            }
            // else: stay crouched silently (player cannot stand up here)
        }

        // ──────────────────────────────────────────────────
        // FOOTSTEP AUDIO HOOK
        // ──────────────────────────────────────────────────

        private void HandleFootsteps()
        {
            if (_cc == null || !_cc.enabled || !CanMove || PlayerHide.IsHidden)
            {
                _footstepTimer = 0f;
                return;
            }

            // Only play footstep sounds when grounded, moving, and movement is enabled
            bool isMoving = (Mathf.Abs(Input.GetAxis("Horizontal")) > 0.1f ||
                             Mathf.Abs(Input.GetAxis("Vertical"))   > 0.1f);

            if (_cc.isGrounded && isMoving)
            {
                // Adjust interval based on speed state
                float interval = IsSprinting ? footstepInterval * 0.6f
                               : _isCrouching ? footstepInterval * 1.4f
                               : footstepInterval;

                _footstepTimer += Time.deltaTime;
                if (_footstepTimer >= interval)
                {
                    _footstepTimer = 0f;
                    PlayFootstep();
                }
            }
            else
            {
                _footstepTimer = 0f; // reset when not moving
            }
        }

        private void PlayFootstep()
        {
            // Hook: assign clips via footstepAudioSource.clip or use
            // PlayOneShot() in a future milestone with a sound manager.
            if (footstepAudioSource != null && footstepAudioSource.clip != null)
                footstepAudioSource.PlayOneShot(footstepAudioSource.clip);
        }
    }
}
