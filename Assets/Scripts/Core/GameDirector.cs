using UnityEngine;
using FPS.AI;
using FPS.Combat;
using FPS.Core;
using FPS.Player;
using FPS.UI;

namespace FPS.Core
{
    public enum RoundPhase
    {
        Active = 0,      // 2 minute round
        Intermission = 1 // 5 second weapon-change window
    }

    /// <summary>
    /// Drives the round loop: a 2 minute PVE round, then a 5 second intermission during
    /// which firing is locked and the player may re-pick weapons. Also wires the active
    /// weapon to the HUD so hit markers and ammo stay in sync across swaps.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class GameDirector : MonoBehaviour
    {
        [Header("Round timing")]
        [SerializeField] private float roundDuration = 120f;
        [SerializeField] private float intermissionDuration = 5f;

        [Header("Refs")]
        [SerializeField] private EnemySpawner spawner;
        [SerializeField] private Transform playerStart;

        private HUD _hud;
        private PlayerLoadout _loadout;
        private PlayerController _player;
        private PlayerCamera _camera;
        private Health _playerHealth;
        private WeaponController _wired;

        private float _phaseTime;
        private int _round = 1;

        public RoundPhase Phase { get; private set; } = RoundPhase.Active;
        public int Round => _round;
        public float TimeLeft => Phase == RoundPhase.Active ? Mathf.Max(0f, roundDuration - _phaseTime)
                                                             : Mathf.Max(0f, intermissionDuration - _phaseTime);
        public bool CanSwitchWeapons => Phase == RoundPhase.Intermission;

        private void Awake()
        {
            if (spawner == null) spawner = FindFirstObjectByType<EnemySpawner>();
            if (playerStart == null)
            {
                var go = GameObject.Find("PlayerStart");
                if (go != null) playerStart = go.transform;
            }
            _hud = FindFirstObjectByType<HUD>();
            _loadout = FindFirstObjectByType<PlayerLoadout>();
            _player = FindFirstObjectByType<PlayerController>();
            if (_player != null)
            {
                _playerHealth = _player.GetComponent<Health>();
                _camera = _player.GetComponentInChildren<PlayerCamera>();
            }
        }

        private void Start()
        {
            if (spawner != null)
            {
                spawner.WaveStarted -= OnWaveStarted;
                spawner.WaveStarted += OnWaveStarted;
            }

            _phaseTime = 0f;
            Phase = RoundPhase.Active;
            ApplyPhase();
            if (spawner != null) spawner.Begin();
        }

        private void Update()
        {
            _phaseTime += Time.deltaTime;

            switch (Phase)
            {
                case RoundPhase.Active:
                    if (_phaseTime >= roundDuration) EnterIntermission();
                    break;
                case RoundPhase.Intermission:
                    if (_phaseTime >= intermissionDuration) BeginNextRound();
                    break;
            }

            SyncWeaponHooks();
            PushHud();
        }

        // ------------------------------------------------------------------ phases

        private void EnterIntermission()
        {
            Phase = RoundPhase.Intermission;
            _phaseTime = 0f;
            ApplyPhase();

            // clear the field and heal up between rounds
            if (spawner != null) spawner.ClearAllEnemies();
            if (_playerHealth != null) _playerHealth.ResetHealth();
            if (_camera != null) _camera.ResetView();
        }

        private void BeginNextRound()
        {
            _round++;
            Phase = RoundPhase.Active;
            _phaseTime = 0f;

            if (_player != null && playerStart != null)
            {
                _player.transform.position = playerStart.position;
                _player.transform.rotation = playerStart.rotation;
            }
            if (_loadout != null && _loadout.Active != null) _loadout.Active.RefillAll();
            ApplyPhase();
        }

        /// <summary>Skips straight to the 5s weapon-change window. Used for testing.</summary>
        public void ForceIntermission() => EnterIntermission();

        /// <summary>Applies the firing / weapon-switch locks for the current phase.</summary>
        private void ApplyPhase()
        {
            bool intermission = Phase == RoundPhase.Intermission;

            if (_loadout != null) _loadout.SwitchingAllowed = intermission;

            foreach (var w in AllWeapons())
            {
                if (w != null) w.CanFire = !intermission;
            }
        }

        private System.Collections.Generic.IEnumerable<WeaponController> AllWeapons()
        {
            if (_loadout == null) yield break;
            if (_loadout.Primary != null) yield return _loadout.Primary;
            if (_loadout.Sniper != null) yield return _loadout.Sniper;
            if (_loadout.Sidearm != null) yield return _loadout.Sidearm;
            if (_loadout.Melee != null) yield return _loadout.Melee;
        }

        // ------------------------------------------------------------------ wiring

        private void SyncWeaponHooks()
        {
            if (_loadout == null) return;
            if (_loadout.Active == _wired) return;

            if (_wired != null) _wired.ShotResolved -= OnShot;
            _wired = _loadout.Active;
            if (_wired != null)
            {
                _wired.ShotResolved += OnShot;
                _wired.CanFire = Phase == RoundPhase.Active;
            }
        }

        private void OnShot(HitZone zone, float damage, bool lethal)
        {
            if (damage <= 0f) return;
            _hud?.OnShotResolved(zone, damage, lethal);
        }

        private void OnWaveStarted(int wave, int count)
        {
            if (_hud != null) _hud.SetWave(wave, count);
        }

        private void PushHud()
        {
            if (_hud == null) return;
            _hud.SetRound(_round, Phase, TimeLeft, CanSwitchWeapons);
            if (spawner != null) _hud.SetWave(spawner.Wave, spawner.AliveCount);
        }

        private void OnDestroy()
        {
            if (spawner != null) spawner.WaveStarted -= OnWaveStarted;
            if (_wired != null) _wired.ShotResolved -= OnShot;
        }
    }
}
