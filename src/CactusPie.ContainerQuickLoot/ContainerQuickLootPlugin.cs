﻿using BepInEx;
using BepInEx.Configuration;
using JetBrains.Annotations;

namespace CactusPie.ContainerQuickLoot
{
    [BepInPlugin("com.cactuspie.containerquikloot", "CactusPie.ContainerQuickLoot", "1.4.2")]
    public class ContainerQuickLootPlugin : BaseUnityPlugin
    {
        internal static ConfigEntry<bool> EnableForCtrlClick { get; set; }

        internal static ConfigEntry<bool> EnableForLooseLoot { get; set; }

        internal static ConfigEntry<bool> AutoMergeStacks { get; set; }

        internal static ConfigEntry<bool> AutoMergeStacksForNonLootContainers { get; set; }

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

            AutoMergeStacks = Config.Bind
            (
                sectionName,
                "Merge stacks",
                true,
                new ConfigDescription
                (
                    "Automatically merge stacks (money, ammo, etc.) when transferring them into a container"
                )
            );

            AutoMergeStacksForNonLootContainers = Config.Bind
            (
                sectionName,
                "Merge stacks for non-loot containers",
                true,
                new ConfigDescription
                (
                    "Automatically merge stacks (money, ammo, etc.) when quickly transferring items to " +
                    "containers not marked with a @loot tag"
                )
            );

            new QuickTransferPatch().Enable();
        }
    }
}
