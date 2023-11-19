using BepInEx;
using JetBrains.Annotations;

namespace CactusPie.ContainerQuickLoot
{
    [BepInPlugin("com.cactuspie.containerquikloot", "CactusPie.ContainerQuickLoot", "1.0.0")]
    public class ContainerQuickLootPlugin : BaseUnityPlugin
    {
        [UsedImplicitly]
        internal void Start()
        {
            new QuickTransferPatch().Enable();
        }
    }
}
