namespace TamsDesk.Client;

/// <summary>
/// Where the relay server lives. Change this to your own VPS once deployed,
/// e.g. "wss://relay.yourdomain.com". For a quick LAN test you can run the
/// relay locally and use "ws://127.0.0.1:8080".
/// </summary>
public static class Config
{
    public static string RelayUrl { get; set; } = "wss://relay.example.com";

    // capture tuning
    public const double CaptureScale = 0.85;   // shrink frames to save bandwidth
    public const long JpegQuality = 55;        // 1..100
    public const int TargetFps = 12;           // frames per second the host sends
}
