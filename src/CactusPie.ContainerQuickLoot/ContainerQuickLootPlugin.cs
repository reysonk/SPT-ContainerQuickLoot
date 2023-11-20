using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using JetBrains.Annotations;

namespace CactusPie.ContainerQuickLoot
{
    [BepInPlugin("com.cactuspie.containerquikloot", "CactusPie.ContainerQuickLoot", "1.0.0")]
    public class ContainerQuickLootPlugin : BaseUnityPlugin
    {
        internal static ConfigEntry<bool> EnableForCtrlClick { get; set; }
        
        internal static ConfigEntry<bool> EnableForLooseLoot { get; set; }
        
        [UsedImplicitly]
        internal void Start()
        {
            const string sectionName = "Container quick loot setting";
            
            EnableForCtrlClick = Config.Bind
            (
                sectionName,
                "Enable for Ctrl+click",
                true,
                new ConfigDescription
                (
                    "Automatically put the items in containers while transferring them with ctrl+click"
                )
            );
                
            EnableForLooseLoot = Config.Bind
            (
                sectionName,
                "Enable for loose loot",
                true,
                new ConfigDescription
                (
                    "Automatically put loose loot in containers"
                )
            );

            new QuickTransferPatch().Enable();
        }
    }
}
