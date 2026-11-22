using HarmonyLib;
using System;
using System.Text;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace ExtraInfo.Systems.FuelProgress;

[HarmonyPatch(typeof(BlockEntityGroundStorage), nameof(BlockEntityGroundStorage.GetBlockInfo))]
public static class BurnablePileProgressPatch
{
    [HarmonyPostfix]
    public static void Postfix(BlockEntityGroundStorage __instance, StringBuilder dsc, float ___burnHoursPerItem)
    {
        if (!ApplyTimersOrProgressBars()) return;
        if (!Config.ShowFuelProgress) return;
        if (!__instance.IsBurning) return;
        if (___burnHoursPerItem <= 0 || __instance.Inventory == null || __instance.Inventory.Empty) return;

        // The game burns one stack item at a time every burnHoursPerItem in-game hours
        // (see BlockEntityGroundStorage.OnBurningTickServer: while (TotalHours - burnStartTotalHours > burnHoursPerItem) { ... }).
        // The private field burnStartTotalHours is advanced every time an item is consumed, so the correct
        // remaining time is StackSize * burnHoursPerItem - (totalHours - burnStartTotalHours).
        //
        // Using the public GetHoursLeft(currentTotalHours) here is wrong: it returns Min(elapsed, totalCapacity),
        // i.e. the elapsed time clamped to the total pile burn time. Treating that as "time elapsed on the current
        // item" makes the displayed remaining time jump backwards every time an item is consumed.
        double burnStart = __instance.GetField<double>("burnStartTotalHours");
        int stackSize = __instance.Inventory[0].StackSize;
        if (stackSize <= 0) return;

        double totalCapacity = stackSize * ___burnHoursPerItem;
        double hoursElapsed = Math.Max(0, __instance.Api.World.Calendar.TotalHours - burnStart);
        double totalRemainingHours = Math.Max(0, totalCapacity - hoursElapsed);

        if (totalCapacity > 0)
        {
            var props = new TimeBasedProgressBarProperties
            {
                HeaderKey = Lang.Get("extrainfo:Fuel"),
                HoursTotal = totalCapacity,
                HoursLeft = totalRemainingHours,

                ShowProgressBar = Config.ShowProgressPercent,
                ShowInGameTime = Config.ShowInGameTimeInfo
            };

            dsc.AppendLine().Append(TimeFormatter.BuildTimeBlockPlusProgressBar(__instance.Api, props, reversed: true));
        }
    }
}
