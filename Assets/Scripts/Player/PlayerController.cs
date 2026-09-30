using UnityEngine;

namespace FPS.Player
{
    public enum Stance
    {
        Standing = 0,
        Crouching = 1
    }

    /// <summary>
    /// Character movement for the player: walk, sprint, crouch and jump.
    /// Uses CharacterController so the procedural animation can read a clean
    /// planar speed and grounded flag.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Speeds (m/s)")]
        [SerializeField] private float walkSpeed = 3.4f;
        [SerializeField] private float sprintSpeed = 6.2f;
        [SerializeField] private float crouchSpeed = 1.7f;
        [SerializeField] private float airControl = 0.35f;

        [Header("Jump / gravity")]
        [SerializeField] private float jumpHeight = 1.15f;
        [SerializeField] private float gravity = -24f;
        [SerializeField] private float coyoteTime = 0.12f;

        [Header("Stance")]
        [SerializeField] private float standHeight = 1.8f;
        [SerializeField] private float crouchHeight = 1.1f;
        [SerializeField] private float stanceLerpSpeed = 12f;

        [Header("Ground")]
        [SerializeField] private float groundCheckDistance = 0.22f;
        [SerializeField] private LayerMask groundMask = ~0;

        private CharacterController _cc;
        private float _verticalVelocity;
        private float _lastGroundedTime = -999f;
        private Stance _stance = Stance.Standing;

        public float PlanarSpeed { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsCrouching => _stance == Stance.Crouching;
        public Stance CurrentStance => _stance;
        public float CurrentSpeed => PlanarSpeed;
        public Transform Transform => transform;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _cc.height = standHeight;
            _cc.center = new Vector3(0f, standHeight * 0.5f, 0f);
        }

        private void Update()
        {
            InputHub.Pump();

            IsGrounded = _cc.isGrounded || CheckGround();
            if (IsGrounded)
            {
                _lastGroundedTime = Time.time;
                if (_verticalVelocity < 0f) _verticalVelocity = -2f;
            }

            // ---- stance ----
            if (InputHub.CrouchQueued)
            {
                if (_stance == Stance.Standing)
                {
                    _stance = Stance.Crouching;
                }
                else if (HasHeadroom(standHeight))
                {
                    // only stand up when there is clearance, otherwise stay crouched
                    _stance = Stance.Standing;
                }
            }
            ApplyStance();

            // ---- horizontal ----
            Vector2 input = InputHub.Move;
            Vector3 wishDir = transform.right * input.x + transform.forward * input.y;

            IsSprinting = InputHub.SprintHeld && _stance == Stance.Standing && input.y > 0.1f;

            float targetSpeed = _stance == Stance.Crouching
                ? crouchSpeed
                : (IsSprinting ? sprintSpeed : walkSpeed);

            Vector3 horiz = wishDir * targetSpeed;
            Vector3 current = new Vector3(_cc.velocity.x, 0f, _cc.velocity.z);

            float control = IsGrounded ? 1f : airControl;
            current = Vector3.Lerp(current, horiz, 1f - Mathf.Exp(-14f * control * Time.deltaTime));
            if (wishDir.sqrMagnitude < 0.0001f && IsGrounded)
            {
                current = Vector3.Lerp(current, Vector3.zero, 1f - Mathf.Exp(-20f * Time.deltaTime));
            }

            PlanarSpeed = new Vector2(current.x, current.z).magnitude;

            // ---- jump / gravity ----
            bool canCoyote = (Time.time - _lastGroundedTime) <= coyoteTime;
            if (InputHub.JumpQueued && canCoyote && _stance == Stance.Standing)
            {
                _verticalVelocity = Mathf.Sqrt(-2f * gravity * jumpHeight);
                _lastGroundedTime = -999f;
            }
            _verticalVelocity += gravity * Time.deltaTime;

            Vector3 motion = current + Vector3.up * _verticalVelocity;
            _cc.Move(motion * Time.deltaTime);
        }

        private void ApplyStance()
        {
            float target = _stance == Stance.Crouching ? crouchHeight : standHeight;
            float t = 1f - Mathf.Exp(-stanceLerpSpeed * Time.deltaTime);
            float newH = Mathf.Lerp(_cc.height, target, t);
            _cc.height = newH;
            _cc.center = new Vector3(0f, newH * 0.5f, 0f);
        }

        private bool HasHeadroom(float wantedHeight)
        {
            float radius = _cc.radius * 0.95f;
            Vector3 bottom = transform.position + Vector3.up * (radius + 0.05f);
            Vector3 top = transform.position + Vector3.up * (wantedHeight - radius);
            return !Physics.CheckCapsule(bottom, top, radius, groundMask, QueryTriggerInteraction.Ignore);
        }

        private bool CheckGround()
        {
            return Physics.Raycast(
                transform.position + Vector3.up * 0.05f,
                Vector3.down,
                out RaycastHit hit,
                _cc.height * 0.5f + groundCheckDistance,
                groundMask,
                QueryTriggerInteraction.Ignore);
        }
    }
}
