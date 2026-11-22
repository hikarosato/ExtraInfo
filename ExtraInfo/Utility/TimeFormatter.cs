using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace ExtraInfo;

public static class TimeFormatter
{
    // Same format as in ExtraInfo 2.0.1: "N hours, M minutes" (both rounded down), game time.
    public static string HoursAndMinutes(double hours)
    {
        if (double.IsNaN(hours) || double.IsInfinity(hours) || hours < 0) hours = 0;
        double minutes = Math.Floor((hours - Math.Truncate(hours)) * 60.0);
        return Lang.Get("{0} hours, {1} minutes", Math.Floor(hours), minutes);
    }

    public static string FormatFullTime(double totalSeconds)
    {
        if (double.IsNaN(totalSeconds) || double.IsInfinity(totalSeconds)) return "";

        totalSeconds = Math.Clamp(totalSeconds, 0, TimeSpan.MaxValue.TotalSeconds - 1);

        TimeSpan t = TimeSpan.FromSeconds(totalSeconds);

        if (t.TotalDays >= 1)
        {
            int days = (int)Math.Floor(t.TotalDays);
            int hours = t.Hours;
            int minutes = t.Minutes;

            if (hours > 0 && minutes > 0)
                return Lang.Get("{0} days, {1} hours, {2} minutes", days, hours, minutes);

            if (hours > 0)
                return Lang.Get("{0} days, {1} hours", days, hours);

            return Lang.Get("count-days", days);
        }

        if (t.TotalHours >= 1)
        {
            int hours = (int)Math.Floor(t.TotalHours);
            int minutes = t.Minutes;
            int seconds = t.Seconds;

            if (minutes > 0 && seconds > 0)
                return Lang.Get("{0} hours, {1} minutes, {2} seconds", hours, minutes, seconds);

            if (minutes > 0)
                return Lang.Get("{0} hours, {1} minutes", hours, minutes);

            return Lang.Get("{0} hours", hours);
        }

        if (t.TotalMinutes >= 1)
        {
            int minutes = t.Minutes;
            int seconds = t.Seconds;

            return seconds > 0
                ? Lang.Get("{0} minutes, {1} seconds", minutes, seconds)
                : Lang.Get("{0} minutes", minutes);
        }

        return Lang.Get("{0} seconds", t.Seconds);
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
            sb.Append(TextExtensions.ColorText(HoursAndMinutes(igSeconds / 3600.0)));

        return sb.ToString().TrimEnd();
    }
}