using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace CalendarBar;

public static class TextContentFormatter
{
    public const int PreviewScanLimit = 4000;

    public static string PlainText(string raw)
    {
        if (!raw.Contains('<')) return raw.Trim();
        return LightweightPlainText(raw, raw.Length);
    }

    public static string LightweightPlainText(string raw, int limit = PreviewScanLimit)
    {
        var input = raw.Length > limit ? raw[..limit] : raw;
        if (!input.Contains('<') && !input.Contains('&')) return input.Trim();

        var text = Regex.Replace(input, @"(?i)<(style|script)[^>]*>[\s\S]*?</\1>", " ");
        text = Regex.Replace(text, @"(?i)<br[^>]*>", "\n");
        text = Regex.Replace(text, @"<[^>]+>", " ");
        text = text
            .Replace("&nbsp;", " ")
            .Replace("&amp;", "&")
            .Replace("&lt;", "<")
            .Replace("&gt;", ">")
            .Replace("&quot;", "\"")
            .Replace("&#39;", "'")
            .Replace("&laquo;", "«")
            .Replace("&raquo;", "»");
        text = Regex.Replace(text, @"[ \t]{2,}", " ");
        text = Regex.Replace(text, @"[ \t]*\n[ \t]*", "\n");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        return text.Trim();
    }
}

public static class Linkify
{
    private static readonly Regex Url = new(
        @"https?://[^\s<>""']+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static FlowDocument Document(string text)
    {
        var doc = new FlowDocument
        {
            FontSize = 13,
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
            PagePadding = new Thickness(0)
        };
        var paragraph = new Paragraph { Margin = new Thickness(0) };
        var last = 0;
        foreach (Match match in Url.Matches(text))
        {
            if (match.Index > last)
                paragraph.Inlines.Add(new Run(text[last..match.Index]));
            var link = new Hyperlink(new Run(match.Value))
            {
                NavigateUri = Uri.TryCreate(match.Value, UriKind.Absolute, out var uri) ? uri : null
            };
            link.RequestNavigate += (_, e) =>
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
                catch { }
                e.Handled = true;
            };
            paragraph.Inlines.Add(link);
            last = match.Index + match.Length;
        }
        if (last < text.Length)
            paragraph.Inlines.Add(new Run(text[last..]));
        doc.Blocks.Add(paragraph);
        return doc;
    }
}
