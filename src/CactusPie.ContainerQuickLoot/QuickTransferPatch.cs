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
            Inventory inventory;

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

            IEnumerable<IContainer> targetContainers = FindTargetContainers(item, inventory);

            foreach (IContainer collectionContainer in targetContainers)
            {
                if (!(collectionContainer is GClass2318 container))
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

            return !TryMergeItemIntoAnExistingStack(item, inventory, controller, simulate, ref __result);
        }

        private static bool TryGetInventory(out Inventory inventory)
        {
            GameWorld gameWorld = Singleton<GameWorld>.Instance;

            // If gameWorld is null that means the game is currently not in progress, for instance you're in your hideout
            if (gameWorld == null)
            {
                inventory = null;
                return false;
            }

            Player player = GetLocalPlayerFromWorld(gameWorld);

            inventory = (Inventory)typeof(Player)
                .GetProperty("Inventory", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(player);

            if (inventory == null)
            {
                return false;
            }

            return true;
        }

        private static IEnumerable<IContainer> FindTargetContainers(Item item, Inventory inventory)
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

            IEnumerable<IContainer> result = matchingContainerCollections
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
            ref GStruct375<GInterface275> result)
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

                GStruct375<GClass2599> mergeResult = GClass2585.Merge(item, targetItem, controller, simulate);
                result = new GStruct375<GInterface275>(mergeResult.Value);
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
