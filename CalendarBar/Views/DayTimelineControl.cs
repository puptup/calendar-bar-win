using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace CalendarBar;

public sealed class DayTimelineControl : UserControl
{
    public event Action<string>? EventSelected;

    private const double HourHeight = 52;
    private const double TimeColumnWidth = 44;
    private const double MinEventHeight = 22;
    private const double TimelineTopInset = 10;

    private readonly ScrollViewer _scroll = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    private readonly Canvas _canvas = new();
    private readonly StackPanel _allDay = new() { Margin = new Thickness(0, 8, 0, 0) };
    private DateTime _day = DateTime.Today;
    private List<CalendarEvent> _events = [];
    private string? _selectedId;
    private readonly DispatcherTimer _nowTimer = new() { Interval = TimeSpan.FromSeconds(30) };

    public DayTimelineControl()
    {
        var root = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(_allDay, Dock.Top);
        root.Children.Add(_allDay);
        _scroll.Content = _canvas;
        root.Children.Add(_scroll);
        Content = root;
        _nowTimer.Tick += (_, _) => { if (_day.Date == DateTime.Today) Rebuild(); };
        Loaded += (_, _) => _nowTimer.Start();
        Unloaded += (_, _) => _nowTimer.Stop();
        SizeChanged += (_, _) => Rebuild();
    }

    public void SetDay(DateTime day, List<CalendarEvent> events, string? selectedId)
    {
        _day = day.Date;
        _events = events;
        _selectedId = selectedId;
        Rebuild();
        ScrollToAnchor();
    }

    public void SetSelected(string? id)
    {
        _selectedId = id;
        Rebuild();
    }

    private void Rebuild()
    {
        var allDay = _events.Where(e => e.IsAllDay).OrderBy(e => e.StartDate).ToList();
        var timed = _events.Where(e => !e.IsAllDay).OrderBy(e => e.StartDate).ToList();
        _allDay.Children.Clear();
        if (allDay.Count > 0)
        {
            _allDay.Children.Add(new TextBlock
            {
                Text = "Весь день",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.Gray,
                Margin = new Thickness(TimeColumnWidth + 12, 0, 0, 6)
            });
            var chips = new UniformGrid { Rows = 1, Margin = new Thickness(TimeColumnWidth + 8, 0, 12, 8) };
            foreach (var eventItem in allDay)
                chips.Children.Add(AllDayChip(eventItem));
            _allDay.Children.Add(chips);
        }

        _canvas.Children.Clear();
        if (timed.Count == 0 && allDay.Count == 0)
        {
            _canvas.Height = 200;
            var empty = new TextBlock
            {
                Text = "Нет событий",
                Foreground = Brushes.Gray,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 48, 0, 0)
            };
            Canvas.SetLeft(empty, 80);
            Canvas.SetTop(empty, 40);
            _canvas.Children.Add(empty);
            return;
        }

        var width = Math.Max(ActualWidth, 280);
        _canvas.Width = width;
        _canvas.Height = 24 * HourHeight + TimelineTopInset + 8;

        for (var hour = 0; hour < 24; hour++)
        {
            var y = TimelineTopInset + hour * HourHeight;
            var label = new TextBlock
            {
                Text = $"{hour:00}:00",
                FontSize = 10,
                Foreground = Brushes.Gray,
                Width = TimeColumnWidth,
                TextAlignment = TextAlignment.Right
            };
            Canvas.SetLeft(label, 0);
            Canvas.SetTop(label, y - 6);
            _canvas.Children.Add(label);
            var line = new Line
            {
                X1 = TimeColumnWidth + 8,
                X2 = width - 12,
                Y1 = y,
                Y2 = y,
                Stroke = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)),
                StrokeThickness = 1
            };
            _canvas.Children.Add(line);
        }

        var layouts = LayoutTimedEvents(timed);
        var totalWidth = Math.Max(width - TimeColumnWidth - 24, 120);
        const double columnGap = 3;
        foreach (var layout in layouts)
        {
            var columnCount = Math.Max(layout.ColumnCount, 1);
            var colWidth = (totalWidth - columnGap * (columnCount - 1)) / columnCount;
            var x = TimeColumnWidth + 8 + layout.Column * (colWidth + columnGap);
            var range = VisibleTimeRange(layout.Event);
            var y = range.Start / 60 * HourHeight + TimelineTopInset;
            var height = Math.Max(MinEventHeight, Math.Max(0, range.End - range.Start) / 60 * HourHeight);
            _canvas.Children.Add(EventBlock(layout, x, y, colWidth, height));
        }

        if (_day.Date == DateTime.Today)
        {
            var now = DateTime.Now;
            var y = MinutesFromMidnight(now) / 60 * HourHeight + TimelineTopInset;
            var time = new TextBlock
            {
                Text = now.ToString("HH:mm", new CultureInfo("ru-RU")),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.IndianRed,
                Width = TimeColumnWidth - 2,
                TextAlignment = TextAlignment.Right
            };
            Canvas.SetLeft(time, 0);
            Canvas.SetTop(time, y - 7);
            _canvas.Children.Add(time);
            var dot = new Ellipse { Width = 6, Height = 6, Fill = Brushes.IndianRed };
            Canvas.SetLeft(dot, TimeColumnWidth + 5);
            Canvas.SetTop(dot, y - 3);
            _canvas.Children.Add(dot);
            var nowLine = new Line
            {
                X1 = TimeColumnWidth + 12,
                X2 = width - 12,
                Y1 = y,
                Y2 = y,
                Stroke = Brushes.IndianRed,
                StrokeThickness = 1
            };
            _canvas.Children.Add(nowLine);
        }
    }

    private UIElement AllDayChip(CalendarEvent eventItem)
    {
        var color = BlockColor(eventItem);
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(_selectedId == eventItem.Id ? (byte)70 : (byte)40, color.R, color.G, color.B)),
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(2, 0, 2, 0),
            Padding = new Thickness(8, 5, 8, 5),
            BorderBrush = _selectedId == eventItem.Id ? new SolidColorBrush(color) : Brushes.Transparent,
            BorderThickness = new Thickness(_selectedId == eventItem.Id ? 1.5 : 0),
            Child = new DockPanel
            {
                Children =
                {
                    new Border { Width = 3, Background = new SolidColorBrush(color), CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 0, 6, 0) },
                    new TextBlock { Text = eventItem.Subject, FontSize = 11, FontWeight = FontWeights.Medium, TextTrimming = TextTrimming.CharacterEllipsis }
                }
            }
        };
        DockPanel.SetDock(((DockPanel)border.Child).Children[0], Dock.Left);
        border.MouseLeftButtonUp += (_, _) => EventSelected?.Invoke(eventItem.Id);
        border.Cursor = Cursors.Hand;
        return border;
    }

    private UIElement EventBlock(TimedLayout layout, double x, double y, double width, double height)
    {
        var color = BlockColor(layout.Event);
        var selected = _selectedId == layout.Event.Id;
        var ru = new CultureInfo("ru-RU");
        var timeLabel = $"{VisibleStart(layout.Event).ToString("HH:mm", ru)}–{VisibleEnd(layout.Event).ToString("HH:mm", ru)}";
        var border = new Border
        {
            Width = width,
            Height = height,
            Background = new SolidColorBrush(Color.FromArgb(46, color.R, color.G, color.B)),
            CornerRadius = new CornerRadius(6),
            BorderBrush = new SolidColorBrush(color) { Opacity = selected ? 1 : 0.35 },
            BorderThickness = new Thickness(selected ? 2 : 0.5),
            Opacity = selected ? 1 : 0.92,
            ClipToBounds = true,
            Child = new DockPanel
            {
                Children =
                {
                    new Border { Width = 3, Background = new SolidColorBrush(color), CornerRadius = new CornerRadius(2, 0, 0, 2) },
                    new Grid
                    {
                        Children =
                        {
                            new TextBlock
                            {
                                Text = layout.Event.Subject,
                                FontSize = 11,
                                FontWeight = FontWeights.SemiBold,
                                TextWrapping = TextWrapping.Wrap,
                                Margin = new Thickness(6, 4, 6, 12)
                            },
                            new TextBlock
                            {
                                Text = timeLabel,
                                FontSize = 9,
                                Foreground = Brushes.Gray,
                                HorizontalAlignment = HorizontalAlignment.Right,
                                VerticalAlignment = VerticalAlignment.Bottom,
                                Margin = new Thickness(6, 0, 6, 2)
                            }
                        }
                    }
                }
            }
        };
        DockPanel.SetDock(((DockPanel)border.Child).Children[0], Dock.Left);
        Canvas.SetLeft(border, x);
        Canvas.SetTop(border, y);
        border.Cursor = Cursors.Hand;
        border.MouseLeftButtonUp += (_, _) => EventSelected?.Invoke(layout.Event.Id);
        return border;
    }

    private static Color BlockColor(CalendarEvent eventItem)
    {
        if (eventItem.IsCancelled) return Colors.Gray;
        return eventItem.ResponseStatus switch
        {
            MeetingResponseStatus.Pending => Color.FromRgb(0xCA, 0x50, 0x10),
            MeetingResponseStatus.Tentative => Color.FromRgb(0xC1, 0x9C, 0x00),
            MeetingResponseStatus.Declined => Color.FromRgb(0xC4, 0x2B, 0x1C),
            _ => NativeMethods.AccentColor()
        };
    }

    private void ScrollToAnchor()
    {
        var timed = _events.Where(e => !e.IsAllDay).OrderBy(e => e.StartDate).ToList();
        CalendarEvent? anchor = null;
        if (_day.Date == DateTime.Today)
            anchor = timed.FirstOrDefault(e => e.EndDate > DateTime.Now);
        else
            anchor = timed.FirstOrDefault();
        var hour = anchor is not null ? anchor.StartDate.Hour : (_day.Date == DateTime.Today ? DateTime.Now.Hour : 0);
        var target = Math.Max(0, hour - 1) * HourHeight;
        _scroll.ScrollToVerticalOffset(target);
    }

    private double MinutesFromMidnight(DateTime date) => (date - _day.Date).TotalMinutes;

    private (double Start, double End) VisibleTimeRange(CalendarEvent eventItem)
    {
        var dayEnd = _day.Date.AddDays(1);
        var visibleStart = eventItem.StartDate > _day.Date ? eventItem.StartDate : _day.Date;
        var visibleEnd = eventItem.EndDate < dayEnd ? eventItem.EndDate : dayEnd;
        return (MinutesFromMidnight(visibleStart), MinutesFromMidnight(visibleEnd));
    }

    private DateTime VisibleStart(CalendarEvent e) => e.StartDate > _day.Date ? e.StartDate : _day.Date;
    private DateTime VisibleEnd(CalendarEvent e) => e.EndDate < _day.Date.AddDays(1) ? e.EndDate : _day.Date.AddDays(1);

    private static List<TimedLayout> LayoutTimedEvents(List<CalendarEvent> events)
    {
        var layouts = new List<TimedLayout>();
        var cluster = new List<CalendarEvent>();
        DateTime ClusterEnd() => cluster.Count == 0 ? DateTime.MinValue : cluster.Max(e => e.EndDate);
        void Flush()
        {
            if (cluster.Count == 0) return;
            layouts.AddRange(LayoutOverlapCluster(cluster));
            cluster.Clear();
        }
        foreach (var eventItem in events)
        {
            if (cluster.Count == 0 || eventItem.StartDate < ClusterEnd()) cluster.Add(eventItem);
            else { Flush(); cluster.Add(eventItem); }
        }
        Flush();
        return layouts;
    }

    private static List<TimedLayout> LayoutOverlapCluster(List<CalendarEvent> events)
    {
        var columnEnds = new List<DateTime>();
        var assignments = new List<(CalendarEvent Event, int Column)>();
        foreach (var eventItem in events.OrderBy(e => e.StartDate))
        {
            var column = columnEnds.FindIndex(end => end <= eventItem.StartDate);
            if (column >= 0)
            {
                columnEnds[column] = eventItem.EndDate;
                assignments.Add((eventItem, column));
            }
            else
            {
                columnEnds.Add(eventItem.EndDate);
                assignments.Add((eventItem, columnEnds.Count - 1));
            }
        }
        var columnCount = Math.Max(columnEnds.Count, 1);
        return assignments.Select(a => new TimedLayout(a.Event, a.Column, columnCount)).ToList();
    }

    private readonly record struct TimedLayout(CalendarEvent Event, int Column, int ColumnCount);
}
