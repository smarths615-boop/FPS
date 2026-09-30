using System;
using UnityEngine;
using FPS.Core;
using FPS.Player;

namespace FPS.Combat
{
    /// <summary>
    /// Handles firing, reloading, aim-down-sights and the movement-based inaccuracy that
    /// gives the game its tactical feel: standing still and scoped is accurate, running
    /// and jumping sprays the cone wide open.
    /// </summary>
    public class WeaponController : MonoBehaviour
    {
        [Header("Definition")]
        [SerializeField] private WeaponDefinition definition = WeaponDefinition.Rifle();

        [Header("Refs")]
        [SerializeField] private Camera viewCamera;
        [SerializeField] private PlayerCamera playerCamera;
        [SerializeField] private PlayerController movement;
        [SerializeField] private Transform muzzle;
        [SerializeField] private GameObject viewModel;
        [SerializeField] private GameObject scopeOverlay;

        [Header("Layers")]
        [Tooltip("Layers the bullet can collide with (world geometry). Enemy hitboxes use DamageZone.")]
        [SerializeField] private LayerMask worldMask = ~0;

        // ---- events ----
        public event Action<int, int> AmmoChanged;      // mag, reserve
        public event Action<HitZone, float, bool> ShotResolved; // zone, damage, wasLethal
        public event Action ShakeRequested;

        private float _nextFireTime;
        private float _bloom;
        private bool _triggerHeldLastFrame;
        private bool _isAiming;
        private bool _isReloading;
        private float _reloadEndTime;
        private int _mag = -1;

        public WeaponDefinition Definition => definition;
        public bool IsAiming => _isAiming;
        public bool IsReloading => _isReloading;

        /// <summary>Live rounds remaining in the magazine.</summary>
        public int CurrentMag => _mag < 0 ? definition.magazineSize : _mag;
        public int CurrentReserve => definition.reserveAmmo;

        /// <summary>Set false by the round director to lock firing during intermission.</summary>
        public bool CanFire = true;
        public int AmmoInMag => definition.magazineSize;
        public int ReserveAmmo => definition.reserveAmmo;
        public float CurrentSpread => ComputeSpreadDegrees();

        public void Configure(WeaponDefinition def, Camera cam, PlayerCamera pcam, PlayerController ctrl, Transform muzzleXf, GameObject model, GameObject scope)
        {
            definition = def != null ? def : WeaponDefinition.Rifle();
            viewCamera = cam != null ? cam : viewCamera;
            playerCamera = pcam != null ? pcam : playerCamera;
            movement = ctrl != null ? ctrl : movement;
            muzzle = muzzleXf != null ? muzzleXf : muzzle;
            viewModel = model != null ? model : viewModel;
            scopeOverlay = scope != null ? scope : scopeOverlay;
            if (scopeOverlay != null) scopeOverlay.SetActive(false);
            AmmoChanged?.Invoke(definition.magazineSize, definition.reserveAmmo);
        }

        private void Update()
        {
            if (definition == null || viewCamera == null) return;
            if (!CanFire)
            {
                SetAiming(false);
                return;
            }

            bool fireHeld = definition.isMelee ? InputHub.KnifeHeld : InputHub.FireHeld;
            bool firePressed = fireHeld && !_triggerHeldLastFrame;
            _triggerHeldLastFrame = fireHeld;

            UpdateBloom(Time.deltaTime);
            UpdateAim();
            UpdateReload();

            if (InputHub.ReloadHeld && !_isReloading)
            {
                if (_mag < definition.magazineSize && definition.reserveAmmo > 0)
                    BeginReload();
            }

            if (fireHeld && !_isReloading && Time.time >= _nextFireTime)
            {
                if (definition.automatic || firePressed)
                {
                    Fire();
                }
            }
        }

        private void UpdateAim()
        {
            if (!definition.canAim)
            {
                SetAiming(false);
                return;
            }
            SetAiming(InputHub.AimHeld);
        }

        private void SetAiming(bool value)
        {
            if (_isAiming == value) return;
            _isAiming = value;

            if (playerCamera != null)
                playerCamera.SetSniperZoom(definition.isSniper, value);

            if (scopeOverlay != null && definition.isSniper)
                scopeOverlay.SetActive(value);
        }

        private void UpdateBloom(float dt)
        {
            _bloom = Mathf.Max(0f, _bloom - definition.bloomDecayPerSecond * dt);
        }

        private void UpdateReload()
        {
            if (!_isReloading) return;
            if (Time.time < _reloadEndTime) return;

            _isReloading = false;
            int need = definition.magazineSize - _mag;
            int take = Mathf.Min(need, definition.reserveAmmo);
            _mag += take;
            definition.reserveAmmo -= take;
            AmmoChanged?.Invoke(_mag, definition.reserveAmmo);
        }

        /// <summary>
        /// Cone half-angle in degrees. Movement is the dominant term, which is what makes
        /// running-and-shooting spray while walking-and-shooting stays controllable.
        /// </summary>
        public float ComputeSpreadDegrees()
        {
            float speed = movement != null ? movement.PlanarSpeed : 0f;
            float runningSpeed = movement != null && movement.IsSprinting ? 6.2f : 4.2f;

            float spread = definition.baseSpread;
            spread += Mathf.Clamp01(speed / runningSpeed) * definition.moveSpread;

            if (movement != null && !movement.IsGrounded)
                spread += definition.airSpread;

            if (movement != null && movement.IsCrouching)
                spread *= definition.crouchSpreadMultiplier;

            if (_isAiming)
                spread *= definition.adsSpreadMultiplier;

            return spread + _bloom;
        }

        public void BeginReload()
        {
            if (_isReloading || definition.isMelee) return;
            if (_mag == -1) _mag = definition.magazineSize;
            if (_mag >= definition.magazineSize || definition.reserveAmmo <= 0) return;
            _isReloading = true;
            _reloadEndTime = Time.time + definition.reloadTime;
        }

        public void Fire()
        {
            if (_mag == -1) _mag = definition.magazineSize;

            if (definition.isMelee)
            {
                DoMelee();
                _nextFireTime = Time.time + 1f / Mathf.Max(0.1f, definition.meleeRate);
                return;
            }

            if (_mag <= 0)
            {
                BeginReload();
                return;
            }

            _mag--;
            AmmoChanged?.Invoke(_mag, definition.reserveAmmo);

            float spread = ComputeSpreadDegrees();
            for (int i = 0; i < Mathf.Max(1, definition.pelletsPerShot); i++)
            {
                FireRay(spread);
            }

            // recoil: instant view kick + cone bloom
            if (playerCamera != null)
                playerCamera.ApplyRecoil(definition.recoilPitch, definition.recoilYaw);
            _bloom = Mathf.Min(definition.maxBloom, _bloom + definition.recoilBloomPerShot);
            ShakeRequested?.Invoke();

            _nextFireTime = Time.time + definition.SecondsPerShot;
        }

        private void FireRay(float spreadDegrees)
        {
            Vector3 origin = muzzle != null ? muzzle.position : viewCamera.transform.position;
            Vector3 dir = viewCamera.transform.forward;

            if (spreadDegrees > 0.0001f)
            {
                // uniform random point inside the cone
                Vector2 disc = UnityEngine.Random.insideUnitCircle * spreadDegrees;
                dir = Quaternion.Euler(disc.y, disc.x, 0f) * dir;
            }

            RaycastHit hit;
            if (Physics.Raycast(origin, dir, out hit, definition.range, worldMask, QueryTriggerInteraction.Collide))
            {
                TraceHit(hit);
            }
            else
            {
                // miss
                ShotResolved?.Invoke(HitZone.Body, 0f, false);
            }
        }

        private void TraceHit(RaycastHit hit)
        {
            DamageZone zone = hit.collider.GetComponent<DamageZone>();
            if (zone == null) zone = hit.collider.GetComponentInParent<DamageZone>();

            if (zone == null)
            {
                // hit world geometry
                SpawnImpact(hit.point, hit.normal);
                ShotResolved?.Invoke(HitZone.Body, 0f, false);
                return;
            }

            Health target = zone.Owner;
            if (target == null || target.IsDead)
            {
                ShotResolved?.Invoke(zone.Zone, 0f, false);
                return;
            }

            float damage = DamageAtDistance(definition.range) * 1f;
            if (zone.Zone == HitZone.Head) damage *= definition.headBonus;

            bool wasAlive = !target.IsDead;
            target.ApplyDamage(damage, zone.Zone, null, hit.point);
            SpawnImpact(hit.point, hit.normal);
            ShotResolved?.Invoke(zone.Zone, damage, wasAlive && target.IsDead);
        }

        public float DamageAtDistance(float distance)
        {
            if (distance <= definition.falloffStart) return definition.baseDamage;
            if (distance >= definition.falloffEnd)
                return definition.baseDamage * definition.falloffFloor;
            float t = (distance - definition.falloffStart) / Mathf.Max(0.001f, definition.falloffEnd - definition.falloffStart);
            return Mathf.Lerp(definition.baseDamage, definition.baseDamage * definition.falloffFloor, t);
        }

        private void DoMelee()
        {
            Vector3 origin = viewCamera.transform.position;
            Vector3 fwd = viewCamera.transform.forward;
            float range = definition.meleeRange;
            float halfArc = definition.meleeArc * 0.5f;

            bool anyHit = false;
            Collider[] overlaps = Physics.OverlapSphere(origin, range, ~0, QueryTriggerInteraction.Collide);
            foreach (Collider c in overlaps)
            {
                Vector3 to = c.ClosestPoint(origin) - origin;
                if (to.sqrMagnitude > 0.0001f)
                {
                    float ang = Vector3.Angle(fwd, to);
                    if (ang > halfArc) continue;
                }

                DamageZone zone = c.GetComponent<DamageZone>() ?? c.GetComponentInParent<DamageZone>();
                if (zone == null) continue;
                Health target = zone.Owner;
                if (target == null || target.IsDead) continue;

                bool wasAlive = !target.IsDead;
                target.ApplyDamage(definition.meleeDamage, HitZone.Body, null, c.bounds.center);
                ShotResolved?.Invoke(HitZone.Body, definition.meleeDamage, wasAlive && target.IsDead);
                anyHit = true;
            }

            if (!anyHit) ShotResolved?.Invoke(HitZone.Body, 0f, false);
        }

        [Header("Impact FX")]
        [SerializeField] private ParticleSystem impactEffect;

        private void SpawnImpact(Vector3 point, Vector3 normal)
        {
            if (impactEffect == null) return;
            impactEffect.transform.position = point;
            impactEffect.transform.rotation = Quaternion.LookRotation(normal);
            impactEffect.Emit(6);
            impactEffect.Play();
        }

        public void RefillAll()
        {
            _mag = definition.magazineSize;
            definition.reserveAmmo = definition.magazineSize * 4;
            AmmoChanged?.Invoke(_mag, definition.reserveAmmo);
        }
    }
}
