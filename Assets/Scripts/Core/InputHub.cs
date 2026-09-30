using UnityEngine;
using UnityEngine.InputSystem;

namespace FPS.Core
{
    /// <summary>
    /// Central input access for the game.
    ///
    /// Reads devices directly from the Input System package (activeInputHandler = New)
    /// instead of relying on the generated <c>InputSystem_Actions</c> wrapper. That keeps
    /// gameplay scripts compiling even before Unity regenerates the wrapper class, and
    /// means no Inspector wiring is required on prefabs.
    /// </summary>
    public static class InputHub
    {
        private static bool _jumpQueued;
        private static bool _crouchQueued;

        public static Vector2 Move
        {
            get
            {
                var k = Keyboard.current;
                if (k == null) return Vector2.zero;
                float x = 0f, y = 0f;
                if (k[Key.A].isPressed || k[Key.LeftArrow].isPressed) x -= 1f;
                if (k[Key.D].isPressed || k[Key.RightArrow].isPressed) x += 1f;
                if (k[Key.S].isPressed || k[Key.DownArrow].isPressed) y -= 1f;
                if (k[Key.W].isPressed || k[Key.UpArrow].isPressed) y += 1f;
                return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
            }
        }

        public static Vector2 Look
        {
            get
            {
                var m = Mouse.current;
                if (m == null) return Vector2.zero;
                return m.delta.ReadValue() * 0.05f; // already scaled to degrees
            }
        }

        public static bool SprintHeld
        {
            get
            {
                var k = Keyboard.current;
                return k != null && k[Key.LeftShift].isPressed;
            }
        }

        public static bool FireHeld => Mouse.current != null && Mouse.current.leftButton.isPressed;
        public static bool AimHeld => Mouse.current != null && Mouse.current.rightButton.isPressed;
        public static bool ReloadHeld => Keyboard.current != null && Keyboard.current[Key.R].isPressed;
        public static bool KnifeHeld => Keyboard.current != null && Keyboard.current[Key.Q].isPressed;

        /// <summary>True on the frame T was pressed - cycles to the next weapon.</summary>
        public static bool CycleWeaponQueued
        {
            get
            {
                var k = Keyboard.current;
                return k != null && k[Key.T].wasPressedThisFrame;
            }
        }

        public static bool JumpQueued
        {
            get
            {
                if (_jumpQueued) { _jumpQueued = false; return true; }
                return false;
            }
        }

        public static bool CrouchQueued
        {
            get
            {
                if (_crouchQueued) { _crouchQueued = false; return true; }
                return false;
            }
        }

        /// <summary>0..3 weapon slot requested this frame, or -1 for none.</summary>
        public static int SlotQueued
        {
            get
            {
                var k = Keyboard.current;
                if (k == null) return -1;
                if (k[Key.Digit1].wasPressedThisFrame) return 0;
                if (k[Key.Digit2].wasPressedThisFrame) return 1;
                if (k[Key.Digit3].wasPressedThisFrame) return 2;
                if (k[Key.Digit4].wasPressedThisFrame) return 3;
                if (k[Key.F].wasPressedThisFrame) return 2;   // quick-swap to the sidearm
                return -1;
            }
        }

        /// <summary>Call once per frame from a single update owner.</summary>
        public static void Pump()
        {
            var k = Keyboard.current;
            if (k == null) return;
            if (k[Key.Space].wasPressedThisFrame) _jumpQueued = true;
            if (k[Key.LeftCtrl].wasPressedThisFrame || k[Key.C].wasPressedThisFrame) _crouchQueued = true;
        }

        public static void ClearQueues()
        {
            _jumpQueued = false;
            _crouchQueued = false;
        }
    }
}
