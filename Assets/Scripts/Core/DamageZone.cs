using UnityEngine;

namespace FPS.Core
{
    /// <summary>Which body part was struck. Drives damage multipliers (Valorant/CS style).</summary>
    public enum HitZone
    {
        Head = 0,
        Body = 1,
        Legs = 2
    }

    /// <summary>
    /// Attach to a child collider under a character to mark it as a hitbox zone.
    /// The <see cref="Health"/> on the parent resolves the zone by walking up the
    /// transform chain, so no per-collider wiring is required on prefabs.
    /// </summary>
    [DisallowMultipleComponent]
    public class DamageZone : MonoBehaviour
    {
        [SerializeField] private HitZone zone = HitZone.Body;

        /// <summary>Optional explicit owner. When null the owner is resolved from parents.</summary>
        [SerializeField] private Health owner;

        public HitZone Zone => zone;
        public Health Owner => owner != null ? owner : GetComponentInParent<Health>();

        public void Configure(HitZone hitZone, Health healthOwner = null)
        {
            zone = hitZone;
            owner = healthOwner;
        }
    }
}
