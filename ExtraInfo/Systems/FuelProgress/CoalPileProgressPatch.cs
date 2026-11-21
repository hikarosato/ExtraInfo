using HarmonyLib;
using System.Text;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace ExtraInfo.Systems.FuelProgress;

[HarmonyPatch(typeof(BlockEntityItemPile), nameof(BlockEntityItemPile.GetBlockInfo))]
public static class CoalPileProgressPatch
{
    [HarmonyPostfix]
    public static void Postfix(BlockEntityItemPile __instance, StringBuilder dsc)
    {
        if (!ApplyTimersOrProgressBars()) return;
        if (!Config.ShowFuelProgress) return;

        if (__instance is not BlockEntityCoalPile coalPile) return;

        if (!coalPile.IsBurning) return;

        // AppendFuelProgress itself decides what to render (bar and/or in-game/real time) based on the same
        // config flags, so we should not short-circuit here on !ShowProgressBars - that would also hide the
        // remaining-time text even when the user only disabled the bar itself.
        dsc.AppendLine();
        InfoExtensions.AppendFuelProgress(dsc, coalPile, Lang.Get("extrainfo:Fuel"));
    }
}
