using DesktopCountdown.Models;

namespace DesktopCountdown.Services;

public static class CountdownFormatter
{
    public static List<CountdownSegment> Format(DateTimeOffset target, DisplayUnitConfig units)
    {
        var remaining = target - DateTimeOffset.Now;
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        var selected = new[]
        {
            units.ShowDays,
            units.ShowHours,
            units.ShowMinutes,
            units.ShowSeconds
        };

        if (!selected.Any(x => x))
        {
            units.ShowDays = true;
        }

        var result = new List<CountdownSegment>();
        var showDays = units.ShowDays;
        var showHours = units.ShowHours;
        var showMinutes = units.ShowMinutes;
        var showSeconds = units.ShowSeconds;

        if (showDays && !showHours && !showMinutes && !showSeconds)
        {
            var value = units.DayRounding == DayRounding.Ceiling
                ? Math.Ceiling(remaining.TotalDays)
                : Math.Floor(remaining.TotalDays);
            result.Add(new CountdownSegment(value.ToString("0"), "天"));
            return result;
        }

        if (!showDays && showHours && !showMinutes && !showSeconds)
        {
            result.Add(new CountdownSegment(Math.Floor(remaining.TotalHours).ToString("0"), "时"));
            return result;
        }

        if (!showDays && !showHours && showMinutes && !showSeconds)
        {
            result.Add(new CountdownSegment(Math.Floor(remaining.TotalMinutes).ToString("0"), "分"));
            return result;
        }

        if (!showDays && !showHours && !showMinutes && showSeconds)
        {
            result.Add(new CountdownSegment(Math.Floor(remaining.TotalSeconds).ToString("0"), "秒"));
            return result;
        }

        var totalSeconds = (long)Math.Floor(remaining.TotalSeconds);
        var highest = showDays ? 0 : showHours ? 1 : showMinutes ? 2 : 3;

        if (highest == 0)
        {
            var days = totalSeconds / 86400;
            totalSeconds %= 86400;
            result.Add(new CountdownSegment(days.ToString(), "天"));
        }

        if (showHours)
        {
            var hours = totalSeconds / 3600;
            totalSeconds %= 3600;
            result.Add(new CountdownSegment(hours.ToString(highest == 1 ? "0" : "00"), "时"));
        }

        if (showMinutes)
        {
            var minutes = totalSeconds / 60;
            totalSeconds %= 60;
            result.Add(new CountdownSegment(minutes.ToString(highest == 2 ? "0" : "00"), "分"));
        }

        if (showSeconds)
        {
            result.Add(new CountdownSegment(totalSeconds.ToString(showSeconds && highest != 3 ? "00" : "0"), "秒"));
        }

        return result;
    }
}
