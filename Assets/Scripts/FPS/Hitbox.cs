using UnityEngine;

public sealed class Hitbox : MonoBehaviour
{
    [SerializeField] private HitZone zone;
    [SerializeField] private float multiplier = 1f;
    [SerializeField] private Damageable owner;

    public void Initialize(Damageable damageable, HitZone hitZone, float damageMultiplier)
    {
        owner = damageable;
        zone = hitZone;
        multiplier = damageMultiplier;
    }

    public void Apply(float baseDamage)
    {
        if (owner != null)
            owner.TakeDamage(baseDamage * multiplier, zone);
    }
}
