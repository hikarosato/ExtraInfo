using HarmonyLib;
using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ExtraInfo.Systems.CokeOvenProgress;

[HarmonyPatch(typeof(Block), nameof(Block.GetPlacedBlockInfo))]
public static class CokeOvenProgressPatch
{
    [HarmonyPostfix]
    public static void Postfix(ref string __result, IWorldAccessor world, BlockPos pos)
    {
        if (!ApplyTimersOrProgressBars()) return;
        if (!Config.ShowCokeOvenProgress) return;
        if (world.BlockAccessor.GetBlock(pos) is not BlockCokeOvenDoor) return;

        BlockPos[] positions = [pos.NorthCopy(), pos.EastCopy(), pos.SouthCopy(), pos.WestCopy()];

        foreach (BlockPos neighborPos in positions)
        {
            if (world.BlockAccessor.GetBlockEntity(neighborPos) is not BlockEntityCoalPile blockEntity) continue;

            if (!blockEntity.IsBurning) continue;

            // Same correct formula as InfoExtensions.AppendFuelProgress: use the actual stack size, not Layers.
            // The game removes one item at a time every BurnHoursPerLayer / 2 in-game hours, so the total burn
            // time for the current pile is StackSize * BurnHoursPerLayer / 2 (which is only equal to
            // Layers * BurnHoursPerLayer when the stack size is even).
            int stackSize = blockEntity.OwnStackSize;
            if (stackSize <= 0) continue;

            double hoursPerItem = blockEntity.BurnHoursPerLayer / 2.0;
            double totalBurnHours = stackSize * hoursPerItem;
            double burnStart = blockEntity.GetField<double>("burnStartTotalHours");
            double hoursElapsed = Math.Max(0, world.Calendar.TotalHours - burnStart);
            double hoursRemaining = Math.Max(0, totalBurnHours - hoursElapsed);

            StringBuilder sb = new(__result);
            sb.AppendLine().AppendLine();

            TimeBasedProgressBarProperties barProps = new()
            {
                HeaderKey = Lang.Get("extrainfo:WillFinishIn"),
                HoursTotal = totalBurnHours,
                HoursLeft = hoursRemaining,

                ShowProgressBar = Config.ShowProgressBars,
                ShowRealTime = Config.ShowRealTimeInfo,
                ShowInGameTime = Config.ShowInGameTimeInfo
            };

            string verticalBlock = TimeFormatter.BuildTimeBlockPlusProgressBar(world.Api, barProps);
            sb.Append(verticalBlock);

            __result = sb.ToString().TrimEnd();
            break;
        }
    }
}
