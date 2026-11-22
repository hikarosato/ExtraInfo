using HarmonyLib;
using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace ExtraInfo.Systems.FirepitProgress;

[HarmonyPatch(typeof(BlockEntityOpenableContainer), nameof(BlockEntityOpenableContainer.GetBlockInfo))]
public static class FirepitProgressPatch
{
    [HarmonyPostfix]
    public static void Postfix(BlockEntityOpenableContainer __instance, StringBuilder dsc)
    {
        if (!ApplyTimersOrProgressBars()) return;
        if (!Config.ShowFirepitProgress) return;
        if (__instance is not BlockEntityFirepit blockEntity) return;

        int inputCount = blockEntity.inputSlot?.Itemstack?.StackSize ?? 0;
        int outputCount = blockEntity.outputSlot?.Itemstack?.StackSize ?? 0;
        int totalItems = inputCount + outputCount;

        // The game advances the cooking counter by clamp((int)(temp / meltingPoint), 1, 30) per second,
        // so counter units must be divided by that speed to get real seconds.
        float cookingSpeed = GetCookingSpeed(blockEntity);

        // 1. Total crafting progress
        if (totalItems > 0)
        {
            float maxTime = blockEntity.maxCookingTime();
            float totalMaxTime = totalItems * maxTime / cookingSpeed;
            float totalRemaining = ((inputCount * maxTime) - blockEntity.inputStackCookingTime) / cookingSpeed;

            var stackProps = new TimeBasedProgressBarProperties
            {
                HeaderKey = Lang.Get("extrainfo:firepit-progress-smeltable-total") + " " + (blockEntity.inputSlot.Empty ? Lang.Get("{0}x {1}", blockEntity.outputSlot.StackSize, blockEntity.outputSlot.Itemstack.GetName()) : Lang.Get("{0}x {1}", totalItems, blockEntity.inputStack.GetName())),
                HoursTotal = totalMaxTime,
                HoursLeft = Math.Max(0, totalRemaining),
                ShowInGameTime = Config.ShowInGameTimeInfo,
                IsRawSeconds = true,

                ShowProgressBar = Config.ShowProgressPercent,
            };

            dsc.AppendLine().Append(TimeFormatter.BuildTimeBlockPlusProgressBar(blockEntity.Api, stackProps, reversed: false));
        }

        // 2. Current crafting progress
        if (blockEntity.inputSlot != null && !blockEntity.inputSlot.Empty)
        {
            float maxTime = blockEntity.maxCookingTime() / cookingSpeed;
            float remaining = (blockEntity.maxCookingTime() - blockEntity.inputStackCookingTime) / cookingSpeed;

            if (maxTime > 0 && remaining > 0)
            {
                var itemProps = new TimeBasedProgressBarProperties
                {
                    HeaderKey = Lang.Get("extrainfo:firepit-progress-smeltable-current") + " " + blockEntity.inputStack.GetName(),
                    HoursTotal = maxTime,
                    HoursLeft = remaining,
                    ShowInGameTime = Config.ShowInGameTimeInfo,
                    IsRawSeconds = true,

                    ShowProgressBar = Config.ShowProgressPercent,
                };

                dsc.AppendLine().Append(TimeFormatter.BuildTimeBlockPlusProgressBar(blockEntity.Api, itemProps, reversed: false));
            }
        }

        // 3. Fuel progress
        if (Config.ShowFuelProgress && blockEntity.IsBurning)
        {
            // The game burns fuel slower when the firepit is idle and already at its max temperature.
            // Mirrors the condition in BlockEntityFirepit.OnBurnTick, so shown times are real seconds.
            bool lowFuelConsumption = Math.Abs(blockEntity.furnaceTemperature - blockEntity.maxTemperature) < 50f && blockEntity.inputSlot.Empty;
            float burnTimeMul = lowFuelConsumption ? blockEntity.emptyFirepitBurnTimeMulBonus : 1f;

            // Total fuel: the item burning right now plus every item still waiting in the fuel slot
            if (!blockEntity.fuelSlot.Empty)
            {
                ItemStack queuedFuel = blockEntity.fuelSlot.Itemstack;
                CombustibleProperties? queuedProps = queuedFuel.Collectible.GetCombustibleProperties(blockEntity.Api.World, queuedFuel, null);

                // Queued fuel may differ from the burning one, so take its own duration
                float burnPerQueuedItem = (queuedProps?.BurnDuration ?? 0f) * blockEntity.BurnDurationModifier;
                if (burnPerQueuedItem <= 0f) burnPerQueuedItem = blockEntity.maxFuelBurnTime;

                float queuedBurn = burnPerQueuedItem * queuedFuel.StackSize;

                var totalFuelProps = new TimeBasedProgressBarProperties
                {
                    HeaderKey = Lang.Get("extrainfo:firepit-progress-fuel-total"),
                    HoursTotal = (blockEntity.maxFuelBurnTime + queuedBurn) * burnTimeMul,
                    HoursLeft = (blockEntity.fuelBurnTime + queuedBurn) * burnTimeMul,
                    ShowInGameTime = Config.ShowInGameTimeInfo,
                    ShowProgressBar = false,
                    IsRawSeconds = true,

                };
                dsc.AppendLine().Append(TimeFormatter.BuildTimeBlockPlusProgressBar(blockEntity.Api, totalFuelProps, reversed: true));
            }

            // Current fuel
            var fuelProps = new TimeBasedProgressBarProperties
            {
                HeaderKey = Lang.Get("extrainfo:firepit-progress-fuel-current"),
                HoursTotal = blockEntity.maxFuelBurnTime * burnTimeMul,
                HoursLeft = blockEntity.fuelBurnTime * burnTimeMul,
                ShowInGameTime = Config.ShowInGameTimeInfo,
                IsRawSeconds = true,

                ShowProgressBar = Config.ShowProgressPercent,
            };
            dsc.AppendLine().Append(TimeFormatter.BuildTimeBlockPlusProgressBar(blockEntity.Api, fuelProps, reversed: true));
        }
    }

    /// <summary>
    /// Mirrors BlockEntityFirepit.heatInput: while the input is at or above its melting point the cooking
    /// counter grows by clamp((int)(temp / meltingPoint), 1, 30) per second. Below it the counter does not
    /// grow at all, so speed 1 is used as the best-case projection. This is a projection for the current
    /// temperature: the real speed rises as the input keeps heating up.
    /// </summary>
    private static float GetCookingSpeed(BlockEntityFirepit blockEntity)
    {
        ItemSlot? slot = blockEntity.inputSlot;
        if (slot == null || slot.Empty) return 1f;

        float meltingPoint = slot.Itemstack.Collectible.GetMeltingPoint(blockEntity.Api.World, (ISlotProvider)blockEntity.Inventory, slot);
        if (meltingPoint <= 0f) return 1f;

        float temp = blockEntity.InputStackTemp;
        if (temp < meltingPoint) return 1f;

        return Math.Clamp((int)(temp / meltingPoint), 1, 30);
    }
}