using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Harmony12;
using UnityModManagerNet;
using UnityEngine;
using TH20;
 
namespace CopyRoom
{
    static class Main
    {
        public static bool enabled;
        public static UnityModManager.ModEntry.ModLogger Logger;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            var harmony = HarmonyInstance.Create(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            Logger = modEntry.Logger;

            modEntry.OnToggle = OnToggle;

            return true;
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            enabled = value;

            return true;
        }
    }

    [HarmonyPatch(typeof(App), "Update")]
    static class App_Update_Patch
    {
        static void Postfix(App __instance)
        {
            if (!Main.enabled)
                return;

            if (Input.GetKeyUp(KeyCode.C) && Input.GetKey(KeyCode.LeftControl))
            {
                if (__instance == null || __instance.Level == null || __instance.Level.CursorManager == null || __instance.Level.BuildingLogic == null 
                    || __instance.Level.BuildingLogic.CurrentState != BuildingLogic.State.Null)
                {
                    return;
                }
                try
                {
                    var cursor = Traverse.Create(__instance.Level.CursorManager).Field("_mode").GetValue<CursorSelect>();
                    if (cursor != null)
                    {
                        var GetSelection = Traverse.Create(cursor).Method("GetSelection", new Type[] { typeof(GridCoord) });
                        var result = GetSelection.GetValue<ICursorSelectable>(__instance.Level.CursorManager.WorldPosition.ToGridCoord());
                        if (result is Room)
                        {
                            var room = ((Room)result);
                            if (!room.Definition.IsHospital && room.IsFunctional())
                            {
                                __instance.Level.BuildingLogic.TransitionToCopyRoomBlueprintState(room, __instance.Level);
                            }
                        }
                        else if (result is RoomItem)
                        {
                            var item = ((RoomItem)result);
                            if (item.OwningRoom != null && !item.OwningRoom.Definition.IsHospital && item.OwningRoom.IsFunctional())
                            {
                                __instance.Level.BuildingLogic.TransitionToCopyRoomBlueprintState(item.OwningRoom, __instance.Level);
                            }
                        }
                    }
                }
                catch(Exception e)
                {
                    Main.Logger.Error(e.ToString());
                }
            }
        }
    }

    static class Extensions
    {
        public static void TransitionToCopyRoomBlueprintState(this BuildingLogic __instance, Room room, Level level)
        {
            RoomDefinition definition = room.Definition;

            //var NewRoomState = typeof(BuildingLogic).GetNestedType("NewRoomState", AccessTools.all);
            var _newRoomState = Traverse.Create(__instance).Field("_newRoomState").GetValue();
            var LeaveCurrentState = Traverse.Create(__instance).Method("LeaveCurrentState", new Type[] { typeof(bool) }).GetValue(false);

            var BlueprintFloorPlan = new BlueprintFloorPlan(room.FloorPlan){AutoFlowActive = true};
            Traverse.Create(_newRoomState).Field("BlueprintFloorPlan").SetValue(BlueprintFloorPlan);
            var BlueprintFloorPlanVisual = new BlueprintFloorPlanVisual(level.WorldState, level.VisualManager, level.DataViewManager, __instance.Configuration.RoomItemEditConfig, level.BuildEvents, "Blueprint", __instance.Configuration.BlueprintFloorTilePrefab, definition._blueprintWallDefinition.Instance, __instance.Configuration.BlueprintFloorMaterialValid, __instance.Configuration.BlueprintFloorMaterialInvalid, __instance.Configuration.BlueprintFloorMaterialInvalidSize);
            Traverse.Create(_newRoomState).Field("BlueprintFloorPlanVisual").SetValue(BlueprintFloorPlanVisual);
            Traverse.Create(__instance).Field("_newRoomState").SetValue(_newRoomState);

            level.BuildEvents.OnBeginNewRoom.InvokeSafe(definition);
            level.CursorManager.PopMode<CursorRoomBuild>();
            level.CursorManager.PushMode(new CursorRoomBuild(level.CursorManager, level, level.Config.GetCursorRoomBuildConfig(), BlueprintFloorPlan, BlueprintFloorPlanVisual));
            level.CursorManager.PushMode(new CursorRoomMove(level.CursorManager, level, level.WorldState, level.BuildEvents, BlueprintFloorPlan, BlueprintFloorPlanVisual, false));

            Traverse.Create(__instance).Field("_currentState").SetValue(BuildingLogic.State.NewRoom);

            level.BuildEvents.OnEnterNewRoomState.InvokeSafe(BlueprintFloorPlan, BlueprintFloorPlanVisual);
            level.HospitalHUDManager.ShowItemsList(definition._type, BlueprintFloorPlan, false);
            level.HospitalHUDManager.TryShowBuildBar();

            foreach (var item in __instance.CurrentBlueprintFloorPlan.Items)
            {
                if (item.UpgradeLevel > 0)
                    Traverse.Create(item).Field("_upgradeLevel").SetValue(0);

                if (item.MaintenanceLevel != null && item.MaintenanceLevel.Value() > 0)
                    item.MaintenanceLevel.SetValue(0, false);
            }
        }
    }

    [HarmonyPatch(typeof(Room), "OnItemBrokeDown")]
    static class Room_OnItemBrokeDown_Patch
    {
        static bool Prefix(Room __instance, RoomItem roomItem, ref int ____numBrokenItems)
        {
            if (!Main.enabled)
                return true;

            try
            {
                if (roomItem.Definition != null && __instance.Definition.RequiresWorkingItem(roomItem.Definition))
                {
                    ____numBrokenItems++;
                }
            }
            catch(Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }

            return false;
        }
    }
}
