using HarmonyLib;
using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ExtraInfo.Systems.BeehiveKilnProgress;

[HarmonyPatch(typeof(BlockEntityBeeHiveKiln), nameof(BlockEntityBeeHiveKiln.GetBlockInfo))]
public static class BeehiveKilnProgressPatch
{
    [HarmonyPostfix]
    public static void Postfix(BlockEntityBeeHiveKiln __instance, IPlayer forPlayer, StringBuilder dsc,
        bool ___receivesHeat, BEBehaviorDoor ___beBehaviorDoor, BlockPos[] ___particlePositions)
    {
        if (!ApplyTimersOrProgressBars()) return;
        if (!Config.ShowBeehiveKilnProgress) return;
        if (___particlePositions == null) return;

        // __instance.TotalHoursHeatReceived is the kiln's own wear counter (it resets only after 168 hours),
        // NOT the progress of the items inside. The real progress is stored per item in the stack attribute
        // "hoursHeatReceived", so the slowest item in the kiln decides when the whole batch is finished.
        if (!TryGetBatchProgress(__instance, ___particlePositions, out double hoursLeft, out double hoursTotal)) return;

        bool isProcessing = ___receivesHeat && __instance.StructureComplete && (___beBehaviorDoor == null || !___beBehaviorDoor.Opened);

        var props = new TimeBasedProgressBarProperties()
        {
            HeaderKey = Lang.Get(isProcessing ? "extrainfo:WillFinishIn" : "extrainfo:beehivekiln-paused-left"),
            HoursTotal = hoursTotal,
            HoursLeft = hoursLeft,

            ShowProgressBar = Config.ShowProgressBars,
            ShowRealTime = Config.ShowRealTimeInfo,
            ShowInGameTime = Config.ShowInGameTimeInfo
        };

        string verticalBlock = TimeFormatter.BuildTimeBlockPlusProgressBar(__instance.Api, props);

        dsc.AppendLine().Append(verticalBlock);
    }

    /// <summary>
    /// Walks the same ground storages as BlockEntityBeeHiveKiln.UpdateGroundStorage (3 levels above each of the
    /// 9 heated positions) and finds the item that will be done last.
    /// </summary>
    private static bool TryGetBatchProgress(BlockEntityBeeHiveKiln kiln, BlockPos[] positions, out double hoursLeft, out double hoursTotal)
    {
        hoursLeft = 0;
        hoursTotal = 0;
        bool found = false;

        ICoreAPI api = kiln.Api;
        double burnHours = BlockEntityBeeHiveKiln.ItemBurnTimeHours;
        double burnTemp = BlockEntityBeeHiveKiln.ItemBurnTemperature;
        double gainPerHour = BlockEntityBeeHiveKiln.ItemTemperatureGainPerHour;

        // The game only looks at the first 9 positions (the 10th one is the smoke particle position)
        int count = Math.Min(9, positions.Length);
        for (int j = 0; j < count; j++)
        {
            for (int i = 1; i < 4; i++)
            {
                BlockPos pos = positions[j].UpCopy(i);
                BlockEntityGroundStorage? groundStorage = api.World.BlockAccessor.GetBlockEntity<BlockEntityGroundStorage>(pos);
                if (groundStorage == null) continue;

                for (int index = 0; index < groundStorage.Inventory.Count; index++)
                {
                    ItemSlot slot = groundStorage.Inventory[index];
                    if (slot.Empty) continue;

                    ItemStack stack = slot.Itemstack;
                    if (!IsFiredInKiln(api, stack)) continue;

                    double received = stack.Attributes.GetFloat("hoursHeatReceived");
                    double left = Math.Max(0, burnHours - received);
                    if (left <= 0) continue;

                    // Heat counts only once the item is at the burn temperature, so add the time it still needs to heat up
                    double temp = stack.Collectible.GetTemperature(api.World, stack);
                    double heatingUp = temp < burnTemp ? (burnTemp - temp) / gainPerHour : 0;

                    double itemLeft = left + heatingUp;
                    if (!found || itemLeft > hoursLeft)
                    {
                        hoursLeft = itemLeft;
                        hoursTotal = burnHours + heatingUp;
                        found = true;
                    }
                }
            }
        }

        return found;
    }

    /// <summary>
    /// Same eligibility check as in BlockEntityBeeHiveKiln.UpdateGroundStorage.
    /// </summary>
    private static bool IsFiredInKiln(ICoreAPI api, ItemStack stack)
    {
        CollectibleObject collectible = stack.Collectible;
        CombustibleProperties? combustible = collectible.GetCombustibleProperties(api.World, stack, null);

        if (combustible != null)
        {
            if (combustible.SmeltingType == EnumSmeltType.Fire) return true;
            if (combustible.SmeltedStack?.ResolvedItemstack?.Block?.BlockMaterial == EnumBlockMaterial.Ceramic) return true;
        }

        return collectible.Attributes?["beehivekiln"].Exists ?? false;
    }
}