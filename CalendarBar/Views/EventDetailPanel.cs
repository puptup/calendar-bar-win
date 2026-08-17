using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace CalendarBar;

public sealed class EventDetailPanel : UserControl
{
    public event Action? CloseRequested;

    private CalendarEvent? _event;
    private readonly TextBlock _title = new() { FontWeight = FontWeights.SemiBold, FontSize = 15, TextWrapping = TextWrapping.Wrap };
    private readonly WrapPanel _badges = new() { Margin = new Thickness(0, 0, 0, 8) };
    private readonly StackPanel _actions = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
    private readonly TextBlock _hint = new() { FontSize = 10, Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _error = new() { FontSize = 10, Foreground = Brushes.IndianRed, TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _body = new();
    private bool _busy;

    public EventDetailPanel()
    {
        var close = new Button { Content = "✕", Style = (Style)Application.Current.FindResource("GhostButton"), Width = 24, Height = 24 };
        close.Click += (_, _) => CloseRequested?.Invoke();
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        DockPanel.SetDock(close, Dock.Right);
        header.Children.Add(close);
        header.Children.Add(_title);

        foreach (var (label, action) in new (string, MeetingAction)[] { ("Принять", MeetingAction.Accept), ("Под вопросом", MeetingAction.Tentative), ("Отклонить", MeetingAction.Decline) })
        {
            var captured = action;
            var b = new Button { Content = label, Style = (Style)Application.Current.FindResource("ChipButton"), Margin = new Thickness(0, 0, 6, 0) };
            b.Click += async (_, _) => await PerformResponse(captured);
            _actions.Children.Add(b);
        }
        var del = new Button { Content = "Удалить", Style = (Style)Application.Current.FindResource("ChipButton"), Foreground = Brushes.IndianRed };
        del.Click += async (_, _) => await PerformDelete();
        _actions.Children.Add(del);

        var content = new StackPanel { Margin = new Thickness(16) };
        content.Children.Add(header);
        content.Children.Add(_badges);
        content.Children.Add(_actions);
        content.Children.Add(_hint);
        content.Children.Add(_error);
        content.Children.Add(_body);
        Content = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    public void Show(CalendarEvent eventItem)
    {
        _event = eventItem;
        _error.Text = "";
        _title.Text = eventItem.Subject;
        _badges.Children.Clear();
        if (eventItem.IsCancelled) _badges.Children.Add(Badge("Отменено", Brushes.IndianRed));
        if (eventItem.IsRecurring) _badges.Children.Add(Badge("Повторяется", Brushes.SteelBlue));
        if (eventItem.ResponseStatus.IsHighlighted() || eventItem.ResponseStatus == MeetingResponseStatus.Declined)
            _badges.Children.Add(Badge(eventItem.ResponseStatus.DisplayName(), ResponseBrush(eventItem.ResponseStatus)));

        var disabled = _busy || eventItem.ResponseStatus == MeetingResponseStatus.Organizer || eventItem.IsCancelled;
        foreach (Button b in _actions.Children) b.IsEnabled = !disabled;
        _hint.Text = eventItem.ResponseStatus == MeetingResponseStatus.Organizer ? "Вы организатор этой встречи"
            : eventItem.IsCancelled ? "Встреча отменена организатором" : "";

        _body.Children.Clear();
        _body.Children.Add(Row("Время", TimeText(eventItem)));
        if (!string.IsNullOrEmpty(eventItem.SourceTimeZone))
            _body.Children.Add(Row("Часовой пояс события", eventItem.SourceTimeZone!));
        if (!string.IsNullOrEmpty(eventItem.Location))
            _body.Children.Add(RichRow("Место", eventItem.Location!));
        if (!string.IsNullOrEmpty(eventItem.Organizer))
            _body.Children.Add(Row("Организатор", eventItem.Organizer!));
        if (eventItem.Attendees.Count > 0)
        {
            _body.Children.Add(Label("Участники"));
            foreach (var a in eventItem.Attendees)
            {
                _body.Children.Add(new TextBlock { Text = a.DisplayName, Margin = new Thickness(0, 4, 0, 0) });
                if (!string.IsNullOrEmpty(a.Email) && a.Email != a.Name)
                    _body.Children.Add(new TextBlock { Text = a.Email, FontSize = 11, Foreground = Brushes.Gray });
                _body.Children.Add(new TextBlock { Text = a.RoleLabel, FontSize = 10, Foreground = Brushes.Gray });
            }
        }
        if (!string.IsNullOrEmpty(eventItem.Body))
            _body.Children.Add(RichRow("Описание", eventItem.Body!));
    }

    private async Task PerformResponse(MeetingAction action)
    {
        if (_event is null) return;
        _busy = true; _error.Text = "";
        try { await CalendarSyncService.Shared.Respond(_event, action); }
        catch (Exception ex) { _error.Text = ex.Message; }
        finally { _busy = false; }
    }

    private async Task PerformDelete()
    {
        if (_event is null) return;
        if (MessageBox.Show("Удалить встречу из календаря?", "CalendarBar", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
            return;
        _busy = true; _error.Text = "";
        try
        {
            await CalendarSyncService.Shared.Delete(_event);
            CloseRequested?.Invoke();
        }
        catch (Exception ex) { _error.Text = ex.Message; }
        finally { _busy = false; }
    }

    private static string TimeText(CalendarEvent e)
    {
        var ru = new CultureInfo("ru-RU");
        if (e.IsAllDay) return $"Весь день · {e.StartDate.ToString("d MMMM yyyy", ru)}";
        return $"{e.StartDate.ToString("d MMM yyyy, HH:mm", ru)} – {e.EndDate.ToString("HH:mm", ru)}";
    }

    private static Brush ResponseBrush(MeetingResponseStatus s) => s switch
    {
        MeetingResponseStatus.Pending => Brushes.Orange,
        MeetingResponseStatus.Tentative => Brushes.Goldenrod,
        MeetingResponseStatus.Declined => Brushes.IndianRed,
        _ => Brushes.Gray
    };

    private static Border Badge(string text, Brush color) => new()
    {
        Background = new SolidColorBrush(((SolidColorBrush)color).Color) { Opacity = 0.12 },
        CornerRadius = new CornerRadius(10),
        Padding = new Thickness(8, 4, 8, 4),
        Margin = new Thickness(0, 0, 6, 6),
        Child = new TextBlock { Text = text, FontSize = 11, FontWeight = FontWeights.Medium, Foreground = color }
    };

    private static UIElement Label(string title) => new TextBlock
    {
        Text = title, FontWeight = FontWeights.SemiBold, FontSize = 12, Foreground = Brushes.Gray, Margin = new Thickness(0, 12, 0, 4)
    };

    private static UIElement Row(string title, string value)
    {
        var p = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        p.Children.Add(Label(title));
        p.Children.Add(new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap });
        return p;
    }

    private static UIElement RichRow(string title, string value)
    {
        var p = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        p.Children.Add(Label(title));
        p.Children.Add(new RichTextBox
        {
            IsReadOnly = true,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Document = Linkify.Document(value)
        });
        return p;
    }
}
