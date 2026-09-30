using UnityEngine;
using FPS.Combat;
using FPS.Core;
using FPS.Player;

namespace FPS.Player
{
    /// <summary>
    /// Owns the player's weapon set and handles switching. The sidearm pistol is always
    /// available and can never be dropped, per the loadout requirement.
    ///
    /// Slot mapping: 1 = primary, 2 = sidearm (also F), 3 = knife (also Q).
    /// </summary>
    public class PlayerLoadout : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Camera viewCamera;
        [SerializeField] private PlayerCamera playerCamera;
        [SerializeField] private PlayerController movement;

        [Header("Weapon anchors (child transforms holding the view models)")]
        [SerializeField] private Transform primaryAnchor;
        [SerializeField] private Transform sidearmAnchor;
        [SerializeField] private Transform meleeAnchor;
        [SerializeField] private GameObject sniperScopeOverlay;

        [Header("Starting loadout")]
        [SerializeField] private WeaponSlot startingSlot = WeaponSlot.Primary;

        private WeaponController _primary;
        private WeaponController _sidearm;
        private WeaponController _melee;
        private WeaponController _active;
        private int _primaryIndex;

        public WeaponController Active => _active;
        public WeaponController Primary => _primary;
        public WeaponController Sidearm => _sidearm;
        public WeaponController Melee => _melee;

        private void Awake()
        {
            if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
            if (playerCamera == null) playerCamera = GetComponentInChildren<PlayerCamera>();
            if (movement == null) movement = GetComponent<PlayerController>();
        }

        /// <summary>
        /// Builds the weapon set in code so the whole loadout works from a single
        /// player prefab with no manual prefab authoring.
        /// </summary>
        public void BuildDefaultLoadout(
            GameObject primaryModel, Transform primaryMuzzle,
            GameObject sidearmModel, Transform sidearmMuzzle,
            GameObject knifeModel, Transform knifeMuzzle)
        {
            _primary = MakeWeapon(primaryAnchor != null ? primaryAnchor : transform, primaryModel, primaryMuzzle,
                WeaponDefinition.Rifle(), sniperScopeOverlay);
            _sidearm = MakeWeapon(sidearmAnchor != null ? sidearmAnchor : transform, sidearmModel, sidearmMuzzle,
                WeaponDefinition.Pistol(), null);
            _melee = MakeWeapon(meleeAnchor != null ? meleeAnchor : transform, knifeModel, knifeMuzzle,
                WeaponDefinition.Knife(), null);

            Equip(startingSlot);
        }

        private WeaponController MakeWeapon(Transform anchor, GameObject model, Transform muzzle,
            WeaponDefinition def, GameObject scope)
        {
            var holder = new GameObject("W_" + def.displayName);
            holder.transform.SetParent(anchor, false);

            var ctrl = holder.AddComponent<WeaponController>();
            ctrl.Configure(def, viewCamera, playerCamera, movement, muzzle, model, scope);
            if (model != null) model.SetActive(true);
            return ctrl;
        }

        private void Update()
        {
            int slot = InputHub.SlotQueued;
            if (slot < 0) return;

            switch (slot)
            {
                case 0: Equip(WeaponSlot.Primary); break;
                case 1: Equip(WeaponSlot.Sidearm); break;
                case 2: Equip(WeaponSlot.Melee); break;
            }
        }

        public void Equip(WeaponSlot slot)
        {
            if (_active != null && _active.IsAiming && playerCamera != null)
                playerCamera.SetSniperZoom(_active.Definition.isSniper, false);

            var next = Resolve(slot);
            if (next == null) return;
            if (next == _active) return;

            SetVisible(_primary, false);
            SetVisible(_sidearm, false);
            SetVisible(_melee, false);

            _active = next;
            SetVisible(_active, true);
        }

        private WeaponController Resolve(WeaponSlot slot)
        {
            switch (slot)
            {
                case WeaponSlot.Sidearm: return _sidearm != null ? _sidearm : _primary;
                case WeaponSlot.Melee: return _melee != null ? _melee : _primary;
                default: return _primary != null ? _primary : _sidearm;
            }
        }

        private void SetVisible(WeaponController w, bool visible)
        {
            if (w == null) return;
            w.enabled = visible;
            var vm = w.transform.Find("ViewModel");
            if (vm != null) vm.gameObject.SetActive(visible);
            // the model is parented directly under the weapon holder
            for (int i = 0; i < w.transform.childCount; i++)
            {
                var c = w.transform.GetChild(i);
                if (c.GetComponent<WeaponController>() == null && !c.name.StartsWith("W_"))
                    c.gameObject.SetActive(visible);
            }
        }
    }
}
