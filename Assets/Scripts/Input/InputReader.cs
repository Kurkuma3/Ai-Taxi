using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Taxi
{
    /// <summary>
    /// Single place that turns raw touches into gameplay input.
    /// - Floating virtual stick: first free finger down sets the origin; drag = direction and deflection.
    /// - Hold pointer: a finger that lands on a "hold target" (decided once, at touch-down, via IsHoldTarget)
    ///   is reported as HoldActive/HoldPos for as long as it stays down and never becomes stick input.
    ///   Used to get out of the car by holding a finger on it.
    /// Editor fallbacks: mouse acts as one finger, WASD / arrows = stick, E = exit.
    /// </summary>
    public class InputReader : MonoBehaviour
    {
        public static InputReader I { get; private set; }

        [SerializeField] GameplayConfig config;

        /// <summary>Screen test evaluated once when a finger lands: true = that finger is a hold, not a stick.</summary>
        public System.Func<Vector2, bool> IsHoldTarget;

        public bool StickActive { get; private set; }
        public Vector2 StickOrigin { get; private set; }
        /// <summary>Knob position, clamped to the stick radius.</summary>
        public Vector2 StickPos { get; private set; }
        /// <summary>Offset from origin, magnitude 0..1.</summary>
        public Vector2 StickVector { get; private set; }
        public float StickRadiusPx => config.stickRadiusScreenFraction * Screen.height;

        /// <summary>A finger that started on a hold target is still down.</summary>
        public bool HoldActive { get; private set; }
        public Vector2 HoldPos { get; private set; }

        public bool ExitPressedThisFrame { get; private set; }

        struct Pointer { public int id; public Vector2 pos; }
        readonly List<Pointer> pointers = new List<Pointer>(8); // stick candidates this frame
        readonly List<int> seenIds = new List<int>(8);          // every pointer down last frame
        readonly List<int> curIds = new List<int>(8);
        readonly List<int> holdIds = new List<int>(4);          // pointers that landed on a hold target
        int stickPointerId = int.MinValue;
        const int MouseId = -1;

        void Awake() { I = this; }
        void OnDestroy() { if (I == this) I = null; }

        void Update()
        {
            CollectPointers();

            bool found = false;
            Vector2 pos = default;
            if (stickPointerId != int.MinValue)
                foreach (var p in pointers) if (p.id == stickPointerId) { found = true; pos = p.pos; break; }
            if (!found && pointers.Count > 0)
            {
                stickPointerId = pointers[0].id;
                StickOrigin = pos = pointers[0].pos;
                found = true;
            }
            if (!found) stickPointerId = int.MinValue;

            StickActive = found;
            if (found)
            {
                float r = Mathf.Max(1f, StickRadiusPx);
                Vector2 v = (pos - StickOrigin) / r;
                if (v.sqrMagnitude > 1f) v.Normalize();
                StickVector = v;
                StickPos = StickOrigin + v * r;
            }
            else StickVector = Vector2.zero;

            ApplyKeyboardFallback();
        }

        void CollectPointers()
        {
            pointers.Clear();
            curIds.Clear();
            HoldActive = false;

            var ts = Touchscreen.current;
            if (ts != null)
            {
                foreach (var t in ts.touches)
                {
                    if (!t.press.isPressed) continue;
                    AddPointer(t.touchId.ReadValue(), t.position.ReadValue());
                }
            }
            var m = Mouse.current;
            if (m != null && m.leftButton.isPressed)
                AddPointer(MouseId, m.position.ReadValue());

            // Forget holds whose finger lifted; remember who was down for next frame's "is new" test.
            for (int k = holdIds.Count - 1; k >= 0; k--)
                if (!curIds.Contains(holdIds[k])) holdIds.RemoveAt(k);
            seenIds.Clear();
            seenIds.AddRange(curIds);
        }

        void AddPointer(int id, Vector2 pos)
        {
            curIds.Add(id);
            bool isNew = !seenIds.Contains(id);
            if (isNew && IsHoldTarget != null && IsHoldTarget(pos)) holdIds.Add(id);

            if (holdIds.Contains(id))
            {
                if (!HoldActive) { HoldActive = true; HoldPos = pos; }
                return;
            }
            pointers.Add(new Pointer { id = id, pos = pos });
        }

        void ApplyKeyboardFallback()
        {
            var kb = Keyboard.current;
            ExitPressedThisFrame = false;
            if (kb == null) return;
            ExitPressedThisFrame = kb.eKey.wasPressedThisFrame;

            if (!StickActive)
            {
                Vector2 v = Vector2.zero;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v.y -= 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) v.x -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) v.x += 1;
                if (v != Vector2.zero)
                {
                    StickActive = true;
                    StickVector = v.normalized;
                    StickOrigin = new Vector2(Screen.width * 0.5f, Screen.height * 0.25f);
                    StickPos = StickOrigin + StickVector * StickRadiusPx;
                }
            }
        }
    }
}
