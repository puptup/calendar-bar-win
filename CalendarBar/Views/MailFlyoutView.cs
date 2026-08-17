using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CalendarBar;

public sealed class MailFlyoutView : UserControl
{
    private readonly ContentControl _body = new();
    private readonly TextBlock _email = new() { FontWeight = FontWeights.SemiBold, FontSize = 15, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _status = new() { FontSize = 11 };
    private readonly StackPanel _tabs = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 0, 12, 8) };
    private readonly ListBox _list = new() { BorderThickness = new Thickness(0), Background = Brushes.Transparent };
    private readonly MailDetailPanel _detail = new();
    private readonly Grid _split = new();
    private readonly TextBlock _footer = new() { FontSize = 11, Foreground = Brushes.Gray };
    private readonly TextBlock _actionError = new() { FontSize = 10, Foreground = Brushes.IndianRed, TextTrimming = TextTrimming.CharacterEllipsis };
    private string? _selectedThreadId;

    public MailFlyoutView()
    {
        var compose = IconButton("✎");
        compose.ToolTip = "Новое письмо";
        compose.Click += (_, _) => MailComposeWindow.Show(MailComposeMode.NewMessage, null);
        var refresh = IconButton("↻");
        refresh.ToolTip = "Обновить";
        refresh.Click += async (_, _) => await MailSyncService.Shared.SyncNow();
        var markAll = IconButton("✉");
        markAll.ToolTip = "Прочитать всё";
        markAll.Click += async (_, _) => await MailSyncService.Shared.MarkAllRead();

        var toolbar = new DockPanel { Margin = new Thickness(16, 10, 16, 10) };
        var titles = new StackPanel();
        titles.Children.Add(_email);
        titles.Children.Add(_status);
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        actions.Children.Add(markAll);
        actions.Children.Add(compose);
        actions.Children.Add(refresh);
        DockPanel.SetDock(actions, Dock.Right);
        toolbar.Children.Add(actions);
        toolbar.Children.Add(titles);

        foreach (var folder in MailFolderKindText.All)
        {
            var captured = folder;
            var tab = new Button
            {
                Content = folder.Title(),
                Style = (Style)Application.Current.FindResource("ChipButton"),
                Margin = new Thickness(0, 0, 6, 0),
                Tag = folder
            };
            tab.Click += (_, _) => MailSyncService.Shared.SelectFolder(captured);
            _tabs.Children.Add(tab);
        }

        _list.ItemTemplate = ThreadTemplate();
        _list.SelectionChanged += (_, _) =>
        {
            if (_list.SelectedItem is MailThread thread)
            {
                _selectedThreadId = thread.Id;
                MailSyncService.Shared.SelectedMessageId = thread.LatestMessage?.Id;
                ApplyDetail();
            }
        };
        _list.MouseDoubleClick += (_, _) =>
        {
            if (_list.SelectedItem is MailThread thread)
                MailWindowManager.Shared.OpenThread(thread.Id, MailSyncService.Shared.SelectedFolder, thread.Subject);
        };

        _split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _split.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0) });
        Grid.SetColumn(_list, 0);
        var divider = new Rectangle { Width = 1, Fill = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)) };
        Grid.SetColumn(divider, 1);
        Grid.SetColumn(_detail, 2);
        _split.Children.Add(_list);
        _split.Children.Add(divider);
        _split.Children.Add(_detail);
        _detail.CloseRequested += () =>
        {
            _selectedThreadId = null;
            MailSyncService.Shared.SelectedMessageId = null;
            _list.SelectedItem = null;
            ApplyDetail();
        };

        var footer = new DockPanel { Margin = new Thickness(16, 8, 16, 8) };
        footer.Children.Add(_footer);
        DockPanel.SetDock(_actionError, Dock.Right);
        footer.Children.Add(_actionError);

        var root = new DockPanel { LastChildFill = true };
        var topLine = Hairline();
        var bottomLine = Hairline();
        DockPanel.SetDock(toolbar, Dock.Top);
        DockPanel.SetDock(_tabs, Dock.Top);
        DockPanel.SetDock(topLine, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        DockPanel.SetDock(bottomLine, Dock.Bottom);
        root.Children.Add(toolbar);
        root.Children.Add(_tabs);
        root.Children.Add(topLine);
        root.Children.Add(footer);
        root.Children.Add(bottomLine);
        root.Children.Add(_body);
        Content = root;

        MailSyncService.Shared.PropertyChanged += (_, _) => Dispatcher.Invoke(Refresh);
        SettingsStore.Shared.PropertyChanged += (_, _) => Dispatcher.Invoke(Refresh);
        Loaded += (_, _) => Refresh();
    }

    private void Refresh()
    {
        var store = SettingsStore.Shared;
        if (!store.IsLoggedIn || store.Password is null)
        {
            _body.Content = new AuthView();
            TrayManager.Shared.SetMailShowingDetail(false);
            return;
        }
        _body.Content = _split;
        _email.Text = string.IsNullOrEmpty(store.Account.Email) ? "Почта" : store.Account.Email;
        _status.Text = MailSyncService.Shared.SyncState.StatusText;
        _status.Foreground = MailSyncService.Shared.SyncState.IsError ? Brushes.IndianRed : Brushes.Gray;
        foreach (Button tab in _tabs.Children)
        {
            var selected = (MailFolderKind)tab.Tag == MailSyncService.Shared.SelectedFolder;
            tab.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
            tab.Opacity = selected ? 1 : 0.7;
        }
        var threads = MailSyncService.Shared.Threads;
        _list.ItemsSource = threads;
        if (_selectedThreadId is not null && threads.All(t => t.Id != _selectedThreadId))
        {
            _selectedThreadId = null;
            MailSyncService.Shared.SelectedMessageId = null;
        }
        if (MailSyncService.Shared.SelectedMessageId is { } mid)
        {
            var thread = threads.FirstOrDefault(t => t.Messages.Any(m => m.Id == mid));
            if (thread is not null) _selectedThreadId = thread.Id;
        }
        _footer.Text = MailSyncService.Shared.UnreadCount > 0
            ? $"Непрочитанных: {MailSyncService.Shared.UnreadCount}"
            : "Нет непрочитанных";
        _actionError.Text = MailSyncService.Shared.ActionError ?? "";
        ApplyDetail();
    }

    private void ApplyDetail()
    {
        var thread = _selectedThreadId is null ? null : MailSyncService.Shared.Threads.FirstOrDefault(t => t.Id == _selectedThreadId);
        if (thread is null)
        {
            _split.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            _split.ColumnDefinitions[2].Width = new GridLength(0);
            _detail.Visibility = Visibility.Collapsed;
            TrayManager.Shared.SetMailShowingDetail(false);
            return;
        }
        _split.ColumnDefinitions[0].Width = new GridLength(MailPopoverMetrics.ListWidth);
        _split.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
        _detail.Visibility = Visibility.Visible;
        _detail.Show(thread, MailSyncService.Shared.SelectedFolder);
        TrayManager.Shared.SetMailShowingDetail(true);
    }

    private static DataTemplate ThreadTemplate()
    {
        var template = new DataTemplate(typeof(MailThread));
        var factory = new FrameworkElementFactory(typeof(MailThreadRow));
        template.VisualTree = factory;
        return template;
    }

    private static Button IconButton(string content) => new()
    {
        Content = content,
        Style = (Style)Application.Current.FindResource("GhostButton"),
        Width = 28,
        Height = 28,
        FontSize = 14
    };

    private static Rectangle Hairline() => new() { Height = 1, Fill = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)) };
}

public sealed class MailThreadRow : UserControl
{
    public MailThreadRow()
    {
        var subject = new TextBlock { FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis };
        var date = new TextBlock { FontSize = 10, Foreground = Brushes.Gray };
        var from = new TextBlock { FontSize = 11, Foreground = Brushes.Gray, TextTrimming = TextTrimming.CharacterEllipsis };
        var preview = new TextBlock { FontSize = 10, Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap, MaxHeight = 32 };
        var dot = new Ellipse { Width = 7, Height = 7, Fill = new SolidColorBrush(NativeMethods.AccentColor()), Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };

        var top = new DockPanel();
        DockPanel.SetDock(date, Dock.Right);
        top.Children.Add(date);
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
        titleRow.Children.Add(dot);
        titleRow.Children.Add(subject);
        top.Children.Add(titleRow);

        var stack = new StackPanel { Margin = new Thickness(16, 10, 16, 10) };
        stack.Children.Add(top);
        stack.Children.Add(from);
        stack.Children.Add(preview);
        Content = stack;

        DataContextChanged += (_, _) =>
        {
            if (DataContext is not MailThread thread) return;
            subject.Text = thread.Subject;
            subject.FontWeight = thread.UnreadCount > 0 ? FontWeights.Bold : FontWeights.SemiBold;
            date.Text = thread.LatestMessage?.ReceivedText ?? "";
            from.Text = thread.LatestMessage?.From?.DisplayName ?? "Без отправителя";
            preview.Text = thread.LatestMessage?.PreviewText ?? "";
            dot.Visibility = thread.UnreadCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        };
    }
}
