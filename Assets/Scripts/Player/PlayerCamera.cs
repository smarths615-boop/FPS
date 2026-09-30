using UnityEngine;
using FPS.Core;

namespace FPS.Player
{
    /// <summary>
    /// First-person look, aim and recoil.
    ///
    /// Recoil is modelled the way Counter-Strike does it: the shot applies an instant
    /// kick to the view, the kick is held briefly so it reads on screen, then springs
    /// back to the original aim point. Vertical and horizontal components are tracked
    /// separately so a spray pattern can be tuned per weapon.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PlayerCamera : MonoBehaviour
    {
        [Header("Look")]
        [SerializeField] private float sensitivity = 2.2f;
        [SerializeField] private float minPitch = -85f;
        [SerializeField] private float maxPitch = 85f;
        [Tooltip("Root that yaw is applied to. Auto-resolved to the player when left empty.")]
        [SerializeField] private Transform playerRoot;

        [Header("Field of view")]
        [SerializeField] private float baseFov = 70f;
        [SerializeField] private float adsFov = 45f;
        [SerializeField] private float sniperAdsFov = 30f; // ~2x from the 60 base
        [SerializeField] private float fovLerpSpeed = 16f;
        [SerializeField] private float sprintFovBonus = 6f;

        [Header("Recoil")]
        [Tooltip("Seconds the kick is held before it starts returning.")]
        [SerializeField] private float recoilHold = 0.055f;
        [SerializeField] private float returnSpeed = 13f;

        private Camera _cam;
        private float _pitch;
        private float _yaw;

        // recoil offsets relative to the current aim point
        private float _recoilPitch;
        private float _recoilYaw;
        private float _recoilHoldLeft;
        private float _returnRate = 9f;

        private float _targetFov;
        private float _appliedFov;

        public Camera Cam => _cam;
        public bool IsAiming { get; private set; }
        public float CurrentFov => _appliedFov;
        public float Yaw => _yaw;
        public float Pitch => _pitch;
        /// <summary>Current recoil offset from the aim point: x = yaw, y = pitch.</summary>
        public Vector2 RecoilOffset => new Vector2(_recoilYaw, _recoilPitch);

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _appliedFov = baseFov;
            _cam.fieldOfView = baseFov;
            _cam.nearClipPlane = 0.02f;

            if (playerRoot == null)
            {
                var pc = GetComponentInParent<PlayerController>();
                if (pc != null) playerRoot = pc.transform;
            }
            if (playerRoot == null) playerRoot = transform.parent != null ? transform.parent : transform;
        }

        public void SetSniperZoom(bool sniper, bool aiming)
        {
            IsAiming = aiming;
            if (!aiming) { _targetFov = baseFov; return; }
            _targetFov = sniper ? sniperAdsFov : adsFov;
        }

        private void Update()
        {
            Vector2 look = InputHub.Look * sensitivity;
            _yaw += look.x;
            _pitch = Mathf.Clamp(_pitch - look.y, minPitch, maxPitch);

            UpdateRecoil(Time.deltaTime);

            if (playerRoot != null && playerRoot != transform)
                playerRoot.localRotation = Quaternion.Euler(0f, _yaw + _recoilYaw, 0f);
            transform.localRotation = Quaternion.Euler(_pitch + _recoilPitch, 0f, 0f);

            var pc = GetComponentInParent<PlayerController>();
            float bonus = (pc != null && pc.IsSprinting && !IsAiming) ? sprintFovBonus : 0f;
            _appliedFov = Mathf.Lerp(_appliedFov, _targetFov + bonus, 1f - Mathf.Exp(-fovLerpSpeed * Time.deltaTime));
            _cam.fieldOfView = _appliedFov;
        }

        /// <summary>
        /// Applies an instant kick. Degrees are added straight to the view so the shot
        /// snaps rather than eases in, which is what makes a spray pattern feel punchy.
        /// Hold and return come from the weapon so each gun recovers at its own rate.
        /// </summary>
        public void ApplyRecoil(float pitchDegrees, float yawDegrees, float hold, float returnRate, float totalCap)
        {
            _recoilPitch = Mathf.Clamp(_recoilPitch + pitchDegrees, -totalCap, totalCap);
            _recoilYaw = Mathf.Clamp(_recoilYaw + yawDegrees, -totalCap, totalCap);
            _recoilHoldLeft = hold > 0f ? hold : recoilHold;
            _returnRate = returnRate > 0f ? returnRate : returnSpeed;
        }

        private void UpdateRecoil(float dt)
        {
            if (_recoilHoldLeft > 0f)
            {
                _recoilHoldLeft -= dt;
                return;
            }

            if (Mathf.Abs(_recoilPitch) < 0.0001f && Mathf.Abs(_recoilYaw) < 0.0001f)
            {
                _recoilPitch = 0f;
                _recoilYaw = 0f;
                return;
            }

            // exponential return back to the pre-shot aim point
            float t = 1f - Mathf.Exp(-_returnRate * dt);
            _recoilPitch = Mathf.Lerp(_recoilPitch, 0f, t);
            _recoilYaw = Mathf.Lerp(_recoilYaw, 0f, t);
        }

        /// <summary>Clears recoil instantly, e.g. on weapon switch or round reset.</summary>
        public void ResetRecoil()
        {
            _recoilPitch = 0f;
            _recoilYaw = 0f;
            _recoilHoldLeft = 0f;
        }

        /// <summary>Snaps the view back to level, used when a round restarts.</summary>
        public void ResetView()
        {
            _yaw = 0f;
            _pitch = 0f;
            ResetRecoil();
        }
    }
}
