using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace TamsDesk.Client.Input;

/// <summary>
/// Injects mouse and keyboard events on the host using the Win32 SendInput API.
/// Coordinates arrive normalized (0..1) from the controller and are mapped to the
/// host's virtual screen.
/// </summary>
[SupportedOSPlatform("windows")]
public static class InputInjector
{
    public static void MoveMouse(double nx, double ny)
    {
        var (ax, ay) = ToAbsolute(nx, ny);
        SendMouse(MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK, ax, ay, 0);
    }

    public static void MouseButton(string button, bool down, double nx, double ny)
    {
        var (ax, ay) = ToAbsolute(nx, ny);
        uint flag = button switch
        {
            "right" => down ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP,
            "middle" => down ? MOUSEEVENTF_MIDDLEDOWN : MOUSEEVENTF_MIDDLEUP,
            _ => down ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP,
        };
        SendMouse(flag | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK, ax, ay, 0);
    }

    public static void MouseWheel(int delta)
        => SendMouse(MOUSEEVENTF_WHEEL, 0, 0, delta);

    public static void Key(int vk, bool down)
    {
        var inp = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = (ushort)vk,
                    dwFlags = down ? 0 : KEYEVENTF_KEYUP
                }
            }
        };
        SendInput(1, new[] { inp }, Marshal.SizeOf<INPUT>());
    }

    private static (int, int) ToAbsolute(double nx, double ny)
    {
        // Map 0..1 across the whole virtual desktop into the 0..65535 range SendInput expects.
        int ax = (int)Math.Round(nx * 65535.0);
        int ay = (int)Math.Round(ny * 65535.0);
        return (Math.Clamp(ax, 0, 65535), Math.Clamp(ay, 0, 65535));
    }

    private static void SendMouse(uint flags, int ax, int ay, int data)
    {
        var inp = new INPUT
        {
            type = INPUT_MOUSE,
            U = new InputUnion
            {
                mi = new MOUSEINPUT { dx = ax, dy = ay, mouseData = data, dwFlags = flags }
            }
        };
        SendInput(1, new[] { inp }, Marshal.SizeOf<INPUT>());
    }

    // --- native ---
    private const int INPUT_MOUSE = 0, INPUT_KEYBOARD = 1;
    private const uint MOUSEEVENTF_MOVE = 0x0001, MOUSEEVENTF_ABSOLUTE = 0x8000, MOUSEEVENTF_VIRTUALDESK = 0x4000;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002, MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008, MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020, MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT { public int type; public InputUnion U; }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT { public int dx, dy, mouseData; public uint dwFlags, time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }
}
