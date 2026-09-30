using UnityEngine;
using UnityEngine.AI;
using FPS.Core;
using FPS.Player;

namespace FPS.AI
{
    public enum AIState
    {
        Idle = 0,
        Patrol = 1,
        Chase = 2,
        Combat = 3,
        Reposition = 4,
        Dead = 5
    }

    /// <summary>
    /// PVE enemy brain. Runs a small state machine (idle -> patrol -> chase -> combat ->
    /// reposition) with line-of-sight awareness, accuracy that rewards standing still,
    /// and hit reactions. Uses NavMeshAgent for navigation.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Health))]
    public class EnemyAI : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField] private float sightRange = 45f;
        [SerializeField] private float fieldOfView = 120f;
        [SerializeField] private float hearRange = 18f;
        [SerializeField] private float memorySeconds = 5f;

        [Header("Combat")]
        [SerializeField] private float damage = 11f;
        [SerializeField] private float shotsPerSecond = 3.2f;
        [SerializeField] private float burstPause = 0.55f;
        [SerializeField] private float accuracy = 0.55f;
        [SerializeField] private float reactionTime = 0.28f;
        [SerializeField] private float engageRange = 32f;
        [SerializeField] private float loseRange = 45f;

        [Header("Movement")]
        [SerializeField] private float patrolSpeed = 2.1f;
        [SerializeField] private float chaseSpeed = 4.6f;
        [SerializeField] private float strafeSpeed = 3.2f;
        [SerializeField] private float preferredRange = 12f;
        [SerializeField] private float repositionInterval = 2.6f;

        [Header("Refs")]
        [SerializeField] private Transform eye;
        [SerializeField] private LayerMask sightMask = ~0;
        [SerializeField] private LayerMask obstacleMask = ~0;

        private NavMeshAgent _agent;
        private Health _health;
        private Transform _target;
        private ProceduralAnimator _anim;
        private PlayerController _targetController;

        private AIState _state = AIState.Idle;
        private float _nextShotTime;
        private float _lastSeenTime = -999f;
        private float _stateEnterTime;
        private float _nextReposition;
        private Vector3 _strafeDir;
        private float _reactionDeadline;
        private bool _hasLineOfSight;
        private Vector3 _homePosition;

        public AIState State => _state;
        public Health HealthRef => _health;
        public bool IsEngaged => _state == AIState.Combat || _state == AIState.Chase;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();
            _anim = GetComponentInChildren<ProceduralAnimator>();
            _homePosition = transform.position;
            _health.Died += OnDied;
        }

        private void OnEnable() => AcquireTarget();
        private void Start() { AcquireTarget(); EnterState(_agent != null && _agent.isOnNavMesh ? AIState.Patrol : AIState.Idle); }

        public void AcquireTarget()
        {
            if (_target != null) return;
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null)
            {
                _target = pc.transform;
                _targetController = pc;
            }
        }

        public void Configure(float newDamage, float sight = -1f)
        {
            damage = newDamage;
            if (sight > 0f) sightRange = sight;
        }

        private void OnDied(Health h)
        {
            EnterState(AIState.Dead);
            if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;
            if (_anim != null) _anim.enabled = false;
            // ragdoll-free: just disable and sink slightly
            StartCoroutine(SinkAndDisable());
        }

        private System.Collections.IEnumerator SinkAndDisable()
        {
            Vector3 start = transform.position;
            Quaternion rot = transform.rotation;
            for (float t = 0f; t < 1f; t += Time.deltaTime * 1.6f)
            {
                transform.position = start + Vector3.down * (t * 1.2f);
                transform.rotation = Quaternion.Slerp(rot, rot * Quaternion.Euler(88f, 0f, 0f), t);
                yield return null;
            }
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_state == AIState.Dead) return;
            if (_target == null) { AcquireTarget(); if (_target == null) { EnterState(AIState.Idle); return; } }

            float dist = Vector3.Distance(transform.position, _target.position);
            _hasLineOfSight = dist <= sightRange && CanSeeTarget();

            if (_hasLineOfSight) _lastSeenTime = Time.time;

            UpdateStateMachine(dist);
            UpdateMovement(dist);
            UpdateCombat(dist);
        }

        private void UpdateStateMachine(float dist)
        {
            float sinceSeen = Time.time - _lastSeenTime;

            switch (_state)
            {
                case AIState.Idle:
                    if (dist <= sightRange && _hasLineOfSight) EnterState(AIState.Chase);
                    else if (Time.time - _stateEnterTime > 3f) EnterState(AIState.Patrol);
                    break;

                case AIState.Patrol:
                    if (_hasLineOfSight) EnterState(AIState.Chase);
                    break;

                case AIState.Chase:
                    if (!_hasLineOfSight && sinceSeen > memorySeconds) EnterState(AIState.Patrol);
                    else if (_hasLineOfSight && dist <= engageRange) EnterState(AIState.Combat);
                    break;

                case AIState.Combat:
                    if (!_hasLineOfSight && sinceSeen > memorySeconds) EnterState(AIState.Chase);
                    else if (dist > loseRange) EnterState(AIState.Chase);
                    break;

                case AIState.Reposition:
                    if (Time.time >= _nextReposition) EnterState(_hasLineOfSight ? AIState.Combat : AIState.Chase);
                    break;
            }
        }

        private void UpdateMovement(float dist)
        {
            if (_agent == null || !_agent.isOnNavMesh) return;

            _agent.speed = _state == AIState.Combat ? strafeSpeed
                          : _state == AIState.Chase ? chaseSpeed
                          : patrolSpeed;

            switch (_state)
            {
                case AIState.Patrol:
                    if (!_agent.hasPath || _agent.remainingDistance < 1.2f)
                    {
                        Vector2 c = Random.insideUnitCircle.normalized * 12f;
                        _agent.SetDestination(_homePosition + new Vector3(c.x, 0f, c.y));
                    }
                    _agent.isStopped = false;
                    break;

                case AIState.Chase:
                    _agent.isStopped = false;
                    _agent.SetDestination(_target.position);
                    break;

                case AIState.Combat:
                    // hold a preferred stand-off distance and strafe sideways
                    Vector3 toTarget = (_target.position - transform.position);
                    toTarget.y = 0f;
                    float d = toTarget.magnitude;
                    Vector3 fwd = toTarget.normalized;
                    Vector3 right = Vector3.Cross(Vector3.up, fwd);

                    if (Time.time >= _nextReposition)
                    {
                        _nextReposition = Time.time + repositionInterval;
                        _strafeDir = Random.value > 0.5f ? right : -right;
                    }

                    Vector3 dest = transform.position;
                    if (d > preferredRange * 1.25f) dest += fwd * 4f;
                    else if (d < preferredRange * 0.6f) dest -= fwd * 4f;
                    dest += _strafeDir * 3f;

                    _agent.isStopped = false;
                    _agent.SetDestination(dest);
                    break;
            }
        }

        private void UpdateCombat(float dist)
        {
            if (_state != AIState.Combat || _target == null) return;

            // reaction delay before the first shot after acquiring
            if (_reactionDeadline == 0f && _hasLineOfSight) _reactionDeadline = Time.time + reactionTime;

            bool canFire = _hasLineOfSight && dist <= engageRange && Time.time >= _nextShotTime
                           && Time.time >= _reactionDeadline && !Blocked();

            if (canFire)
            {
                FireAtTarget();
                _nextShotTime = Time.time + (1f / Mathf.Max(0.1f, shotsPerSecond)) + Random.Range(0f, burstPause);
                _reactionDeadline = 0f;
            }

            // face the target
            if (eye != null)
            {
                Vector3 look = _target.position + Vector3.up * 1.2f - eye.position;
                Quaternion want = Quaternion.LookRotation(look);
                eye.rotation = Quaternion.Slerp(eye.rotation, want, 8f * Time.deltaTime);
            }
        }

        private void FireAtTarget()
        {
            Vector3 origin = eye != null ? eye.position : transform.position + Vector3.up * 1.5f;
            Vector3 targetPoint = _target.position + Vector3.up * 1.1f;
            Vector3 dir = (targetPoint - origin).normalized;

            // scatter based on accuracy and how fast we are moving
            float scatter = (1f - Mathf.Clamp01(accuracy)) * 8f;
            if (_agent != null && _agent.velocity.magnitude > 0.5f) scatter *= 1.6f;
            dir = Quaternion.Euler(Random.Range(-scatter, scatter), Random.Range(-scatter, scatter), 0f) * dir;

            if (Physics.Raycast(origin, dir, out RaycastHit hit, 120f, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                Health h = hit.collider.GetComponentInParent<Health>();
                if (h != null && !h.IsDead) h.ApplyDamage(damage, HitZone.Body);
                else SpawnTracer(origin, hit.point);
            }
            else
            {
                SpawnTracer(origin, origin + dir * 60f);
            }
        }

        private bool Blocked()
        {
            Vector3 origin = eye != null ? eye.position : transform.position + Vector3.up * 1.5f;
            Vector3 dir = (_target.position + Vector3.up * 1.1f - origin).normalized;
            if (Physics.Raycast(origin, dir, out RaycastHit hit, 120f, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                Health h = hit.collider.GetComponentInParent<Health>();
                return h == null; // world geometry in the way
            }
            return false;
        }

        [Header("Feedback")]
        [SerializeField] private LineRenderer tracer;
        [SerializeField] private GameObject muzzleFlash;

        private void SpawnTracer(Vector3 from, Vector3 to)
        {
            if (muzzleFlash != null)
            {
                muzzleFlash.SetActive(true);
                CancelInvoke(nameof(HideFlash));
                Invoke(nameof(HideFlash), 0.05f);
            }
            if (tracer == null) return;
            tracer.positionCount = 2;
            tracer.SetPosition(0, from);
            tracer.SetPosition(1, to);
            tracer.enabled = true;
            CancelInvoke(nameof(HideTracer));
            Invoke(nameof(HideTracer), 0.06f);
        }

        private void HideFlash() { if (muzzleFlash != null) muzzleFlash.SetActive(false); }
        private void HideTracer() { if (tracer != null) tracer.enabled = false; }

        private bool CanSeeTarget()
        {
            if (_target == null) return false;
            Vector3 origin = eye != null ? eye.position : transform.position + Vector3.up * 1.5f;
            Vector3 targetPoint = _target.position + Vector3.up * 1.1f;
            Vector3 to = targetPoint - origin;

            // field of view check (skip when very close - they can hear you)
            if (to.magnitude > 3f)
            {
                float ang = Vector3.Angle(transform.forward, to);
                if (ang > fieldOfView * 0.5f) return false;
            }

            if (Physics.Raycast(origin, to.normalized, out RaycastHit hit, to.magnitude, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                return hit.collider.GetComponentInParent<PlayerController>() != null;
            }
            return false;
        }

        private void EnterState(AIState next)
        {
            _state = next;
            _stateEnterTime = Time.time;
            if (next == AIState.Combat) _reactionDeadline = 0f;
        }

        /// <summary>Called by the spawner so agents do not instantly notice the player.</summary>
        public void SetHome(Vector3 pos)
        {
            _homePosition = pos;
            transform.position = pos;
        }
    }
}
