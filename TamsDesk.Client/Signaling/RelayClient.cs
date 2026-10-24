using System.Net.WebSockets;
using System.Text;
using TamsDesk.Client.Protocol;

namespace TamsDesk.Client.Signaling;

/// <summary>
/// WebSocket connection to the TAMSDESK relay. Handles JSON control messages and
/// raw binary screen frames, and exposes them as events on a background loop.
/// </summary>
public sealed class RelayClient : IDisposable
{
    private readonly ClientWebSocket _ws = new();
    private readonly Uri _uri;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public event Action<Message>? OnMessage;
    public event Action<byte[]>? OnBinary;
    public event Action<string>? OnClosed;

    public RelayClient(string relayUrl) => _uri = new Uri(relayUrl);

    public async Task ConnectAsync()
    {
        await _ws.ConnectAsync(_uri, _cts.Token);
        _ = Task.Run(ReceiveLoop);
    }

    public async Task SendAsync(Message m)
    {
        var bytes = Encoding.UTF8.GetBytes(m.ToJson());
        await _sendLock.WaitAsync();
        try
        {
            if (_ws.State == WebSocketState.Open)
                await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, _cts.Token);
        }
        finally { _sendLock.Release(); }
    }

    public async Task SendBinaryAsync(byte[] data)
    {
        await _sendLock.WaitAsync();
        try
        {
            if (_ws.State == WebSocketState.Open)
                await _ws.SendAsync(data, WebSocketMessageType.Binary, true, _cts.Token);
        }
        finally { _sendLock.Release(); }
    }

    private async Task ReceiveLoop()
    {
        var buffer = new byte[64 * 1024];
        using var frame = new MemoryStream();
        try
        {
            while (_ws.State == WebSocketState.Open && !_cts.IsCancellationRequested)
            {
                frame.SetLength(0);
                WebSocketReceiveResult result;
                do
                {
                    result = await _ws.ReceiveAsync(buffer, _cts.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        OnClosed?.Invoke("closed by relay");
                        return;
                    }
                    frame.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var json = Encoding.UTF8.GetString(frame.ToArray());
                    var msg = Message.FromJson(json);
                    if (msg != null) OnMessage?.Invoke(msg);
                }
                else // binary = screen frame
                {
                    OnBinary?.Invoke(frame.ToArray());
                }
            }
        }
        catch (Exception ex)
        {
            OnClosed?.Invoke(ex.Message);
        }
    }

    public void Dispose()
    {
        try { _cts.Cancel(); } catch { }
        try { _ws.Dispose(); } catch { }
    }
}
