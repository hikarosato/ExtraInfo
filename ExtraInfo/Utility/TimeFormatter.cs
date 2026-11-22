using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace ExtraInfo;

public static class TimeFormatter
{
    public static string FormatGameTime(double hours, double hoursPerDay = 24)
    {
        if (double.IsNaN(hours) || double.IsInfinity(hours) || hours < 0) hours = 0;
        if (hoursPerDay <= 0) hoursPerDay = 24;

        int days = (int)Math.Floor(hours / hoursPerDay);
        double rest = hours - days * hoursPerDay;
        int h = (int)Math.Floor(rest);
        int m = (int)Math.Floor((rest - h) * 60.0);

        // 2 days or more: days only
        if (days >= 2)
            return Lang.Get("count-days", days);

        // 1 day and up to 23 hours: days plus hours, no minutes
        if (days == 1)
        {
            string daysText = Lang.Get("count-days", days);
            return h > 0 ? daysText + ", " + Lang.Get("{0} hours", h) : daysText;
        }

        // Under an hour: minutes only, never less than 1 minute
        if (h == 0)
            return Lang.Get("extrainfo:Minutes", Math.Max(1, m));

        // Under a day: hours and minutes
        return Lang.Get("{0} hours, {1} minutes", h, m);
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
            sb.Append(TextExtensions.ColorText(FormatGameTime(igSeconds / 3600.0, api.World.Calendar.HoursPerDay)));

        return sb.ToString().TrimEnd();
    }
}