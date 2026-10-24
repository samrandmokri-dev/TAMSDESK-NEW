using System.Security.Cryptography;
using System.Windows;
using System.Windows.Threading;
using TamsDesk.Client.Capture;
using TamsDesk.Client.Input;
using TamsDesk.Client.Protocol;
using TamsDesk.Client.Signaling;

namespace TamsDesk.Client;

public partial class MainWindow : Window
{
    private RelayClient? _hostRelay;
    private DispatcherTimer? _captureTimer;
    private ScreenCapturer? _capturer;
    private bool _hosting;
    private bool _sessionActive;
    private string _hostId = "";
    private string _hostPw = "";

    public MainWindow()
    {
        InitializeComponent();
        RelayBox.Text = Config.RelayUrl;
    }

    // ---------------- HOST ----------------

    private async void HostToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_hosting) { StopHosting(); return; }

        Config.RelayUrl = RelayBox.Text.Trim();
        _hostId = NewId();
        _hostPw = NewPassword();
        HostIdBox.Text = _hostId;
        HostPwBox.Text = _hostPw;
        HostStatus.Text = "Connecting to relay...";

        try
        {
            _hostRelay = new RelayClient(Config.RelayUrl);
            _hostRelay.OnMessage += HostOnMessage;
            _hostRelay.OnClosed += reason => Dispatcher.Invoke(() =>
            {
                HostStatus.Text = "Disconnected: " + reason;
                StopHosting();
            });
            await _hostRelay.ConnectAsync();
            await _hostRelay.SendAsync(new Message { Type = MsgType.RegisterHost, Id = Normalize(_hostId) });

            _hosting = true;
            HostToggleBtn.Content = "Stop allowing connections";
            HostStatus.Text = "Online — waiting for a connection";
        }
        catch (Exception ex)
        {
            HostStatus.Text = "Failed: " + ex.Message;
            _hostRelay?.Dispose();
            _hostRelay = null;
        }
    }

    private void HostOnMessage(Message m)
    {
        switch (m.Type)
        {
            case MsgType.ConnectRequest:
                Dispatcher.Invoke(() => HandleConnectRequest(m));
                break;

            case MsgType.SessionStart:
                Dispatcher.Invoke(StartCaptureLoop);
                break;

            case MsgType.PeerDisconnected:
                Dispatcher.Invoke(() =>
                {
                    StopCaptureLoop();
                    HostStatus.Text = "Online — waiting for a connection";
                });
                break;

            // input coming from the controller
            case MsgType.MouseMove: InputInjector.MoveMouse(m.X, m.Y); break;
            case MsgType.MouseButton: InputInjector.MouseButton(m.Button ?? "left", m.Down, m.X, m.Y); break;
            case MsgType.MouseWheel: InputInjector.MouseWheel(m.Delta); break;
            case MsgType.KeyEvent: InputInjector.Key(m.Key, m.Down); break;
        }
    }

    private async void HandleConnectRequest(Message m)
    {
        if (_sessionActive)
        {
            await _hostRelay!.SendAsync(new Message { Type = MsgType.Reject, Reason = "busy" });
            return;
        }

        bool pwOk = string.Equals(m.Password, _hostPw, StringComparison.Ordinal);
        if (!pwOk)
        {
            await _hostRelay!.SendAsync(new Message { Type = MsgType.Reject, Reason = "bad-password" });
            HostStatus.Text = "Rejected a connection (wrong password)";
            return;
        }

        // Explicit human consent, just like AnyDesk's accept dialog.
        var answer = MessageBox.Show(
            this,
            "Someone with the correct password wants to control this computer.\n\nAllow this session?",
            "TAMSDESK — incoming connection",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (answer == MessageBoxResult.Yes)
        {
            _capturer ??= new ScreenCapturer();
            await _hostRelay!.SendAsync(new Message { Type = MsgType.Accept });
            await _hostRelay.SendAsync(new Message
            {
                Type = MsgType.ScreenInfo,
                W = _capturer.ScreenWidth,
                H = _capturer.ScreenHeight
            });
            _sessionActive = true;
            HostStatus.Text = "Connected — your screen is being shared";
        }
        else
        {
            await _hostRelay!.SendAsync(new Message { Type = MsgType.Reject, Reason = "denied" });
            HostStatus.Text = "Online — waiting for a connection";
        }
    }

    private void StartCaptureLoop()
    {
        _capturer ??= new ScreenCapturer();
        _captureTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(1000.0 / Config.TargetFps)
        };
        _captureTimer.Tick += async (_, _) =>
        {
            if (_hostRelay == null || !_sessionActive) return;
            try
            {
                var jpeg = await Task.Run(() => _capturer!.CaptureJpeg(Config.CaptureScale, Config.JpegQuality));
                if (jpeg.Length > 0) await _hostRelay.SendBinaryAsync(jpeg);
            }
            catch { /* skip this frame */ }
        };
        _captureTimer.Start();
    }

    private void StopCaptureLoop()
    {
        _captureTimer?.Stop();
        _captureTimer = null;
        _sessionActive = false;
    }

    private void StopHosting()
    {
        StopCaptureLoop();
        _hostRelay?.Dispose();
        _hostRelay = null;
        _hosting = false;
        HostToggleBtn.Content = "Allow incoming connections";
        HostStatus.Text = "Offline";
    }

    // ---------------- CONTROLLER ----------------

    private void Connect_Click(object sender, RoutedEventArgs e)
    {
        var id = PeerIdBox.Text.Trim();
        var pw = PeerPwBox.Text.Trim();
        if (id.Length == 0) { MessageBox.Show(this, "Enter the remote ID."); return; }

        Config.RelayUrl = RelayBox.Text.Trim();
        var win = new RemoteWindow(Config.RelayUrl, id, pw) { Owner = this };
        win.Show();
    }

    // ---------------- helpers ----------------

    public static string Normalize(string id) => new(id.Where(char.IsLetterOrDigit).ToArray());

    private static string NewId()
    {
        // 9-digit numeric ID, grouped for readability
        var n = RandomNumberGenerator.GetInt32(100_000_000, 999_999_999);
        var s = n.ToString();
        return $"{s[..3]} {s[3..6]} {s[6..]}";
    }

    private static string NewPassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var sb = new char[6];
        for (int i = 0; i < sb.Length; i++)
            sb[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
        return new string(sb);
    }
}
