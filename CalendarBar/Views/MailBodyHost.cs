using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace CalendarBar;

public sealed class MailBodyHost : UserControl
{
    private readonly MailMessage _message;
    private readonly MailFolderKind? _folder;
    private readonly RichTextBox _plain = new() { IsReadOnly = true, BorderThickness = new Thickness(0), Background = System.Windows.Media.Brushes.Transparent };
    private readonly WebView2 _web = new() { Height = 40, Visibility = Visibility.Collapsed };
    private bool _ready;

    public MailBodyHost(MailMessage message, MailFolderKind? folder)
    {
        _message = message;
        _folder = folder;
        Content = new Grid { Children = { _plain, _web } };
        _plain.Document = Linkify.Document(FallbackText());
        Loaded += async (_, _) => await LoadAsync();
    }

    private string FallbackText()
    {
        if (_message.Body?.Type == MailBodyType.Html)
            return TextContentFormatter.LightweightPlainText(_message.Body.Data);
        return _message.DisplayBodyText;
    }

    private async Task LoadAsync()
    {
        await MailSyncService.Shared.FetchFullBodyIfNeeded(_message, _folder);
        var store = SettingsStore.Shared;
        var current = MailSyncService.Shared.Thread(_message.ThreadKey, _folder ?? MailSyncService.Shared.SelectedFolder)
            ?.Messages.FirstOrDefault(m => m.Id == _message.Id) ?? _message;
        if (!store.MailHtmlRenderingEnabled || current.Body?.Type != MailBodyType.Html)
        {
            _plain.Document = Linkify.Document(current.DisplayBodyText);
            return;
        }
        var html = store.MailImagesEnabled
            ? await MailSyncService.Shared.HtmlWithInlineImages(current.Body.Data, current)
            : current.Body.Data;
        var wrapped = MailHtmlDocument.Wrap(html, store.MailImagesEnabled);
        try
        {
            var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CalendarBar", "WebView2"));
            await _web.EnsureCoreWebView2Async(env);
            _web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            _web.CoreWebView2.Settings.IsScriptEnabled = false;
            _web.Height = 200;
            _web.CoreWebView2.NewWindowRequested += (_, e) =>
            {
                e.Handled = true;
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri) { UseShellExecute = true }); } catch { }
            };
            _web.NavigationCompleted += (_, _) =>
            {
                _web.Visibility = Visibility.Visible;
                _plain.Visibility = Visibility.Collapsed;
                _ = MeasureHtml();
            };
            _web.NavigateToString(wrapped);
            _ready = true;
        }
        catch
        {
            _plain.Document = Linkify.Document(TextContentFormatter.LightweightPlainText(current.Body.Data));
        }
    }

    private async Task MeasureHtml()
    {
        try
        {
            var result = await _web.ExecuteScriptAsync("document.body ? document.body.scrollHeight.toString() : '80'");
            if (double.TryParse(result.Trim('"'), out var height))
                _web.Height = Math.Clamp(height + 8, 40, 20000);
        }
        catch { }
    }
}
