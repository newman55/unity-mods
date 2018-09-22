using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Harmony12;
using UnityModManagerNet;
using TH20;
using UnityEngine.AI;

namespace MoreSpeedModes
{
    static class Main
    {
        public static bool enabled; 

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            var harmony = HarmonyInstance.Create(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            modEntry.OnToggle = OnToggle;

            return true;
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            if (!value)
                return false;

            enabled = value;

            return true;
        }
    }
     
    [HarmonyPatch(typeof(GameTime), "CanIncreaseTimeScale")]
    static class GameTime_CanIncreaseTimeScale_Patch
    {
        static bool Prefix(GameTime __instance, ref bool __result, bool useDevSpeeds, int ____timeScaleIndex, GameTime.Config ____config)
        {
            if (!Main.enabled)
                return true;

            __result = ____timeScaleIndex < ((!useDevSpeeds) ? ____config.ReleaseMaxTimeScaleIndex + 2 : (____config.TimeScales.Length - 1));

            return false;
        }
    }

    [HarmonyPatch(typeof(NavPath), MethodType.Constructor, typeof(Character), typeof(bool))]
    static class NavPath_ctor_Patch
    {
        static void Postfix(NavPath __instance, NavMeshAgent ____navMeshAgent)
        {
            if (!Main.enabled)
                return;

            //____navMeshAgent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
        }
    }

    [HarmonyPatch(typeof(NavPath), "RestoreFromSave")]
    static class NavPath_RestoreFromSave_Patch
    {
        static void Postfix(NavPath __instance, NavMeshAgent ____navMeshAgent)
        {
            if (!Main.enabled)
                return;

            //____navMeshAgent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
        }
    }
}
