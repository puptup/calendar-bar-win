using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CalendarBar;

public sealed class AuthView : UserControl
{
    private readonly TextBox _email = new() { Padding = new Thickness(6, 4, 6, 4) };
    private readonly TextBox _server = new() { Padding = new Thickness(6, 4, 6, 4) };
    private readonly TextBox _domain = new() { Padding = new Thickness(6, 4, 6, 4) };
    private readonly TextBox _username = new() { Padding = new Thickness(6, 4, 6, 4) };
    private readonly PasswordBox _password = new() { Padding = new Thickness(6, 4, 6, 4) };
    private readonly TextBlock _error = new() { Foreground = Brushes.IndianRed, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
    private readonly Button _signIn;

    public AuthView()
    {
        var store = SettingsStore.Shared;
        _email.Text = store.Account.Email;
        _server.Text = store.Account.Server;
        _domain.Text = store.Account.Domain;
        _username.Text = store.Account.Username;
        _password.Password = store.Password ?? "";

        var header = new StackPanel
        {
            Margin = new Thickness(16, 12, 16, 8),
            Children =
            {
                new TextBlock { Text = "Учётная запись Exchange", FontWeight = FontWeights.SemiBold, FontSize = 16 },
                new TextBlock { Text = "Настройка как в Календаре iPhone", FontSize = 11, Foreground = Brushes.Gray, Margin = new Thickness(0, 4, 0, 0) }
            }
        };

        var fields = new StackPanel { Margin = new Thickness(16, 8, 16, 8) };
        fields.Children.Add(Labeled("Email", _email));
        fields.Children.Add(Labeled("Сервер", _server));
        fields.Children.Add(Labeled("Домен", _domain));
        fields.Children.Add(Labeled("Имя пользователя", _username));
        fields.Children.Add(LabeledPassword());
        fields.Children.Add(_error);

        var cancel = new Button { Content = "Отмена", Padding = new Thickness(14, 6, 14, 6) };
        cancel.Click += (_, _) => { _password.Password = ""; _error.Text = ""; };

        _signIn = new Button { Content = "Войти", IsDefault = true, Padding = new Thickness(14, 6, 14, 6), Margin = new Thickness(8, 0, 0, 0) };
        _signIn.Click += async (_, _) => await SignIn();

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(16)
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(_signIn);

        var quit = new Button
        {
            Content = "Закрыть CalendarBar",
            Style = (Style)Application.Current.FindResource("GhostButton"),
            FontSize = 11,
            Foreground = Brushes.Gray,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        quit.Click += (_, _) => App.Quit();
        var quitBar = new Border { Padding = new Thickness(16, 0, 16, 12), Child = quit };

        var root = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(quitBar, Dock.Bottom);
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(header);
        root.Children.Add(quitBar);
        root.Children.Add(buttons);
        root.Children.Add(fields);
        Content = root;
    }

    private static UIElement Labeled(string label, TextBox box) => new StackPanel
    {
        Margin = new Thickness(0, 0, 0, 10),
        Children =
        {
            new TextBlock { Text = label, FontSize = 11, Foreground = Brushes.Gray, Margin = new Thickness(0, 0, 0, 4) },
            box
        }
    };

    private UIElement LabeledPassword() => new StackPanel
    {
        Margin = new Thickness(0, 0, 0, 10),
        Children =
        {
            new TextBlock { Text = "Пароль", FontSize = 11, Foreground = Brushes.Gray, Margin = new Thickness(0, 0, 0, 4) },
            _password
        }
    };

    private async Task SignIn()
    {
        _error.Text = "";
        var email = _email.Text.Trim();
        var server = _server.Text.Trim();
        var domain = _domain.Text.Trim();
        var username = _username.Text.Trim();
        var password = _password.Password;
        if (string.IsNullOrEmpty(email)) { _error.Text = "Укажите email"; return; }
        if (string.IsNullOrEmpty(server)) { _error.Text = "Укажите сервер"; return; }
        if (string.IsNullOrEmpty(password)) { _error.Text = "Укажите пароль"; return; }
        var resolvedUsername = string.IsNullOrEmpty(username) ? email : username;
        _signIn.IsEnabled = false;
        _signIn.Content = "…";
        try
        {
            var store = SettingsStore.Shared;
            var deviceId = string.IsNullOrEmpty(store.Account.DeviceId)
                ? AccountSettings.GenerateDeviceId()
                : store.Account.ResolvedDeviceId;
            var settings = new AccountSettings
            {
                Email = email, Server = server, Domain = domain, Username = resolvedUsername, DeviceId = deviceId
            };
            var client = new ExchangeClient(settings, password);
            await client.TestConnection();
            store.SaveCredentials(settings.Email, settings.Server, settings.Domain, settings.Username, password);
            store.Account = new AccountSettings
            {
                Email = store.Account.Email,
                Server = store.Account.Server,
                Domain = store.Account.Domain,
                Username = store.Account.Username,
                DeviceId = client.DeviceId
            };
            await NotificationService.Shared.RequestAuthorization();
            await CalendarSyncService.Shared.SyncNow();
        }
        catch (Exception ex)
        {
            _error.Text = ex.Message;
        }
        finally
        {
            _signIn.IsEnabled = true;
            _signIn.Content = "Войти";
        }
    }
}
