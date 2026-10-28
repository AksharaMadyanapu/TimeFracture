// ============================================================
// GhostBase.cs
// ------------------------------------------------------------
// Abstract base class for all Ghost AI entities in Time Fracture.
//
// RESPONSIBILITIES:
//   1. NavMeshAgent-driven finite state machine:
//      Idle -> Roam -> Investigate -> Hunt -> Search -> Cooldown
//   2. Patrols waypoints during Roam.
//   3. Controls movement speeds per state.
//   4. Broadcasts state change events (OnStateChanged) and kill events (OnPlayerKilled).
//   5. Provides template methods for specialized ghost sensors (Sound for Echo, Light for Residue).
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using TimeFracture.Player;
using TimeFracture.Hiding;

namespace TimeFracture.Ghost
{
    public enum GhostState
    {
        Idle,
        Roam,
        Investigate,
        Hunt,
        Search,
        Cooldown
    }

    [RequireComponent(typeof(NavMeshAgent))]
    public abstract class GhostBase : MonoBehaviour
    {
        // ──────────────────────────────────────────────────
        // STATIC & INSTANCE EVENTS
        // ──────────────────────────────────────────────────

        /// <summary>Fired whenever any ghost kills the player (triggers game fail state).</summary>
        public static event Action OnPlayerKilled;

        /// <summary>Fired whenever this ghost's AI state changes.</summary>
        public event Action<GhostState> OnStateChanged;

        // ──────────────────────────────────────────────────
        // INSPECTOR FIELDS — STATE SPEEDS & TIMERS
        // ──────────────────────────────────────────────────

        [Header("Patrol Waypoints")]
        [Tooltip("Waypoints for roaming patrol. If empty, ghost roams near spawn position.")]
        [SerializeField] protected Transform[] waypoints;

        [Tooltip("Minimum distance to waypoint to consider it reached.")]
        [SerializeField] protected float waypointArrivalDistance = 1.0f;

        [Header("State Movement Speeds")]
        [Tooltip("Speed when patrolling waypoints.")]
        [SerializeField] protected float roamSpeed = 2.0f;

        [Tooltip("Speed when investigating a suspicious noise or disturbance.")]
        [SerializeField] protected float investigateSpeed = 3.2f;

        [Tooltip("Speed when actively hunting the player.")]
        [SerializeField] protected float huntSpeed = 5.0f;

        [Tooltip("Speed when searching nearby hiding spots.")]
        [SerializeField] protected float searchSpeed = 2.4f;

        [Tooltip("Speed during cooldown / recovery.")]
        [SerializeField] protected float cooldownSpeed = 1.5f;

        [Header("Timers & Delays")]
        [Tooltip("How long to wait in Idle before moving to the next waypoint.")]
        [SerializeField] protected float idleDuration = 3.0f;

        [Tooltip("How long to search and look around an investigation point before giving up.")]
        [SerializeField] protected float investigateDuration = 4.0f;

        [Tooltip("Maximum duration of a hunt before giving up into Search or Cooldown.")]
        [SerializeField] protected float huntMaxDuration = 12.0f;

        [Tooltip("Duration of cooldown state before resuming patrol.")]
        [SerializeField] protected float cooldownDuration = 5.0f;

        [Header("Attack / Kill Distance")]
        [Tooltip("Distance to player at which the ghost kills the player.")]
        [SerializeField] protected float killDistance = 1.4f;

        // ──────────────────────────────────────────────────
        // PROTECTED & INTERNAL STATE
        // ──────────────────────────────────────────────────

        protected NavMeshAgent agent;
        protected Transform playerTransform;
        protected GhostState currentState = GhostState.Idle;

        protected Vector3 lastKnownPlayerPos;
        protected Vector3 investigateTargetPos;

        protected float stateTimer = 0f;
        protected int currentWaypointIndex = 0;
        protected bool isDeadOrDisabled = false;

        public GhostState CurrentState => currentState;
        public Transform[] Waypoints => waypoints;
        public NavMeshAgent Agent => agent;

        // ──────────────────────────────────────────────────
        // UNITY LIFECYCLE
        // ──────────────────────────────────────────────────

        protected virtual void Awake()
        {
            SelfWireComponents();
        }

        protected virtual void Reset()
        {
            SelfWireComponents();
        }

        protected virtual void SelfWireComponents()
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (agent == null) agent = gameObject.AddComponent<NavMeshAgent>();

            // Configure agent defaults
            if (agent != null)
            {
                agent.speed = roamSpeed > 0f ? roamSpeed : 2.0f;
                agent.angularSpeed = 240f;
                agent.acceleration = 12f;
                agent.stoppingDistance = waypointArrivalDistance > 0f ? waypointArrivalDistance : 1.0f;
                agent.height = 2f;
                agent.radius = 0.5f;
            }

            // Find player transform with robust multi-step fallbacks
            if (playerTransform == null)
            {
                PlayerController pc = FindFirstObjectByType<PlayerController>();
                if (pc != null)
                {
                    playerTransform = pc.transform;
                }
                else
                {
                    GameObject pObj = GameObject.FindWithTag("Player");
                    if (pObj != null)
                    {
                        playerTransform = pObj.transform;
                    }
                    else
                    {
                        CharacterController cc = FindFirstObjectByType<CharacterController>();
                        if (cc != null)
                        {
                            playerTransform = cc.transform;
                        }
                        else if (Camera.main != null)
                        {
                            playerTransform = Camera.main.transform.root;
                        }
                    }
                }
            }

            // Self-wire waypoints if not explicitly assigned in inspector
            if (waypoints == null || waypoints.Length == 0)
            {
                AutoFindWaypoints();
            }
        }

        protected void AutoFindWaypoints()
        {
            GameObject wpGroup = GameObject.Find("Echo_Waypoints") ?? GameObject.Find("_Milestone5_Test/Echo_Waypoints");
            if (wpGroup != null && wpGroup.transform.childCount > 0)
            {
                waypoints = new Transform[wpGroup.transform.childCount];
                for (int i = 0; i < wpGroup.transform.childCount; i++)
                {
                    waypoints[i] = wpGroup.transform.GetChild(i);
                }
                Debug.Log($"[{GetType().Name}] Auto-wired {waypoints.Length} waypoints from '{wpGroup.name}'.");
            }
        }

        protected virtual void Start()
        {
            if (waypoints == null || waypoints.Length == 0)
            {
                AutoFindWaypoints();
            }

            SetState(GhostState.Idle);
        }

        protected virtual void Update()
        {
            if (isDeadOrDisabled || !agent.enabled || !agent.isOnNavMesh) return;

            stateTimer += Time.deltaTime;

            switch (currentState)
            {
                case GhostState.Idle:
                    UpdateIdle();
                    break;
                case GhostState.Roam:
                    UpdateRoam();
                    break;
                case GhostState.Investigate:
                    UpdateInvestigate();
                    break;
                case GhostState.Hunt:
                    UpdateHunt();
                    break;
                case GhostState.Search:
                    UpdateSearch();
                    break;
                case GhostState.Cooldown:
                    UpdateCooldown();
                    break;
            }
        }

        // ──────────────────────────────────────────────────
        // STATE MACHINE TRANSITIONS
        // ──────────────────────────────────────────────────

        public virtual void SetState(GhostState newState)
        {
            if (currentState == newState && stateTimer > 0.05f) return;

            currentState = newState;
            stateTimer = 0f;

            if (agent.enabled && agent.isOnNavMesh)
            {
                switch (newState)
                {
                    case GhostState.Idle:
                        agent.speed = 0f;
                        agent.isStopped = true;
                        break;
                    case GhostState.Roam:
                        agent.speed = roamSpeed;
                        agent.isStopped = false;
                        SetDestinationToCurrentWaypoint();
                        break;
                    case GhostState.Investigate:
                        agent.speed = investigateSpeed;
                        agent.isStopped = false;
                        agent.SetDestination(investigateTargetPos);
                        break;
                    case GhostState.Hunt:
                        agent.speed = huntSpeed;
                        agent.isStopped = false;
                        if (playerTransform != null) agent.SetDestination(playerTransform.position);
                        break;
                    case GhostState.Search:
                        agent.speed = searchSpeed;
                        agent.isStopped = false;
                        break;
                    case GhostState.Cooldown:
                        agent.speed = cooldownSpeed;
                        agent.isStopped = false;
                        break;
                }
            }

            OnStateChanged?.Invoke(newState);
            Debug.Log($"[{GetType().Name}] Entered state: {newState}");
        }

        // ──────────────────────────────────────────────────
        // STATE UPDATE LOGIC
        // ──────────────────────────────────────────────────

        protected virtual void UpdateIdle()
        {
            if (stateTimer >= idleDuration)
            {
                AdvanceWaypoint();
                SetState(GhostState.Roam);
            }
        }

        protected virtual void UpdateRoam()
        {
            if (!agent.pathPending && agent.remainingDistance <= waypointArrivalDistance)
            {
                SetState(GhostState.Idle);
            }
        }

        protected virtual void UpdateInvestigate()
        {
            // If arrived at investigation point, look around for investigateDuration
            if (!agent.pathPending && agent.remainingDistance <= waypointArrivalDistance + 0.5f)
            {
                if (stateTimer >= investigateDuration)
                {
                    SetState(GhostState.Cooldown);
                }
            }
        }

        protected abstract void UpdateHunt();
        protected abstract void UpdateSearch();

        protected virtual void UpdateCooldown()
        {
            if (stateTimer >= cooldownDuration)
            {
                SetState(GhostState.Idle);
            }
        }

        // ──────────────────────────────────────────────────
        // WAYPOINT HELPERS
        // ──────────────────────────────────────────────────

        protected void SetDestinationToCurrentWaypoint()
        {
            if (!agent.enabled || !agent.isOnNavMesh) return;

            if (waypoints != null && waypoints.Length > 0)
            {
                if (currentWaypointIndex >= 0 && currentWaypointIndex < waypoints.Length && waypoints[currentWaypointIndex] != null)
                {
                    agent.SetDestination(waypoints[currentWaypointIndex].position);
                    return;
                }
            }

            // Fallback: roam in a random direction on NavMesh
            Vector3 randomDirection = UnityEngine.Random.insideUnitSphere * 8f + transform.position;
            if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, 8f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
        }

        protected void AdvanceWaypoint()
        {
            if (waypoints != null && waypoints.Length > 0)
            {
                currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
            }
        }

        // ──────────────────────────────────────────────────
        // KILL / FAIL STATE HOOK
        // ──────────────────────────────────────────────────

        /// <summary>
        /// Kills the player and broadcasts the static fail event.
        /// </summary>
        public virtual void Kill()
        {
            Debug.LogWarning($"[{GetType().Name}] Killed the player!");
            OnPlayerKilled?.Invoke();
        }

        // ──────────────────────────────────────────────────
        // PUBLIC TUNING ACCESSORS
        // ──────────────────────────────────────────────────

        public void SetWaypoints(Transform[] newWaypoints)
        {
            waypoints = newWaypoints;
            currentWaypointIndex = 0;
        }

        // ──────────────────────────────────────────────────
        // MILESTONE 6 HUNT HOOK
        // ──────────────────────────────────────────────────

        /// <summary>
        /// True when this ghost is able to begin hunting (not already hunting or dead).
        /// Used by GhostActivityManager to decide whether to call StartHunt().
        /// </summary>
        public bool CanHunt => !isDeadOrDisabled &&
                               currentState != GhostState.Hunt &&
                               currentState != GhostState.Search;

        /// <summary>
        /// Called by GhostActivityManager when the activity meter hits the Hunting tier.
        /// Forces the ghost into Hunt state immediately, targeting the player.
        /// </summary>
        public void StartHunt()
        {
            if (!CanHunt) return;

            // Make sure we have a player position to aim at
            if (playerTransform != null)
            {
                lastKnownPlayerPos = playerTransform.position;
                investigateTargetPos = playerTransform.position;
            }

            SetState(GhostState.Hunt);
            Debug.Log($"[{GetType().Name}] Hunt triggered by GhostActivityManager!");
        }
    }
}
