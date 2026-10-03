using UnityEngine;

public sealed class FpsNetworkHitbox : MonoBehaviour
{
    [SerializeField] private HitZone zone;
    [SerializeField] private float multiplier = 1f;
    public float Multiplier => multiplier;
    public HitZone Zone => zone;

    public void Initialize(HitZone hitZone, float damageMultiplier)
    {
        zone = hitZone;
        multiplier = damageMultiplier;
    }
}
