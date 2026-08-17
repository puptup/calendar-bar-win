namespace CalendarBar;

public sealed class MailInlineImage
{
    public string ContentId { get; init; } = "";
    public string MimeType { get; init; } = "";
    public string Base64Data { get; init; } = "";
    public string DataUri => $"data:{MimeType};base64,{Base64Data}";
}

public static class MailHtmlInliner
{
    public static string NormalizedContentId(string raw) =>
        raw.Trim().Trim('<', '>', ' ', '\t', '\n');

    public static bool References(string contentId, string html) =>
        !string.IsNullOrEmpty(contentId) && html.Contains($"cid:{contentId}");

    public static bool ContainsInlineReferences(string html) => html.Contains("cid:");

    public static string ReplacingInlineImages(string html, IEnumerable<MailInlineImage> images)
    {
        var result = html;
        foreach (var image in images)
        {
            if (string.IsNullOrEmpty(image.ContentId)) continue;
            result = result.Replace($"cid:{image.ContentId}", image.DataUri);
        }
        return result;
    }

    public static string ImageMimeType(byte[] data)
    {
        if (data.Length >= 4 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
            return "image/png";
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            return "image/jpeg";
        if (data.Length >= 4 && data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x38)
            return "image/gif";
        if (data.Length >= 12 && data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50)
            return "image/webp";
        if (data.Length >= 4 && data[0] == 0x3C && data[1] == 0x73 && data[2] == 0x76 && data[3] == 0x67)
            return "image/svg+xml";
        return "application/octet-stream";
    }
}

public static class MailHtmlDocument
{
    public static string Wrap(string raw, bool allowImages = true)
    {
        var imagePolicy = allowImages ? "img-src data: https: http:;" : "";
        var hiddenImages = allowImages ? "" : "img { display: none; }";
        return $"""
            <!doctype html>
            <html>
            <head>
            <meta charset="utf-8">
            <meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'; font-src data:; {imagePolicy}">
            <style>
                :root {{ color-scheme: light; }}
                body {{
                    margin: 0;
                    padding: 12px;
                    background: #ffffff;
                    color: #1d1d1f;
                    font-family: "Segoe UI Variable", "Segoe UI", sans-serif;
                    font-size: 13px;
                    overflow-wrap: break-word;
                }}
                img {{ max-width: 100%; height: auto; }}
                table {{ max-width: 100%; }}
                {hiddenImages}
            </style>
            </head>
            <body>{raw}</body>
            </html>
            """;
    }
}
