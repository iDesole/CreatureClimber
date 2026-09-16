using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CreatureClimb
{
    /// <summary>
    /// Touch, mouse, keyboard, and optional gamepad.
    /// Left/right = screen half or digital left/right.
    /// </summary>
    public static class TapInput
    {
        /// <summary>
        /// Left/Right from keyboard, gamepad, or left/right half of screen (touch/mouse).
        /// </summary>
        public static bool TryGetTapSide(out LeafSide side)
        {
            side = LeafSide.Left;

            if (WasLeftPressed())
            {
                side = LeafSide.Left;
                return true;
            }

            if (WasRightPressed())
            {
                side = LeafSide.Right;
                return true;
            }

            if (TryGetPointerDown(out var screenX))
            {
                if (IsPointerOverUI()) return false;
                side = screenX < Screen.width * 0.5f ? LeafSide.Left : LeafSide.Right;
                return true;
            }

            return false;
        }

        public static bool WasLeftPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null &&
                (Keyboard.current.leftArrowKey.wasPressedThisFrame ||
                 Keyboard.current.aKey.wasPressedThisFrame))
                return true;

            if (Gamepad.current != null)
            {
                if (Gamepad.current.dpad.left.wasPressedThisFrame ||
                    Gamepad.current.leftShoulder.wasPressedThisFrame)
                    return true;

                if (PollStickSide() == LeafSide.Left)
                    return true;
            }
#endif
            return Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
        }

        public static bool WasRightPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null &&
                (Keyboard.current.rightArrowKey.wasPressedThisFrame ||
                 Keyboard.current.dKey.wasPressedThisFrame))
                return true;

            if (Gamepad.current != null)
            {
                if (Gamepad.current.dpad.right.wasPressedThisFrame ||
                    Gamepad.current.rightShoulder.wasPressedThisFrame)
                    return true;

                if (PollStickSide() == LeafSide.Right)
                    return true;
            }
#endif
            return Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
        }

        public static bool WasConfirmPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null &&
                (Keyboard.current.spaceKey.wasPressedThisFrame ||
                 Keyboard.current.enterKey.wasPressedThisFrame))
                return true;

            if (Gamepad.current != null &&
                (Gamepad.current.buttonSouth.wasPressedThisFrame ||
                 Gamepad.current.startButton.wasPressedThisFrame))
                return true;
#endif
            return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
        }

        public static bool WasUpPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null &&
                (Keyboard.current.upArrowKey.wasPressedThisFrame ||
                 Keyboard.current.wKey.wasPressedThisFrame))
                return true;

            if (Gamepad.current != null &&
                (Gamepad.current.dpad.up.wasPressedThisFrame ||
                 Gamepad.current.rightStick.up.wasPressedThisFrame))
                return true;
#endif
            return Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
        }

        public static bool WasDownPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null &&
                (Keyboard.current.downArrowKey.wasPressedThisFrame ||
                 Keyboard.current.sKey.wasPressedThisFrame))
                return true;

            if (Gamepad.current != null &&
                (Gamepad.current.dpad.down.wasPressedThisFrame ||
                 Gamepad.current.rightStick.down.wasPressedThisFrame))
                return true;
#endif
            return Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
        }

        public static bool WasPausePressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null &&
                (Keyboard.current.escapeKey.wasPressedThisFrame ||
                 Keyboard.current.pKey.wasPressedThisFrame))
                return true;

            if (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame)
                return true;
#endif
            return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P);
        }

        // Cached raycast list — avoids GC on every tap (Android / Play Store jank risk).
        static readonly List<RaycastResult> s_raycastResults = new(8);
        static PointerEventData s_pointerEventData;

        public static bool IsPointerOverUI()
        {
            var es = EventSystem.current;
            if (es == null)
                return false;

            // Prefer raycast over IsPointerOverGameObject — more reliable with
            // the new Input System + multi-touch on Android.
            Vector2 screenPos;
            if (!TryGetActivePointerPosition(out screenPos))
                return false;

            if (s_pointerEventData == null)
                s_pointerEventData = new PointerEventData(es);
            else
                s_pointerEventData.Reset();

            s_pointerEventData.position = screenPos;
            s_raycastResults.Clear();
            es.RaycastAll(s_pointerEventData, s_raycastResults);
            return s_raycastResults.Count > 0;
        }

        static bool TryGetActivePointerPosition(out Vector2 screenPos)
        {
            screenPos = default;

#if ENABLE_INPUT_SYSTEM
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            {
                screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
                return true;
            }

            if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                screenPos = Mouse.current.position.ReadValue();
                return true;
            }
#endif

            if (Input.touchCount > 0)
            {
                screenPos = Input.GetTouch(0).position;
                return true;
            }

            if (Input.GetMouseButton(0) || Input.GetMouseButtonDown(0))
            {
                screenPos = Input.mousePosition;
                return true;
            }

            return false;
        }

        static bool TryGetPointerDown(out float screenX)
        {
            screenX = 0f;

#if ENABLE_INPUT_SYSTEM
            if (Touchscreen.current != null)
            {
                var touch = Touchscreen.current.primaryTouch;
                if (touch.press.wasPressedThisFrame)
                {
                    screenX = touch.position.ReadValue().x;
                    return true;
                }
            }

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                screenX = Mouse.current.position.ReadValue().x;
                return true;
            }
#endif

            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                if (touch.phase == UnityEngine.TouchPhase.Began)
                {
                    screenX = touch.position.x;
                    return true;
                }
            }

            if (Input.GetMouseButtonDown(0))
            {
                screenX = Input.mousePosition.x;
                return true;
            }

            return false;
        }

#if ENABLE_INPUT_SYSTEM
        // Edge-trigger stick so holding left/right doesn't spam jumps.
        // Cached once per frame so Left/Right polls share the same sample.
        static int stickPollFrame = -1;
        static LeafSide? stickEdgeThisFrame;
        const float StickThreshold = 0.55f;
        const float StickReset = 0.3f;
        static float stickLatched;

        static LeafSide? PollStickSide()
        {
            if (stickPollFrame == Time.frameCount)
                return stickEdgeThisFrame;

            stickPollFrame = Time.frameCount;
            stickEdgeThisFrame = null;

            if (Gamepad.current == null)
                return null;

            var x = Gamepad.current.leftStick.ReadValue().x;

            if (stickLatched == 0f)
            {
                if (x <= -StickThreshold)
                {
                    stickLatched = -1f;
                    stickEdgeThisFrame = LeafSide.Left;
                }
                else if (x >= StickThreshold)
                {
                    stickLatched = 1f;
                    stickEdgeThisFrame = LeafSide.Right;
                }
            }
            else if (Mathf.Abs(x) < StickReset)
            {
                stickLatched = 0f;
            }

            return stickEdgeThisFrame;
        }
#endif
    }
}

