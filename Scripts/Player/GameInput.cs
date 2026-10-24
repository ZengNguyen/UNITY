using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Bộ điều hợp phím bấm thông minh (GameInput):
/// - Tự động tương thích hoàn toàn với New Input System (Unity 6) và Legacy Input Manager.
/// - Tuyệt đối không bao giờ bị văng lỗi InvalidOperationException!
/// </summary>
public static class GameInput
{
    public static bool GetKeyDown(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            switch (key)
            {
                case KeyCode.Space: return kb.spaceKey.wasPressedThisFrame;
                case KeyCode.A: return kb.aKey.wasPressedThisFrame;
                case KeyCode.D: return kb.dKey.wasPressedThisFrame;
                case KeyCode.W: return kb.wKey.wasPressedThisFrame;
                case KeyCode.S: return kb.sKey.wasPressedThisFrame;
                case KeyCode.E: return kb.eKey.wasPressedThisFrame;
                case KeyCode.Q: return kb.qKey.wasPressedThisFrame;
                case KeyCode.F: return kb.fKey.wasPressedThisFrame;
                case KeyCode.C: return kb.cKey.wasPressedThisFrame;
                case KeyCode.X: return kb.xKey.wasPressedThisFrame;
                case KeyCode.LeftArrow: return kb.leftArrowKey.wasPressedThisFrame;
                case KeyCode.RightArrow: return kb.rightArrowKey.wasPressedThisFrame;
                case KeyCode.UpArrow: return kb.upArrowKey.wasPressedThisFrame;
                case KeyCode.DownArrow: return kb.downArrowKey.wasPressedThisFrame;
                case KeyCode.Escape: return kb.escapeKey.wasPressedThisFrame;
                case KeyCode.Return: return kb.enterKey.wasPressedThisFrame;
            }
            return false;
        }
        return false;
#else
        try
        {
            return Input.GetKeyDown(key);
        }
        catch
        {
            return false;
        }
#endif
    }

    public static bool GetKeyUp(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            switch (key)
            {
                case KeyCode.Space: return kb.spaceKey.wasReleasedThisFrame;
                case KeyCode.A: return kb.aKey.wasReleasedThisFrame;
                case KeyCode.D: return kb.dKey.wasReleasedThisFrame;
                case KeyCode.W: return kb.wKey.wasReleasedThisFrame;
                case KeyCode.S: return kb.sKey.wasReleasedThisFrame;
                case KeyCode.E: return kb.eKey.wasReleasedThisFrame;
                case KeyCode.Q: return kb.qKey.wasReleasedThisFrame;
                case KeyCode.F: return kb.fKey.wasReleasedThisFrame;
                case KeyCode.C: return kb.cKey.wasReleasedThisFrame;
                case KeyCode.X: return kb.xKey.wasReleasedThisFrame;
                case KeyCode.LeftArrow: return kb.leftArrowKey.wasReleasedThisFrame;
                case KeyCode.RightArrow: return kb.rightArrowKey.wasReleasedThisFrame;
                case KeyCode.UpArrow: return kb.upArrowKey.wasReleasedThisFrame;
                case KeyCode.DownArrow: return kb.downArrowKey.wasReleasedThisFrame;
                case KeyCode.Escape: return kb.escapeKey.wasReleasedThisFrame;
                case KeyCode.Return: return kb.enterKey.wasReleasedThisFrame;
            }
            return false;
        }
        return false;
#else
        try
        {
            return Input.GetKeyUp(key);
        }
        catch
        {
            return false;
        }
#endif
    }

    public static bool GetKey(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            switch (key)
            {
                case KeyCode.Space: return kb.spaceKey.isPressed;
                case KeyCode.A: return kb.aKey.isPressed;
                case KeyCode.D: return kb.dKey.isPressed;
                case KeyCode.W: return kb.wKey.isPressed;
                case KeyCode.S: return kb.sKey.isPressed;
                case KeyCode.E: return kb.eKey.isPressed;
                case KeyCode.Q: return kb.qKey.isPressed;
                case KeyCode.F: return kb.fKey.isPressed;
                case KeyCode.C: return kb.cKey.isPressed;
                case KeyCode.X: return kb.xKey.isPressed;
                case KeyCode.LeftArrow: return kb.leftArrowKey.isPressed;
                case KeyCode.RightArrow: return kb.rightArrowKey.isPressed;
                case KeyCode.UpArrow: return kb.upArrowKey.isPressed;
                case KeyCode.DownArrow: return kb.downArrowKey.isPressed;
                case KeyCode.Escape: return kb.escapeKey.isPressed;
                case KeyCode.Return: return kb.enterKey.isPressed;
            }
            return false;
        }
        return false;
#else
        try
        {
            return Input.GetKey(key);
        }
        catch
        {
            return false;
        }
#endif
    }

    public static float GetHorizontalAxis()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            float h = 0f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) h -= 1f;
            return h;
        }
        return 0f;
#else
        try
        {
            return Input.GetAxisRaw("Horizontal");
        }
        catch
        {
            return 0f;
        }
#endif
    }

    public static bool GetMouseButtonDown(int button)
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse != null)
        {
            if (button == 0) return mouse.leftButton.wasPressedThisFrame;
            if (button == 1) return mouse.rightButton.wasPressedThisFrame;
            if (button == 2) return mouse.middleButton.wasPressedThisFrame;
        }
        return false;
#else
        try
        {
            return Input.GetMouseButtonDown(button);
        }
        catch
        {
            return false;
        }
#endif
    }

    public static Vector2 GetMousePosition()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse != null)
        {
            return mouse.position.ReadValue();
        }
        return Vector2.zero;
#else
        try
        {
            return Input.mousePosition;
        }
        catch
        {
            return Vector2.zero;
        }
#endif
    }
}
