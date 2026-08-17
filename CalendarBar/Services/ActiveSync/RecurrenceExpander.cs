using System.Globalization;
using System.Text.RegularExpressions;

namespace CalendarBar;

public static class ActiveSyncDateParser
{
    public static DateTime? Parse(string value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var date))
            return date.ToLocalTime();

        var match = Regex.Match(value, @"^(\d{4})(\d{2})(\d{2})T(\d{2})(\d{2})(\d{2})Z?$");
        if (match.Success)
        {
            var utc = value.EndsWith('Z');
            var dt = new DateTime(
                int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value),
                int.Parse(match.Groups[4].Value), int.Parse(match.Groups[5].Value), int.Parse(match.Groups[6].Value),
                utc ? DateTimeKind.Utc : DateTimeKind.Local);
            return utc ? dt.ToLocalTime() : dt;
        }
        return null;
    }

    public static string Format(DateTime date) =>
        date.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
}

public static class RecurrenceExpander
{
    private const int MaxInstancesPerSeries = 120;

    public static List<NormalizedCalendarEvent> Expand(
        IEnumerable<NormalizedCalendarEvent> events, DateTime windowStart, DateTime windowEnd)
    {
        var expanded = new List<NormalizedCalendarEvent>();
        foreach (var eventItem in events)
        {
            if (eventItem.Recurrence is null || IsExpandedInstance(eventItem))
            {
                expanded.Add(eventItem);
                continue;
            }
            var masterStart = ActiveSyncDateParser.Parse(eventItem.StartAt);
            if (masterStart is null)
            {
                expanded.Add(eventItem);
                continue;
            }
            var masterEnd = ActiveSyncDateParser.Parse(string.IsNullOrEmpty(eventItem.EndAt) ? eventItem.StartAt : eventItem.EndAt)
                            ?? masterStart.Value;
            var duration = masterEnd - masterStart.Value;
            var rule = NormalizeRecurrenceRule(eventItem.Recurrence, masterStart.Value);
            var instances = rule.Type == "0"
                ? GenerateDailyInstances(eventItem, rule, masterStart.Value, duration, windowStart, windowEnd)
                : GenerateRecurrenceInstancesByDay(eventItem, rule, masterStart.Value, duration, windowStart, windowEnd);
            expanded.AddRange(instances);
        }
        return expanded;
    }

    private static bool IsExpandedInstance(NormalizedCalendarEvent e) => e.InstanceType is "2" or "3";

    private static CalendarRecurrence NormalizeRecurrenceRule(CalendarRecurrence recurrence, DateTime masterStart)
    {
        var trimmedType = recurrence.Type.Trim();
        string inferredType;
        if (!string.IsNullOrEmpty(trimmedType)) inferredType = trimmedType;
        else if (recurrence.DayOfWeek is not null) inferredType = "1";
        else if (recurrence.DayOfMonth is not null) inferredType = recurrence.WeekOfMonth is not null ? "3" : "2";
        else if (recurrence.MonthOfYear is not null) inferredType = recurrence.WeekOfMonth is not null ? "6" : "5";
        else inferredType = "1";

        int? inferredDayOfWeek = inferredType == "1" && (recurrence.DayOfWeek ?? 0) <= 0
            ? ActiveSyncWeekdayBitValue(masterStart.DayOfWeek)
            : recurrence.DayOfWeek;

        return new CalendarRecurrence
        {
            Type = inferredType,
            Interval = Math.Max(recurrence.Interval, 1),
            Occurrences = recurrence.Occurrences,
            Until = recurrence.Until,
            DayOfWeek = inferredDayOfWeek,
            DayOfMonth = recurrence.DayOfMonth,
            WeekOfMonth = recurrence.WeekOfMonth,
            MonthOfYear = recurrence.MonthOfYear
        };
    }

    private static List<NormalizedCalendarEvent> GenerateDailyInstances(
        NormalizedCalendarEvent eventItem, CalendarRecurrence recurrence, DateTime masterStart,
        TimeSpan duration, DateTime windowStart, DateTime windowEnd)
    {
        var untilDate = ActiveSyncDateParser.Parse(recurrence.Until);
        var interval = Math.Max(recurrence.Interval, 1);
        var masterDay = masterStart.Date;
        var lastDay = windowEnd.Date;
        var day = windowStart.Date;
        if (day < masterDay) day = masterDay;
        else
        {
            var daysFromMaster = (int)(day - masterDay).TotalDays;
            var remainder = daysFromMaster % interval;
            if (remainder != 0) day = day.AddDays(interval - remainder);
        }

        var instances = new List<NormalizedCalendarEvent>();
        while (day < lastDay)
        {
            if (untilDate is not null && day > untilDate.Value.Date) break;
            var occurrenceStart = Combine(day, masterStart);
            var occurrenceEnd = occurrenceStart + duration;
            if (occurrenceEnd > windowStart && occurrenceStart < windowEnd)
            {
                var instance = MakeInstance(eventItem, occurrenceStart, occurrenceEnd);
                if (instance is not null)
                {
                    instances.Add(instance);
                    if (instances.Count >= MaxInstancesPerSeries) return instances;
                }
            }
            day = day.AddDays(interval);
        }
        return instances;
    }

    private static List<NormalizedCalendarEvent> GenerateRecurrenceInstancesByDay(
        NormalizedCalendarEvent eventItem, CalendarRecurrence recurrence, DateTime masterStart,
        TimeSpan duration, DateTime windowStart, DateTime windowEnd)
    {
        var untilDate = ActiveSyncDateParser.Parse(recurrence.Until);
        var maxOccurrences = recurrence.Occurrences;
        var instances = new List<NormalizedCalendarEvent>();
        var occurrenceIndex = 0;
        var lastDay = windowEnd.Date;
        var day = windowStart.Date.AddDays(-1);

        while (day < lastDay)
        {
            if (untilDate is not null && day > untilDate.Value.Date) break;
            if (maxOccurrences is not null && occurrenceIndex >= maxOccurrences) break;
            if (MatchesRecurrence(day, masterStart, recurrence))
            {
                var occurrenceStart = Combine(day, masterStart);
                var occurrenceEnd = occurrenceStart + duration;
                if (occurrenceEnd > windowStart && occurrenceStart < windowEnd)
                {
                    var instance = MakeInstance(eventItem, occurrenceStart, occurrenceEnd);
                    if (instance is not null)
                    {
                        instances.Add(instance);
                        if (instances.Count >= MaxInstancesPerSeries) return instances;
                    }
                }
                occurrenceIndex++;
            }
            day = day.AddDays(1);
        }
        return instances;
    }

    private static bool MatchesRecurrence(DateTime day, DateTime masterStart, CalendarRecurrence recurrence)
    {
        var masterDay = masterStart.Date;
        if (day < masterDay) return false;
        var interval = Math.Max(recurrence.Interval, 1);

        switch (recurrence.Type)
        {
            case "0":
                return (int)(day - masterDay).TotalDays % interval == 0;
            case "1":
            {
                var mask = (recurrence.DayOfWeek ?? 0) > 0
                    ? recurrence.DayOfWeek!.Value
                    : ActiveSyncWeekdayBitValue(masterStart.DayOfWeek);
                if (!ActiveSyncWeekdayBit(day.DayOfWeek, mask)) return false;
                var weeksDiff = WeekOrdinal(day) - WeekOrdinal(masterDay);
                return weeksDiff >= 0 && weeksDiff % interval == 0;
            }
            case "2":
                if (recurrence.DayOfMonth is null || day.Day != recurrence.DayOfMonth) return false;
                var months = (day.Year - masterDay.Year) * 12 + day.Month - masterDay.Month;
                return months >= 0 && months % interval == 0;
            case "3":
                return MatchesRelativeMonthly(day, masterStart, recurrence);
            case "5":
                if (recurrence.DayOfMonth is null || recurrence.MonthOfYear is null) return false;
                if (day.Month != recurrence.MonthOfYear || day.Day != recurrence.DayOfMonth) return false;
                var years = day.Year - masterDay.Year;
                return years >= 0 && years % interval == 0;
            case "6":
                return MatchesRelativeYearly(day, masterStart, recurrence);
            default:
                if (recurrence.DayOfWeek is > 0)
                {
                    if (!ActiveSyncWeekdayBit(day.DayOfWeek, recurrence.DayOfWeek.Value)) return false;
                    var weeksDiff = WeekOrdinal(day) - WeekOrdinal(masterDay);
                    return weeksDiff >= 0 && weeksDiff % interval == 0;
                }
                return day.Date == masterStart.Date;
        }
    }

    private static int WeekOrdinal(DateTime date)
    {
        var cal = CultureInfo.CurrentCulture.Calendar;
        return cal.GetYear(date) * 100 + cal.GetWeekOfYear(date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
    }

    private static int ActiveSyncWeekdayBitValue(DayOfWeek weekday) => weekday switch
    {
        DayOfWeek.Sunday => 1,
        DayOfWeek.Monday => 2,
        DayOfWeek.Tuesday => 4,
        DayOfWeek.Wednesday => 8,
        DayOfWeek.Thursday => 16,
        DayOfWeek.Friday => 32,
        DayOfWeek.Saturday => 64,
        _ => 0
    };

    private static bool ActiveSyncWeekdayBit(DayOfWeek weekday, int mask) =>
        (mask & ActiveSyncWeekdayBitValue(weekday)) != 0;

    private static bool MatchesRelativeMonthly(DateTime day, DateTime masterStart, CalendarRecurrence recurrence)
    {
        if (recurrence.DayOfWeek is null || recurrence.WeekOfMonth is null) return false;
        var interval = Math.Max(recurrence.Interval, 1);
        var months = (day.Year - masterStart.Year) * 12 + day.Month - masterStart.Month;
        if (months < 0 || months % interval != 0) return false;
        if (!ActiveSyncWeekdayBit(day.DayOfWeek, recurrence.DayOfWeek.Value)) return false;
        return WeekOfMonthInMonth(day, recurrence.WeekOfMonth.Value);
    }

    private static bool MatchesRelativeYearly(DateTime day, DateTime masterStart, CalendarRecurrence recurrence)
    {
        if (recurrence.DayOfWeek is null || recurrence.WeekOfMonth is null || recurrence.MonthOfYear is null) return false;
        var interval = Math.Max(recurrence.Interval, 1);
        var years = day.Year - masterStart.Year;
        if (years < 0 || years % interval != 0) return false;
        if (day.Month != recurrence.MonthOfYear) return false;
        if (!ActiveSyncWeekdayBit(day.DayOfWeek, recurrence.DayOfWeek.Value)) return false;
        return WeekOfMonthInMonth(day, recurrence.WeekOfMonth.Value);
    }

    private static bool WeekOfMonthInMonth(DateTime day, int weekOfMonth)
    {
        if (weekOfMonth == 5)
        {
            var lastDay = new DateTime(day.Year, day.Month, 1).AddMonths(1).AddDays(-1);
            if (lastDay.DayOfWeek != day.DayOfWeek) return false;
            return day.AddDays(7) > lastDay;
        }
        var week = (day.Day - 1) / 7 + 1;
        return week == weekOfMonth;
    }

    private static DateTime Combine(DateTime day, DateTime reference) =>
        day.Date.Add(reference.TimeOfDay);

    private static CalendarException? MatchingException(DateTime occurrenceStart, List<CalendarException> exceptions)
    {
        return exceptions.FirstOrDefault(exception =>
        {
            var exceptionStart = ActiveSyncDateParser.Parse(exception.ExceptionStartAt);
            if (exceptionStart is null) return false;
            return Math.Abs((exceptionStart.Value - occurrenceStart).TotalMinutes) < 1
                   || exceptionStart.Value.Date == occurrenceStart.Date;
        });
    }

    private static NormalizedCalendarEvent? MakeInstance(
        NormalizedCalendarEvent eventItem, DateTime occurrenceStart, DateTime occurrenceEnd)
    {
        var exception = MatchingException(occurrenceStart, eventItem.Exceptions);
        if (exception is not null)
        {
            if (exception.Deleted) return null;
            var start = ActiveSyncDateParser.Parse(exception.StartAt) ?? occurrenceStart;
            var end = ActiveSyncDateParser.Parse(exception.EndAt) ?? occurrenceEnd;
            return eventItem.AsInstance(
                $"{eventItem.ServerId}-{(long)occurrenceStart.ToUniversalTime().Subtract(DateTime.UnixEpoch).TotalSeconds}",
                ActiveSyncDateParser.Format(start),
                ActiveSyncDateParser.Format(end),
                string.IsNullOrEmpty(exception.Title) ? eventItem.Title : exception.Title,
                string.IsNullOrEmpty(exception.Location) ? eventItem.Location : exception.Location,
                string.IsNullOrEmpty(exception.Description) ? eventItem.Description : exception.Description,
                exception.AllDay ?? eventItem.AllDay);
        }

        return eventItem.AsInstance(
            $"{eventItem.ServerId}-{(long)occurrenceStart.ToUniversalTime().Subtract(DateTime.UnixEpoch).TotalSeconds}",
            ActiveSyncDateParser.Format(occurrenceStart),
            ActiveSyncDateParser.Format(occurrenceEnd));
    }
}
