using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Aki.Reflection.Patching;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;

namespace CactusPie.ContainerQuickLoot
{
    public class QuickTransferPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            MethodInfo method = typeof(GClass2585).GetMethod("QuickFindAppropriatePlace", BindingFlags.Public | BindingFlags.Static);
            return method;
        }

        [PatchPostfix]
        public static void PatchPostfix(
            ref GStruct375<GInterface275> __result,
            object __instance,
            Item item,
            TraderControllerClass controller,
            IEnumerable<LootItemClass> targets,
            GClass2585.EMoveItemOrder order,
            bool simulate)
        {
            if (order == GClass2585.EMoveItemOrder.MoveToAnotherSide)
            {
                GameWorld gameWorld = Singleton<GameWorld>.Instance;

                if (gameWorld == null)
                {
                    return;
                }
                
                Player player = GetLocalPlayerFromWorld(gameWorld);
                var inventory = (Inventory)typeof(Player).GetProperty("Inventory", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(player);
                
                if (inventory == null)
                {
                    return;
                }

                ContainerCollection containerCollection = null;

                Item targetContainer = inventory.Equipment.GetAllItems().FirstOrDefault(x =>
                    x.IsContainer && 
                    x.TryGetItemComponent(out TagComponent tagComponent) &&
                    tagComponent.Name.Contains("@loot") &&
                    (containerCollection = x as ContainerCollection) != null &&
                    containerCollection.Containers.Any(y => y.CanAccept(item)));

                if (targetContainer == null || containerCollection == null)
                {
                    return;
                }

                foreach (IContainer collectionContainer in containerCollection.Containers)
                {
                    var container = collectionContainer as GClass2318;

                    if (container == null)
                    {
                        return;
                    }
                    
                    // ReSharper disable once PossibleMultipleEnumeration
                    if (!(targets.SingleOrDefaultWithoutException() is EquipmentClass))
                    {
                        continue;
                    }

                    GClass2580 location = container.FindLocationForItem(item);
                    if (location == null)
                    {
                        continue;
                    }

                    GStruct375<GClass2597> moveResult = GClass2585.Move(item, location, controller, simulate);
                    if (moveResult.Failed)
                    {
                        return;
                    }

                    if (!moveResult.Value.ItemsDestroyRequired)
                    {
                        __result = moveResult.Cast<GClass2597, GInterface275>();
                    }

                    return;
                }
            }
        }
        
        private static Player GetLocalPlayerFromWorld(GameWorld gameWorld)
        {
            if (gameWorld == null || gameWorld.MainPlayer == null)
            {
                return null;
            }

            return gameWorld.MainPlayer;
        }
    }
}
