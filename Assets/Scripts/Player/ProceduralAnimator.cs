using UnityEngine;
using FPS.Core;

namespace FPS.Player
{
    /// <summary>
    /// Bone-driven animation for the Low Poly SWAT character. The source FBX ships a
    /// humanoid skeleton but no animation clips, so walk / run / crouch / aim poses are
    /// generated procedurally by rotating the rig each frame.
    ///
    /// Bind via <see cref="AutoBind"/> - the child names match the imported FBX.
    /// </summary>
    [DisallowMultipleComponent]
    public class ProceduralAnimator : MonoBehaviour
    {
        [Header("Rig bones (auto-bound if left empty)")]
        [SerializeField] private Transform hips, spine, chest, neck, head;
        [SerializeField] private Transform shoulderL, upperArmL, lowerArmL;
        [SerializeField] private Transform shoulderR, upperArmR, lowerArmR;
        [SerializeField] private Transform thighL, shinL, footL;
        [SerializeField] private Transform thighR, shinR, footR;

        [Header("Gait")]
        [SerializeField] private float walkCycleSpeed = 7.5f;
        [SerializeField] private float runCycleSpeed = 11f;
        [SerializeField] private float strideAmount = 34f;
        [SerializeField] private float runStrideAmount = 52f;
        [SerializeField] private float armSwingAmount = 26f;
        [SerializeField] private float bobAmount = 0.045f;
        [SerializeField] private float blendSpeed = 10f;

        [Header("Aiming")]
        [SerializeField] private float aimShoulderPitch = 62f;
        [SerializeField] private float aimShoulderYaw = 12f;
        [SerializeField] private float aimElbowBend = 48f;

        [Header("Crouch")]
        [SerializeField] private float crouchHipDrop = -0.42f;
        [SerializeField] private float crouchThighBend = 78f;
        [SerializeField] private float crouchSpineBend = 22f;

        private PlayerController _controller;
        private PlayerCamera _camera;
        private float _cycle;
        private float _blendCrouch;
        private float _blendAim;
        private float _recoilKick;

        private Quaternion _hipBase, _spineBase, _chestBase, _neckBase, _headBase;
        private Quaternion _shLBase, _uaLBase, _laLBase;
        private Quaternion _shRBase, _uaRBase, _laRBase;
        private Quaternion _thLBase, _shLBase2, _ftLBase;
        private Quaternion _thRBase, _shRBase2, _ftRBase;
        private Vector3 _hipsBaseLocalPos;

        public float RecoilKick => _recoilKick;

        private void Awake()
        {
            _controller = GetComponentInParent<PlayerController>();
            _camera = GetComponentInParent<PlayerCamera>();
            AutoBind();
            CacheRestPose();
        }

        /// <summary>Finds the standard humanoid bones by their imported names.</summary>
        public void AutoBind()
        {
            hips = hips ?? Find("Hips");
            spine = spine ?? Find("Spine_01");
            chest = chest ?? Find("Spine_03");
            neck = neck ?? Find("Neck");
            head = head ?? Find("Head");
            shoulderL = shoulderL ?? Find("Shoulder_L");
            upperArmL = upperArmL ?? Find("Elbow_L")?.parent;   // Elbow_L sits at the upper-arm tip
            lowerArmL = lowerArmL ?? Find("Elbow_L");
            shoulderR = shoulderR ?? Find("Shoulder_R");
            upperArmR = upperArmR ?? Find("Elbow_R")?.parent;
            lowerArmR = lowerArmR ?? Find("Elbow_R");
            thighL = thighL ?? Find("thigh_stretch.l")?.parent;
            shinL = shinL ?? Find("leg_stretch.l")?.parent;
            footL = footL ?? Find("foot.l");
            thighR = thighR ?? Find("thigh_stretch.r")?.parent;
            shinR = shinR ?? Find("leg_stretch.r")?.parent;
            footR = footR ?? Find("foot.r");
        }

        private Transform Find(string boneName)
        {
            Transform t = transform.Find(boneName);
            if (t != null) return t;
            foreach (Transform c in GetComponentsInChildren<Transform>(true))
                if (c.name == boneName) return c;
            return null;
        }

        private void CacheRestPose()
        {
            _hipBase = hips ? hips.localRotation : Quaternion.identity;
            _spineBase = spine ? spine.localRotation : Quaternion.identity;
            _chestBase = chest ? chest.localRotation : Quaternion.identity;
            _neckBase = neck ? neck.localRotation : Quaternion.identity;
            _headBase = head ? head.localRotation : Quaternion.identity;
            _shLBase = shoulderL ? shoulderL.localRotation : Quaternion.identity;
            _uaLBase = upperArmL ? upperArmL.localRotation : Quaternion.identity;
            _laLBase = lowerArmL ? lowerArmL.localRotation : Quaternion.identity;
            _shRBase = shoulderR ? shoulderR.localRotation : Quaternion.identity;
            _uaRBase = upperArmR ? upperArmR.localRotation : Quaternion.identity;
            _laRBase = lowerArmR ? lowerArmR.localRotation : Quaternion.identity;
            _thLBase = thighL ? thighL.localRotation : Quaternion.identity;
            _shLBase2 = shinL ? shinL.localRotation : Quaternion.identity;
            _ftLBase = footL ? footL.localRotation : Quaternion.identity;
            _thRBase = thighR ? thighR.localRotation : Quaternion.identity;
            _shRBase2 = shinR ? shinR.localRotation : Quaternion.identity;
            _ftRBase = footR ? footR.localRotation : Quaternion.identity;
            _hipsBaseLocalPos = hips ? hips.localPosition : Vector3.zero;
        }

        public void KickRecoil(float amount) => _recoilKick = Mathf.Min(1f, _recoilKick + amount);

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            float speed = _controller ? _controller.PlanarSpeed : 0f;
            bool crouching = _controller && _controller.IsCrouching;
            bool grounded = _controller == null || _controller.IsGrounded;

            _blendCrouch = Mathf.MoveTowards(_blendCrouch, crouching ? 1f : 0f, blendSpeed * dt);
            _recoilKick = Mathf.MoveTowards(_recoilKick, 0f, 5f * dt);

            bool sprinting = _controller && _controller.IsSprinting;
            _blendAim = Mathf.MoveTowards(_blendAim,
                (_camera != null && _camera.IsAiming) ? 1f : 0f, blendSpeed * dt);

            // stride frequency scales with speed so the feet do not skate
            float freq = speed < 0.05f ? 0f
                       : sprinting ? runCycleSpeed
                       : Mathf.Lerp(walkCycleSpeed, runCycleSpeed, Mathf.InverseLerp(3.4f, 6.2f, speed));
            _cycle += freq * dt;
            if (_cycle > Mathf.PI * 2f) _cycle -= Mathf.PI * 2f;

            float s = Mathf.Sin(_cycle);
            float c = Mathf.Sin(_cycle * 2f);
            float stride = Mathf.Lerp(strideAmount, runStrideAmount, sprinting ? 1f : 0f);
            float moveAmount = Mathf.Clamp01(speed / 3.4f) * (grounded ? 1f : 0f);

            ApplyLegs(s, moveAmount, stride);
            ApplyArms(s, moveAmount, sprinting);
            ApplySpine(moveAmount, c);
            ApplyCrouchOverlay();
        }

        private void ApplyLegs(float s, float moveAmount, float stride)
        {
            float l = s * stride * moveAmount;
            float r = -s * stride * moveAmount;

            if (thighL) thighL.localRotation = _thLBase * Quaternion.Euler(l, 0f, 0f);
            if (thighR) thighR.localRotation = _thRBase * Quaternion.Euler(r, 0f, 0f);

            // knees bend on the return half of the stride
            float kneeL = Mathf.Max(0f, -s) * 52f * moveAmount;
            float kneeR = Mathf.Max(0f, s) * 52f * moveAmount;
            if (shinL) shinL.localRotation = _shLBase2 * Quaternion.Euler(kneeL, 0f, 0f);
            if (shinR) shinR.localRotation = _shRBase2 * Quaternion.Euler(kneeR, 0f, 0f);

            if (footL) footL.localRotation = _ftLBase * Quaternion.Euler(Mathf.Max(0f, s) * 16f * moveAmount, 0f, 0f);
            if (footR) footR.localRotation = _ftRBase * Quaternion.Euler(Mathf.Max(0f, -s) * 16f * moveAmount, 0f, 0f);
        }

        private void ApplyArms(float s, float moveAmount, bool sprinting)
        {
            // when aiming, the right arm holds the weapon toward the crosshair
            float armPose = Mathf.Lerp(armSwingAmount * moveAmount, 0f, _blendAim);
            float swing = sprinting ? 42f * moveAmount : armPose;

            if (upperArmL)
            {
                Quaternion swingPose = _uaLBase * Quaternion.Euler(-s * swing, 0f, 0f);
                Quaternion aimPose = _uaLBase * Quaternion.Euler(-aimShoulderPitch * 0.35f, -18f, 26f);
                upperArmL.localRotation = Quaternion.Slerp(swingPose, aimPose, _blendAim);
            }
            if (upperArmR)
            {
                Quaternion swingPose = _uaRBase * Quaternion.Euler(s * swing, 0f, 0f);
                Quaternion aimPose = _uaRBase * Quaternion.Euler(-aimShoulderPitch, aimShoulderYaw, -14f);
                upperArmR.localRotation = Quaternion.Slerp(swingPose, aimPose, _blendAim);
            }
            if (shoulderL) shoulderL.localRotation = _shLBase * Quaternion.Euler(0f, 0f, Mathf.Lerp(6f, 20f, _blendAim) * moveAmount);
            if (shoulderR) shoulderR.localRotation = _shRBase * Quaternion.Euler(0f, 0f, -Mathf.Lerp(6f, 10f, _blendAim) * moveAmount);

            float elbowBend = Mathf.Lerp(12f, aimElbowBend, _blendAim) + _recoilKick * 18f;
            if (lowerArmL) lowerArmL.localRotation = _laLBase * Quaternion.Euler(-elbowBend, 0f, 0f);
            if (lowerArmR) lowerArmR.localRotation = _laRBase * Quaternion.Euler(-elbowBend, 0f, 0f);
        }

        private void ApplySpine(float moveAmount, float c)
        {
            float lean = moveAmount * 6f + _blendCrouch * 8f;
            if (spine) spine.localRotation = _spineBase * Quaternion.Euler(lean, c * 3f * moveAmount, 0f);
            if (chest) chest.localRotation = _chestBase * Quaternion.Euler(lean * 0.5f, c * 4f * moveAmount, 0f);

            float aimPitch = _blendAim * 18f;
            if (neck) neck.localRotation = _neckBase * Quaternion.Euler(aimPitch, 0f, 0f);
            if (head) head.localRotation = _headBase * Quaternion.Euler(aimPitch * 0.6f, 0f, 0f);

            if (hips)
            {
                hips.localPosition = _hipsBaseLocalPos + Vector3.up * (c * bobAmount * moveAmount);
            }
        }

        private void ApplyCrouchOverlay()
        {
            if (_blendCrouch <= 0.001f) return;
            if (hips)
            {
                hips.localPosition = Vector3.Lerp(hips.localPosition,
                    _hipsBaseLocalPos + Vector3.up * crouchHipDrop, _blendCrouch);
            }
            if (thighL) thighL.localRotation = Quaternion.Slerp(thighL.localRotation,
                _thLBase * Quaternion.Euler(crouchThighBend, 0f, 0f), _blendCrouch);
            if (thighR) thighR.localRotation = Quaternion.Slerp(thighR.localRotation,
                _thRBase * Quaternion.Euler(crouchThighBend, 0f, 0f), _blendCrouch);
            if (shinL) shinL.localRotation = Quaternion.Slerp(shinL.localRotation,
                _shLBase2 * Quaternion.Euler(-crouchThighBend * 0.9f, 0f, 0f), _blendCrouch);
            if (shinR) shinR.localRotation = Quaternion.Slerp(shinR.localRotation,
                _shRBase2 * Quaternion.Euler(-crouchThighBend * 0.9f, 0f, 0f), _blendCrouch);
            if (spine) spine.localRotation = Quaternion.Slerp(spine.localRotation,
                _spineBase * Quaternion.Euler(crouchSpineBend, 0f, 0f), _blendCrouch);
        }
    }
}
