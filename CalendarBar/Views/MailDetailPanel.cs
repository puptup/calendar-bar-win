using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CalendarBar;

public sealed class MailDetailPanel : UserControl
{
    public event Action? CloseRequested;

    private MailThread? _thread;
    private MailFolderKind _folder;
    private readonly TextBlock _title = new() { FontWeight = FontWeights.SemiBold, FontSize = 15, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _unread = new() { FontSize = 11, Foreground = Brushes.Gray };
    private readonly StackPanel _cards = new();
    private readonly HashSet<string> _expanded = [];

    public MailDetailPanel()
    {
        var close = new Button { Content = "✕", Style = (Style)Application.Current.FindResource("GhostButton"), Width = 24, Height = 24, ToolTip = "Закрыть письмо" };
        close.Click += (_, _) => CloseRequested?.Invoke();
        var header = new DockPanel { Margin = new Thickness(16) };
        var titles = new StackPanel();
        titles.Children.Add(_title);
        titles.Children.Add(_unread);
        DockPanel.SetDock(close, Dock.Right);
        header.Children.Add(close);
        header.Children.Add(titles);

        var root = new DockPanel { LastChildFill = true };
        var line = new System.Windows.Shapes.Rectangle { Height = 1, Fill = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)) };
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(line, Dock.Top);
        root.Children.Add(header);
        root.Children.Add(line);
        root.Children.Add(new ScrollViewer { Content = _cards, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(16) });
        Content = root;
        MailSyncService.Shared.PropertyChanged += (_, _) =>
        {
            if (_thread is null) return;
            Dispatcher.Invoke(() =>
            {
                var updated = MailSyncService.Shared.Thread(_thread.Id, _folder);
                if (updated is not null) Show(updated, _folder);
            });
        };
    }

    public void Show(MailThread thread, MailFolderKind folder)
    {
        _thread = thread;
        _folder = folder;
        _title.Text = thread.Subject;
        _unread.Text = thread.UnreadCount > 0 ? $"{thread.UnreadCount} непрочит." : "";
        if (_expanded.Count == 0 && thread.LatestMessage is { } latest)
            _expanded.Add(latest.Id);
        _cards.Children.Clear();
        foreach (var message in thread.Messages)
            _cards.Children.Add(MessageCard(message));
    }

    private UIElement MessageCard(MailMessage message)
    {
        var expanded = _expanded.Contains(message.Id);
        var header = new Button { Style = (Style)Application.Current.FindResource("GhostButton"), HorizontalContentAlignment = HorizontalAlignment.Stretch };
        var chevron = new TextBlock { Text = expanded ? "▾" : "▸", Foreground = Brushes.Gray, Width = 14 };
        var from = new TextBlock
        {
            Text = message.From?.DisplayName ?? "Без отправителя",
            FontWeight = message.IsRead ? FontWeights.SemiBold : FontWeights.Bold,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var date = new TextBlock { Text = message.ReceivedText, FontSize = 10, Foreground = Brushes.Gray };
        var top = new DockPanel();
        DockPanel.SetDock(date, Dock.Right);
        DockPanel.SetDock(chevron, Dock.Left);
        top.Children.Add(date);
        top.Children.Add(chevron);
        top.Children.Add(from);
        var headStack = new StackPanel();
        headStack.Children.Add(top);
        if (!string.IsNullOrEmpty(message.From?.Email))
            headStack.Children.Add(new TextBlock { Text = message.From!.Email, FontSize = 10, Foreground = Brushes.Gray });
        if (!expanded && !string.IsNullOrEmpty(message.PreviewText))
            headStack.Children.Add(new TextBlock { Text = message.PreviewText, FontSize = 11, Foreground = Brushes.Gray, TextTrimming = TextTrimming.CharacterEllipsis });
        header.Content = headStack;
        header.Click += (_, _) =>
        {
            if (!_expanded.Add(message.Id)) _expanded.Remove(message.Id);
            else MailSyncService.Shared.SelectedMessageId = message.Id;
            if (_thread is not null) Show(_thread, _folder);
        };

        var card = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        card.Children.Add(header);
        if (expanded)
        {
            card.Children.Add(new MailBodyHost(message, _folder) { Margin = new Thickness(0, 8, 0, 8) });
            var attachments = VisibleAttachments(message);
            if (attachments.Count > 0)
            {
                card.Children.Add(new TextBlock { Text = "Вложения", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Brushes.Gray, Margin = new Thickness(0, 4, 0, 4) });
                foreach (var attachment in attachments)
                {
                    var captured = attachment;
                    var row = new Button { Style = (Style)Application.Current.FindResource("GhostButton"), HorizontalContentAlignment = HorizontalAlignment.Stretch, ToolTip = "Скачать вложение" };
                    row.Content = new DockPanel
                    {
                        Children =
                        {
                            new TextBlock { Text = attachment.DisplayName, TextTrimming = TextTrimming.CharacterEllipsis },
                            new TextBlock { Text = attachment.SizeText, Foreground = Brushes.Gray, FontSize = 11 }
                        }
                    };
                    DockPanel.SetDock(((DockPanel)row.Content).Children[1], Dock.Right);
                    row.Click += async (_, _) => await MailSyncService.Shared.Download(captured);
                    card.Children.Add(row);
                }
            }
            var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            if (_folder != MailFolderKind.Drafts)
            {
                var read = Chip(message.IsRead ? "Непрочитано" : "Прочитано");
                read.Click += async (_, _) => await MailSyncService.Shared.SetRead(message, !message.IsRead);
                bar.Children.Add(read);
            }
            var reply = Chip("Ответить");
            reply.Click += (_, _) => { MailSyncService.Shared.SelectedMessageId = message.Id; MailComposeWindow.Show(MailComposeMode.Reply, message); };
            var replyAll = Chip("Всем");
            replyAll.Click += (_, _) => { MailSyncService.Shared.SelectedMessageId = message.Id; MailComposeWindow.Show(MailComposeMode.ReplyAll, message); };
            var forward = Chip("Переслать");
            forward.Click += (_, _) => { MailSyncService.Shared.SelectedMessageId = message.Id; MailComposeWindow.Show(MailComposeMode.Forward, message); };
            bar.Children.Add(reply);
            bar.Children.Add(replyAll);
            bar.Children.Add(forward);
            card.Children.Add(bar);
        }

        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(MailSyncService.Shared.SelectedMessageId == message.Id ? (byte)28 : (byte)12, 0, 0, 0)),
            CornerRadius = new CornerRadius(10),
            BorderBrush = new SolidColorBrush(Color.FromArgb(20, 0, 0, 0)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 10),
            Child = card
        };
    }

    private static List<MailAttachment> VisibleAttachments(MailMessage message)
    {
        if (message.Body?.Type != MailBodyType.Html) return message.Attachments;
        return message.Attachments.Where(a => !(a.IsInline && a.ContentId is not null)).ToList();
    }

    private static Button Chip(string text) => new()
    {
        Content = text,
        Style = (Style)Application.Current.FindResource("ChipButton"),
        Margin = new Thickness(0, 0, 6, 0),
        FontSize = 11
    };
}
