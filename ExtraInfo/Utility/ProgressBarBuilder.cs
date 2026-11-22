using System;
using System.Text;

namespace ExtraInfo;

public static class ProgressBarBuilder
{
    public static void Build(StringBuilder sb, double progress)
    {
        progress = Math.Clamp(progress, 0.0, 100.0);
        sb.Append(TextExtensions.ColorText(progress.ToString("F0") + "%"));
    }

    public static string Build(double progress)
    {
        StringBuilder sb = new();
        Build(sb, progress);
        return sb.ToString();
    }
}