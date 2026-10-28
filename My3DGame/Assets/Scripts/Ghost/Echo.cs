// ============================================================
// Echo.cs
// ------------------------------------------------------------
// Ghost #1: "The Echo"
//
// DESIGN PILLARS (GAME_DESIGN.md):
//   1. Exists ONLY in the Past laboratory timeline.
//      - Present: completely disabled (cannot hunt, invisible, agent off).
//      - Past: active, respawns at designated spawn point on timeline swap.
//   2. Hunts by SOUND:
//      - Subscribes to NoiseEmitter.OnNoiseMade.
//      - Within hearing radius: switches to Investigate at noise position.
//      - Loud noise (sprinting, era switch) or repeated noise escalates to Hunt.
//   3. Hunt & Search:
//      - Chases player to last-seen position.
//      - If player hides, transitions to Search: inspects nearby lockers.
//      - If it inspects an occupied locker, catches player unless breath-hold
//        succeeds and survival chance roll passes!
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using TimeFracture.Player;
using TimeFracture.Hiding;
using TimelineEra = TimeFracture.Era.Era;
using TimeFracture.Era;

namespace TimeFracture.Ghost
{
    public class Echo : GhostBase
    {
        // ──────────────────────────────────────────────────
        // INSPECTOR FIELDS — SENSORS & SUSPICION
        // ──────────────────────────────────────────────────

        [Header("Timeline / Era")]
        [Tooltip("Where The Echo respawns when timeline shifts back into the Past.")]
        [SerializeField] private Transform spawnPoint;
        public Transform SpawnPoint => spawnPoint;

        [Header("Sound Detection (Hearing)")]
        [Tooltip("Base hearing radius in metres. Noises inside this radius alert The Echo.")]
        [SerializeField] private float hearingRadius = 16.0f;

        [Tooltip("Noise radius that immediately triggers a full Hunt (e.g. sprint = 12m, era swap = 15m).")]
        [SerializeField] private float loudNoiseThreshold = 10.0f;

        [Tooltip("Time window in seconds where a second noise escalates Investigate into a Hunt.")]
        [SerializeField] private float repeatedNoiseWindow = 5.0f;

        [Header("Locker Search Mechanics")]
        [Tooltip("Radius around the last known player position to search for hiding spots.")]
        [SerializeField] private float searchHidingRadius = 14.0f;

        [Tooltip("How many seconds The Echo stares at/inspects a locker before moving on.")]
        [SerializeField] private float inspectLockerDuration = 2.0f;

        [Tooltip("Maximum total duration of the Search phase before giving up into Cooldown.")]
        [SerializeField] private float searchMaxDuration = 12.0f;

        [Header("Hiding Catch Chances (Tunable)")]
        [Tooltip("Probability (0.0 to 1.0) of being caught in a searched locker while NOT holding breath.")]
        [Range(0f, 1f)]
        [SerializeField] private float catchChanceWhileBreathing = 0.90f;

        [Tooltip("Probability (0.0 to 1.0) of being caught in a searched locker even WHILE holding breath.")]
        [Range(0f, 1f)]
        [SerializeField] private float catchChanceWhileHoldingBreath = 0.15f;

        [Header("Audio (Optional hooks)")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip alertSound;
        [SerializeField] private AudioClip huntRoarSound;
        [SerializeField] private AudioClip killSound;

        // ──────────────────────────────────────────────────
        // INTERNAL STATE
        // ──────────────────────────────────────────────────

        private Vector3 _initialSpawnPosition;
        private Quaternion _initialSpawnRotation;

        private float _lastNoiseTime = -99f;
        private int _recentNoiseCount = 0;

        // Locker search queue
        private readonly List<HidingSpot> _spotsToSearch = new List<HidingSpot>();
        private int _currentSearchSpotIndex = 0;
        private bool _isInspectingSpot = false;
        private float _spotInspectTimer = 0f;

        // Renderer and Collider components to toggle between eras
        private Renderer[] _renderers;
        private Collider[] _colliders;

        // ──────────────────────────────────────────────────
        // UNITY LIFECYCLE
        // ──────────────────────────────────────────────────

        protected override void Awake()
        {
            SelfWireEcho();
            base.Awake();

            _renderers = GetComponentsInChildren<Renderer>(true);
            _colliders = GetComponentsInChildren<Collider>(true);

            // Record initial spawn transform
            _initialSpawnPosition = spawnPoint != null ? spawnPoint.position : transform.position;
            _initialSpawnRotation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;
        }

        protected override void Reset()
        {
            base.Reset();
            SelfWireEcho();
        }

        private void SelfWireEcho()
        {
            // 1. Auto-wire spawn point if unassigned
            if (spawnPoint == null)
            {
                GameObject spObj = GameObject.Find("GhostSpawnPoint") ?? GameObject.Find("_Milestone5_Test/GhostSpawnPoint");
                if (spObj != null)
                {
                    spawnPoint = spObj.transform;
                }
                else if (waypoints != null && waypoints.Length > 0 && waypoints[0] != null)
                {
                    spawnPoint = waypoints[0];
                }
                else
                {
                    spawnPoint = transform;
                }
            }

            // 2. AudioSource self-wiring
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
            }
            if (audioSource != null)
            {
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1.0f;
                audioSource.minDistance = 2f;
                audioSource.maxDistance = 25f;
            }

            // 3. Procedural AudioClips if inspector clips are unassigned (Rule 3)
            if (alertSound == null) alertSound = GenerateProceduralClip("Echo_Procedural_Alert", 520f, 0.35f, false);
            if (huntRoarSound == null) huntRoarSound = GenerateProceduralClip("Echo_Procedural_Roar", 130f, 0.85f, true);
            if (killSound == null) killSound = GenerateProceduralClip("Echo_Procedural_Kill", 260f, 0.6f, true);

            // 4. Mesh / Renderer fallback if missing (Rule 3)
            MeshRenderer mr = GetComponent<MeshRenderer>();
            MeshFilter mf = GetComponent<MeshFilter>();
            if (mr == null || mf == null)
            {
                GameObject tempCapsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                if (mf == null) mf = gameObject.AddComponent<MeshFilter>();
                mf.sharedMesh = tempCapsule.GetComponent<MeshFilter>().sharedMesh;
                if (mr == null) mr = gameObject.AddComponent<MeshRenderer>();
                DestroyImmediate(tempCapsule);
            }

            if (mr != null && mr.sharedMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                Material mat = new Material(shader) { color = new Color(0.85f, 0.12f, 0.15f, 0.9f) };
                mr.sharedMaterial = mat;
            }

            // 5. Collider fallback
            if (GetComponent<Collider>() == null)
            {
                CapsuleCollider col = gameObject.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0f, 1f, 0f);
                col.radius = 0.5f;
                col.height = 2f;
            }
        }

        private static AudioClip GenerateProceduralClip(string clipName, float frequency, float duration, bool noise)
        {
            int sampleRate = 44100;
            int samplesCount = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samplesCount];

            for (int i = 0; i < samplesCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = 1f - (float)i / samplesCount;
                if (noise)
                {
                    data[i] = (UnityEngine.Random.value * 2f - 1f) * envelope * 0.45f;
                }
                else
                {
                    data[i] = Mathf.Sin(2 * Mathf.PI * frequency * t) * envelope * 0.5f;
                }
            }

            AudioClip clip = AudioClip.Create(clipName, samplesCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (agent == null) agent = GetComponent<NavMeshAgent>() ?? gameObject.AddComponent<NavMeshAgent>();
            if (agent != null) agent.baseOffset = 1.0f;

            EraManager.OnEraChanged += HandleEraChanged;
            NoiseEmitter.OnNoiseMade += HandleNoiseMade;

            // Check era on startup
            if (EraManager.Instance != null)
            {
                ApplyEraState(EraManager.Instance.CurrentEra);
            }
            else
            {
                ApplyEraState(TimelineEra.Past);
            }
        }

        private void OnDisable()
        {
            EraManager.OnEraChanged -= HandleEraChanged;
            NoiseEmitter.OnNoiseMade -= HandleNoiseMade;
        }

        // ──────────────────────────────────────────────────
        // ERA TIMELINE SHIFTS (PAST ONLY)
        // ──────────────────────────────────────────────────

        private void HandleEraChanged(TimelineEra newEra)
        {
            ApplyEraState(newEra);
        }

        private void ApplyEraState(TimelineEra era)
        {
            if (era == TimelineEra.Past)
            {
                // Active in the Past: respawn and enable
                isDeadOrDisabled = false;

                // Move to spawn point
                Vector3 respawnPos = spawnPoint != null ? spawnPoint.position : _initialSpawnPosition;
                Quaternion respawnRot = spawnPoint != null ? spawnPoint.rotation : _initialSpawnRotation;

                if (agent != null)
                {
                    agent.enabled = false;
                    transform.position = respawnPos;
                    transform.rotation = respawnRot;
                    agent.enabled = true;
                }
                else
                {
                    transform.position = respawnPos;
                    transform.rotation = respawnRot;
                }

                SetVisualsAndCollision(true);
                SetState(GhostState.Idle);
                Debug.Log("[Echo] Materialized in the Past laboratory.");
            }
            else
            {
                // Inactive in the Present: vanish completely
                isDeadOrDisabled = true;

                if (agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                    agent.enabled = false;
                }

                SetVisualsAndCollision(false);
                Debug.Log("[Echo] Dematerialized in the Present facility.");
            }
        }

        private void SetVisualsAndCollision(bool active)
        {
            if (_renderers != null)
            {
                foreach (var r in _renderers)
                    if (r != null) r.enabled = active;
            }

            if (_colliders != null)
            {
                foreach (var c in _colliders)
                    if (c != null) c.enabled = active;
            }
        }

        // ──────────────────────────────────────────────────
        // SOUND DETECTION (NoiseEmitter.OnNoiseMade)
        // ──────────────────────────────────────────────────

        private void HandleNoiseMade(Vector3 noisePos, float noiseRadius)
        {
            // The Echo only hears sound while alive and active in the Past
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (isDeadOrDisabled || agent == null || !agent.enabled || !agent.isOnNavMesh) return;

            // Distance from ghost to noise origin
            float dist = Vector3.Distance(transform.position, noisePos);

            // Is the noise audible within hearing radius?
            if (dist <= hearingRadius)
            {
                float timeSinceLastNoise = Time.time - _lastNoiseTime;
                _lastNoiseTime = Time.time;

                if (timeSinceLastNoise <= repeatedNoiseWindow)
                {
                    _recentNoiseCount++;
                }
                else
                {
                    _recentNoiseCount = 1;
                }

                Debug.Log($"[Echo] Heard noise! Dist={dist:F1}m, Radius={noiseRadius:F1}m, Count={_recentNoiseCount}");

                // 1. Loud noise (sprint, explosion, temporal tear) -> ESCALATE TO HUNT IMMEDIATELY
                if (noiseRadius >= loudNoiseThreshold)
                {
                    TriggerHunt(noisePos, "Loud noise detected!");
                    return;
                }

                // 2. Repeated noise in short window or during Investigate -> ESCALATE TO HUNT
                if (_recentNoiseCount >= 2 || currentState == GhostState.Investigate)
                {
                    TriggerHunt(noisePos, "Repeated noise detected!");
                    return;
                }

                // 3. Normal noise -> INVESTIGATE
                if (currentState == GhostState.Idle || currentState == GhostState.Roam)
                {
                    investigateTargetPos = noisePos;
                    SetState(GhostState.Investigate);

                    if (audioSource != null && alertSound != null)
                        audioSource.PlayOneShot(alertSound);
                }
            }
        }

        private void TriggerHunt(Vector3 noiseOrigin, string reason)
        {
            lastKnownPlayerPos = noiseOrigin;

            if (audioSource != null && huntRoarSound != null)
                audioSource.PlayOneShot(huntRoarSound);

            Debug.Log($"[Echo] HUNT TRIGGERED: {reason}");
            SetState(GhostState.Hunt);
        }

        // ──────────────────────────────────────────────────
        // HUNT BEHAVIOUR
        // ──────────────────────────────────────────────────

        protected override void UpdateHunt()
        {
            // If the player is NOT currently hidden
            if (!PlayerHide.IsHidden)
            {
                if (playerTransform != null)
                {
                    lastKnownPlayerPos = playerTransform.position;
                    agent.SetDestination(playerTransform.position);

                    // Check for kill contact
                    float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
                    if (distToPlayer <= killDistance)
                    {
                        Kill();
                        return;
                    }
                }
            }
            else
            {
                // The player is hiding!
                // Head to last known position where the player was last tracked
                agent.SetDestination(lastKnownPlayerPos);

                // When arrived at the last known position, begin searching nearby lockers
                if (!agent.pathPending && agent.remainingDistance <= 1.2f)
                {
                    Debug.Log("[Echo] Arrived at last known player position. Starting Search for lockers!");
                    PrepareLockerSearch();
                    SetState(GhostState.Search);
                    return;
                }
            }

            // Hunt timeout fallback
            if (stateTimer >= huntMaxDuration)
            {
                Debug.Log("[Echo] Hunt timed out without catching player. Searching area.");
                PrepareLockerSearch();
                SetState(GhostState.Search);
            }
        }

        // ──────────────────────────────────────────────────
        // SEARCH BEHAVIOUR (LOCKERS & BREATH-HOLD MECHANIC)
        // ──────────────────────────────────────────────────

        private void PrepareLockerSearch()
        {
            _spotsToSearch.Clear();
            _currentSearchSpotIndex = 0;
            _isInspectingSpot = false;
            _spotInspectTimer = 0f;

            // Find all active hiding spots in the scene within search radius
            foreach (HidingSpot spot in HidingSpot.AllSpots)
            {
                if (spot == null || !spot.gameObject.activeInHierarchy) continue;

                float dist = Vector3.Distance(lastKnownPlayerPos, spot.transform.position);
                if (dist <= searchHidingRadius)
                {
                    _spotsToSearch.Add(spot);
                }
            }

            // Sort closest first
            _spotsToSearch.Sort((a, b) =>
                Vector3.Distance(transform.position, a.transform.position)
                .CompareTo(Vector3.Distance(transform.position, b.transform.position))
            );

            Debug.Log($"[Echo] Found {_spotsToSearch.Count} nearby hiding spots to inspect.");
        }

        protected override void UpdateSearch()
        {
            // If search time exceeded, enter cooldown
            if (stateTimer >= searchMaxDuration)
            {
                Debug.Log("[Echo] Search duration exceeded. Entering Cooldown.");
                SetState(GhostState.Cooldown);
                return;
            }

            // If no spots to search, wander around last known pos or cooldown
            if (_spotsToSearch.Count == 0 || _currentSearchSpotIndex >= _spotsToSearch.Count)
            {
                SetState(GhostState.Cooldown);
                return;
            }

            HidingSpot targetSpot = _spotsToSearch[_currentSearchSpotIndex];
            if (targetSpot == null)
            {
                _currentSearchSpotIndex++;
                return;
            }

            if (!_isInspectingSpot)
            {
                // Move towards target locker exit/front point
                Vector3 inspectPoint = targetSpot.ExitPoint.position;
                agent.SetDestination(inspectPoint);

                if (!agent.pathPending && agent.remainingDistance <= 1.2f)
                {
                    // Arrived at locker! Begin inspection
                    _isInspectingSpot = true;
                    _spotInspectTimer = 0f;
                    agent.isStopped = true;

                    // Face locker
                    Vector3 lookDir = (targetSpot.transform.position - transform.position).normalized;
                    lookDir.y = 0f;
                    if (lookDir != Vector3.zero)
                    {
                        transform.rotation = Quaternion.LookRotation(lookDir);
                    }

                    Debug.Log($"[Echo] Inspecting hiding spot: {targetSpot.name}...");
                }
            }
            else
            {
                // Staring into / sniffing locker
                _spotInspectTimer += Time.deltaTime;

                if (_spotInspectTimer >= inspectLockerDuration)
                {
                    // Evaluate catch: Is the player inside this specific spot?
                    if (HidingSpot.PlayerIsHiddenAt == targetSpot)
                    {
                        bool isHoldingBreath = PlayerHide.IsPlayerHoldingBreath;
                        float catchChance = isHoldingBreath ? catchChanceWhileHoldingBreath : catchChanceWhileBreathing;
                        float roll = UnityEngine.Random.value;

                        Debug.Log($"[Echo] Player is inside! HoldingBreath={isHoldingBreath}, CatchChance={catchChance:P0}, Roll={roll:F2}");

                        if (roll < catchChance)
                        {
                            // CAUGHT! Force player out and kill
                            Debug.LogWarning("[Echo] DISCOVERED PLAYER IN LOCKER! Tearing locker open!");
                            if (PlayerHide.Instance != null)
                            {
                                PlayerHide.Instance.ForceEject();
                            }
                            Kill();
                            return;
                        }
                        else
                        {
                            Debug.Log("[Echo] Player held breath and stayed completely silent. Echo moved on!");
                        }
                    }

                    // Move on to next spot
                    _isInspectingSpot = false;
                    _spotInspectTimer = 0f;
                    _currentSearchSpotIndex++;
                    agent.isStopped = false;
                }
            }
        }

        public override void Kill()
        {
            if (audioSource != null && killSound != null)
            {
                audioSource.PlayOneShot(killSound);
            }

            base.Kill();
        }

        // ──────────────────────────────────────────────────
        // GIZMOS (HEARING RADIUS VISUALIZATION)
        // ──────────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            DrawHearingGizmos();
        }

        private void OnDrawGizmos()
        {
            DrawHearingGizmos();
        }

        private void DrawHearingGizmos()
        {
            Color color;
            switch (currentState)
            {
                case GhostState.Hunt:
                    color = new Color(1f, 0.1f, 0.1f, 0.35f); // Red
                    break;
                case GhostState.Investigate:
                    color = new Color(1f, 0.9f, 0.1f, 0.3f); // Yellow
                    break;
                case GhostState.Search:
                    color = new Color(1f, 0.5f, 0.1f, 0.3f); // Orange
                    break;
                default:
                    color = new Color(0.2f, 0.8f, 1f, 0.25f); // Cyan
                    break;
            }

            Gizmos.color = color;
            Gizmos.DrawWireSphere(transform.position, hearingRadius);

            // Draw kill distance
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, killDistance);

            // Draw investigation destination ray if investigating
            if (currentState == GhostState.Investigate)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, investigateTargetPos);
                Gizmos.DrawWireSphere(investigateTargetPos, 0.6f);
            }
        }
    }
}
