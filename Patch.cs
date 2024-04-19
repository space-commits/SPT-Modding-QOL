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

using GameState = GClass1849; //.InRaid
using ApplyItemStruct = GStruct413; //LootItemClass.Apply() return type
using CannotApplyClass = GClass3300; //Inventory Errors/Cannot apply item
using CannotApplyInRaidClass = GClass3297; //Inventory Errors/Not moddable in raid (cstor takes in item)
using VitalPartErrorClass = EFT.InventoryLogic.Slot.GClass3314; //because it is missing vital parts:\n
using ErrorEventClass = GClass2767; //base.RaiseAddEvent(item, status, profileId, silent);

using ItemMoveResultStruct = GStruct414<GClass2786>; //LootItemClass.Apply()...InteractionsHandlerClass.Move(...)
using ItemSplitResultStruct = GStruct414<GClass2795>;
using UIClass = GClass747; //LootItemClass.Apply()...DisabledForNow
using CanSwapClass = GClass2775; //LootItemClass.Apply()....CanSwap
using GenericIntentoryErrorClass = GClass3293; //LootItemClass.Apply()...value3.Error is

using ResultStruct = GStruct414<GInterface324>;// gstruct5 = InteractionsHandlerClass.QuickFindAppropriatePlace
using ResultStruct2 = GStruct416<GClass3348>; //returntype of InteractionsHandlerClass.smethod_1()
using ResultStuct3 = GStruct416<bool>; //gstruct = InteractionsHandlerClass.DestinationCheck
using CompilerFuckedClass = GClass3348; //final returned value for InteractionsHandlerClass.smethod_1()

using CannotModifyVitalPartClass = GClass3299; //InteractionsHandlerClass.smethod_1().... != null && gclass2.Slot.Required) return new...
using CannotMoveItemDuringRaidClass = InteractionsHandlerClass.GClass3336; //itemComponent != null && !(container is IItemOwner)....
using EFT.UI.WeaponModding;
using Diz.LanguageExtensions;


//If you are looking through this code, you have my sympathy. It's a lot of decompiled gibberish,
//because the UI code is confusing and convoluted and I can't make sense of it enough to rewrite it cleanly.

namespace ModdingQOL
{
    public static class Utils
    {
        public static string[] disallowedSlots = { "FirstPrimaryWeapon", "SecondPrimaryWeapon", "Holster" };

        public static Player GetPlayer()
        {
            GameWorld gameWorld = Singleton<GameWorld>.Instance;
            return gameWorld.MainPlayer != null ? gameWorld.MainPlayer : null;
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
            if (!Plugin.RequireMultiTool.Value) 
            {
                return true;
            }

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


    public class ModdingScreenPatch : ModulePatch
    {
        private static Type _targetType;
        private static MethodInfo _targetMethod;

        public ModdingScreenPatch()
        {
            _targetType = AccessTools.TypeByName("ModdingScreenSlotView");
            _targetMethod = AccessTools.Method(_targetType, "SetLockedStatus");
        }

        protected override MethodBase GetTargetMethod()
        {
            return _targetMethod;
        }

        [PatchPrefix]
        private static void PatchPrefix(ref bool locked)
        {
            locked = false;
        }
    }

    public class InteractPatch : ModulePatch
    {
        private static Type _targetType;
        private static MethodInfo _targetMethod;

        public InteractPatch()
        {
            _targetType = AccessTools.TypeByName("GClass3052");
            _targetMethod = AccessTools.Method(_targetType, "IsInteractive");
        }

        protected override MethodBase GetTargetMethod()
        {
            return _targetMethod;
        }

        [PatchPostfix]
        private static void PatchPostfix(EItemInfoButton button, ref IResult __result)
        {
            if (!GameState.InRaid && button == EItemInfoButton.Modding || button == EItemInfoButton.EditBuild)
            {
                __result = SuccessfulResult.New;
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
            if (GameState.InRaid && (mod = (item as Mod)) != null)
            {
                Weapon weapon = Utils.GetParentWeapon(mod);
                Slot slot = mod.Parent.Container as Slot;
                Player player = Utils.GetPlayer();
                EquipmentClass equipment = (EquipmentClass)AccessTools.Property(typeof(Player), "Equipment").GetValue(player);
                bool weaponIsInHands = weapon != null && Utils.disallowedSlots.Contains(weapon.Parent.Container.ID);
                bool weaponIsPlayers = weapon != null && weapon?.Owner?.ID != null && weapon.Owner.ID == Singleton<GameWorld>.Instance.MainPlayer.ProfileId;

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
                bool weaponIsPlayers = weapon != null && weapon?.Owner?.ID != null && weapon.Owner.ID == Singleton<GameWorld>.Instance.MainPlayer.ProfileId; 

                if (equipment != null && player != null && ((weaponIsInHands && weaponIsPlayers) || !Utils.hasTool(equipment)))
                {
                    return false;
                }
            }
            return true;
        }

        [PatchPrefix]
        private static bool Prefix(LootItemClass __instance, TraderControllerClass itemController, Item item, int count, bool simulate, ref ApplyItemStruct __result)
        {
            if (!item.ParentRecursiveCheck(__instance))
            {
                __result = new CannotApplyClass(item, __instance);
                return false;
            }
            bool inRaid = GameState.InRaid;
            Error error = null;
            Error error2 = null;
            Mod mod = item as Mod;
            Slot[] array = (mod != null && inRaid) ? Enumerable.ToArray<Slot>(__instance.VitalParts) : null;
            VitalPartErrorClass gclass3;
            if (inRaid && mod != null && !mod.RaidModdable && !isModable(inRaid, mod))
            {
                error2 = new CannotApplyInRaidClass(mod);
            }
            else if (!InteractionsHandlerClass.CheckMissingParts(mod, __instance.CurrentAddress, itemController, out gclass3))
            {
                error2 = gclass3;
            }
            bool flag = false;
            foreach (Slot slot in __instance.AllSlots)
            {
                if ((error2 == null || !flag) && slot.CanAccept(item))
                {
                    if (error2 != null)
                    {
                        VitalPartErrorClass gclass4;
                        if ((gclass4 = (error2 as VitalPartErrorClass)) != null)
                        {
                            error2 = new VitalPartErrorClass(gclass4.Item, slot, gclass4.MissingParts);
                        }
                        flag = true;
                    }
                    else if (array != null && Enumerable.Contains<Slot>(array, slot))
                    {
                        if (inRaid && isModable(inRaid, mod))
                        {
                            ErrorEventClass to = new ErrorEventClass(slot);
                            ItemMoveResultStruct value = InteractionsHandlerClass.Move(item, to, itemController, simulate);
                            if (value.Succeeded)
                            {
                                __result = value;
                                return false;
                            }
                        }
                        error = new CannotApplyInRaidClass(mod);
                    }
                    else
                    {
                        ErrorEventClass to = new ErrorEventClass(slot);
                        ItemMoveResultStruct value = InteractionsHandlerClass.Move(item, to, itemController, simulate);
                        if (value.Succeeded)
                        {
                            __result = value;
                            return false;
                        }
                        ItemSplitResultStruct value2 = InteractionsHandlerClass.SplitMax(item, int.MaxValue, to, itemController, itemController, simulate);
                        if (value2.Succeeded)
                        {
                            __result = value2;
                            return false;
                        }
                        error = value.Error;
                        if (!UIClass.DisabledForNow && CanSwapClass.CanSwap(item, slot))
                        {
                            __result = new ApplyItemStruct((Error)null);
                            return false;
                        }
                    }
                }
            }
            if (!flag)
            {
                error2 = null;
            }
            ResultStruct value3 = InteractionsHandlerClass.QuickFindAppropriatePlace(item, itemController, __instance.ToEnumerable<LootItemClass>(), InteractionsHandlerClass.EMoveItemOrder.Apply, simulate);
            if (value3.Succeeded)
            {
                __result = value3;
                return false;
            }
            if (!(value3.Error is GenericIntentoryErrorClass))
            {
                error = value3.Error;
            }
            Error error3;
            if ((error3 = error2) == null)
            {
                error3 = (error ?? new CannotApplyClass(item, __instance));
            }
            __result = error3;
            return false;
        }
    }

    public class CanDetatchPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(InteractionsHandlerClass).GetMethod("smethod_1", BindingFlags.Static | BindingFlags.Public);
        }

        [PatchPrefix]
        private static bool Prefix(ref ResultStruct2 __result, Item item, ItemAddress to, TraderControllerClass itemController)
        {
            if (to.Container.ID == "Dogtag")
            {
                return false;
            }
            if (GameState.InRaid)
            {
                Player player = Utils.GetPlayer();
                EquipmentClass equipment = (EquipmentClass)AccessTools.Property(typeof(Player), "Equipment").GetValue(player);
                Mod mod = item as Mod;
                Weapon weapon = Utils.GetParentWeapon(mod);

                if (equipment != null && Utils.hasTool(equipment))
                {
                    bool weaponIsInHands = weapon != null && Utils.disallowedSlots.Contains(weapon.Parent.Container.ID);
                    bool weaponIsPlayers = weapon != null && weapon?.Owner?.ID != null && weapon.Owner.ID == Singleton<GameWorld>.Instance.MainPlayer.ProfileId;
 
                    if (!weaponIsInHands || !weaponIsPlayers)
                    {
                        __result = CompilerFuckedClass._;
                        return false;
                    }

                    if (weapon == null)
                    {
                        __result = CompilerFuckedClass._;
                        return false;
                    }
                }

                if ((mod = (item as Mod)) != null && !mod.RaidModdable && (to is ErrorEventClass || item.Parent is ErrorEventClass))
                {
                    __result = new CannotApplyInRaidClass(mod);
                    return false;
                }
                ErrorEventClass gclass;
                if (((gclass = (to as ErrorEventClass)) != null || (gclass = (item.Parent as ErrorEventClass)) != null) && gclass.Slot.Required)
                {
                    __result = new CannotModifyVitalPartClass(gclass.Slot);
                    return false;
                }
            }
            if (item is LootItemClass && item.Parent is ErrorEventClass)
            {
                CantRemoveFromSlotsDuringRaidComponent itemComponent = item.GetItemComponent<CantRemoveFromSlotsDuringRaidComponent>();
                EFT.InventoryLogic.IContainer container = item.Parent.Container;
                if (itemComponent != null && !(container is IItemOwner) && container.ParentItem is EquipmentClass && !itemComponent.CanRemoveFromSlotDuringRaid(container.ID))
                {
                    __result = new CannotMoveItemDuringRaidClass(item, container.ID);
                    return false;
                }
            }
            ResultStuct3 gstruct = InteractionsHandlerClass.DestinationCheck(item.Parent, to, itemController.OwnerType);
            if (gstruct.Failed)
            {
                __result = gstruct.Error;
            }
            __result = CompilerFuckedClass._;
            return false;
        }
    }


    public class ItemSpecificationPanelPatch : ModulePatch
    {

        protected override MethodBase GetTargetMethod()
        {
            return typeof(ItemSpecificationPanel).GetMethod("method_17", BindingFlags.Instance | BindingFlags.Public);
        }

        private static bool checkSlot(Slot slot, List<string> itemList, Item item)
        {
            if (!GameState.InRaid)
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
        private static bool Prefix(ref KeyValuePair<EModLockedState, ModSlotView.GStruct399> __result, Slot slot, Item ___item_0, List<string> ___list_0)
        {
            string text = (slot.ContainedItem != null) ? slot.ContainedItem.Name.Localized(null) : string.Empty;
            if (!checkSlot(slot, ___list_0, ___item_0))
            {
                __result = new KeyValuePair<EModLockedState, ModSlotView.GStruct399>(EModLockedState.Unlocked, new ModSlotView.GStruct399 {Error = text });
                return false;
            }
            if (slot.ID.StartsWith("camora"))
            {
                __result = new KeyValuePair<EModLockedState, ModSlotView.GStruct399>(EModLockedState.ChamberUnchecked, new ModSlotView.GStruct399 { Error = "<color=#d77400>" + "You need to check the revolver drum".Localized(null) + "</color>" });
                return false;
            }
            __result = new KeyValuePair<EModLockedState, ModSlotView.GStruct399>(EModLockedState.ChamberUnchecked, new ModSlotView.GStruct399 { Error = "<color=#d77400>" + "You need to check chamber in weapon".Localized(null) + "</color>" });
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
        private static bool Prefix(ref Mod __instance, ref ResultStuct3 __result, IContainer toContainer)
        {
            if (!GameState.InRaid)
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
                bool weaponIsPlayers = weapon != null && weapon?.Owner?.ID != null && weapon.Owner.ID == Singleton<GameWorld>.Instance.MainPlayer.ProfileId;

                if (weaponIsInHands && weaponIsPlayers && modParentSlot.Required)
                {
                    __result = new CannotApplyInRaidClass(__instance);
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
                    __result = new CannotApplyInRaidClass(__instance);
                    return false;
                }
                if (slot.Required)
                {
                    __result = new CannotModifyVitalPartClass(slot);
                    return false;
                }
            }
            __result = true;
            return false;
        }
    }
}