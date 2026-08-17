using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CalendarBar;

public enum MailComposeMode { NewMessage, Reply, ReplyAll, Forward }

public static class MailComposeModeText
{
    public static string Title(this MailComposeMode mode) => mode switch
    {
        MailComposeMode.NewMessage => "Новое письмо",
        MailComposeMode.Reply => "Ответить",
        MailComposeMode.ReplyAll => "Ответить всем",
        MailComposeMode.Forward => "Переслать",
        _ => ""
    };
}

public sealed class MailComposeWindow : Window
{
    public static void Show(MailComposeMode mode, MailMessage? message)
    {
        var w = new MailComposeWindow(mode, message);
        w.Show();
        w.Activate();
    }

    public MailComposeWindow(MailComposeMode mode, MailMessage? message)
    {
        Title = mode.Title();
        Width = 480;
        Height = 360;
        MinWidth = 420;
        MinHeight = 280;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Text, Segoe UI");

        var to = new TextBox();
        var cc = new TextBox();
        var subject = new TextBox();
        var body = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MinHeight = 140 };
        var error = new TextBlock { Foreground = System.Windows.Media.Brushes.IndianRed, FontSize = 11, TextWrapping = TextWrapping.Wrap };
        var send = new Button { Content = "Отправить", IsDefault = true, Padding = new Thickness(14, 6, 14, 6), Margin = new Thickness(8, 0, 0, 0) };
        var cancel = new Button { Content = "Отмена", IsCancel = true, Padding = new Thickness(14, 6, 14, 6) };
        cancel.Click += (_, _) => Close();

        if (message is not null)
        {
            switch (mode)
            {
                case MailComposeMode.Reply:
                case MailComposeMode.ReplyAll:
                    subject.Text = message.DisplaySubject;
                    break;
                case MailComposeMode.Forward:
                    subject.Text = message.DisplaySubject;
                    body.Text = $"\n\n--- Пересылаемое сообщение ---\n{message.DisplayBodyText}";
                    break;
            }
        }

        send.Click += async (_, _) =>
        {
            if ((mode is MailComposeMode.NewMessage or MailComposeMode.Forward) && string.IsNullOrWhiteSpace(to.Text))
            {
                error.Text = "Укажите получателя";
                return;
            }
            if (string.IsNullOrWhiteSpace(body.Text)) return;
            send.IsEnabled = false;
            send.Content = "Отправляем…";
            switch (mode)
            {
                case MailComposeMode.NewMessage:
                    await MailSyncService.Shared.Send(to.Text, cc.Text, subject.Text, body.Text);
                    break;
                case MailComposeMode.Reply when message is not null:
                    await MailSyncService.Shared.Reply(message, body.Text, false);
                    break;
                case MailComposeMode.ReplyAll when message is not null:
                    await MailSyncService.Shared.Reply(message, body.Text, true);
                    break;
                case MailComposeMode.Forward when message is not null:
                    await MailSyncService.Shared.Forward(message, to.Text, body.Text);
                    break;
            }
            if (MailSyncService.Shared.ActionError is null) Close();
            else
            {
                error.Text = MailSyncService.Shared.ActionError;
                send.IsEnabled = true;
                send.Content = "Отправить";
            }
        };

        var form = new StackPanel { Margin = new Thickness(18) };
        form.Children.Add(new TextBlock { Text = mode.Title(), FontWeight = FontWeights.SemiBold, FontSize = 16, Margin = new Thickness(0, 0, 0, 12) });
        if (mode is MailComposeMode.NewMessage or MailComposeMode.Forward)
            form.Children.Add(Labeled("Кому", to));
        if (mode == MailComposeMode.NewMessage)
        {
            form.Children.Add(Labeled("Копия", cc));
            form.Children.Add(Labeled("Тема", subject));
        }
        form.Children.Add(body);
        form.Children.Add(error);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(cancel);
        buttons.Children.Add(send);
        form.Children.Add(buttons);
        Content = form;
    }

    private static UIElement Labeled(string label, TextBox box)
    {
        var caption = new TextBlock
        {
            Text = label, Width = 52, Foreground = System.Windows.Media.Brushes.Gray,
            FontSize = 11, VerticalAlignment = VerticalAlignment.Center
        };
        var row = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        DockPanel.SetDock(caption, Dock.Left);
        row.Children.Add(caption);
        row.Children.Add(box);
        return row;
    }
}
