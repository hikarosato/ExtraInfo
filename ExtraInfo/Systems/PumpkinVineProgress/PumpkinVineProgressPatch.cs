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

        // Stage is encoded in the second-to-last code part (e.g. "pumpkin-vine-3-normal" / "pumpkin-vine-3-blooming" / "pumpkin-vine-4-withered").
        // Stage 4 = withered: the game no longer grows it, so don't show a misleading "next stage" timer.
        Block vineBlock = vine.Api.World.BlockAccessor.GetBlock(vine.Pos);
        int vineStage = -1;
        if (vineBlock?.Code?.Path != null)
        {
            int.TryParse(vineBlock.LastCodePart(1), out vineStage);
        }
        if (vineStage >= 4) return;

        double currentHours = vine.Api.World.Calendar.TotalHours;

        // 1. Vine progress.
        //
        // TryGrow() advances totalHoursForNextStage by vineHoursToGrow (12h) every stage.
        // BUT: when transitioning out of stage 2, GrowVine() *also* adds vineHoursToGrowStage2 (6h)
        // BEFORE the +12h from TryGrow — so the first time you see stage 3, the timer actually
        // holds 18h, not 12h. canBloom == true marks exactly that case (it's set to true in the
        // currentStage==2 branch, then cleared on the next GrowVine when currentStage==3).
        // Blooming and second-and-later stage-3 ticks use the regular 12h.
        double vineHoursTotal = BlockEntityPumpkinVine.vineHoursToGrow;
        if (vineStage == 3 && vine.canBloom)
        {
            vineHoursTotal += BlockEntityPumpkinVine.vineHoursToGrowStage2;
        }

        double vineRemaining = vine.totalHoursForNextStage - currentHours;

        if (vineRemaining > 0)
        {
            var vineProps = new TimeBasedProgressBarProperties()
            {
                HeaderKey = Lang.Get("extrainfo:NextVineStageIn"),
                HoursTotal = vineHoursTotal,
                HoursLeft = Math.Min(vineRemaining, vineHoursTotal),
                ShowProgressBar = Config.ShowProgressPercent,
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
                        ShowProgressBar = Config.ShowProgressPercent,
                        ShowInGameTime = Config.ShowInGameTimeInfo
                    };

                    dsc.AppendLine().Append(TimeFormatter.BuildTimeBlockPlusProgressBar(vine.Api, fruitProps));
                }
            }
        }
    }
}
