using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CalendarBar;

public sealed class MailWindowManager
{
    public static MailWindowManager Shared { get; } = new();

    private const string WidthKey = "mailMessageWindow.width";
    private const string HeightKey = "mailMessageWindow.height";
    private static readonly PopoverSize DefaultSize = new(640, 640);
    private static readonly PopoverSize MinSize = new(460, 400);

    private readonly Dictionary<string, Window> _windows = [];

    public void OpenThread(string id, MailFolderKind folder, string title)
    {
        if (_windows.TryGetValue(id, out var existing))
        {
            existing.Activate();
            return;
        }
        var window = new Window
        {
            Title = title,
            Width = StoredSize.Width,
            Height = StoredSize.Height,
            MinWidth = MinSize.Width,
            MinHeight = MinSize.Height,
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI")
        };
        var host = new ContentControl();
        void Refresh()
        {
            var thread = MailSyncService.Shared.Thread(id, folder);
            if (thread is null)
            {
                host.Content = new TextBlock
                {
                    Text = "Письмо больше недоступно",
                    Foreground = Brushes.Gray,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                return;
            }
            var detail = new MailDetailPanel();
            detail.Show(thread, folder);
            detail.CloseRequested += () => window.Close();
            host.Content = detail;
        }
        Refresh();
        MailSyncService.Shared.PropertyChanged += (_, _) => window.Dispatcher.Invoke(Refresh);
        window.Content = host;
        window.Closed += (_, _) =>
        {
            Save(window);
            _windows.Remove(id);
        };
        window.SizeChanged += (_, _) => Save(window);
        var offset = _windows.Count * 24;
        window.Left = SystemParameters.WorkArea.Left + 80 + offset;
        window.Top = SystemParameters.WorkArea.Top + 80 + offset;
        _windows[id] = window;
        window.Show();
    }

    public void CloseAll()
    {
        foreach (var window in _windows.Values.ToList()) window.Close();
        _windows.Clear();
    }

    private PopoverSize StoredSize
    {
        get
        {
            var width = AppData.GetDouble(WidthKey);
            var height = AppData.GetDouble(HeightKey);
            if (width < MinSize.Width || height < MinSize.Height) return DefaultSize;
            return new PopoverSize(width, height);
        }
    }

    private static void Save(Window window)
    {
        if (window.ActualWidth >= MinSize.Width && window.ActualHeight >= MinSize.Height)
        {
            AppData.SetDouble(WidthKey, window.ActualWidth);
            AppData.SetDouble(HeightKey, window.ActualHeight);
        }
    }
}
