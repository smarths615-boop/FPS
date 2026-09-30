using System;
using UnityEngine;

namespace FPS.Combat
{
    /// <summary>
    /// Tunable stats for a weapon. Presets mirror common tactical-shooter handling:
    /// an accurate rifle, a high-spread SMG, a 2x sniper and a fixed sidearm pistol.
    /// </summary>
    [Serializable]
    public class WeaponDefinition
    {
        [Header("Identity")]
        public string displayName = "Rifle";
        public WeaponSlot slot = WeaponSlot.Primary;

        [Header("Damage")]
        public float baseDamage = 26f;
        [Tooltip("Extra multiplier applied on top of the zone multiplier for a head hit.")]
        public float headBonus = 1f;
        public float range = 220f;
        public float falloffStart = 60f;
        public float falloffEnd = 160f;
        [Tooltip("Damage retained at falloffEnd, as a fraction of baseDamage.")]
        public float falloffFloor = 0.6f;

        [Header("Fire")]
        public float roundsPerMinute = 640f;
        public int magazineSize = 30;
        public int reserveAmmo = 120;
        public float reloadTime = 2.1f;
        public bool automatic = true;
        public int pelletsPerShot = 1;

        [Header("Accuracy (degrees of cone)")]
        [Tooltip("Cone while perfectly still and fully settled.")]
        public float baseSpread = 0.12f;
        [Tooltip("Extra spread scaled by planar movement speed - the CS/Valorant feel.")]
        public float moveSpread = 3.1f;
        [Tooltip("Extra spread while airborne (jumping).")]
        public float airSpread = 5.5f;
        [Tooltip("Multiplier applied to spread while crouched - crouching tightens aim.")]
        public float crouchSpreadMultiplier = 0.62f;
        [Tooltip("Multiplier applied to spread while aiming down sights.")]
        public float adsSpreadMultiplier = 0.25f;
        [Tooltip("Cone added per shot and slowly bled off, creating spray control.")]
        public float recoilBloomPerShot = 0.11f;
        public float maxBloom = 3.2f;
        public float bloomDecayPerSecond = 2.4f;

        [Header("Recoil (camera kick, degrees)")]
        [Tooltip("Vertical kick on the first shot of a burst.")]
        public float recoilPitch = 0.85f;
        [Tooltip("Horizontal kick on the first shot of a burst.")]
        public float recoilYaw = 0.28f;

        [Header("Spray pattern (Counter-Strike style)")]
        [Tooltip("Extra vertical climb per consecutive shot, as a fraction of the step.")]
        public float climbRate = 0.22f;
        [Tooltip("Hard cap on the vertical kick from one burst, in degrees.")]
        public float maxVertical = 3.2f;
        [Tooltip("Ceiling on the TOTAL accumulated view kick while spraying, in degrees. Stops a long burst from swinging the view off screen while the spray still visibly climbs before it plateaus.")]
        public float maxRecoilTotal = 6f;
        [Tooltip("Growth of the horizontal circle radius per consecutive shot.")]
        public float horizontalStep = 0.10f;
        [Tooltip("Cap on the horizontal radius, in degrees.")]
        public float maxHorizontal = 0.95f;
        [Tooltip("Seconds without firing before the pattern resets to its first shot.")]
        public float patternResetTime = 0.30f;
        [Tooltip("Seconds the kick is held before it starts returning.")]
        public float recoilHold = 0.05f;
        [Tooltip("How fast the view springs back to the original aim point.")]
        public float returnSpeed = 9f;

        [Header("Aim down sights")]
        public bool canAim = true;
        public bool isSniper = false;
        [Tooltip("Field of view while scoped. 30 from a 60 base reads as ~2x.")]
        public float adsFov = 45f;

        [Header("Melee (knife)")]
        public bool isMelee = false;
        public float meleeRange = 2.1f;
        public float meleeDamage = 55f;
        public float meleeRate = 1.6f;
        public float meleeArc = 100f;

        public float SecondsPerShot => 60f / Mathf.Max(1f, roundsPerMinute);

        // ---------- presets ----------

        public static WeaponDefinition Rifle() => new WeaponDefinition
        {
            displayName = "Rifle",
            slot = WeaponSlot.Primary,
            baseDamage = 26f, roundsPerMinute = 640f, magazineSize = 30, reserveAmmo = 120,
            baseSpread = 0.14f, moveSpread = 3.2f, adsSpreadMultiplier = 0.3f,
            recoilPitch = 0.78f, recoilYaw = 0.12f, adsFov = 45f,
            climbRate = 0.30f, maxVertical = 2.6f, maxRecoilTotal = 6.2f,
            horizontalStep = 0.085f, maxHorizontal = 0.8f,
            returnSpeed = 9f
        };

        public static WeaponDefinition Smg() => new WeaponDefinition
        {
            displayName = "SMG",
            slot = WeaponSlot.Primary,
            baseDamage = 21f, roundsPerMinute = 880f, magazineSize = 32, reserveAmmo = 160,
            baseSpread = 0.34f, moveSpread = 2.0f, adsSpreadMultiplier = 0.42f,
            recoilPitch = 0.52f, recoilYaw = 0.16f, adsFov = 52f,
            climbRate = 0.38f, maxVertical = 2.8f, maxRecoilTotal = 7.6f,
            horizontalStep = 0.15f, maxHorizontal = 1.5f,
            returnSpeed = 8f
        };

        public static WeaponDefinition Shotgun() => new WeaponDefinition
        {
            displayName = "Shotgun",
            slot = WeaponSlot.Primary,
            baseDamage = 13f, roundsPerMinute = 78f, magazineSize = 7, reserveAmmo = 32,
            automatic = false, pelletsPerShot = 9,
            baseSpread = 2.4f, moveSpread = 2.4f, adsSpreadMultiplier = 0.6f,
            recoilPitch = 1.85f, recoilYaw = 0.3f, adsFov = 62f,
            climbRate = 0.1f, maxVertical = 2.4f, maxRecoilTotal = 3.0f,
            horizontalStep = 0.1f, maxHorizontal = 0.6f, returnSpeed = 7f,
            range = 45f, falloffStart = 12f, falloffEnd = 34f, falloffFloor = 0.18f
        };

        public static WeaponDefinition Sniper() => new WeaponDefinition
        {
            displayName = "Sniper",
            slot = WeaponSlot.Sniper,
            baseDamage = 118f, headBonus = 1.35f,
            roundsPerMinute = 42f, magazineSize = 5, reserveAmmo = 20,
            automatic = false, reloadTime = 2.9f,
            baseSpread = 0.6f, moveSpread = 9.5f, airSpread = 14f,
            adsSpreadMultiplier = 0.02f, crouchSpreadMultiplier = 0.7f,
            recoilBloomPerShot = 1.1f, maxBloom = 4.5f, bloomDecayPerSecond = 1.5f,
            recoilPitch = 2.6f, recoilYaw = 0.3f,
            climbRate = 0.05f, maxVertical = 3.0f, maxRecoilTotal = 4.5f,
            horizontalStep = 0.12f, maxHorizontal = 0.7f,
            returnSpeed = 5.5f, recoilHold = 0.12f,
            canAim = true, isSniper = true, adsFov = 30f,   // ~2x zoom
            range = 400f, falloffStart = 300f, falloffEnd = 400f, falloffFloor = 0.85f
        };

        public static WeaponDefinition Pistol() => new WeaponDefinition
        {
            displayName = "Pistol",
            slot = WeaponSlot.Sidearm,       // always available, never dropped
            baseDamage = 27f, roundsPerMinute = 400f, magazineSize = 12, reserveAmmo = 60,
            automatic = false, reloadTime = 1.5f,
            baseSpread = 0.22f, moveSpread = 2.2f, adsSpreadMultiplier = 0.28f,
            recoilPitch = 0.85f, recoilYaw = 0.16f, adsFov = 50f,
            climbRate = 0.26f, maxVertical = 2.0f, maxRecoilTotal = 3.2f,
            horizontalStep = 0.12f, maxHorizontal = 0.9f, returnSpeed = 9f
        };

        public static WeaponDefinition Knife() => new WeaponDefinition
        {
            displayName = "Knife",
            slot = WeaponSlot.Melee,
            isMelee = true, automatic = true, canAim = false,
            meleeRange = 2.1f, meleeDamage = 55f, meleeRate = 1.6f, meleeArc = 100f,
            baseDamage = 55f
        };
    }

    public enum WeaponSlot
    {
        Primary = 0,
        Sniper = 1,
        Sidearm = 2,
        Melee = 3
    }
}
