using System.Collections.Generic;
using UnityEngine;
#if BW_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace Bubblewright
{
    /// <summary>
    /// One place that merges touch, keyboard and gamepad into the three things the game needs:
    /// a move direction, fire held, and confirm (restart).
    ///
    /// Touch (from the sheet): left half of the screen is a RELATIVE drag pad. Where your thumb lands
    /// is the centre; drag left or right of it to run. Right half is the fire button; hold to keep firing.
    /// The mouse acts as one finger, so the touch controls can be tried on desktop too.
    ///
    /// Uses the Input System package when it is the active input backend, otherwise the legacy Input Manager.
    /// </summary>
    public class GameInput
    {
        public float Move { get; private set; }
        public bool FireHeld { get; private set; }
        public bool FirePressed { get; private set; }
        public bool ConfirmPressed { get; private set; }
        public bool AnyInput { get; private set; }
        /// <summary>Confirm from keys or pad only, for menus where a tap should hit a button instead.</summary>
        public bool KeyConfirmPressed { get; private set; }
        /// <summary>Screen position (pixels, origin bottom left) of a finger or click that began this frame.</summary>
        public Vector2? TapPosition { get; private set; }

        // Desktop debug helpers: 1-4 spawn an XL bubble of each type, F1 toggles hitboxes.
        public int DebugSpawn { get; private set; }
        public bool DebugToggleHitboxes { get; private set; }
        /// <summary>Esc, P or pad Start. F2 opens the debug panel (it pauses the game too).</summary>
        public bool PausePressed { get; private set; }
        public bool DebugPanelPressed { get; private set; }

        // Touch pad state, read by the on-screen view.
        public bool TouchUsed { get; private set; }
        public bool PadActive => padPointer != NoPointer;
        public Vector2 PadOrigin { get; private set; }
        public Vector2 PadCurrent { get; private set; }
        public bool TouchFireHeld { get; private set; }

        const int NoPointer = int.MinValue;
        const int MousePointer = -1000;
        int padPointer = NoPointer;
        readonly HashSet<int> firePointers = new HashSet<int>();
        readonly List<PointerSample> pointers = new List<PointerSample>(8);

        struct PointerSample
        {
            public int id;
            public Vector2 position;
            public bool began;
        }

        /// <summary>Pixels of drag before Wick starts running, scaled with screen density.</summary>
        float DeadZone => Screen.dpi > 0 ? Screen.dpi * 0.06f : Screen.height * 0.02f;
        float MaxDrag => DeadZone * 3f;

        public GameInput()
        {
#if !(BW_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM)
            Input.simulateMouseWithTouches = false; // otherwise touch 0 is reported twice
            Input.multiTouchEnabled = true;
#endif
        }

        public void Update()
        {
            pointers.Clear();
            float keyMove = 0f;
            bool keyFire = false, keyFirePressed = false, confirm = false, any = false;
            DebugSpawn = 0;
            DebugToggleHitboxes = false;
            PausePressed = false;
            DebugPanelPressed = false;

#if BW_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) keyMove -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) keyMove += 1f;
                keyFire |= kb.spaceKey.isPressed || kb.jKey.isPressed || kb.kKey.isPressed || kb.wKey.isPressed || kb.upArrowKey.isPressed;
                keyFirePressed |= kb.spaceKey.wasPressedThisFrame || kb.jKey.wasPressedThisFrame || kb.kKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame;
                confirm |= kb.rKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
                any |= kb.anyKey.wasPressedThisFrame;
                if (kb.digit1Key.wasPressedThisFrame) DebugSpawn = 1;
                if (kb.digit2Key.wasPressedThisFrame) DebugSpawn = 2;
                if (kb.digit3Key.wasPressedThisFrame) DebugSpawn = 3;
                if (kb.digit4Key.wasPressedThisFrame) DebugSpawn = 4;
                DebugToggleHitboxes = kb.f1Key.wasPressedThisFrame;
                PausePressed |= kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame;
                DebugPanelPressed = kb.f2Key.wasPressedThisFrame;
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                float x = pad.leftStick.ReadValue().x + pad.dpad.ReadValue().x;
                if (Mathf.Abs(x) > 0.35f) keyMove += Mathf.Sign(x);
                keyFire |= pad.buttonSouth.isPressed || pad.buttonWest.isPressed || pad.rightTrigger.isPressed || pad.rightShoulder.isPressed;
                keyFirePressed |= pad.buttonSouth.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame || pad.rightTrigger.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame;
                confirm |= pad.startButton.wasPressedThisFrame || pad.buttonSouth.wasPressedThisFrame;
                PausePressed |= pad.startButton.wasPressedThisFrame;
                any |= keyFirePressed || Mathf.Abs(x) > 0.35f;
            }
            var ts = Touchscreen.current;
            if (ts != null)
            {
                foreach (TouchControl t in ts.touches)
                {
                    if (!t.press.isPressed) continue;
                    pointers.Add(new PointerSample { id = t.touchId.ReadValue(), position = t.position.ReadValue(), began = t.press.wasPressedThisFrame });
                }
            }
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed && pointers.Count == 0)
                pointers.Add(new PointerSample { id = MousePointer, position = mouse.position.ReadValue(), began = mouse.leftButton.wasPressedThisFrame });
#else
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) keyMove -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) keyMove += 1f;
            float axis = 0f;
            try { axis = Input.GetAxisRaw("Horizontal"); } catch (System.ArgumentException) { }
            if (keyMove == 0f && Mathf.Abs(axis) > 0.35f) keyMove = Mathf.Sign(axis);
            KeyCode[] fireKeys = { KeyCode.Space, KeyCode.J, KeyCode.K, KeyCode.W, KeyCode.UpArrow, KeyCode.JoystickButton0, KeyCode.JoystickButton2, KeyCode.JoystickButton5 };
            foreach (var k in fireKeys)
            {
                keyFire |= Input.GetKey(k);
                keyFirePressed |= Input.GetKeyDown(k);
            }
            confirm |= Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)
                       || Input.GetKeyDown(KeyCode.JoystickButton7) || Input.GetKeyDown(KeyCode.JoystickButton0);
            any |= Input.anyKeyDown;
            for (int d = 1; d <= 4; d++) if (Input.GetKeyDown(KeyCode.Alpha0 + d)) DebugSpawn = d;
            DebugToggleHitboxes = Input.GetKeyDown(KeyCode.F1);
            PausePressed = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.JoystickButton7);
            DebugPanelPressed = Input.GetKeyDown(KeyCode.F2);
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) continue;
                pointers.Add(new PointerSample { id = t.fingerId, position = t.position, began = t.phase == TouchPhase.Began });
            }
            if (Input.touchCount == 0 && Input.GetMouseButton(0))
                pointers.Add(new PointerSample { id = MousePointer, position = Input.mousePosition, began = Input.GetMouseButtonDown(0) });
#endif

            float touchMove = UpdateTouch(out bool touchFire, out bool touchFirePressed, out bool touchBegan);

            Move = Mathf.Clamp(touchMove != 0f ? touchMove : keyMove, -1f, 1f);
            FireHeld = keyFire || touchFire;
            FirePressed = keyFirePressed || touchFirePressed;
            ConfirmPressed = confirm || touchBegan;
            KeyConfirmPressed = confirm;
            AnyInput = any || touchBegan;
        }

        float UpdateTouch(out bool fire, out bool firePressed, out bool anyBegan)
        {
            fire = false;
            firePressed = false;
            anyBegan = false;
            TapPosition = null;
            float half = Screen.width * 0.5f;

            // Drop pointers that lifted.
            if (padPointer != NoPointer && !Contains(padPointer)) padPointer = NoPointer;
            firePointers.RemoveWhere(id => !Contains(id));

            foreach (var p in pointers)
            {
                if (!p.began) continue;
                anyBegan = true;
                TouchUsed = true;
                TapPosition ??= p.position;
                if (p.position.x < half && padPointer == NoPointer)
                {
                    padPointer = p.id;
                    PadOrigin = p.position;
                    PadCurrent = p.position;
                }
                else if (p.position.x >= half)
                {
                    firePointers.Add(p.id);
                    firePressed = true;
                }
            }

            float move = 0f;
            foreach (var p in pointers)
            {
                if (p.id == padPointer)
                {
                    PadCurrent = p.position;
                    float delta = p.position.x - PadOrigin.x;
                    // The origin trails the thumb, so reversing never needs a long drag back.
                    if (Mathf.Abs(delta) > MaxDrag)
                    {
                        PadOrigin = new Vector2(p.position.x - Mathf.Sign(delta) * MaxDrag, PadOrigin.y);
                        delta = Mathf.Sign(delta) * MaxDrag;
                    }
                    if (Mathf.Abs(delta) > DeadZone) move = Mathf.Sign(delta);
                }
                else if (firePointers.Contains(p.id))
                {
                    fire = true;
                }
            }
            TouchFireHeld = fire;
            return move;
        }

        bool Contains(int id)
        {
            foreach (var p in pointers) if (p.id == id) return true;
            return false;
        }
    }
}
