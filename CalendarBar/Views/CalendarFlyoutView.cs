using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace CalendarBar;

public sealed class CalendarFlyoutView : UserControl
{
    private readonly ContentControl _body = new();
    private readonly TextBlock _email = new() { FontWeight = FontWeights.SemiBold, FontSize = 15, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _status = new() { FontSize = 11 };
    private readonly TextBlock _notifyHint = new() { FontSize = 10, Foreground = Brushes.Orange, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _dayTitle = new() { FontWeight = FontWeights.SemiBold, FontSize = 12, MinWidth = 56, TextAlignment = TextAlignment.Center };
    private readonly Button _todayButton;
    private readonly TextBlock _footerStatus = new() { FontSize = 11, Foreground = Brushes.Gray, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly DayTimelineControl _timeline = new();
    private readonly EventDetailPanel _detail = new();
    private readonly Grid _split = new();
    private DateTime _selectedDay = DateTime.Today;
    private string? _selectedEventId;

    public CalendarFlyoutView()
    {
        _todayButton = Chip("Сегодня");
        _todayButton.Click += (_, _) => GoToToday();
        _todayButton.Visibility = Visibility.Collapsed;

        var prev = IconButton("‹");
        prev.Click += (_, _) => GoToPreviousDay();
        var next = IconButton("›");
        next.Click += (_, _) => GoToNextDay();
        var refresh = IconButton("↻");
        refresh.ToolTip = "Обновить";
        refresh.Click += async (_, _) => await CalendarSyncService.Shared.SyncNow();

        var dateNav = new StackPanel { VerticalAlignment = VerticalAlignment.Top };
        var dayRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        dayRow.Children.Add(prev);
        dayRow.Children.Add(_dayTitle);
        dayRow.Children.Add(next);
        dateNav.Children.Add(dayRow);
        dateNav.Children.Add(_todayButton);

        var toolbar = new DockPanel { Margin = new Thickness(16, 10, 16, 10) };
        var titles = new StackPanel();
        titles.Children.Add(_email);
        titles.Children.Add(_status);
        titles.Children.Add(_notifyHint);
        DockPanel.SetDock(refresh, Dock.Right);
        DockPanel.SetDock(dateNav, Dock.Right);
        toolbar.Children.Add(refresh);
        toolbar.Children.Add(dateNav);
        toolbar.Children.Add(titles);

        _split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _split.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0) });
        Grid.SetColumn(_timeline, 0);
        var divider = new Rectangle { Width = 1, Fill = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)) };
        Grid.SetColumn(divider, 1);
        Grid.SetColumn(_detail, 2);
        _split.Children.Add(_timeline);
        _split.Children.Add(divider);
        _split.Children.Add(_detail);
        _detail.CloseRequested += () => SelectEvent(null);
        _timeline.EventSelected += SelectEvent;

        var gear = IconButton("⚙");
        gear.ToolTip = "Настройки";
        gear.Click += (_, e) => ShowSettings(gear);

        var footer = new DockPanel { Margin = new Thickness(16, 8, 16, 8) };
        DockPanel.SetDock(gear, Dock.Left);
        footer.Children.Add(gear);
        footer.Children.Add(_footerStatus);

        var root = new DockPanel { LastChildFill = true };
        var topLine = new Rectangle { Height = 1, Fill = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)) };
        var bottomLine = new Rectangle { Height = 1, Fill = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)) };
        DockPanel.SetDock(toolbar, Dock.Top);
        DockPanel.SetDock(topLine, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        DockPanel.SetDock(bottomLine, Dock.Bottom);
        root.Children.Add(toolbar);
        root.Children.Add(topLine);
        root.Children.Add(footer);
        root.Children.Add(bottomLine);
        root.Children.Add(_body);

        Content = root;
        Bind();
        Loaded += (_, _) => Refresh();
        var dayTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        dayTimer.Tick += (_, _) =>
        {
            if (_selectedDay < DateTime.Today) GoToToday();
        };
        dayTimer.Start();
    }

    private void Bind()
    {
        SettingsStore.Shared.PropertyChanged += (_, _) => Dispatcher.Invoke(Refresh);
        CalendarSyncService.Shared.PropertyChanged += (_, _) => Dispatcher.Invoke(Refresh);
        NotificationService.Shared.PropertyChanged += (_, _) => Dispatcher.Invoke(RefreshNotify);
    }

    private void Refresh()
    {
        var store = SettingsStore.Shared;
        if (!store.IsLoggedIn || store.Password is null)
        {
            _body.Content = new AuthView();
            TrayManager.Shared.SetCalendarShowingDetail(false);
            return;
        }
        _body.Content = _split;
        _email.Text = string.IsNullOrEmpty(store.Account.Email) ? "Календарь" : store.Account.Email;
        _status.Text = CalendarSyncService.Shared.SyncState.StatusText;
        _status.Foreground = CalendarSyncService.Shared.SyncState.IsError ? Brushes.IndianRed : Brushes.Gray;
        RefreshNotify();
        UpdateDayTitle();
        var events = CalendarSyncService.Shared.Events.Where(e => e.Occurs(_selectedDay)).ToList();
        _timeline.SetDay(_selectedDay, events, _selectedEventId);
        _footerStatus.Text = CalendarSyncService.Shared.FooterStatusText ?? "";
        ApplyDetail();
    }

    private void RefreshNotify()
    {
        _notifyHint.Text = NotificationService.Shared.AuthorizationState switch
        {
            NotificationAuthorizationState.Denied => "Уведомления отключены в Windows",
            NotificationAuthorizationState.NotDetermined => "Разрешите уведомления для CalendarBar",
            _ => ""
        };
    }

    private void SelectEvent(string? id)
    {
        _selectedEventId = _selectedEventId == id ? null : id;
        ApplyDetail();
        _timeline.SetSelected(_selectedEventId);
    }

    private void ApplyDetail()
    {
        var eventItem = _selectedEventId is null ? null
            : CalendarSyncService.Shared.Events.FirstOrDefault(e => e.Id == _selectedEventId && e.Occurs(_selectedDay));
        if (eventItem is null)
        {
            _selectedEventId = null;
            _split.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            _split.ColumnDefinitions[2].Width = new GridLength(0);
            _detail.Visibility = Visibility.Collapsed;
            TrayManager.Shared.SetCalendarShowingDetail(false);
            return;
        }
        _split.ColumnDefinitions[0].Width = new GridLength(PopoverMetrics.TimelineWidth);
        _split.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
        _detail.Visibility = Visibility.Visible;
        _detail.Show(eventItem);
        TrayManager.Shared.SetCalendarShowingDetail(true);
    }

    private void UpdateDayTitle()
    {
        var ru = new CultureInfo("ru-RU");
        _dayTitle.Text = _selectedDay.Date == DateTime.Today ? "Сегодня"
            : _selectedDay.Date == DateTime.Today.AddDays(1) ? "Завтра"
            : _selectedDay.ToString("d MMM", ru);
        _todayButton.Visibility = _selectedDay.Date == DateTime.Today ? Visibility.Collapsed : Visibility.Visible;
    }

    private void GoToPreviousDay()
    {
        if (_selectedDay.Date <= DateTime.Today) return;
        _selectedDay = _selectedDay.AddDays(-1).Date;
        _selectedEventId = null;
        Refresh();
    }

    private void GoToNextDay()
    {
        _selectedDay = _selectedDay.AddDays(1).Date;
        _selectedEventId = null;
        Refresh();
    }

    private void GoToToday()
    {
        _selectedDay = DateTime.Today;
        Refresh();
    }

    private void ShowSettings(FrameworkElement anchor)
    {
        if (Window.GetWindow(this) is FlyoutWindow flyout) flyout.KeepOpenBriefly();
        var menu = new ContextMenu();
        var notify = new MenuItem { Header = "Уведомление за" };
        foreach (var mins in new[] { 5, 10, 15, 30, 60 })
        {
            var item = new MenuItem
            {
                Header = mins == 60 ? "1 час" : $"{mins} мин",
                IsCheckable = true,
                IsChecked = SettingsStore.Shared.NotifyMinutesBefore == mins
            };
            var captured = mins;
            item.Click += (_, _) => SettingsStore.Shared.NotifyMinutesBefore = captured;
            notify.Items.Add(item);
        }
        menu.Items.Add(notify);
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Разрешить уведомления…", () =>
        {
            _ = NotifyPermission();
        }));
        menu.Items.Add(Item("Настройки уведомлений Windows…", () => NotificationService.Shared.OpenSystemNotificationSettings()));
        menu.Items.Add(new Separator());
        var launch = new MenuItem { Header = "Запускать при входе", IsCheckable = true, IsChecked = SettingsStore.Shared.LaunchAtLogin };
        launch.Click += (_, _) => SettingsStore.Shared.SetLaunchAtLogin(launch.IsChecked);
        menu.Items.Add(launch);
        menu.Items.Add(new Separator());
        var interval = new MenuItem { Header = "Интервал синхронизации" };
        foreach (var mins in new[] { 5, 15, 30 })
        {
            var item = new MenuItem { Header = $"{mins} минут", IsCheckable = true, IsChecked = SettingsStore.Shared.SyncIntervalMinutes == mins };
            var captured = mins;
            item.Click += (_, _) => SettingsStore.Shared.SyncIntervalMinutes = captured;
            interval.Items.Add(item);
        }
        menu.Items.Add(interval);
        menu.Items.Add(new Separator());
        var mail = new MenuItem { Header = "Почта", IsCheckable = true, IsChecked = SettingsStore.Shared.MailEnabled };
        mail.Click += (_, _) => SettingsStore.Shared.MailEnabled = mail.IsChecked;
        menu.Items.Add(mail);
        if (SettingsStore.Shared.MailEnabled)
        {
            var html = new MenuItem { Header = "Форматирование писем (HTML)", IsCheckable = true, IsChecked = SettingsStore.Shared.MailHtmlRenderingEnabled };
            html.Click += (_, _) => SettingsStore.Shared.MailHtmlRenderingEnabled = html.IsChecked;
            menu.Items.Add(html);
            if (SettingsStore.Shared.MailHtmlRenderingEnabled)
            {
                var images = new MenuItem { Header = "Картинки в письмах", IsCheckable = true, IsChecked = SettingsStore.Shared.MailImagesEnabled };
                images.Click += (_, _) => SettingsStore.Shared.MailImagesEnabled = images.IsChecked;
                menu.Items.Add(images);
            }
        }
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("О приложении", () =>
        {
            new AboutWindow { Owner = Window.GetWindow(this) }.ShowDialog();
        }));
        menu.Items.Add(new Separator());
        var logout = new MenuItem { Header = "Выйти из аккаунта" };
        logout.Click += (_, _) => SettingsStore.Shared.Logout();
        menu.Items.Add(logout);
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Закрыть CalendarBar", App.Quit));
        menu.PlacementTarget = anchor;
        menu.IsOpen = true;
    }

    private static async Task NotifyPermission()
    {
        await NotificationService.Shared.RequestAuthorization();
        await NotificationService.Shared.RescheduleFromCurrentEvents();
        if (NotificationService.Shared.AuthorizationState == NotificationAuthorizationState.Denied)
            NotificationService.Shared.OpenSystemNotificationSettings();
    }

    private static MenuItem Item(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    private static Button IconButton(string content) => new()
    {
        Content = content,
        Style = (Style)Application.Current.FindResource("GhostButton"),
        Width = 28,
        Height = 28,
        FontSize = 14,
        FontWeight = FontWeights.SemiBold
    };

    private static Button Chip(string content)
    {
        var b = new Button { Content = content, Style = (Style)Application.Current.FindResource("ChipButton"), Margin = new Thickness(0, 2, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
        return b;
    }
}
