using HarmonyLib;
using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ExtraInfo.Systems.PumpkinVineProgress;

[HarmonyPatch(typeof(BlockEntity), nameof(BlockEntity.GetBlockInfo))]
public static class PumpkinVineProgressPatch
{
    [HarmonyPostfix]
    public static void Postfix(BlockEntity __instance, StringBuilder dsc)
    {
        if (!ApplyTimersOrProgressBars()) return;
        if (!Config.ShowPumpkinVineProgress) return;

        if (__instance is not BlockEntityPumpkinVine vine || vine.Api == null) return;

        double currentHours = vine.Api.World.Calendar.TotalHours;

        // The vine block encodes its current stage in its last-but-one code part (e.g. "pumpkin-vine-2-normal").
        // The game burns vineHoursToGrow (12h) per stage, except when transitioning out of stage 2 to stage 3,
        // where GrowVine() also adds vineHoursToGrowStage2 (6h) on top of the regular vineHoursToGrow, so the
        // total transition takes 18h. Using always vineHoursToGrow makes the bar show 100% for the first 6h of
        // that stage transition. Blooming vines and stage-3 vines take just vineHoursToGrow again.
        double vineHoursTotal = BlockEntityPumpkinVine.vineHoursToGrow;
        Block vineBlock = vine.Api.World.BlockAccessor.GetBlock(vine.Pos);
        if (int.TryParse(vineBlock.LastCodePart(1), out int vineStage) && vineStage == 2)
        {
            vineHoursTotal = BlockEntityPumpkinVine.vineHoursToGrow + BlockEntityPumpkinVine.vineHoursToGrowStage2;
        }

        // 1. Vine progress
        double vineRemaining = vine.totalHoursForNextStage - currentHours;

        if (vineRemaining > 0)
        {
            var vineProps = new TimeBasedProgressBarProperties()
            {
                HeaderKey = Lang.Get("extrainfo:NextVineStageIn"),
                HoursTotal = vineHoursTotal,
                HoursLeft = Math.Min(vineRemaining, vineHoursTotal),
                ShowProgressBar = Config.ShowProgressBars,
                ShowRealTime = Config.ShowRealTimeInfo,
                ShowInGameTime = Config.ShowInGameTimeInfo
            };

            dsc.AppendLine().Append(TimeFormatter.BuildTimeBlockPlusProgressBar(vine.Api, vineProps));
        }

        // 2. Pumpkin progress (per facing)
        foreach (var facing in BlockFacing.HORIZONTALS)
        {
            if (vine.pumpkinTotalHoursForNextStage.TryGetValue(facing, out double targetHours) && targetHours > 0)
            {
                double fruitRemaining = targetHours - currentHours;

                if (fruitRemaining > 0)
                {
                    var fruitProps = new TimeBasedProgressBarProperties()
                    {
                        HeaderKey = Lang.Get("extrainfo:NextPumpkinStageIn"),
                        HoursTotal = BlockEntityPumpkinVine.pumpkinHoursToGrow,
                        HoursLeft = Math.Min(fruitRemaining, BlockEntityPumpkinVine.pumpkinHoursToGrow),
                        ShowProgressBar = Config.ShowProgressBars,
                        ShowRealTime = Config.ShowRealTimeInfo,
                        ShowInGameTime = Config.ShowInGameTimeInfo
                    };

                    dsc.AppendLine().Append(TimeFormatter.BuildTimeBlockPlusProgressBar(vine.Api, fruitProps));
                }
            }
        }
    }
}
