using UnityEngine;

namespace FPS.Player
{
    /// <summary>
    /// First-person look + aim. Handles the sniper 2x scope zoom by driving camera FOV,
    /// and offsets the near clip plane so the weapon model does not clip into geometry.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PlayerCamera : MonoBehaviour
    {
        [Header("Look")]
        [SerializeField] private float sensitivity = 2.2f;
        [SerializeField] private float minPitch = -85f;
        [SerializeField] private float maxPitch = 85f;
        [SerializeField] private Transform playerRoot;

        [Header("Field of view")]
        [SerializeField] private float baseFov = 70f;
        [SerializeField] private float adsFov = 45f;
        [SerializeField] private float sniperAdsFov = 30f; // ~2x from the 60 base
        [SerializeField] private float fovLerpSpeed = 14f;
        [SerializeField] private float sprintFovBonus = 8f;

        private Camera _cam;
        private float _pitch;
        private float _targetFov;
        private float _appliedFov;
        private float _recoilPitch;
        private float _recoilYaw;
        private float _recoilRecovery;

        public Camera Cam => _cam;
        public bool IsAiming { get; private set; }
        public float CurrentFov => _appliedFov;

        public void SetSniperZoom(bool sniper, bool aiming)
        {
            IsAiming = aiming;
            if (!aiming) { _targetFov = baseFov; return; }
            _targetFov = sniper ? sniperAdsFov : adsFov;
        }

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _appliedFov = baseFov;
            _cam.fieldOfView = baseFov;
            _cam.nearClipPlane = 0.02f;
        }

        private void Update()
        {
            Vector2 look = InputHub.Look * sensitivity;

            // recoil is applied to the view then springs back
            _pitch = Mathf.Clamp(_pitch - look.y - _recoilPitch, minPitch, maxPitch);
            float yaw = look.x + _recoilYaw;

            if (playerRoot != null)
            {
                playerRoot.localRotation = Quaternion.Euler(0f, yaw, 0f);
            }
            transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);

            RecoverRecoil(Time.deltaTime);

            // sprinting widens FOV a touch for a sense of speed
            var pc = GetComponentInParent<PlayerController>();
            float bonus = (pc != null && pc.IsSprinting && !IsAiming) ? sprintFovBonus : 0f;
            float goal = _targetFov + bonus;
            _appliedFov = Mathf.Lerp(_appliedFov, goal, 1f - Mathf.Exp(-fovLerpSpeed * Time.deltaTime));
            _cam.fieldOfView = _appliedFov;
        }

        /// <summary>Kick the view upward (and slightly sideways) when firing.</summary>
        public void ApplyRecoil(float pitchDegrees, float yawDegrees, float recovery = 6f)
        {
            _recoilPitch += pitchDegrees;
            _recoilYaw += (Random.value > 0.5f ? 1f : -1f) * yawDegrees;
            _recoilRecovery = recovery;
        }

        private void RecoverRecoil(float dt)
        {
            if (Mathf.Abs(_recoilPitch) < 0.0001f && Mathf.Abs(_recoilYaw) < 0.0001f) return;
            float t = 1f - Mathf.Exp(-_recoilRecovery * dt);
            _recoilPitch = Mathf.Lerp(_recoilPitch, 0f, t);
            _recoilYaw = Mathf.Lerp(_recoilYaw, 0f, t);
        }

        public void ResetRecoil()
        {
            _recoilPitch = 0f;
            _recoilYaw = 0f;
            _pitch = 0f;
        }
    }
}
