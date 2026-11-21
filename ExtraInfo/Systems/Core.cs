global using static ExtraInfo.InfoExtensions;
global using static ExtraInfo.Systems.Core;
using ExtraInfo.Configuration;
using HarmonyLib;
using Vintagestory.API.Common;

namespace ExtraInfo.Systems;

public class Core : ModSystem
{
    private Harmony? _harmony;
    public Harmony HarmonyInstance => _harmony ??= new Harmony(Mod.Info.ModID);

#nullable disable
    public static Config Config { get; set; }
#nullable enable

    public override void StartPre(ICoreAPI api)
    {
        Config = ModConfig.ReadConfig(api);

        if (api.ModLoader.IsModEnabled("configlib"))
        {
            _ = new ConfigLibCompatibility(api);
        }

        HarmonyInstance.PatchAllUncategorized();
        Mod.Logger.Event("started mod");
    }

    public override void Dispose()
    {
        // Only unpatch if we actually patched in this instance.
        if (_harmony != null)
        {
            _harmony.UnpatchAll(_harmony.Id);
        }
    }
}
