using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;

namespace CalendarBar;

public sealed class FlyoutWindow : Window
{
    private readonly PopoverKind _kind;
    private readonly Func<bool> _showingDetail;
    private DateTime _keepOpenUntil;

    public FlyoutWindow(UIElement content, PopoverKind kind, Func<bool> showingDetail)
    {
        _kind = kind;
        _showingDetail = showingDetail;
        ShowInTaskbar = false;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.CanResize;
        Topmost = true;
        ShowActivated = true;
        SnapsToDevicePixels = true;
        Background = NativeMethods.IsDarkTheme()
            ? new SolidColorBrush(Color.FromArgb(230, 32, 32, 32))
            : new SolidColorBrush(Color.FromArgb(235, 249, 249, 249));
        Foreground = NativeMethods.IsDarkTheme() ? Brushes.White : Brushes.Black;
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        FontSize = 13;

        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 0,
            CornerRadius = new CornerRadius(12),
            GlassFrameThickness = new Thickness(-1),
            ResizeBorderThickness = new Thickness(6)
        });

        var size = PopoverSizeStore.Shared.Size(kind, showingDetail());
        Width = size.Width;
        Height = size.Height;
        var min = PopoverSizeStore.MinSize(kind, showingDetail());
        MinWidth = min.Width;
        MinHeight = min.Height;

        Content = new Border
        {
            CornerRadius = new CornerRadius(12),
            Child = content,
            Padding = new Thickness(0)
        };

        SourceInitialized += (_, _) => NativeMethods.ApplyWindows11Chrome(this);
        Deactivated += async (_, _) =>
        {
            await Task.Delay(80);
            if (DateTime.UtcNow < _keepOpenUntil) return;
            if (!IsActive && OwnedWindows.Count == 0) Hide();
        };
        SizeChanged += (_, _) =>
        {
            if (!IsVisible) return;
            PopoverSizeStore.Shared.Save(new PopoverSize(Width, Height), _kind, _showingDetail());
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Hide();
        };
    }

    public void KeepOpenBriefly() => _keepOpenUntil = DateTime.UtcNow.AddSeconds(8);

    public void ShowNearTray()
    {
        ApplyStoredSize(_showingDetail());
        var mouse = System.Windows.Forms.Control.MousePosition;
        var wa = SystemParameters.WorkArea;
        Left = Math.Clamp(mouse.X - Width / 2, wa.Left + 8, wa.Right - Width - 8);
        Top = Math.Clamp(mouse.Y - Height - 16, wa.Top + 8, wa.Bottom - Height - 8);
        if (mouse.Y < wa.Top + 80)
            Top = mouse.Y + 16;
        Show();
        Activate();
    }

    public void ApplyStoredSize(bool showingDetail)
    {
        var size = PopoverSizeStore.Shared.Size(_kind, showingDetail);
        var min = PopoverSizeStore.MinSize(_kind, showingDetail);
        MinWidth = min.Width;
        MinHeight = min.Height;
        Width = size.Width;
        Height = size.Height;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}
