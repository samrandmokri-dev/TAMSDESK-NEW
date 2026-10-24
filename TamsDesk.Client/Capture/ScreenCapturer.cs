using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace TamsDesk.Client.Capture;

/// <summary>
/// Captures the primary screen with GDI BitBlt and encodes it to JPEG.
/// Simple and dependency-free; good enough for a v1. For higher frame rates,
/// swap this for the Windows.Graphics.Capture (WGC) API later.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ScreenCapturer
{
    public int ScreenWidth { get; private set; }
    public int ScreenHeight { get; private set; }

    private readonly ImageCodecInfo _jpegCodec;

    public ScreenCapturer()
    {
        _jpegCodec = ImageCodecInfo.GetImageEncoders()
            .First(c => c.FormatID == ImageFormat.Jpeg.Guid);
        RefreshBounds();
    }

    public void RefreshBounds()
    {
        ScreenWidth = GetSystemMetrics(SM_CXSCREEN);
        ScreenHeight = GetSystemMetrics(SM_CYSCREEN);
    }

    /// <summary>
    /// Capture the whole primary screen and return JPEG bytes.
    /// <paramref name="scale"/> shrinks the image (0.5 = half size) to save bandwidth.
    /// <paramref name="quality"/> is JPEG quality 1..100.
    /// </summary>
    public byte[] CaptureJpeg(double scale = 1.0, long quality = 55)
    {
        RefreshBounds();
        int w = ScreenWidth, h = ScreenHeight;
        if (w <= 0 || h <= 0) return Array.Empty<byte>();

        using var full = new Bitmap(w, h, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(full))
        {
            g.CopyFromScreen(0, 0, 0, 0, new Size(w, h), CopyPixelOperation.SourceCopy);
            DrawCursor(g);
        }

        Bitmap toEncode = full;
        Bitmap? scaled = null;
        if (scale > 0 && scale < 0.999)
        {
            int sw = Math.Max(1, (int)(w * scale));
            int sh = Math.Max(1, (int)(h * scale));
            scaled = new Bitmap(sw, sh, PixelFormat.Format24bppRgb);
            using var g = Graphics.FromImage(scaled);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
            g.DrawImage(full, 0, 0, sw, sh);
            toEncode = scaled;
        }

        using var ms = new MemoryStream();
        using (var p = new EncoderParameters(1))
        {
            p.Param[0] = new EncoderParameter(Encoder.Quality, quality);
            toEncode.Save(ms, _jpegCodec, p);
        }
        scaled?.Dispose();
        return ms.ToArray();
    }

    private static void DrawCursor(Graphics g)
    {
        // Draw the live cursor onto the captured frame using GDI DrawIconEx
        // (no WinForms dependency, so this compiles in a pure WPF project).
        var ci = new CURSORINFO { cbSize = Marshal.SizeOf<CURSORINFO>() };
        if (!GetCursorInfo(ref ci) || ci.flags != CURSOR_SHOWING || ci.hCursor == IntPtr.Zero) return;
        if (!GetIconInfo(ci.hCursor, out var icon)) return;

        IntPtr hdc = g.GetHdc();
        try
        {
            int x = ci.ptScreenPos.X - icon.xHotspot;
            int y = ci.ptScreenPos.Y - icon.yHotspot;
            DrawIconEx(hdc, x, y, ci.hCursor, 0, 0, 0, IntPtr.Zero, DI_NORMAL);
        }
        finally
        {
            g.ReleaseHdc(hdc);
            if (icon.hbmColor != IntPtr.Zero) DeleteObject(icon.hbmColor);
            if (icon.hbmMask != IntPtr.Zero) DeleteObject(icon.hbmMask);
        }
    }

    // --- native ---
    private const int SM_CXSCREEN = 0, SM_CYSCREEN = 1, CURSOR_SHOWING = 1, DI_NORMAL = 0x0003;

    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int n);
    [DllImport("user32.dll")] private static extern bool GetCursorInfo(ref CURSORINFO pci);
    [DllImport("user32.dll")] private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);
    [DllImport("user32.dll")] private static extern bool DrawIconEx(IntPtr hdc, int x, int y, IntPtr hIcon, int cx, int cy, int step, IntPtr brush, int flags);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct CURSORINFO { public int cbSize; public int flags; public IntPtr hCursor; public POINT ptScreenPos; }

    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO { public bool fIcon; public int xHotspot; public int yHotspot; public IntPtr hbmMask; public IntPtr hbmColor; }
}
