using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CalendarBar;

public static class AppInfo
{
    public const string Author = "Кошевар Кирилл Петрович, ДАНИС";
    public const string Version = "beta 0.0.1";
    public const string Name = "CalendarBar";
}

public sealed class AboutWindow : Window
{
    public AboutWindow()
    {
        Title = "О приложении";
        Width = 340;
        Height = 220;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        var ok = new Button { Content = "OK", IsDefault = true, Padding = new Thickness(18, 6, 18, 6), HorizontalAlignment = HorizontalAlignment.Right };
        ok.Click += (_, _) => Close();
        Content = new StackPanel
        {
            Margin = new Thickness(24),
            Children =
            {
                new TextBlock { Text = "📅", FontSize = 28, HorizontalAlignment = HorizontalAlignment.Center },
                new TextBlock { Text = AppInfo.Name, FontSize = 18, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 12) },
                new TextBlock { Text = $"Автор: {AppInfo.Author}", Foreground = Brushes.Gray },
                new TextBlock { Text = $"Версия: {AppInfo.Version}", Foreground = Brushes.Gray, Margin = new Thickness(0, 4, 0, 16) },
                ok
            }
        };
    }
}
