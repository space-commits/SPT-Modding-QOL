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
            if (GClass1757.InRaid && (mod = (item as Mod)) != null)
            {
                Weapon weapon = Utils.GetParentWeapon(mod);
                Slot slot = mod.Parent.Container as Slot;
                Player player = Utils.GetPlayer();
                EquipmentClass equipment = (EquipmentClass)AccessTools.Property(typeof(Player), "Equipment").GetValue(player);
                if (weapon != null && equipment != null && slot.Required && (Utils.disallowedSlots.Contains(weapon.Parent.Container.ID) || !Utils.hasTool(equipment)))
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
                if (equipment != null && player != null && weapon != null && (Utils.disallowedSlots.Contains(weapon.Parent.Container.ID) || !Utils.hasTool(equipment)))
                {
                    return false;
                }
            }
            return true;
        }

        [PatchPrefix]
        private static bool Prefix(LootItemClass __instance, TraderControllerClass itemController, Item item, int count, bool simulate, ref GStruct324 __result)
        {
            if (!item.ParentRecursiveCheck(__instance))
            {
                __result = new GClass2855(item, __instance);
                return false;
            }
            bool inRaid = GClass1757.InRaid;
            GClass2823 gclass = null;
            GClass2823 gclass2 = null;
            Mod mod = item as Mod;
            Slot[] array = (mod != null && inRaid) ? Enumerable.ToArray<Slot>(__instance.VitalParts) : null;
            Slot.GClass2866 gclass3;

            if (inRaid && mod != null && !mod.RaidModdable && !isModable(inRaid, mod))
            {
                gclass2 = new GClass2853(mod);
            }
            else if (!GClass2429.CheckMissingParts(mod, __instance.CurrentAddress, itemController, out gclass3))
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
                        Slot.GClass2866 gclass4;
                        if ((gclass4 = (gclass2 as Slot.GClass2866)) != null)
                        {
                            gclass2 = new Slot.GClass2866(gclass4.Item, slot, gclass4.MissingParts);
                        }
                        flag = true;
                    }
                    else if (array != null && Enumerable.Contains<Slot>(array, slot))
                    {
                        if (inRaid && isModable(inRaid, mod))
                        {
                            GClass2422 to = new GClass2422(slot);
                            GStruct325<GClass2441> value = GClass2429.Move(item, to, itemController, simulate);
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
                        GClass2422 to = new GClass2422(slot);
                        GStruct325<GClass2441> value = GClass2429.Move(item, to, itemController, simulate);
                        if (value.Succeeded)
                        {
                            __result = value;
                            return false;
                        }
                        GStruct325<GClass2450> value2 = GClass2429.SplitMax(item, int.MaxValue, to, itemController, itemController, simulate);
                        if (value2.Succeeded)
                        {
                            __result = value2;
                            return false;
                        }
                        gclass = value.Error;
                        if (!GClass770.DisabledForNow && GClass2430.CanSwap(item, slot))
                        {
                            //ambiguous refernce, wrong ref might bug it out
                            __result = new GStruct324((GInterface265)null);
                            return false;
                        }
                    }
                }
            }
            if (!flag)
            {
                gclass2 = null;
            }
            GStruct325<GInterface266> value3 = GClass2429.QuickFindAppropriatePlace(item, itemController, __instance.ToEnumerable<LootItemClass>(), GClass2429.EMoveItemOrder.Apply, simulate);
            if (value3.Succeeded)
            {
                __result = value3;
                return false;
            }
            if (!(value3.Error is GClass2848))
            {
                gclass = value3.Error;
            }
            GClass2823 error;
            if ((error = gclass2) == null)
            {
                error = (gclass ?? new GClass2855(item, __instance));
            }
            __result = error;
            return false;
        }
    }

    public class CanDetatchPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(GClass2429).GetMethod("smethod_1", BindingFlags.Static | BindingFlags.NonPublic);
        }

        [PatchPrefix]
        private static bool Prefix(ref GStruct326<GClass2897> __result, Item item, ItemAddress to, TraderControllerClass itemController)
        {
            if (to.Container.ID == "Dogtag")
            {
                return false;
            }
            if (GClass1757.InRaid)
            {
                Player player = Utils.GetPlayer();
                EquipmentClass equipment = (EquipmentClass)AccessTools.Property(typeof(Player), "Equipment").GetValue(player);
                Mod mod = item as Mod;
                Weapon weapon = Utils.GetParentWeapon(mod);

                if (equipment != null && Utils.hasTool(equipment))
                {
                    if (weapon != null && !Utils.disallowedSlots.Contains(weapon.Parent.Container.ID))
                    {
                        __result = GClass2897._;
                        return false;
                    }

                    if (weapon == null)
                    {
                        __result = GClass2897._;
                        return false;
                    }
                }

                if ((mod = (item as Mod)) != null && !mod.RaidModdable && (to is GClass2422 || item.Parent is GClass2422))
                {
                    __result = new GClass2853(mod);
                    return false;
                }
                GClass2422 gclass;
                if (((gclass = (to as GClass2422)) != null || (gclass = (item.Parent as GClass2422)) != null) && gclass.Slot.Required)
                {
                    __result = new GClass2854(gclass.Slot);
                    return false;
                }
            }
            if (item is LootItemClass && item.Parent is GClass2422)
            {
                CantRemoveFromSlotsDuringRaidComponent itemComponent = item.GetItemComponent<CantRemoveFromSlotsDuringRaidComponent>();
                IContainer container = item.Parent.Container;
                if (itemComponent != null && !(container is IItemOwner) && container.ParentItem is EquipmentClass && !itemComponent.CanRemoveFromSlotDuringRaid(container.ID))
                {
                    __result = new GClass2429.GClass2888(item, container.ID);
                    return false;
                }
            }
            GStruct326<bool> gstruct = GClass2429.DestinationCheck(item.Parent, to, itemController.OwnerType);
            if (gstruct.Failed)
            {
                __result = gstruct.Error;
            }
            __result = GClass2897._;
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
            if (!GClass1757.InRaid)
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
        private static bool Prefix(ref KeyValuePair<EModLockedState, string> __result, Slot slot, Item ___item_0, InventoryControllerClass ___gclass2417_0, List<string> ___list_0)
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
        private static bool Prefix(ref Mod __instance, ref GStruct326<bool> __result, IContainer toContainer)
        {
            if (!GClass1757.InRaid)
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
                if (weapon != null && Utils.disallowedSlots.Contains(weapon.Parent.Container.ID) && modParentSlot.Required)
                {
                    __result = new GClass2852(__instance);
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
                    __result = new GClass2852(__instance);
                    return false;
                }
                if (slot.Required)
                {
                    __result = new GClass2854(slot);
                    return false;
                }
            }
            __result = true;
            return false;
        }
    }
}