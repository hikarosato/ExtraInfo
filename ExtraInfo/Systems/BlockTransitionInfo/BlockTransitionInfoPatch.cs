using HarmonyLib;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace ExtraInfo.Systems.BlockTransitionInfo;

[HarmonyPatch(typeof(BlockEntity), nameof(BlockEntity.GetBlockInfo))]
public static class BlockTransitionInfoPatch
{
    [HarmonyPostfix]
    public static void Postfix(BlockEntity __instance, StringBuilder dsc)
    {
        if (!ApplyTimersOrProgressBars()) return;
        if (!Config.ShowBlockTransitionInfo) return;
        if (__instance is not BlockEntityTransient blockEntity) return;

        TransientProperties props = blockEntity.GetField<TransientProperties>("props");
        if (props == null) return;

        double hoursLeft = blockEntity.GetField<double>("transitionHoursLeft");

        if (hoursLeft > 0)
        {
            TimeBasedProgressBarProperties barProps = new()
            {
                HeaderKey = Lang.Get("extrainfo:TimeUntilTransition"),
                HoursTotal = props.InGameHours,
                HoursLeft = hoursLeft,

                ShowProgressBar = Config.ShowProgressPercent,
                ShowInGameTime = Config.ShowInGameTimeInfo
            };

            string verticalBlock = TimeFormatter.BuildTimeBlockPlusProgressBar(blockEntity.Api, barProps);

            dsc.AppendLine()
                .Append(verticalBlock);
        }
    }
}