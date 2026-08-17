using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CalendarBar;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public static class PopoverMetrics
{
    public const double TimelineWidth = 340;
    public const double DetailWidth = 300;
    public const double Height = 420;
    public static double TotalWidth(bool showingDetail) => showingDetail ? TimelineWidth + DetailWidth : TimelineWidth;
}

public static class MailPopoverMetrics
{
    public const double ListWidth = 390;
    public const double DetailWidth = 320;
    public const double Height = 420;
    public static double TotalWidth(bool showingDetail) => showingDetail ? ListWidth + DetailWidth : ListWidth;
}

public enum PopoverKind { Calendar, Mail }

public sealed class PopoverSizeStore
{
    public static PopoverSizeStore Shared { get; } = new();

    public PopoverSize Size(PopoverKind kind, bool showingDetail)
    {
        var fallback = DefaultSize(kind, showingDetail);
        var min = MinSize(kind, showingDetail);
        var width = AppData.GetDouble(Key(kind, showingDetail, "width"));
        var height = AppData.GetDouble(Key(kind, showingDetail, "height"));
        return new PopoverSize(
            width > 0 ? Math.Max(width, min.Width) : fallback.Width,
            height > 0 ? Math.Max(height, min.Height) : fallback.Height);
    }

    public void Save(PopoverSize size, PopoverKind kind, bool showingDetail)
    {
        AppData.SetDouble(Key(kind, showingDetail, "width"), size.Width);
        AppData.SetDouble(Key(kind, showingDetail, "height"), size.Height);
    }

    public static PopoverSize DefaultSize(PopoverKind kind, bool showingDetail) => kind switch
    {
        PopoverKind.Calendar => new PopoverSize(PopoverMetrics.TotalWidth(showingDetail), PopoverMetrics.Height),
        _ => new PopoverSize(MailPopoverMetrics.TotalWidth(showingDetail), MailPopoverMetrics.Height)
    };

    public static PopoverSize MinSize(PopoverKind kind, bool showingDetail) => kind switch
    {
        PopoverKind.Calendar => new PopoverSize(showingDetail ? 560 : 300, 360),
        _ => new PopoverSize(showingDetail ? 600 : 320, 360)
    };

    private static string Key(PopoverKind kind, bool showingDetail, string dimension) =>
        $"popoverSize.{kind}.{(showingDetail ? "detail" : "list")}.{dimension}";
}

public readonly record struct PopoverSize(double Width, double Height);
