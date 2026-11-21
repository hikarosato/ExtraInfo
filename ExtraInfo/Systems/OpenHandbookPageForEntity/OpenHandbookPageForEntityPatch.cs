using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace ExtraInfo.Systems.OpenHandbookPageForEntity;

[HarmonyPatch(typeof(ModSystemSurvivalHandbook), methodName: "OnSurvivalHandbookHotkey")]
public static class OpenHandbookPageForEntityPatch
{
    [HarmonyPrefix]
    public static bool Prefix(ModSystemSurvivalHandbook __instance, ref bool __result, GuiDialogHandbook ___dialog, ICoreClientAPI ___capi)
    {
        if (Config?.OpenHandbookPageForEntity != true) return true;
        if (___capi?.World?.Player?.CurrentEntitySelection == null) return true;
        if (___dialog == null) return true;

        Entity entity = ___capi.World.Player.CurrentEntitySelection.Entity;
        if (entity == null) return true;

        ItemStack? stack = ___capi.World.GetEntityType(entity.Code)?.GetCreatureStack(___capi);
        if (stack == null)
        {
            // No handbook page for this creature - fall through to vanilla behavior.
            return true;
        }

        // OpenDetailPageFor() does NOT open the dialog itself, only navigates inside it. So we have to
        // open the dialog first (if it was closed). Otherwise the page navigation is silent.
        if (!___dialog.IsOpened())
        {
            ___dialog.TryOpen();
            ___dialog.ignoreNextKeyPress = true;
        }

        if (!___dialog.OpenDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(stack)))
        {
            ___dialog.OpenDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(new ItemStack(stack.Collectible)));
        }

        // We opened the dialog and navigated to the entity's page. Returning true would let the vanilla
        // OnSurvivalHandbookHotkey run again, which sees the dialog already opened and *closes* it.
        // Returning false skips the original and keeps the dialog open on the entity's page.
        __result = false;
        return false;
    }
}
