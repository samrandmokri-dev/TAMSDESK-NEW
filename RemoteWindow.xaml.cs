using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using TamsDesk.Client.Protocol;
using TamsDesk.Client.Signaling;

namespace TamsDesk.Client;

public partial class RemoteWindow : Window
{
    private readonly string _relayUrl;
    private readonly string _peerId;
    private readonly string _password;
    private RelayClient? _relay;

    // host screen size, for mapping; we actually send normalized 0..1 coords,
    // so we only need the displayed image's own area.
    public RemoteWindow(string relayUrl, string peerId, string password)
    {
        InitializeComponent();
        _relayUrl = relayUrl;
        _peerId = peerId;
        _password = password;
        Loaded += async (_, _) => await StartAsync();
        Closed += (_, _) => _relay?.Dispose();
    }

    private async System.Threading.Tasks.Task StartAsync()
    {
        try
        {
            _relay = new RelayClient(_relayUrl);
            _relay.OnMessage += OnMessage;
            _relay.OnBinary += OnFrame;
            _relay.OnClosed += reason => Dispatcher.Invoke(() => ShowStatus("Disconnected: " + reason));
            await _relay.ConnectAsync();
            await _relay.SendAsync(new Message
            {
                Type = MsgType.Connect,
                Target = MainWindow.Normalize(_peerId),
                Password = _password
            });
            ShowStatus("Waiting for the remote computer to accept...");
        }
        catch (Exception ex)
        {
            ShowStatus("Failed: " + ex.Message);
        }
    }

    private void OnMessage(Message m)
    {
        switch (m.Type)
        {
            case MsgType.Accepted:
                Dispatcher.Invoke(() => ShowStatus("Accepted — starting..."));
                break;
            case MsgType.Rejected:
                Dispatcher.Invoke(() => ShowStatus("Rejected: " + (m.Reason ?? "denied")));
                break;
            case MsgType.Error:
                Dispatcher.Invoke(() => ShowStatus("Error: " + (m.Error ?? "unknown")));
                break;
            case MsgType.ScreenInfo:
                Dispatcher.Invoke(HideOverlay);
                break;
            case MsgType.PeerDisconnected:
                Dispatcher.Invoke(() => ShowStatus("The remote computer ended the session."));
                break;
        }
    }

    private void OnFrame(byte[] jpeg)
    {
        Dispatcher.Invoke(() =>
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = new MemoryStream(jpeg);
                bmp.EndInit();
                bmp.Freeze();
                Screen.Source = bmp;
                HideOverlay();
            }
            catch { /* skip bad frame */ }
        });
    }

    // ---------------- input capture ----------------

    private async void Screen_MouseMove(object sender, MouseEventArgs e)
    {
        var (nx, ny) = Norm(e.GetPosition(Screen));
        if (nx < 0) return;
        await Send(new Message { Type = MsgType.MouseMove, X = nx, Y = ny });
    }

    private async void Screen_MouseDown(object sender, MouseButtonEventArgs e)
    {
        Focus();
        var (nx, ny) = Norm(e.GetPosition(Screen));
        if (nx < 0) return;
        await Send(new Message { Type = MsgType.MouseButton, Button = ButtonName(e.ChangedButton), Down = true, X = nx, Y = ny });
    }

    private async void Screen_MouseUp(object sender, MouseButtonEventArgs e)
    {
        var (nx, ny) = Norm(e.GetPosition(Screen));
        if (nx < 0) return;
        await Send(new Message { Type = MsgType.MouseButton, Button = ButtonName(e.ChangedButton), Down = false, X = nx, Y = ny });
    }

    private async void Screen_MouseWheel(object sender, MouseWheelEventArgs e)
        => await Send(new Message { Type = MsgType.MouseWheel, Delta = e.Delta });

    private async void Window_KeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        await Send(new Message { Type = MsgType.KeyEvent, Key = KeyInterop.VirtualKeyFromKey(e.Key == Key.System ? e.SystemKey : e.Key), Down = true });
    }

    private async void Window_KeyUp(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        await Send(new Message { Type = MsgType.KeyEvent, Key = KeyInterop.VirtualKeyFromKey(e.Key == Key.System ? e.SystemKey : e.Key), Down = false });
    }

    /// <summary>
    /// Convert a point inside the Image control to a 0..1 coordinate over the
    /// actual displayed picture (accounting for Uniform letterboxing).
    /// Returns (-1,-1) when the point is outside the picture.
    /// </summary>
    private (double, double) Norm(Point p)
    {
        if (Screen.Source is not BitmapSource src) return (-1, -1);
        double cw = Screen.ActualWidth, ch = Screen.ActualHeight;
        double iw = src.PixelWidth, ih = src.PixelHeight;
        if (cw <= 0 || ch <= 0 || iw <= 0 || ih <= 0) return (-1, -1);

        double scale = Math.Min(cw / iw, ch / ih);
        double dw = iw * scale, dh = ih * scale;
        double ox = (cw - dw) / 2, oy = (ch - dh) / 2;

        double x = (p.X - ox) / dw;
        double y = (p.Y - oy) / dh;
        if (x is < 0 or > 1 || y is < 0 or > 1) return (-1, -1);
        return (x, y);
    }

    private static string ButtonName(MouseButton b) => b switch
    {
        MouseButton.Right => "right",
        MouseButton.Middle => "middle",
        _ => "left"
    };

    private async System.Threading.Tasks.Task Send(Message m)
    {
        if (_relay != null) try { await _relay.SendAsync(m); } catch { }
    }

    private void ShowStatus(string text) { StatusText.Text = text; Overlay.Visibility = Visibility.Visible; }
    private void HideOverlay() => Overlay.Visibility = Visibility.Collapsed;
}
