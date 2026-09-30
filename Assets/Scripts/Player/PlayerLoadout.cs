using UnityEngine;
using FPS.Combat;
using FPS.Core;

namespace FPS.Player
{
    /// <summary>
    /// Owns the player's weapon set and handles switching. The sidearm pistol is always
    /// available and can never be dropped, per the loadout requirement.
    ///
    /// Slots: 1 = primary rifle, 2 = sniper, 3 = sidearm pistol (also F), 4 = knife (also Q).
    /// </summary>
    public class PlayerLoadout : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Camera viewCamera;
        [SerializeField] private PlayerCamera playerCamera;
        [SerializeField] private PlayerController movement;

        [Header("Weapon anchors (child transforms holding the view models)")]
        [SerializeField] private Transform primaryAnchor;
        [SerializeField] private Transform sniperAnchor;
        [SerializeField] private Transform sidearmAnchor;
        [SerializeField] private Transform meleeAnchor;
        [SerializeField] private GameObject sniperScopeOverlay;

        [Header("Weapon models (assigned on the prefab)")]
        [SerializeField] private GameObject primaryModel;
        [SerializeField] private GameObject sniperModel;
        [SerializeField] private GameObject sidearmModel;
        [SerializeField] private GameObject knifeModel;
        [SerializeField] private Transform primaryMuzzle;
        [SerializeField] private Transform sniperMuzzle;
        [SerializeField] private Transform sidearmMuzzle;
        [SerializeField] private Transform knifeMuzzle;

        [Header("Starting loadout")]
        [SerializeField] private WeaponSlot startingSlot = WeaponSlot.Primary;

        private WeaponController _primary;
        private WeaponController _sniper;
        private WeaponController _sidearm;
        private WeaponController _melee;
        private WeaponController _active;

        /// <summary>
        /// When false the loadout is locked and only the T-cycle key is ignored. The round
        /// director opens this during the 5s intermission so the player can re-pick.
        /// </summary>
        public bool SwitchingAllowed { get; set; } = true;

        private readonly WeaponSlot[] _cycleOrder =
        {
            WeaponSlot.Primary, WeaponSlot.Sniper, WeaponSlot.Sidearm, WeaponSlot.Melee
        };

        public WeaponController Active => _active;
        public WeaponController Primary => _primary;
        public WeaponController Sniper => _sniper;
        public WeaponController Sidearm => _sidearm;
        public WeaponController Melee => _melee;

        private void Awake()
        {
            if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
            if (playerCamera == null) playerCamera = GetComponentInChildren<PlayerCamera>();
            if (movement == null) movement = GetComponent<PlayerController>();

            // self-assemble the loadout when weapon models were wired on the prefab
            if (primaryModel != null || sniperModel != null)
                BuildDefaultLoadout();
        }

        /// <summary>
        /// Builds the weapon set in code so the whole loadout works from a single
        /// player prefab with no manual prefab authoring.
        /// </summary>
        public void BuildDefaultLoadout()
        {
            _primary = MakeWeapon(primaryAnchor, primaryModel, WeaponDefinition.Rifle(), null);
            _sniper = MakeWeapon(sniperAnchor, sniperModel, WeaponDefinition.Sniper(), sniperScopeOverlay);
            _sidearm = MakeWeapon(sidearmAnchor, sidearmModel, WeaponDefinition.Pistol(), null);
            _melee = MakeWeapon(meleeAnchor, knifeModel, WeaponDefinition.Knife(), null);

            Equip(startingSlot);
        }

        private WeaponController MakeWeapon(Transform anchor, GameObject model, WeaponDefinition def, GameObject scope)
        {
            if (anchor == null) anchor = transform;

            var holder = new GameObject("W_" + def.displayName);
            holder.transform.SetParent(anchor, false);

            // Adopt the view model so toggling the holder hides the model too. Models are
            // parented to the anchor by the setup wizard, so they start out as siblings.
            Transform muzzle = null;
            if (model != null)
            {
                model.transform.SetParent(holder.transform, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;

                var m = FindChild(model.transform, "Muzzle");
                if (m != null) muzzle = m;
                model.SetActive(true);
            }

            var ctrl = holder.AddComponent<WeaponController>();
            ctrl.Configure(def, viewCamera, playerCamera, movement, muzzle, model, scope);
            return ctrl;
        }

        private static Transform FindChild(Transform t, string childName)
        {
            foreach (Transform c in t)
                if (c.name == childName) return c;
            return null;
        }

        private void Update()
        {
            if (!SwitchingAllowed) return;

            // T cycles to the next weapon
            if (InputHub.CycleWeaponQueued)
            {
                CycleNext();
                return;
            }

            int slot = InputHub.SlotQueued;
            if (slot < 0) return;

            switch (slot)
            {
                case 0: Equip(WeaponSlot.Primary); break;
                case 1: Equip(WeaponSlot.Sniper); break;
                case 2: Equip(WeaponSlot.Sidearm); break;
                case 3: Equip(WeaponSlot.Melee); break;
            }
        }

        public void CycleNext()
        {
            if (_active == null) return;
            int idx = System.Array.IndexOf(_cycleOrder, _active.Definition.slot);
            if (idx < 0) idx = 0;
            for (int step = 1; step <= _cycleOrder.Length; step++)
            {
                var next = _cycleOrder[(idx + step) % _cycleOrder.Length];
                var w = Resolve(next);
                if (w != null && w != _active) { Equip(next); return; }
            }
        }

        public void Equip(WeaponSlot slot)
        {
            if (_active != null && _active.IsAiming && playerCamera != null)
                playerCamera.SetSniperZoom(_active.Definition.isSniper, false);

            var next = Resolve(slot);
            if (next == null) return;

            SetHolderActive(_primary, false);
            SetHolderActive(_sniper, false);
            SetHolderActive(_sidearm, false);
            SetHolderActive(_melee, false);

            _active = next;
            SetHolderActive(_active, true);
        }

        private WeaponController Resolve(WeaponSlot slot)
        {
            switch (slot)
            {
                case WeaponSlot.Sniper: return _sniper != null ? _sniper : _primary;
                case WeaponSlot.Sidearm: return _sidearm != null ? _sidearm : _primary;
                case WeaponSlot.Melee: return _melee != null ? _melee : _primary;
                default: return _primary != null ? _primary : _sidearm;
            }
        }

        /// <summary>Disabling the holder GameObject hides the model and stops its Update.</summary>
        private void SetHolderActive(WeaponController w, bool active)
        {
            if (w == null) return;
            w.gameObject.SetActive(active);
        }
    }
}
