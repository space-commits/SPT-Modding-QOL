using BepInEx;

namespace ModdingQOL
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            new ItemSpecificationPanelPatch().Enable();
            new CanBeMovedPatch().Enable();
            new CanDetatchPatch().Enable();
            new CanAttachPatch().Enable();
            new ThrowItemPatch().Enable();
            new InteractPatch().Enable();

            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        }
    }
}
