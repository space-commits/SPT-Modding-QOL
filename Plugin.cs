using BepInEx;
using BepInEx.Configuration;

namespace ModdingQOL
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        public static ConfigEntry<bool> RequireMultiTool { get; set; }


        private void Awake()
        {
            RequireMultiTool = Config.Bind<bool>("Options", "Require Multi Tool", true, new ConfigDescription("Require Multi Tool For Removing Vital Parts In Raid.", null, new ConfigurationManagerAttributes { Order = 1 }));

            new ItemSpecificationPanelPatch().Enable();
            new CanBeMovedPatch().Enable();
            new CanDetatchPatch().Enable();
            new CanAttachPatch().Enable();
            new ThrowItemPatch().Enable();
            new InteractPatch().Enable();
            new ModdingScreenPatch().Enable();

            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        }
    }
}
