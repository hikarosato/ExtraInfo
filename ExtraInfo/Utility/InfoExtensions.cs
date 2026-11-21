using System;
using System.Text;
using Vintagestory.GameContent;

namespace ExtraInfo;

public static class InfoExtensions
{
    /// <summary>
    /// Appends a progress bar + remaining time info for a burning <see cref="BlockEntityCoalPile"/>.
    /// </summary>
    /// <remarks>
    /// The game burns coal piles one stack item at a time, every <see cref="BlockEntityCoalPile.BurnHoursPerLayer"/> / 2
    /// in-game hours (a "layer" is two items stacked visually). The private field <c>burnStartTotalHours</c> is
    /// advanced by that half-layer duration every time one item is consumed, so the correct remaining time is
    /// <c>StackSize * BurnHoursPerLayer / 2 - (totalHours - burnStart)</c>. Using <c>Layers * BurnHoursPerLayer</c>
    /// would underestimate whenever the current stack size is odd (e.g. 3 items left = 1 visual layer, but
    /// actually 3 * BurnHoursPerLayer/2 hours of burn time).
    /// </remarks>
    public static void AppendFuelProgress(this StringBuilder sb, BlockEntityCoalPile fuelPile, string headerKey)
    {
        if (!fuelPile.IsBurning) return;

        double totalHours = fuelPile.Api.World.Calendar.TotalHours;
        double burnStart = fuelPile.GetField<double>("burnStartTotalHours");

        // OwnStackSize returns inventory[0].StackSize, the actual number of remaining items.
        int stackSize = fuelPile.OwnStackSize;
        if (stackSize <= 0) return;

        // Each item takes BurnHoursPerLayer / 2 hours to burn (see BlockEntityCoalPile.OnBurningTickServer).
        double hoursPerItem = fuelPile.BurnHoursPerLayer / 2.0;
        double totalCapacity = stackSize * hoursPerItem;
        double hoursElapsed = Math.Max(0, totalHours - burnStart);
        double totalRemaining = Math.Max(0, totalCapacity - hoursElapsed);

        var props = new TimeBasedProgressBarProperties
        {
            HeaderKey = headerKey,
            HoursTotal = totalCapacity,
            HoursLeft = totalRemaining,

            ShowProgressBar = Config.ShowProgressBars,
            ShowRealTime = Config.ShowRealTimeInfo,
            ShowInGameTime = Config.ShowInGameTimeInfo
        };

        sb.AppendLine();
        sb.Append(TimeFormatter.BuildTimeBlockPlusProgressBar(fuelPile.Api, props, reversed: true));
    }

    public static bool ApplyTimersOrProgressBars()
    {
        return Config != null && (Config.ShowProgressBars || Config.ShowRealTimeInfo || Config.ShowInGameTimeInfo);
    }
}
