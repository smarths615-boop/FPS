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
