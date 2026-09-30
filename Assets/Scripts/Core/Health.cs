using System;
using UnityEngine;

namespace FPS.Core
{
    /// <summary>
    /// Central damage model. Resolves per-zone multipliers so head shots hurt far
    /// more than legs. Shared by the player and every AI unit.
    /// </summary>
    [DisallowMultipleComponent]
    public class Health : MonoBehaviour
    {
        [Header("Vitals")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private bool invulnerable = false;

        [Header("Incoming damage multipliers (final = base * zoneMultiplier)")]
        [SerializeField] private float headMultiplier = 4f;
        [SerializeField] private float bodyMultiplier = 1f;
        [SerializeField] private float legMultiplier = 0.75f;

        [Header("Regeneration")]
        [SerializeField] private bool regenEnabled = false;
        [SerializeField] private float regenDelay = 6f;
        [SerializeField] private float regenPerSecond = 10f;

        private float _current;
        private float _lastDamageTime = -999f;

        public float Max => maxHealth;
        public float Current => _current;
        public float Normalized => maxHealth <= 0f ? 0f : _current / maxHealth;
        public bool IsDead => _current <= 0f;
        public bool Invulnerable => invulnerable;

        /// <summary>(attacker, amount actually applied, zone struck, world hit point)</summary>
        public event Action<Health, float, HitZone, Vector3> Damaged;
        public event Action<Health> Died;
        public event Action<Health, float> Healed;

        private void Awake()
        {
            _current = maxHealth;
        }

        public void Configure(float newMax, bool canRegen = false)
        {
            maxHealth = newMax;
            _current = maxHealth;
            regenEnabled = canRegen;
        }

        public void SetInvulnerable(bool value) => invulnerable = value;

        public float MultiplierFor(HitZone zone)
        {
            switch (zone)
            {
                case HitZone.Head: return headMultiplier;
                case HitZone.Legs: return legMultiplier;
                default: return bodyMultiplier;
            }
        }

        /// <summary>Apply damage. Returns the amount actually removed from health.</summary>
        public float ApplyDamage(float baseAmount, HitZone zone, Health attacker = null, Vector3 point = default)
        {
            if (IsDead || invulnerable || baseAmount <= 0f) return 0f;

            float applied = baseAmount * MultiplierFor(zone);
            _current = Mathf.Max(0f, _current - applied);
            _lastDamageTime = Time.time;

            Damaged?.Invoke(attacker, applied, zone, point);

            if (IsDead) Died?.Invoke(this);
            return applied;
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            float before = _current;
            _current = Mathf.Min(maxHealth, _current + amount);
            if (!Mathf.Approximately(before, _current)) Healed?.Invoke(this, _current - before);
        }

        public void ResetHealth()
        {
            _current = maxHealth;
            _lastDamageTime = -999f;
        }

        private void Update()
        {
            if (!regenEnabled || IsDead || _current >= maxHealth) return;
            if (Time.time - _lastDamageTime < regenDelay) return;
            Heal(regenPerSecond * Time.deltaTime);
        }
    }
}
