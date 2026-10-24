using System.Text.Json;
using System.Text.Json.Serialization;

namespace TamsDesk.Client.Protocol;

/// <summary>
/// All control messages are small JSON objects with a "type" field.
/// Screen frames travel as raw binary WebSocket messages (JPEG bytes), not as JSON.
/// </summary>
public class Message
{
    [JsonPropertyName("type")] public string Type { get; set; } = "";

    // registration / connect
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("target")] public string? Target { get; set; }
    [JsonPropertyName("password")] public string? Password { get; set; }
    [JsonPropertyName("reason")] public string? Reason { get; set; }
    [JsonPropertyName("message")] public string? Error { get; set; }

    // host -> controller: the host screen size, so the controller can map coordinates
    [JsonPropertyName("w")] public int W { get; set; }
    [JsonPropertyName("h")] public int H { get; set; }

    // input events (controller -> host). Coordinates are normalized 0..1.
    [JsonPropertyName("x")] public double X { get; set; }
    [JsonPropertyName("y")] public double Y { get; set; }
    [JsonPropertyName("button")] public string? Button { get; set; }   // left|right|middle
    [JsonPropertyName("down")] public bool Down { get; set; }          // for button/key press state
    [JsonPropertyName("delta")] public int Delta { get; set; }         // wheel
    [JsonPropertyName("key")] public int Key { get; set; }             // Windows virtual key code
    [JsonPropertyName("text")] public string? Text { get; set; }       // clipboard / typed text

    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
    };

    public string ToJson() => JsonSerializer.Serialize(this, Opts);

    public static Message? FromJson(string json)
    {
        try { return JsonSerializer.Deserialize<Message>(json); }
        catch { return null; }
    }
}

public static class MsgType
{
    public const string RegisterHost = "register-host";
    public const string Registered = "registered";
    public const string Connect = "connect";
    public const string ConnectRequest = "connect-request";
    public const string Accept = "accept";
    public const string Reject = "reject";
    public const string Accepted = "accepted";
    public const string Rejected = "rejected";
    public const string SessionStart = "session-start";
    public const string PeerDisconnected = "peer-disconnected";
    public const string Error = "error";

    // session traffic
    public const string ScreenInfo = "screen-info";
    public const string MouseMove = "mouse-move";
    public const string MouseButton = "mouse-button";
    public const string MouseWheel = "mouse-wheel";
    public const string KeyEvent = "key-event";
    public const string Clipboard = "clipboard";
}
