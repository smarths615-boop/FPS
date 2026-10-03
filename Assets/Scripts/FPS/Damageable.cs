using System;
using UnityEngine;

public enum HitZone
{
    Head,
    Body,
    Leg
}

public sealed class Damageable : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;
    private float health;
    public float Health => health;
    public float MaxHealth => maxHealth;
    public event Action Died;

    private void Awake() => health = maxHealth;

    public void ResetHealth() => health = maxHealth;

    public void TakeDamage(float damage, HitZone zone)
    {
        if (health <= 0f) return;
        health = Mathf.Max(0f, health - damage);
        Debug.Log($"{name}: {zone} hit for {damage:0.0}; remaining health {Mathf.Max(health, 0f):0.0}");
        if (health <= 0f) Died?.Invoke();
    }
}
