using UnityEditor;
using UnityEngine;
using FPS.Core;

namespace FPS.EditorTools
{
    /// <summary>Debug helpers for exercising round flow without waiting out the timer.</summary>
    public static class DebugTools
    {
        [MenuItem("FPS/Debug/Force Intermission (5s)")]
        public static void ForceIntermission()
        {
            var d = Object.FindFirstObjectByType<GameDirector>();
            if (d == null) { Debug.LogWarning("[FPS] No GameDirector in the open scene."); return; }
            d.ForceIntermission();
            Debug.Log("[FPS] Forced intermission - weapon switching should now be allowed.");
        }

        [MenuItem("FPS/Debug/Reset Recoil + View")]
        public static void ResetRecoil()
        {
            var cam = Object.FindFirstObjectByType<Player.PlayerCamera>();
            if (cam == null) { Debug.LogWarning("[FPS] No PlayerCamera in the open scene."); return; }
            cam.ResetView();
            Debug.Log("[FPS] View and recoil reset.");
        }

        [MenuItem("FPS/Debug/Simulate Burst (shows spray pattern)")]
        public static void SimulateBurst()
        {
            var l = Object.FindFirstObjectByType<Player.PlayerLoadout>();
            var cam = Object.FindFirstObjectByType<Player.PlayerCamera>();
            if (l == null || l.Primary == null || cam == null)
            {
                Debug.LogWarning("[FPS] Need a PlayerLoadout + PlayerCamera in the open scene.");
                return;
            }

            var w = l.Primary;
            l.Equip(FPS.Combat.WeaponSlot.Primary);
            cam.ResetRecoil();

            var sb = new System.Text.StringBuilder();
            sb.Append("[FPS] burst pattern for " + w.Definition.displayName + ":");
            for (int i = 0; i < 12; i++)
            {
                w.Fire();
                Vector2 o = cam.RecoilOffset;
                sb.Append(string.Format("\n   shot {0,2}: recoilPitch={1,6:F2}  recoilYaw={2,6:F2}", i + 1, o.y, o.x));
            }
            Debug.Log(sb.ToString());
        }

        [MenuItem("FPS/Debug/Cycle Weapon (same path as T)")]
        public static void CycleWeapon()
        {
            var l = Object.FindFirstObjectByType<Player.PlayerLoadout>();
            if (l == null) { Debug.LogWarning("[FPS] No PlayerLoadout in the open scene."); return; }
            if (!l.SwitchingAllowed) { Debug.LogWarning("[FPS] Switching is locked (only allowed during intermission)."); return; }
            l.CycleNext();
            LogWeapon();
        }

        [MenuItem("FPS/Debug/Log Active Weapon")]
        public static void LogWeapon()
        {
            var l = Object.FindFirstObjectByType<Player.PlayerLoadout>();
            if (l == null || l.Active == null) { Debug.LogWarning("[FPS] No active weapon."); return; }
            var w = l.Active;
            Debug.Log("[FPS] weapon=" + w.Definition.displayName +
                      " mag=" + w.CurrentMag + "/" + w.CurrentReserve +
                      " spread=" + w.CurrentSpread.ToString("F2") + "deg" +
                      " aiming=" + w.IsAiming);
        }
    }
}
