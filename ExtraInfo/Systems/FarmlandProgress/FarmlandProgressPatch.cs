using HarmonyLib;
using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace ExtraInfo.Systems.FarmlandProgress;

[HarmonyPatch(typeof(BlockEntityFarmland), nameof(BlockEntityFarmland.GetBlockInfo))]
public static class FarmlandProgressPatch
{
    [HarmonyPostfix]
    public static void Postfix(BlockEntityFarmland __instance, StringBuilder dsc, float ___growthRateMul)
    {
        if (!ApplyTimersOrProgressBars()) return;
        if (!Config.ShowFarmlandProgress) return;

        if (__instance?.Api == null) return;

        Block cropBlock = __instance.GetCrop();

        if (cropBlock?.CropProps == null) return;

        int currentStage = __instance.GetCropStage(cropBlock);
        int growthStages = cropBlock.CropProps.GrowthStages;

        if (currentStage >= growthStages) return;

        double targetHours = __instance.TotalHoursForNextStage;
        double currentHours = __instance.Api.World.Calendar.TotalHours;
        double hoursRemaining = targetHours - currentHours;

        // 1. Next stage
        if (hoursRemaining > 0)
        {
            double lastUpdate = __instance.TotalHoursLastUpdate;
            double totalStageWindow = targetHours - lastUpdate;

            var props = new TimeBasedProgressBarProperties()
            {
                HeaderKey = Lang.Get("extrainfo:NextStageIn"),
                HoursTotal = totalStageWindow,
                HoursLeft = hoursRemaining,

                ShowProgressBar = Config.ShowProgressPercent,
                ShowInGameTime = Config.ShowInGameTimeInfo
            };

            string verticalBlock = TimeFormatter.BuildTimeBlockPlusProgressBar(__instance.Api, props);

            dsc.AppendLine().Append(verticalBlock);
        }

        // 2. Whole growth.
        // The game moves the crop one stage at a time (N -> N + 1) until GrowthStages is reached, so after the
        // next stage there are (GrowthStages - currentStage - 1) stages left.
        int extraStages = growthStages - currentStage - 1;
        if (extraStages <= 0) return;

        double perStageHours = GetHoursPerStage(__instance, cropBlock, ___growthRateMul);
        if (perStageHours <= 0) return;

        double totalHoursLeft = Math.Max(0, hoursRemaining) + extraStages * perStageHours;
        double wholeCycleHours = Math.Max(1, growthStages - 1) * perStageHours;

        var totalProps = new TimeBasedProgressBarProperties()
        {
            HeaderKey = Lang.Get("extrainfo:TotalGrowthIn"),
            HoursTotal = wholeCycleHours,
            HoursLeft = Math.Clamp(totalHoursLeft, 0, wholeCycleHours),

            ShowProgressBar = Config.ShowProgressPercent,
            ShowInGameTime = Config.ShowInGameTimeInfo
        };

        dsc.AppendLine().Append(TimeFormatter.BuildTimeBlockPlusProgressBar(__instance.Api, totalProps));
    }

    /// <summary>
    /// Mirrors BlockEntityFarmland.GetHoursForNextStage without its random 0.9..1.1 factor, so the shown total
    /// does not jump on every tooltip refresh. It is a projection for the current soil moisture and nutrients.
    /// </summary>
    private static double GetHoursPerStage(BlockEntityFarmland be, Block cropBlock, float growthRateMul)
    {
        BlockCropProperties cropProps = cropBlock.CropProps;
        IGameCalendar calendar = be.Api.World.Calendar;

        float totalDays = cropProps.TotalGrowthDays;
        totalDays = totalDays > 0f
            ? totalDays / 12f * calendar.DaysPerMonth
            : cropProps.TotalGrowthMonths * calendar.DaysPerMonth;

        float growthRate = be.GetGrowthRate(cropProps.RequiredNutrient);
        if (growthRate <= 0f || growthRateMul <= 0f) return 0;

        return calendar.HoursPerDay * (double)totalDays / Math.Max(1, cropProps.GrowthStages - 1) / growthRate / growthRateMul;
    }
}