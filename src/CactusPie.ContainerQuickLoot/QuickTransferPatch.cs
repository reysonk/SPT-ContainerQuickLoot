using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Aki.Reflection.Patching;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;

namespace CactusPie.ContainerQuickLoot
{
    public class QuickTransferPatch : ModulePatch
    {
        private static readonly Regex LootTagRegex = new Regex("@loot[0-9]*", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

        protected override MethodBase GetTargetMethod()
        {
            MethodInfo method = typeof(InteractionsHandlerClass).GetMethod("QuickFindAppropriatePlace", BindingFlags.Public | BindingFlags.Static);
            return method;
        }

        [PatchPrefix]
        public static bool PatchPrefix(
            ref GStruct414<GInterface324> __result,
            object __instance,
            Item item,
            TraderControllerClass controller,
            IEnumerable<LootItemClass> targets,
            InteractionsHandlerClass.EMoveItemOrder order,
            bool simulate)
        {
            Inventory inventory;

            // If is ctrl+click loot
            if (order == InteractionsHandlerClass.EMoveItemOrder.MoveToAnotherSide)
            {
                if (!ContainerQuickLootPlugin.EnableForCtrlClick.Value)
                {
                    return true;
                }
            }
            // If is loose loot pick up
            else if (order == InteractionsHandlerClass.EMoveItemOrder.PickUp && controller.OwnerType == EOwnerType.Profile)
            {
                if (!ContainerQuickLootPlugin.EnableForLooseLoot.Value)
                {
                    if (!TryGetInventory(out inventory))
                    {
                        return true;
                    }

                    return !TryMergeItemIntoAnExistingStack(item, inventory, controller, simulate, ref __result);
                }
            }
            else
            {
                return true;
            }

            // This check needs to be done only in game - otherwise we will not be able to receive quest rewards!
            if (item.QuestItem)
            {
                return true;
            }

            if (!TryGetInventory(out inventory))
            {
                return true;
            }

            IEnumerable<EFT.InventoryLogic.IContainer> targetContainers = FindTargetContainers(item, inventory);

            foreach (EFT.InventoryLogic.IContainer collectionContainer in targetContainers)
            {
                if (!(collectionContainer is StashGridClass container))
                {
                    return !TryMergeItemIntoAnExistingStack(item, inventory, controller, simulate, ref __result);
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

                        GStruct414<GClass2788> mergeResult = InteractionsHandlerClass.Merge(item, containedItem.Key, controller, simulate);
                        __result = new GStruct414<GInterface324>(mergeResult.Value);
                        return false;
                    }
                }

                GClass2769 location = container.FindLocationForItem(item);
                if (location == null)
                {
                    continue;
                }

                GStruct414<GClass2786> moveResult = InteractionsHandlerClass.Move(item, location, controller, simulate);
                if (moveResult.Failed)
                {
                    return true;
                }

                if (!moveResult.Value.ItemsDestroyRequired)
                {
                    __result = moveResult.Cast<GClass2786, GInterface324>();
                }

                return false;
            }

            return !TryMergeItemIntoAnExistingStack(item, inventory, controller, simulate, ref __result);
        }

        private static bool TryGetInventory(out Inventory inventory)
        {
            GameWorld gameWorld = Singleton<GameWorld>.Instance;

            // If gameWorld is null that means the game is currently not in progress, for instance you're in your hideout
            //start 3.7 hideout gameWorld nolonger be null.So need to use getlocationId
            if (gameWorld == null || gameWorld.LocationId == null)
            {
                inventory = null;
                // Logger.LogError("在这里代表gameworld==null");
                return false;
            }
            
            Player player = GetLocalPlayerFromWorld(gameWorld);
            // inventory = (Inventory)typeof(Player)
            // .GetProperty("Inventory", BindingFlags.NonPublic | BindingFlags.Instance)
            // ?.GetValue(player);
            inventory = (Inventory)typeof(Player).GetProperty("Inventory")?.GetValue(player);

            if (inventory == null)
            {
                // Logger.LogError("在这里表示inventory==null");
                return false;
            }

            return true;
        }

        private static IEnumerable<EFT.InventoryLogic.IContainer> FindTargetContainers(Item item, Inventory inventory)
        {
            var matchingContainerCollections = new List<(ContainerCollection containerCollection, int priority)>();

            foreach (Item inventoryItem in inventory.Equipment.GetAllItems())
            {
                // It has to be a container collection - an item that we can transfer the loot into
                if (!inventoryItem.IsContainer)
                {
                    continue;
                }

                // The container has to have a tag - later we will check it's the @loot tag
                if (!inventoryItem.TryGetItemComponent(out TagComponent tagComponent))
                {
                    continue;
                }

                // We check if there is a @loot tag
                Match regexMatch = LootTagRegex.Match(tagComponent.Name);

                if (!regexMatch.Success)
                {
                    continue;
                }

                // We check if any of the containers in the collection can hold our item
                var containerCollection = inventoryItem as ContainerCollection;

                if (containerCollection == null || !containerCollection.Containers.Any(container => container.CanAccept(item)))
                {
                    continue;
                }

                // We extract the suffix - if not suffix provided, we assume 0
                // Length of @loot - we only want the number suffix
                const int lootTagLength = 5;

                string priorityString = regexMatch.Value.Substring(lootTagLength);
                int priority = priorityString.Length == 0 ? 0 : int.Parse(priorityString);

                matchingContainerCollections.Add((containerCollection, priority));
            }

            IEnumerable<EFT.InventoryLogic.IContainer> result = matchingContainerCollections
                .OrderBy(x => x.priority)
                .SelectMany(x => x.containerCollection.Containers);

            return result;
        }

        // If there are not matching @loot containers found, we will try to merge the item into an existing stack
        // anyway - but only if this behavior is enabled in the config
        private static bool TryMergeItemIntoAnExistingStack(
            Item item,
            Inventory inventory,
            TraderControllerClass controller,
            bool simulate,
            ref GStruct414<GInterface324> result)
        {
            if (!ContainerQuickLootPlugin.AutoMergeStacksForNonLootContainers.Value)
            {
                return false;
            }

            if (item.Template.StackMaxSize <= 1)
            {
                return false;
            }

            foreach (Item targetItem in inventory.Equipment.GetAllItems())
            {
                if (targetItem.Template._id != item.Template._id)
                {
                    continue;
                }

                if (targetItem.StackObjectsCount + item.StackObjectsCount > item.Template.StackMaxSize)
                {
                    continue;
                }

                GStruct414<GClass2788> mergeResult = InteractionsHandlerClass.Merge(item, targetItem, controller, simulate);

                if (!mergeResult.Succeeded)
                {
                    return false;
                }

                result = new GStruct414<GInterface324>(mergeResult.Value);
                return true;
            }

            return false;
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
