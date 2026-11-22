using HarmonyLib;
using System;
using System.Text;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ExtraInfo.Systems.TreeProgress;

[HarmonyPatch(typeof(BlockEntitySapling), nameof(BlockEntitySapling.GetBlockInfo))]
public static class TreeProgressPatch
{
    [HarmonyPostfix]
    public static void Postfix(BlockEntitySapling __instance, StringBuilder dsc)
    {
        if (!ApplyTimersOrProgressBars()) return;
        if (!Config.ShowTreeProgress) return;

        double targetHours = __instance.GetField<double>("totalHoursTillGrowth");
        double currentHours = __instance.Api.World.Calendar.TotalHours;
        double hoursRemaining = targetHours - currentHours;

        if (hoursRemaining > 0)
        {
            NatFloat growthRnd = __instance.GetProperty<NatFloat>("nextStageDaysRnd");
            float growthRateMod = __instance.GetProperty<float>("GrowthRateMod");

            double totalStageHours = growthRnd.avg * 24.0 * growthRateMod;

            var props = new TimeBasedProgressBarProperties
            {
                HeaderKey = Lang.Get(__instance.stage == EnumTreeGrowthStage.Seed ? "extrainfo:WillSproutIn" : "extrainfo:WillMatureIn"),
                HoursTotal = totalStageHours,
                HoursLeft = Math.Clamp(hoursRemaining, 0, totalStageHours),

                ShowProgressBar = Config.ShowProgressPercent,
                ShowInGameTime = Config.ShowInGameTimeInfo
            };

            dsc.AppendLine().Append(TimeFormatter.BuildTimeBlockPlusProgressBar(__instance.Api, props));

            // A planted seed first sprouts into a sapling and then needs a second stage to become a tree.
            // A sapling has only that last stage, so the line above already is the full time for it.
            if (__instance.stage != EnumTreeGrowthStage.Seed) return;

            // Same default and attribute as BlockEntitySapling.nextStageDaysRnd uses for the sapling stage
            NatFloat matureRnd = NatFloat.create(EnumDistribution.UNIFORM, 7f, 2f);
            JsonObject? attributes = __instance.Block?.Attributes;
            if (attributes != null)
            {
                matureRnd = attributes["matureDays"].AsObject(matureRnd);
            }

            double matureHours = matureRnd.avg * 24.0 * growthRateMod;
            double wholeHours = totalStageHours + matureHours;

            var totalProps = new TimeBasedProgressBarProperties
            {
                HeaderKey = Lang.Get("extrainfo:TotalGrowthIn"),
                HoursTotal = wholeHours,
                HoursLeft = Math.Clamp(hoursRemaining + matureHours, 0, wholeHours),

                ShowProgressBar = Config.ShowProgressPercent,
                ShowInGameTime = Config.ShowInGameTimeInfo
            };

            dsc.AppendLine().Append(TimeFormatter.BuildTimeBlockPlusProgressBar(__instance.Api, totalProps));
        }
    }
}