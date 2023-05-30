using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Aki.Reflection.Patching;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;

namespace ModdingQOL
{
    public static class Utils
    {
        public static string[] disallowedSlots = { "FirstPrimaryWeapon", "SecondPrimaryWeapon", "Holster" };

        public static Player GetPlayer()
        {
            GameWorld gameWorld = Singleton<GameWorld>.Instance;
            return gameWorld.AllPlayers[0] != null ? gameWorld.AllPlayers[0] : null;
        }

        public static Weapon GetParentWeapon(Item item)
        {
            if (item != null)
            {
                IEnumerable<Item> parents = item.GetAllParentItems();

                foreach (Item i in parents)
                {
                    if (i is Weapon)
                    {
                        return i as Weapon;
                    }
                }
            }
            return null;
        }

        private static bool checkId(string id)
        {
            return id == "544fb5454bdc2df8738b456a";
        }

        public static bool hasTool(EquipmentClass equipment)
        {
            if (equipment != null)
            {
                IEnumerable<Slot> slots = equipment.GetAllSlots();

                foreach (Slot slot in slots)
                {
                    Item slotItem = slot.ContainedItem;
                    if (slotItem != null)
                    {
                        bool isContainer = slotItem.IsContainer;
                        if (isContainer)
                        {
                            IEnumerable<Item> items = slotItem.GetAllItems();
                            foreach (Item item in items)
                            {
                                if (checkId(item.TemplateId))
                                {
                                    return true;
                                }
                            }
                        }
                        else
                        {
                            if (checkId(slotItem.TemplateId))
                            {
                                return true;
                            }
                        }
                    }

                }
            }
            return false;
        }
    }

    public class InteractPatch : ModulePatch
    {
        private static Type _targetType;
        private static MethodInfo _targetMethod;

        public InteractPatch()
        {
            _targetType = AccessTools.TypeByName("GClass2680");
            _targetMethod = AccessTools.Method(_targetType, "IsInteractive");
        }

        protected override MethodBase GetTargetMethod()
        {
            return _targetMethod;
        }

        [PatchPostfix]
        private static void PatchPostfix(EItemInfoButton button, ref ValueTuple<bool, string> __result)
        {
            if (!GClass1756.InRaid && button == EItemInfoButton.Modding || button == EItemInfoButton.EditBuild)
            {
                __result = new ValueTuple<bool, string>(true, string.Empty);
            }
        }
    }

    public class ThrowItemPatch : ModulePatch
    {
        private static Type _targetType;
        private static MethodInfo _targetMethod;

        public ThrowItemPatch()
        {
            _targetType = AccessTools.TypeByName("PlayerInventoryController");
            _targetMethod = AccessTools.Method(_targetType, "ThrowItem");
        }

        protected override MethodBase GetTargetMethod()
        {
            return _targetMethod;
        }

        [PatchPrefix]
        private static bool Prefix(Item item)
        {
            Mod mod;
            if (GClass1756.InRaid && (mod = (item as Mod)) != null)
            {
                Weapon weapon = Utils.GetParentWeapon(mod);
                Slot slot = mod.Parent.Container as Slot;
                Player player = Utils.GetPlayer();
                EquipmentClass equipment = (EquipmentClass)AccessTools.Property(typeof(Player), "Equipment").GetValue(player);
                bool weaponIsInHands = weapon != null && Utils.disallowedSlots.Contains(weapon.Parent.Container.ID);
                bool weaponIsPlayers = weapon != null && weapon?.Owner?.ID != null && weapon.Owner.ID.StartsWith("pmc");

                if (weapon != null && equipment != null && slot.Required && ((weaponIsInHands && weaponIsPlayers) || !Utils.hasTool(equipment)))
                {
                    return false;
                }
            }
            return true;
        }
    }

    public class CanAttachPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(LootItemClass).GetMethod("Apply", BindingFlags.Instance | BindingFlags.Public);

        }

        private static bool isModable(bool inRaid, Mod mod)
        {
            if (inRaid)
            {
                Weapon weapon = Utils.GetParentWeapon(mod);
                Player player = Utils.GetPlayer();
                EquipmentClass equipment = (EquipmentClass)AccessTools.Property(typeof(Player), "Equipment").GetValue(player);
                bool weaponIsInHands = weapon != null && Utils.disallowedSlots.Contains(weapon.Parent.Container.ID);
                bool weaponIsPlayers = weapon != null && weapon?.Owner?.ID != null && weapon.Owner.ID.StartsWith("pmc");

                if (equipment != null && player != null && ((weaponIsInHands && weaponIsPlayers) || !Utils.hasTool(equipment)))
                {
                    return false;
                }
            }
            return true;
        }

        [PatchPrefix]
        private static bool Prefix(LootItemClass __instance, TraderControllerClass itemController, Item item, int count, bool simulate, ref GStruct321 __result)
        {
            if (!item.ParentRecursiveCheck(__instance))
            {
                __result = new GClass2856(item, __instance);
                return false;
            }
            bool inRaid = GClass1756.InRaid;
            GClass2824 gclass = null;
            GClass2824 gclass2 = null;
            Mod mod = item as Mod;
            Slot[] array = (mod != null && inRaid) ? Enumerable.ToArray<Slot>(__instance.VitalParts) : null;
            Slot.GClass2867 gclass3;

            if (inRaid && mod != null && !mod.RaidModdable && !isModable(inRaid, mod))
            {
                gclass2 = new GClass2853(mod);
            }
            else if (!GClass2428.CheckMissingParts(mod, __instance.CurrentAddress, itemController, out gclass3))
            {
                gclass2 = gclass3;
            }
            bool flag = false;
            foreach (Slot slot in __instance.AllSlots)
            {
                if ((gclass2 == null || !flag) && slot.CanAccept(item))
                {
                    if (gclass2 != null)
                    {
                        Slot.GClass2867 gclass4;
                        if ((gclass4 = (gclass2 as Slot.GClass2867)) != null)
                        {
                            gclass2 = new Slot.GClass2867(gclass4.Item, slot, gclass4.MissingParts);
                        }
                        flag = true;
                    }
                    else if (array != null && Enumerable.Contains<Slot>(array, slot))
                    {
                        if (inRaid && isModable(inRaid, mod))
                        {
                            GClass2421 to = new GClass2421(slot);
                            GStruct322<GClass2440> value = GClass2428.Move(item, to, itemController, simulate);
                            if (value.Succeeded)
                            {
                                __result = value;
                                return false;
                            }
                        }
                        gclass = new GClass2853(mod);
                    }
                    else
                    {
                        GClass2421 to = new GClass2421(slot);
                        GStruct322<GClass2440> value = GClass2428.Move(item, to, itemController, simulate);
                        if (value.Succeeded)
                        {
                            __result = value;
                            return false;
                        }
                        GStruct322<GClass2449> value2 = GClass2428.SplitMax(item, int.MaxValue, to, itemController, itemController, simulate);
                        if (value2.Succeeded)
                        {
                            __result = value2;
                            return false;
                        }
                        gclass = value.Error;
                        if (!GClass770.DisabledForNow && GClass2429.CanSwap(item, slot))
                        {
                            //ambiguous refernce, wrong ref might bug it out
                            __result = new GStruct321((GInterface264)null);
                            return false;
                        }
                    }
                }
            }
            if (!flag)
            {
                gclass2 = null;
            }
            GStruct322<GInterface265> value3 = GClass2428.QuickFindAppropriatePlace(item, itemController, __instance.ToEnumerable<LootItemClass>(), GClass2428.EMoveItemOrder.Apply, simulate);
            if (value3.Succeeded)
            {
                __result = value3;
                return false;
            }
            if (!(value3.Error is GClass2848))
            {
                gclass = value3.Error;
            }
            GClass2824 error;
            if ((error = gclass2) == null)
            {
                error = (gclass ?? new GClass2856(item, __instance));
            }
            __result = error;
            return false;
        }
    }

    public class CanDetatchPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(GClass2428).GetMethod("smethod_1", BindingFlags.Static | BindingFlags.NonPublic);
        }

        [PatchPrefix]
        private static bool Prefix(ref GStruct323<GClass2898> __result, Item item, ItemAddress to, TraderControllerClass itemController)
        {
            if (to.Container.ID == "Dogtag")
            {
                return false;
            }
            if (GClass1756.InRaid)
            {
                Player player = Utils.GetPlayer();
                EquipmentClass equipment = (EquipmentClass)AccessTools.Property(typeof(Player), "Equipment").GetValue(player);
                Mod mod = item as Mod;
                Weapon weapon = Utils.GetParentWeapon(mod);

                if (equipment != null && Utils.hasTool(equipment))
                {
                    bool weaponIsInHands = weapon != null && Utils.disallowedSlots.Contains(weapon.Parent.Container.ID);
                    bool weaponIsPlayers = weapon != null && weapon?.Owner?.ID != null && weapon.Owner.ID.StartsWith("pmc");
 
                    if (!weaponIsInHands || !weaponIsPlayers)
                    {
                        __result = GClass2898._;
                        return false;
                    }

                    if (weapon == null)
                    {
                        __result = GClass2898._;
                        return false;
                    }
                }

                if ((mod = (item as Mod)) != null && !mod.RaidModdable && (to is GClass2421 || item.Parent is GClass2421))
                {
                    __result = new GClass2853(mod);
                    return false;
                }
                GClass2421 gclass;
                if (((gclass = (to as GClass2421)) != null || (gclass = (item.Parent as GClass2421)) != null) && gclass.Slot.Required)
                {
                    __result = new GClass2855(gclass.Slot);
                    return false;
                }
            }
            if (item is LootItemClass && item.Parent is GClass2421)
            {
                CantRemoveFromSlotsDuringRaidComponent itemComponent = item.GetItemComponent<CantRemoveFromSlotsDuringRaidComponent>();
                IContainer container = item.Parent.Container;
                if (itemComponent != null && !(container is IItemOwner) && container.ParentItem is EquipmentClass && !itemComponent.CanRemoveFromSlotDuringRaid(container.ID))
                {
                    __result = new GClass2428.GClass2889(item, container.ID);
                    return false;
                }
            }
            GStruct323<bool> gstruct = GClass2428.DestinationCheck(item.Parent, to, itemController.OwnerType);
            if (gstruct.Failed)
            {
                __result = gstruct.Error;
            }
            __result = GClass2898._;
            return false;
        }
    }


    public class ItemSpecificationPanelPatch : ModulePatch
    {

        protected override MethodBase GetTargetMethod()
        {
            return typeof(ItemSpecificationPanel).GetMethod("method_17", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        private static bool checkSlot(Slot slot, List<string> itemList, Item item)
        {
            if (!GClass1756.InRaid)
            {
                return false;
            }
            if ((slot.ID.StartsWith("chamber") || slot.ID.StartsWith("patron_in_weapon")) && item is Weapon)
            {
                return !itemList.Contains(item.Id);
            }
            Weapon weapon;
            return slot.ID.StartsWith("camora") && (weapon = (item as Weapon)) != null && weapon.GetCurrentMagazine() is CylinderMagazineClass && !itemList.Contains(item.Id);
        }

        [PatchPrefix]
        private static bool Prefix(ref KeyValuePair<EModLockedState, string> __result, Slot slot, Item ___item_0, InventoryControllerClass ___gclass2416_0, List<string> ___list_0)
        {
            string text = (slot.ContainedItem != null) ? slot.ContainedItem.Name.Localized(null) : string.Empty;
            if (!checkSlot(slot, ___list_0, ___item_0))
            {
                __result = new KeyValuePair<EModLockedState, string>(EModLockedState.Unlocked, text);
                return false;
            }
            if (slot.ID.StartsWith("camora"))
            {
                __result = new KeyValuePair<EModLockedState, string>(EModLockedState.ChamberUnchecked, "<color=#d77400>" + "You need to check the revolver drum".Localized(null) + "</color>");
                return false;
            }
            __result = new KeyValuePair<EModLockedState, string>(EModLockedState.ChamberUnchecked, "<color=#d77400>" + "You need to check chamber in weapon".Localized(null) + "</color>");
            return false;
        }
    }

    public class CanBeMovedPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(Mod).GetMethod("CanBeMoved", BindingFlags.Instance | BindingFlags.Public);
        }

        [PatchPrefix]
        private static bool Prefix(ref Mod __instance, ref GStruct323<bool> __result, IContainer toContainer)
        {
            if (!GClass1756.InRaid)
            {
                __result = true;
                return false;
            }
            Player player = Utils.GetPlayer();
            EquipmentClass equipment = (EquipmentClass)AccessTools.Property(typeof(Player), "Equipment").GetValue(player);
            Weapon weapon = Utils.GetParentWeapon(__instance);
            Slot modParentSlot = __instance.Parent.Container as Slot;   

            if (equipment != null && Utils.hasTool(equipment))
            {
                bool weaponIsInHands = weapon != null && Utils.disallowedSlots.Contains(weapon.Parent.Container.ID);
                bool weaponIsPlayers = weapon != null && weapon?.Owner?.ID != null && weapon.Owner.ID.StartsWith("pmc");

                if (weaponIsInHands && weaponIsPlayers && modParentSlot.Required)
                {
                    __result = new GClass2853(__instance);
                    return false;
                }
                if (weapon == null)
                {
                    __result = true;
                    return false;
                }
            }
            Slot slot = toContainer as Slot;
            if (slot != null)
            {
                if (!__instance.RaidModdable)
                {
                    __result = new GClass2853(__instance);
                    return false;
                }
                if (slot.Required)
                {
                    __result = new GClass2855(slot);
                    return false;
                }
            }
            __result = true;
            return false;
        }
    }
}