using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace ExtraInfo;

public static class TimeFormatter
{
    private const int MonthsPerYear = 12;

    // How many adjacent units to show at most: "1 y. 3 mo.", "3 d. 4 h", "12 h 30 min"
    private const int MaxParts = 2;

    public static string FormatGameTime(double hours, double hoursPerDay = 24, double daysPerMonth = 9)
    {
        if (double.IsNaN(hours) || double.IsInfinity(hours) || hours < 0) hours = 0;
        if (hoursPerDay <= 0) hoursPerDay = 24;
        if (daysPerMonth < 1) daysPerMonth = 9;

        long secPerDay = (long)Math.Round(hoursPerDay * 3600.0);
        long secPerMonth = secPerDay * (long)Math.Round(daysPerMonth);
        long secPerYear = secPerMonth * MonthsPerYear;

        (long Size, string Key)[] units =
        [
            (secPerYear, "extrainfo:Time.Year"),
            (secPerMonth, "extrainfo:Time.Month"),
            (secPerDay, "extrainfo:Time.Day"),
            (3600, "extrainfo:Time.Hour"),
            (60, "extrainfo:Time.Minute"),
        ];

        // Smallest unit is a minute: anything under it is shown as "1 min"
        long rest = Math.Max(60, (long)Math.Round(hours * 3600.0));

        var sb = new StringBuilder();
        int parts = 0;

        foreach (var (size, key) in units)
        {
            if (size <= 0) continue;

            long count = rest / size;
            rest -= count * size;

            if (count > 0)
            {
                if (parts > 0) sb.Append(' ');
                sb.Append(Lang.Get(key, (int)count));
                parts++;
            }
            else if (parts > 0)
            {
                // Only adjacent units: no "1 y. 0 mo. 3 d."
                break;
            }

            if (parts == MaxParts) break;
        }

        return sb.ToString();
    }

    public static string BuildTimeBlockPlusProgressBar(ICoreAPI api, TimeBasedProgressBarProperties props, bool reversed = false)
    {
        if (!props.ShowProgressBar && !props.ShowInGameTime) return "";

        float completedPercent = 0;
        if (props.HoursTotal > 0)
        {
            double ratio = props.HoursLeft / props.HoursTotal;
            completedPercent = (float)Math.Clamp((reversed ? ratio : 1.0 - ratio) * 100, 0, 100);
        }

        double igSeconds;
        if (props.IsRawSeconds)
            igSeconds = props.HoursLeft * api.World.Calendar.SpeedOfTime * api.World.Calendar.CalendarSpeedMul;
        else
            igSeconds = props.HoursLeft * 3600;

        var sb = new StringBuilder();

        // Text
        if (!string.IsNullOrEmpty(props.HeaderKey))
            sb.Append(props.HeaderKey.TrimEnd()).Append(' ');

        // %
        if (props.ShowProgressBar)
        {
            ProgressBarBuilder.Build(sb, completedPercent);
            sb.Append('\u00A0');
        }

        // Time
        if (igSeconds > 0 && props.ShowInGameTime)
        {
            var calendar = api.World.Calendar;
            sb.Append(TextExtensions.ColorText(
                FormatGameTime(igSeconds / 3600.0, calendar.HoursPerDay, calendar.DaysPerMonth)));
        }

        return sb.ToString().TrimEnd();
    }
}