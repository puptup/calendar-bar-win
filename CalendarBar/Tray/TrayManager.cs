using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace CalendarBar;

public sealed class TrayManager : IDisposable
{
    public static TrayManager Shared { get; } = new();

    private NotifyIcon? _calendar;
    private NotifyIcon? _mail;
    private FlyoutWindow? _calendarFlyout;
    private FlyoutWindow? _mailFlyout;
    private bool _calendarShowingDetail;
    private bool _mailShowingDetail;

    public void InstallCalendar()
    {
        if (_calendar is not null)
        {
            UpdateCalendarIcon();
            return;
        }
        _calendar = new NotifyIcon
        {
            Icon = TrayIcons.Calendar(loggedIn: SettingsStore.Shared.IsLoggedIn, badge: null),
            Visible = true,
            Text = "CalendarBar"
        };
        _calendar.MouseClick += (_, e) =>
        {
            if (e.Button is MouseButtons.Left or MouseButtons.Right)
                ToggleCalendar();
        };
        UpdateCalendarIcon();
    }

    public void InstallMail()
    {
        if (_mail is not null)
        {
            UpdateMailIcon();
            return;
        }
        _mail = new NotifyIcon
        {
            Icon = TrayIcons.Mail(loggedIn: SettingsStore.Shared.IsLoggedIn, unread: 0),
            Visible = true,
            Text = "CalendarBar Почта"
        };
        _mail.MouseClick += (_, e) =>
        {
            if (e.Button is MouseButtons.Left or MouseButtons.Right)
                ToggleMail();
        };
        UpdateMailIcon();
    }

    public void UninstallMail()
    {
        _mailFlyout?.Close();
        _mailFlyout = null;
        if (_mail is not null)
        {
            _mail.Visible = false;
            _mail.Dispose();
            _mail = null;
        }
        _mailShowingDetail = false;
    }

    public void UpdateCalendarIcon()
    {
        if (_calendar is null) return;
        var title = CalendarSyncService.Shared.MenuBarLabelText;
        _calendar.Icon?.Dispose();
        _calendar.Icon = TrayIcons.Calendar(SettingsStore.Shared.IsLoggedIn, string.IsNullOrEmpty(title) ? null : CalendarSyncService.Shared.MenuBarCountText);
        _calendar.Text = string.IsNullOrEmpty(title) ? "CalendarBar" : $"CalendarBar — {title}";
    }

    public void UpdateCalendarTitle(string title)
    {
        if (_calendar is null) return;
        _calendar.Icon?.Dispose();
        _calendar.Icon = TrayIcons.Calendar(SettingsStore.Shared.IsLoggedIn, string.IsNullOrEmpty(title) ? null : CalendarSyncService.Shared.MenuBarCountText);
        _calendar.Text = string.IsNullOrEmpty(title) ? "CalendarBar" : $"CalendarBar — {title}";
    }

    public void UpdateMailIcon()
    {
        if (_mail is null) return;
        var unread = MailSyncService.Shared.UnreadCount;
        _mail.Icon?.Dispose();
        _mail.Icon = TrayIcons.Mail(SettingsStore.Shared.IsLoggedIn, unread);
        _mail.Text = unread > 0 ? $"CalendarBar Почта — {unread}" : "CalendarBar Почта";
    }

    public void UpdateMailTitle(string title)
    {
        UpdateMailIcon();
    }

    public void ToggleCalendar()
    {
        if (_calendarFlyout is { IsVisible: true })
        {
            _calendarFlyout.Hide();
            return;
        }
        ShowCalendar();
    }

    public void ShowCalendar()
    {
        _mailFlyout?.Hide();
        _calendarFlyout ??= new FlyoutWindow(new CalendarFlyoutView(), PopoverKind.Calendar, () => _calendarShowingDetail);
        _calendarFlyout.ShowNearTray();
        SettingsStore.Shared.RefreshLaunchAtLoginStatus();
    }

    public void ToggleMail()
    {
        if (_mailFlyout is { IsVisible: true })
        {
            _mailFlyout.Hide();
            return;
        }
        ShowMailPanel();
    }

    public void ShowMailPanel()
    {
        if (_mail is null) return;
        _calendarFlyout?.Hide();
        _mailFlyout ??= new FlyoutWindow(new MailFlyoutView(), PopoverKind.Mail, () => _mailShowingDetail);
        _mailFlyout.ShowNearTray();
        SettingsStore.Shared.RefreshLaunchAtLoginStatus();
    }

    public void SetCalendarShowingDetail(bool showing)
    {
        if (_calendarShowingDetail == showing) return;
        _calendarShowingDetail = showing;
        _calendarFlyout?.ApplyStoredSize(showing);
    }

    public void SetMailShowingDetail(bool showing)
    {
        if (_mailShowingDetail == showing) return;
        _mailShowingDetail = showing;
        _mailFlyout?.ApplyStoredSize(showing);
    }

    public void Dispose()
    {
        UninstallMail();
        _calendarFlyout?.Close();
        if (_calendar is not null)
        {
            _calendar.Visible = false;
            _calendar.Dispose();
            _calendar = null;
        }
    }
}

internal static class TrayIcons
{
    public static Icon Calendar(bool loggedIn, string? badge)
    {
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        var accent = Color.FromArgb(0, 120, 212);
        using var fill = new SolidBrush(accent);
        using var pen = new Pen(Color.White, 2);
        g.FillRoundedRectangle(fill, new Rectangle(4, 6, 24, 22), 4);
        g.DrawLine(pen, 10, 4, 10, 10);
        g.DrawLine(pen, 22, 4, 22, 10);
        using var white = new SolidBrush(Color.White);
        g.FillRectangle(white, 6, 14, 20, 12);
        using var font = new Font("Segoe UI", 9, FontStyle.Bold, GraphicsUnit.Pixel);
        using var dark = new SolidBrush(accent);
        var day = DateTime.Now.Day.ToString();
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(loggedIn ? day : "+", font, dark, new RectangleF(6, 13, 20, 14), sf);
        if (!string.IsNullOrEmpty(badge)) DrawBadge(g, badge);
        return CopyIcon(bmp);
    }

    public static Icon Mail(bool loggedIn, int unread)
    {
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        var accent = Color.FromArgb(0, 120, 212);
        using var fill = new SolidBrush(accent);
        using var pen = new Pen(Color.White, 2);
        g.FillRoundedRectangle(fill, new Rectangle(4, 8, 24, 16), 3);
        g.DrawLine(pen, 4, 10, 16, 18);
        g.DrawLine(pen, 28, 10, 16, 18);
        if (!loggedIn)
        {
            using var font = new Font("Segoe UI", 10, FontStyle.Bold, GraphicsUnit.Pixel);
            g.DrawString("+", font, Brushes.White, 20, 2);
        }
        if (unread > 0) DrawBadge(g, unread > 99 ? "99+" : unread.ToString());
        return CopyIcon(bmp);
    }

    private static Icon CopyIcon(Bitmap bmp)
    {
        var handle = bmp.GetHicon();
        try
        {
            using var tmp = Icon.FromHandle(handle);
            return (Icon)tmp.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(handle);
        }
    }

    private static void DrawBadge(Graphics g, string text)
    {
        using var red = new SolidBrush(Color.FromArgb(196, 43, 28));
        using var font = new Font("Segoe UI", 8, FontStyle.Bold, GraphicsUnit.Pixel);
        var size = g.MeasureString(text, font);
        var rect = new RectangleF(32 - size.Width - 2, 0, size.Width + 2, size.Height);
        g.FillEllipse(red, rect);
        g.DrawString(text, font, Brushes.White, rect, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
    }

    private static void FillRoundedRectangle(this Graphics g, Brush brush, Rectangle bounds, int radius)
    {
        using var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }
}
