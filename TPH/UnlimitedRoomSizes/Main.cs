using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Harmony12;
using UnityModManagerNet;
using UnityEngine;
using TH20;

namespace UnlimitedRoomSizes
{
    static class Main
    {
        public static bool enabled;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            modEntry.OnToggle = OnToggle;

            var harmony = HarmonyInstance.Create(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            return true;
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            enabled = value;

            return true;
        }
    }

    [HarmonyPatch(typeof(RoomAlgorithms), "DoesFloorPlanContainAreaOfSize")]
    static class RoomAlgorithms_DoesFloorPlanContainAreaOfSize_Patch
    {
        static bool Prefix(FloorPlan floorPlan, ref bool __result)
        {
            if (!Main.enabled)
                return true;

            if (floorPlan.Width() >= 1 && floorPlan.Height() >= 1)
            {
                __result = true;
            }
            return false;
        }
    }
}
