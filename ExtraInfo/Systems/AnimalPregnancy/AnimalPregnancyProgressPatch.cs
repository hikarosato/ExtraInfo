using HarmonyLib;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace ExtraInfo.Systems.AnimalPregnancyProgress;

[HarmonyPatch(typeof(EntityBehaviorMultiply), nameof(EntityBehaviorMultiply.GetInfoText))]
public static class AnimalPregnancyProgressPatch
{
    [HarmonyPostfix]
    public static void Postfix(EntityBehaviorMultiply __instance, StringBuilder infotext)
    {
        if (!ApplyTimersOrProgressBars()) return;
        if (!Config.ShowAnimalPregnancyProgress) return;

        ICoreAPI api = __instance.entity.World.Api;
        float hoursPerDay = api.World.Calendar.HoursPerDay;
        var (pregnancyDays, cooldownMax) = ResolveMultiply(__instance);

        if (__instance.IsPregnant)
        {
            double daysNow = api.World.Calendar.TotalDays;
            double daysLeft = pregnancyDays - (daysNow - __instance.TotalDaysPregnancyStart);

            double hoursLeft = daysLeft * hoursPerDay;
            double totalDurationHours = pregnancyDays * hoursPerDay;

            if (hoursLeft <= 0)
            {
                infotext.AppendLine(Lang.Get("extrainfo:animal-giving-birth-soon"));
            }
            else
            {
                infotext.AppendLine().Append(BuildBar(api, "extrainfo:animal-will-give-birth-in", totalDurationHours, hoursLeft));
            }
        }
        else
        {
            double daysNow = api.World.Calendar.TotalDays;
            double cooldownLeft = __instance.TotalDaysCooldownUntil - daysNow;

            if (cooldownLeft > 0)
            {
                double totalCooldownHours = cooldownMax * hoursPerDay;
                double hoursLeft = cooldownLeft * hoursPerDay;

                infotext.AppendLine().Append(BuildBar(api, "extrainfo:animal-ready-to-mate-in", totalCooldownHours, hoursLeft));
            }
        }
    }

    private static string BuildBar(ICoreAPI api, string headerKey, double totalHours, double hoursLeft)
    {
        TimeBasedProgressBarProperties barProps = new()
        {
            HeaderKey = Lang.Get(headerKey),
            HoursTotal = totalHours,
            HoursLeft = hoursLeft,
            ShowProgressBar = Config.ShowProgressPercent,
            ShowInGameTime = Config.ShowInGameTimeInfo
        };
        return TimeFormatter.BuildTimeBlockPlusProgressBar(api, barProps);
    }

    private static readonly Dictionary<string, (double Pregnancy, double CooldownMax)> VanillaMultiply = new()
    {
        ["sheep"] = (20, 11),
        ["goat"] = (20, 11),
        ["pig"] = (25, 11),
        ["hare"] = (5, 5),
        ["fox"] = (5, 5),
    };

    private static (double PregnancyDays, double CooldownMax) ResolveMultiply(EntityBehaviorMultiply b)
    {
        JsonObject[] server = b.entity.Properties.Server?.BehaviorsAsJsonObj;
        if (server != null)
        {
            foreach (var cfg in server)
            {
                if (cfg["code"].AsString() == "multiply" && cfg["pregnancyDays"].Exists)
                {
                    return (cfg["pregnancyDays"].AsDouble(b.PregnancyDays),
                            cfg["multiplyCooldownDaysMax"].AsDouble(b.MultiplyCooldownDaysMax));
                }
            }
        }

        string path = b.entity.Code.Path;
        int dash = path.IndexOf('-');
        string species = dash > 0 ? path.Substring(0, dash) : path;

        if (VanillaMultiply.TryGetValue(species, out var v)) return v;

        return (b.PregnancyDays, b.MultiplyCooldownDaysMax);
    }
}