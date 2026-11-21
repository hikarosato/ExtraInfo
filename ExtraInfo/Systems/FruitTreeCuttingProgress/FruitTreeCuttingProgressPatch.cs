using HarmonyLib;
using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace ExtraInfo.Systems.FruitTreeCuttingProgress;

[HarmonyPatch(typeof(FruitTreeGrowingBranchBH), nameof(FruitTreeGrowingBranchBH.GetBlockInfo))]
public static class FruitTreeCuttingProgressPatch
{
    [HarmonyPostfix]
    public static void Postfix(FruitTreeGrowingBranchBH __instance, StringBuilder dsc)
    {
        if (!ApplyTimersOrProgressBars()) return;
        if (!Config.ShowTreeProgress) return;
        if (__instance.Blockentity is not BlockEntityFruitTreeBranch be) return;
        if (be.PartType != EnumTreePartType.Cutting || be.FoliageState == EnumFoliageState.Dead) return;
        if (be.TreeType == null || be.RootOff == null) return;

        ICoreAPI api = be.Api;
        if (api.World.BlockAccessor.GetBlockEntity(be.Pos.AddCopy(be.RootOff)) is not BlockEntityFruitTreeBranch rootBe) return;

        var rootBh = rootBe.GetBehavior<FruitTreeRootBH>();
        if (rootBh == null || !rootBh.propsByType.TryGetValue(be.TreeType, out var treeProps)) return;

        // The game attempts growth once per GrowthStepDays, but no more often than every 0.5 days.
        // The first attempt of a cutting (GrowTries < 1) does nothing; the second one rolls for rooting/grafting or death, which is the "next growth stage".
        double stepDays = Math.Max(treeProps.GrowthStepDays, 0.01);
        double waitPerAttempt = Math.Max(stepDays, 0.5);
        int skippedAttempts = be.GrowTries < 1 ? 1 : 0;

        double totalDays = stepDays + waitPerAttempt;
        double nextStageAt = be.lastGrowthAttemptTotalDays + skippedAttempts * stepDays + waitPerAttempt;
        double daysLeft = nextStageAt - api.World.Calendar.TotalDays;
        if (daysLeft <= 0) return;

        float hoursPerDay = api.World.Calendar.HoursPerDay;
        double totalHours = totalDays * hoursPerDay;

        var barProps = new TimeBasedProgressBarProperties
        {
            HeaderKey = Lang.Get("extrainfo:NextStageIn"),
            HoursTotal = totalHours,
            HoursLeft = Math.Clamp(daysLeft * hoursPerDay, 0, totalHours),

            ShowProgressBar = Config.ShowProgressBars,
            ShowRealTime = Config.ShowRealTimeInfo,
            ShowInGameTime = Config.ShowInGameTimeInfo
        };

        dsc.AppendLine().Append(TimeFormatter.BuildTimeBlockPlusProgressBar(api, barProps));
    }
}