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

        [PatchPrefix]
        public static bool PatchPrefix(
            ref GStruct375<GInterface275> __result,
            object __instance,
            Item item,
            TraderControllerClass controller,
            IEnumerable<LootItemClass> targets,
            GClass2585.EMoveItemOrder order,
            bool simulate)
        {
            // If is ctrl+click loot
            if (order == GClass2585.EMoveItemOrder.MoveToAnotherSide)
            {
                if (!ContainerQuickLootPlugin.EnableForCtrlClick.Value)
                {
                    return true;
                }
            }
            // If is loose loot pick up
            else if (order == GClass2585.EMoveItemOrder.PickUp && controller.OwnerType == EOwnerType.Profile)
            {
                if (!ContainerQuickLootPlugin.EnableForLooseLoot.Value)
                {
                    return true;
                }
            }
            else
            {
                return true;
            }
            
            GameWorld gameWorld = Singleton<GameWorld>.Instance;

            // If gameWorld is null that means the game is currently not in progress, for instance you're in your hideout
            if (gameWorld == null)
            {
                return true;
            }
            
            // This check needs to be done only in game - otherwise we will not be able to receive quest rewards!
            if (item.QuestItem)
            {
                return true;
            }
                
            Player player = GetLocalPlayerFromWorld(gameWorld);
            var inventory = (Inventory)typeof(Player).GetProperty("Inventory", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(player);
                
            if (inventory == null)
            {
                return true;
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
                return true;
            }

            foreach (IContainer collectionContainer in containerCollection.Containers)
            {
                if (!(collectionContainer is GClass2318 container))
                {
                    return true;
                }
                    
                // ReSharper disable once PossibleMultipleEnumeration
                if (!(targets.SingleOrDefaultWithoutException() is EquipmentClass))
                {
                    continue;
                }

                if (ContainerQuickLootPlugin.AutoMergeStacks.Value && item.StackMaxSize > 1 && item.StackObjectsCount != item.StackMaxSize)
                {
                    foreach (KeyValuePair<Item, LocationInGrid> containedItem in container.ContainedItems)
                    {
                        if (containedItem.Key.Template._id != item.Template._id)
                        {
                            continue;
                        }
                        
                        if (containedItem.Key.StackObjectsCount + item.StackObjectsCount > item.StackMaxSize)
                        {
                            continue;
                        }
                        
                        GStruct375<GClass2599> mergeResult = GClass2585.Merge(item, containedItem.Key, controller, simulate);
                        __result = new GStruct375<GInterface275>(mergeResult.Value);
                        return false;
                    }
                }
                

                GClass2580 location = container.FindLocationForItem(item);
                if (location == null)
                {
                    continue;
                }

                GStruct375<GClass2597> moveResult = GClass2585.Move(item, location, controller, simulate);
                if (moveResult.Failed)
                {
                    return true;
                }

                if (!moveResult.Value.ItemsDestroyRequired)
                {
                    __result = moveResult.Cast<GClass2597, GInterface275>();
                }
                    
                return false;
            }

            return true;
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
