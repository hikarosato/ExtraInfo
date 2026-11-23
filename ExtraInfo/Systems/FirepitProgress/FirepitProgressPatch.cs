using HarmonyLib;
using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Vintagestory.API.Datastructures;

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
        ForecastResult? forecast = GetForecast(blockEntity);

        // 1. Total crafting progress
        if (totalItems > 0)
        {
            float maxTime = blockEntity.maxCookingTime();
            float totalMaxTime, totalRemaining;
            if (forecast?.SecondsToAllDone is float simAll)
            {
                // Simulation: includes warm-up, speed growth and SmeltedRatio
                totalRemaining = simAll;
                totalMaxTime = simAll + (outputCount * maxTime + blockEntity.inputStackCookingTime) / cookingSpeed;
            }
            else
            {
                totalMaxTime = totalItems * maxTime / cookingSpeed;
                totalRemaining = ((inputCount * maxTime) - blockEntity.inputStackCookingTime) / cookingSpeed;
            }

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
            float remaining = forecast?.SecondsToFirst
                ?? (blockEntity.maxCookingTime() - blockEntity.inputStackCookingTime) / cookingSpeed;
            float maxTime = remaining + blockEntity.inputStackCookingTime / cookingSpeed;

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

        if (Config.ShowHeatForecast) AppendHeatForecast(forecast, dsc);

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

    private sealed class ForecastResult
    {
        public float CurrentTemp;
        public float MeltingPoint;
        public float MaxReachableTemp;
        public bool Reachable;
        public float? SecondsToFirst;
        public float? SecondsToAllDone;
        public int SmeltsTotal;
        public int SmeltsDone;
        public bool FinishesOnResidualHeat;
    }

    private sealed class ForecastCache
    {
        public long Tick;
        public ForecastResult? Result;
    }

    private static readonly ConditionalWeakTable<BlockEntityFirepit, ForecastCache> forecastCache = new();

    private static ForecastResult? GetForecast(BlockEntityFirepit blockEntity)
    {
        // The simulation can take thousands of steps, so recompute at most once per second
        ForecastCache cache = forecastCache.GetOrCreateValue(blockEntity);
        long now = Environment.TickCount64;
        if (now - cache.Tick > 1000)
        {
            try
            {
                cache.Result = SimulateForecast(blockEntity);
            }
            catch (Exception)
            {
                // A forecast bug must never break the rest of the tooltip
                cache.Result = null;
            }
            cache.Tick = now;
        }
        return cache.Result;
    }

    private static void AppendHeatForecast(ForecastResult? r, StringBuilder dsc)
    {
        if (r == null) return;

        // Only while the item is still warming up: afterwards the times already say everything
        if (r.CurrentTemp < r.MeltingPoint)
        {
            dsc.AppendLine().Append(Lang.Get("extrainfo:firepit-forecast-temp", (int)r.CurrentTemp, (int)r.MeltingPoint));
        }

        if (!r.Reachable)
        {
            dsc.AppendLine().Append(Lang.Get("extrainfo:firepit-forecast-unreachable", (int)r.MaxReachableTemp));
            return;
        }

        if (r.SecondsToAllDone == null)
        {
            dsc.AppendLine().Append(Lang.Get("extrainfo:firepit-forecast-fuel-short", r.SmeltsDone, r.SmeltsTotal));
        }
        else if (r.FinishesOnResidualHeat)
        {
            dsc.AppendLine().Append(Lang.Get("extrainfo:firepit-forecast-residual"));
        }
    }

    /// <summary>
    /// Replays BlockEntityFirepit.OnBurnTick in 0.25 s steps: fuel timeline, furnace warm-up (changeTemperature),
    /// input heating (heatInput), natural cooling of the item and the cooking counter. When the fuel runs out the
    /// furnace cools down and the item keeps cooking on leftover heat, until the stack is done or nothing can heat it anymore.
    /// </summary>
    private static ForecastResult? SimulateForecast(BlockEntityFirepit be)
    {
        ItemSlot input = be.inputSlot;
        if (input == null || input.Empty) return null;

        // Same gate the game uses before cooking: no valid recipe / full output slot => the counter is reset every tick
        if (!be.canHeatInput()) return null;

        var world = be.Api.World;
        ItemStack stack = input.Itemstack;

        float meltingPoint = stack.Collectible.GetMeltingPoint(world, (ISlotProvider)be.Inventory, input);
        float maxCook = be.maxCookingTime();
        if (meltingPoint <= 0f || maxCook <= 0f) return null;

        // Fuel timeline: the burning item (if any), then the queued fuel, which the game lights by itself
        // while the firepit is burning or still smoldering (canIgniteFuel)
        var segments = new List<(float Seconds, float Temp)>();
        if (be.IsBurning) segments.Add((be.fuelBurnTime, be.maxTemperature));
        if (!be.fuelSlot.Empty && (be.IsBurning || be.canIgniteFuel))
        {
            ItemStack queued = be.fuelSlot.Itemstack;
            CombustibleProperties? qp = queued.Collectible.GetCombustibleProperties(world, queued, null);
            float qTemp = (int)((qp?.BurnTemperature ?? 0) * be.HeatModifier);
            float qSeconds = (qp?.BurnDuration ?? 0f) * be.BurnDurationModifier * queued.StackSize;
            if (qTemp > 0f && qSeconds > 0f) segments.Add((qSeconds, qTemp));
        }

        // Not burning and nothing to light: a forecast only makes sense while leftover heat can still cook the item
        if (segments.Count == 0 && be.InputStackTemp < meltingPoint && be.furnaceTemperature <= be.InputStackTemp) return null;

        // The item can also reach whatever temperature the furnace has right now
        float maxFuelTemp = be.furnaceTemperature;
        foreach (var s in segments) maxFuelTemp = Math.Max(maxFuelTemp, s.Temp);
        CombustibleProperties? props = stack.Collectible.GetCombustibleProperties(world, stack, null);

        // Same cap as in heatInput
        int inputMaxTemp = Math.Max(
            props?.MaxTemperature ?? 0,
            (stack.ItemAttributes?["maxTemperature"] != null) ? stack.ItemAttributes["maxTemperature"].AsInt() : 0);
        float reachable = inputMaxTemp > 0 ? Math.Min(inputMaxTemp, maxFuelTemp) : maxFuelTemp;

        // A pot/container is smelted as one batch; a plain stack consumes SmeltedRatio items per cycle
        bool container = be.otherCookingSlots.Length > 0;
        int ratio = container ? 1 : Math.Max(1, props?.SmeltedRatio ?? 1);
        int smeltsTotal = container ? 1 : stack.StackSize / ratio;
        if (smeltsTotal <= 0) return null;

        var result = new ForecastResult
        {
            CurrentTemp = be.InputStackTemp,
            MeltingPoint = meltingPoint,
            MaxReachableTemp = reachable,
            // A hot item can still cook on stored heat even when the fuel itself is too weak
            Reachable = reachable >= meltingPoint || be.InputStackTemp >= meltingPoint,
            SmeltsTotal = smeltsTotal,
        };
        if (!result.Reachable) return segments.Count == 0 ? null : result;

        // The item cools on its own, but only after a 0.5 h (in-game) delay since it was last heated
        ItemStack coolStack = stack;
        if (container)
        {
            foreach (ItemSlot cs in be.otherCookingSlots)
            {
                if (!cs.Empty) { coolStack = cs.Itemstack; break; }
            }
        }
        var tempTree = coolStack.Attributes?["temperature"] as ITreeAttribute;
        float cooldownSpeed = tempTree?.GetFloat("cooldownSpeed", 120f) ?? 120f;
        float coolDelay = tempTree == null
            ? 0f
            : (float)Math.Max(0.0, tempTree.GetDouble("temperatureLastUpdate") - world.Calendar.TotalHours);
        float hoursPerSecond = world.Calendar.SpeedOfTime * world.Calendar.CalendarSpeedMul / 3600f;

        const float dt = 0.25f;
        const int maxSteps = 43200; // 43200 steps * 0.25 s = three hours of simulated time
        float dtHours = dt * hoursPerSecond;
        float environmentTemp = be.enviromentTemperature();

        float furnace = be.furnaceTemperature;
        float itemTemp = be.InputStackTemp;
        float cook = be.inputStackCookingTime;
        int remaining = stack.StackSize;
        int seg = 0;
        bool fuelOver = segments.Count == 0;
        float segLeft = fuelOver ? 0f : segments[0].Seconds;
        float t = 0f;

        for (int step = 0; step < maxSteps; step++)
        {
            if (!fuelOver)
            {
                segLeft -= dt;
                if (segLeft <= 0f)
                {
                    seg++;
                    if (seg >= segments.Count) fuelOver = true; // from now on the furnace only cools down
                    else segLeft = segments[seg].Seconds;
                }
            }
            t += dt;

            furnace = be.changeTemperature(furnace, fuelOver ? environmentTemp : segments[seg].Temp, dt);

            // Natural cooling of the item (mirrors CollectibleObject.GetTemperature)
            if (coolDelay > 0f)
            {
                coolDelay -= dtHours;
            }
            else if (itemTemp > 0f)
            {
                itemTemp = Math.Max(0f, itemTemp - cooldownSpeed * Math.Max(1f, itemTemp / 200f) * dtHours);
            }

            // heatInput
            if (itemTemp < furnace)
            {
                float f = (1f + Math.Clamp((furnace - itemTemp) / 30f, 0f, 1.6f)) * dt;
                if (itemTemp >= meltingPoint) f /= 11f;
                float newTemp = be.changeTemperature(itemTemp, furnace, f);
                newTemp = (newTemp + (remaining - 1f) * itemTemp) / remaining;
                if (inputMaxTemp > 0) newTemp = Math.Min(inputMaxTemp, newTemp);
                if (newTemp > itemTemp) coolDelay = 0.5f; // SetTemperature delays the cooldown when heating
                itemTemp = newTemp;
            }

            if (itemTemp >= meltingPoint)
            {
                cook += Math.Clamp((int)(itemTemp / meltingPoint), 1, 30) * dt;
            }
            else if (cook > 0f)
            {
                cook -= dt * 10f; // the game subtracts 1 per 0.1 s tick
            }

            if (cook > maxCook)
            {
                result.SmeltsDone++;
                if (result.SmeltsDone == 1) result.SecondsToFirst = t;
                remaining -= ratio;
                cook = 0f;
                if (result.SmeltsDone >= smeltsTotal || remaining < ratio)
                {
                    result.SecondsToAllDone = t;
                    result.FinishesOnResidualHeat = fuelOver;
                    break;
                }
            }

            // Fuel is over, the item is below the melting point and nothing can heat it anymore
            if (fuelOver && itemTemp < meltingPoint && furnace <= itemTemp) break;
        }

        return result;
    }
}