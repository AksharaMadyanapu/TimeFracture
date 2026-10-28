// ============================================================
// Residue.cs
// ------------------------------------------------------------
// Ghost #2: "The Residue"
//
// DESIGN PILLARS (GAME_DESIGN.md):
//   1. Exists ONLY in the Present facility timeline.
//      - Past: completely disabled (dematerialized, invisible, agent off).
//      - Present: active and patrols the decayed ruins.
//   2. Hunts by SIGHT and LIGHT:
//      - Field-of-View vision cone (angle & distance checks + raycasts).
//      - Detection meter builds gradually while player is in sight.
//      - Player's FLASHLIGHT dramatically accelerates detection!
//      - Player CROUCHING slows down detection.
//   3. Countered by FLARES:
//      - Repelled by FlareZone.IsPointProtected.
//      - Cannot attack or enter active flare light zones.
//   4. Hiding in lockers blocks line of sight.
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using TimeFracture.Player;
using TimeFracture.Items;
using TimeFracture.Hiding;
using TimeFracture.Era;
using TimelineEra = TimeFracture.Era.Era;

namespace TimeFracture.Ghost
{
    public class Residue : GhostBase
    {
        // ──────────────────────────────────────────────────
        // INSPECTOR FIELDS — VISION & SIGHT DETECTION
        // ──────────────────────────────────────────────────

        [Header("Timeline / Era")]
        [Tooltip("Where The Residue respawns when shifting into the Present.")]
        [SerializeField] private Transform spawnPoint;
        public Transform SpawnPoint => spawnPoint;

        [Header("Sight Detection (Vision Cone)")]
        [Tooltip("Maximum sight range in metres.")]
        [SerializeField] private float viewDistance = 18.0f;

        [Tooltip("Horizontal field-of-view angle (degrees).")]
        [Range(10f, 180f)]
        [SerializeField] private float viewAngle = 70.0f;

        [Tooltip("Seconds of continuous line of sight required to trigger a full Hunt.")]
        [SerializeField] private float baseDetectionTime = 2.0f;

        [Tooltip("Detection rate multiplier when the player's flashlight is turned ON.")]
        [SerializeField] private float flashlightDetectionMultiplier = 2.8f;

        [Tooltip("Detection rate multiplier when the player is crouching.")]
        [SerializeField] private float crouchDetectionMultiplier = 0.45f;

        [Tooltip("LayerMask for obstacles that block vision raycasts.")]
        [SerializeField] private LayerMask visionObstacleMask = ~0;

        [Header("Flare Vulnerability")]
        [Tooltip("Distance from ghost to a flare zone where the ghost is blinded and repelled.")]
        [SerializeField] private float flareRepelDistance = 7.0f;

        [Header("Audio (Optional hooks)")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip alertHissSound;
        [SerializeField] private AudioClip huntDroneSound;

        // ──────────────────────────────────────────────────
        // INTERNAL STATE
        // ──────────────────────────────────────────────────

        private float _detectionMeter = 0f;
        private bool _canSeePlayer = false;
        private Renderer[] _renderers;
        private Collider[] _colliders;

        // ──────────────────────────────────────────────────
        // PROPERTIES
        // ──────────────────────────────────────────────────

        public float DetectionMeter => _detectionMeter;
        public bool CanSeePlayer => _canSeePlayer;

        // ──────────────────────────────────────────────────
        // UNITY LIFECYCLE
        // ──────────────────────────────────────────────────

        protected override void Awake()
        {
            SelfWireResidue();
            base.Awake();

            _renderers = GetComponentsInChildren<Renderer>(true);
            _colliders = GetComponentsInChildren<Collider>(true);
        }

        protected override void Reset()
        {
            base.Reset();
            SelfWireResidue();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EraManager.OnEraChanged += HandleEraChanged;

            if (EraManager.Instance != null)
            {
                ApplyEraState(EraManager.Instance.CurrentEra);
            }
            else
            {
                ApplyEraState(TimelineEra.Present);
            }
        }

        private void OnDisable()
        {
            EraManager.OnEraChanged -= HandleEraChanged;
        }

        protected override void Update()
        {
            base.Update();

            if (isDeadOrDisabled) return;

            // 1. Check flare protection
            CheckFlareProximity();

            // 2. Perform Sight Sensor checks
            CheckVision();
        }

        // ──────────────────────────────────────────────────
        // ERA TIMELINE SHIFTS (PRESENT ONLY)
        // ──────────────────────────────────────────────────

        private void HandleEraChanged(TimelineEra newEra)
        {
            ApplyEraState(newEra);
        }

        private void ApplyEraState(TimelineEra era)
        {
            if (era == TimelineEra.Present)
            {
                // Active in Present
                isDeadOrDisabled = false;
                SetVisualsAndCollision(true);

                if (agent != null)
                {
                    agent.enabled = false;
                    transform.position = spawnPoint != null ? spawnPoint.position : transform.position;
                    agent.enabled = true;
                }

                SetState(GhostState.Idle);
                Debug.Log("[Residue] Materialized in the decayed Present facility.");
            }
            else
            {
                // Dematerialized in Past
                isDeadOrDisabled = true;
                if (agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                    agent.enabled = false;
                }

                SetVisualsAndCollision(false);
                Debug.Log("[Residue] Dematerialized in the Past laboratory.");
            }
        }

        private void SetVisualsAndCollision(bool active)
        {
            if (_renderers != null)
            {
                for (int i = 0; i < _renderers.Length; i++)
                    if (_renderers[i] != null) _renderers[i].enabled = active;
            }
            if (_colliders != null)
            {
                for (int i = 0; i < _colliders.Length; i++)
                    if (_colliders[i] != null) _colliders[i].enabled = active;
            }
        }

        // ──────────────────────────────────────────────────
        // VISION & SIGHT DETECTION
        // ──────────────────────────────────────────────────

        private void CheckVision()
        {
            _canSeePlayer = false;

            if (playerTransform == null) return;

            // Player hidden in locker cannot be seen
            if (PlayerHide.IsHidden)
            {
                DecayDetection();
                return;
            }

            // Player protected by active flare zone cannot be targeted
            if (FlareZone.IsPointProtected(playerTransform.position))
            {
                DecayDetection();
                return;
            }

            Vector3 eyePos = transform.position + Vector3.up * 1.6f;
            Vector3 targetPos = playerTransform.position + Vector3.up * 1.0f;
            Vector3 dirToPlayer = targetPos - eyePos;
            float dist = dirToPlayer.magnitude;

            if (dist <= viewDistance)
            {
                float angle = Vector3.Angle(transform.forward, dirToPlayer.normalized);
                if (angle <= viewAngle * 0.5f)
                {
                    // Raycast line of sight check
                    RaycastHit hit;
                    if (Physics.Raycast(eyePos, dirToPlayer.normalized, out hit, dist, visionObstacleMask, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.transform == playerTransform || hit.transform.IsChildOf(playerTransform) || hit.transform.CompareTag("Player"))
                        {
                            _canSeePlayer = true;
                        }
                    }
                }
            }

            // Update detection meter
            if (_canSeePlayer)
            {
                BuildDetection(dist);
            }
            else
            {
                DecayDetection();
            }
        }

        private void BuildDetection(float distance)
        {
            float rate = 1.0f / Mathf.Max(0.1f, baseDetectionTime);

            // Flashlight bonus
            Flashlight fl = Flashlight.Instance;
            if (fl == null) fl = UnityEngine.Object.FindFirstObjectByType<Flashlight>();
            if (fl != null && fl.IsOn)
            {
                rate *= flashlightDetectionMultiplier;
            }

            // Crouch reduction
            PlayerController pc = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            if (pc != null && pc.IsCrouching)
            {
                rate *= crouchDetectionMultiplier;
            }

            _detectionMeter += rate * Time.deltaTime;
            _detectionMeter = Mathf.Clamp01(_detectionMeter);

            // Play alert sound when detection starts
            if (_detectionMeter > 0.35f && audioSource != null && !audioSource.isPlaying && alertHissSound != null)
            {
                audioSource.PlayOneShot(alertHissSound, 0.6f);
            }

            // Trigger Hunt on full detection
            if (_detectionMeter >= 1.0f && CanHunt)
            {
                lastKnownPlayerPos = playerTransform.position;
                SetState(GhostState.Hunt);
                Debug.Log("[Residue] Player spotted! Starting Hunt!");
            }
        }

        // ──────────────────────────────────────────────────
        // HUNT & SEARCH IMPLEMENTATION (GHOSTBASE ABSTRACTS)
        // ──────────────────────────────────────────────────

        protected override void UpdateHunt()
        {
            if (playerTransform == null)
            {
                SetState(GhostState.Cooldown);
                return;
            }

            // Move relentlessly toward player
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.SetDestination(playerTransform.position);
            }

            // Evaluate kill distance
            float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            if (distToPlayer <= killDistance)
            {
                // Verify player is not protected by an active flare zone
                if (!FlareZone.IsPointProtected(playerTransform.position))
                {
                    Kill();
                    return;
                }
            }

            // Lose sight or hunt timeout
            if (!_canSeePlayer && stateTimer >= huntMaxDuration)
            {
                SetState(GhostState.Search);
            }
        }

        protected override void UpdateSearch()
        {
            // Residue searches area around last known player position
            if (stateTimer >= 6.0f)
            {
                SetState(GhostState.Cooldown);
            }
        }

        private void DecayDetection()
        {
            if (_detectionMeter > 0f)
            {
                _detectionMeter -= Time.deltaTime * 0.75f;
                _detectionMeter = Mathf.Clamp01(_detectionMeter);
            }
        }

        // ──────────────────────────────────────────────────
        // FLARE REPULSION
        // ──────────────────────────────────────────────────

        private void CheckFlareProximity()
        {
            // If inside or near an active flare zone, repel immediately
            if (FlareZone.IsPointProtected(transform.position))
            {
                if (currentState == GhostState.Hunt)
                {
                    Debug.Log("[Residue] Repelled by FlareZone light! Disengaging hunt into Cooldown.");
                    SetState(GhostState.Cooldown);
                    _detectionMeter = 0f;
                }

                // Push away from active zones
                IReadOnlyList<FlareZone> zones = FlareZone.ActiveZones;
                for (int i = 0; i < zones.Count; i++)
                {
                    if (zones[i] != null && zones[i].IsActive)
                    {
                        Vector3 pushDir = (transform.position - zones[i].transform.position).normalized;
                        if (agent != null && agent.enabled && agent.isOnNavMesh)
                        {
                            agent.Move(pushDir * roamSpeed * Time.deltaTime * 2f);
                        }
                    }
                }
            }
        }

        // ──────────────────────────────────────────────────
        // SELF-WIRING HELPERS (ZERO MANUAL SETUP)
        // ──────────────────────────────────────────────────

        private void SelfWireResidue()
        {
            roamSpeed = 1.3f;
            huntSpeed = 2.5f;

            if (agent == null) agent = GetComponent<NavMeshAgent>() ?? gameObject.AddComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.height = 2f;
                agent.radius = 0.5f;
                agent.baseOffset = 1.0f; // Stand upright on floor
            }

            if (spawnPoint == null)
            {
                GameObject sp = GameObject.Find("ResidueSpawnPoint") ?? GameObject.Find("_Milestone9_Test/ResidueSpawnPoint");
                if (sp != null) spawnPoint = sp.transform;
                else spawnPoint = transform;
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
            }
            if (audioSource != null)
            {
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1.0f;
                audioSource.minDistance = 2f;
                audioSource.maxDistance = 20f;
            }

            // Procedural Audio clips
            if (alertHissSound == null) alertHissSound = CreateProceduralHissClip("Residue_AlertHiss", 0.5f);
            if (huntDroneSound == null) huntDroneSound = CreateProceduralHissClip("Residue_HuntDrone", 1.2f);
        }

        private static AudioClip CreateProceduralHissClip(string clipName, float duration)
        {
            int sampleRate = 44100;
            int totalSamples = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                samples[i] = (UnityEngine.Random.value * 2f - 1f) * 0.35f;
            }

            AudioClip clip = AudioClip.Create(clipName, totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, viewDistance);

            Vector3 leftRay = Quaternion.Euler(0, -viewAngle * 0.5f, 0) * transform.forward * viewDistance;
            Vector3 rightRay = Quaternion.Euler(0, viewAngle * 0.5f, 0) * transform.forward * viewDistance;
            Gizmos.DrawRay(transform.position + Vector3.up * 1.6f, leftRay);
            Gizmos.DrawRay(transform.position + Vector3.up * 1.6f, rightRay);
        }
    }
}
