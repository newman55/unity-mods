using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityModManagerNet;
using UnityEngine;
using Harmony12;

namespace StorageBiggerCapacity
{
    public class Settings : UnityModManager.ModSettings
    {
        public int StorageMultiplierId = 0;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }
     
    static class Main
    {
        public static bool enabled;
        public static Settings settings;
        static UnityModManager.ModEntry.ModLogger Logger;

        public static float storageMultiplier = 2f;
        public static float lockerCapacityKg = 20000f;
        public static float liquidReservoirCapacityKg = 5000f;
        public static float gasReservoirCapacityKg = 150f;
        public static float rationBoxCapacityKg = 150f;
        public static float refrigeratorCapacityKg = 100f;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            var harmony = HarmonyInstance.Create(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            settings = Settings.Load<Settings>(modEntry);
            storageMultiplier = multipliers[Mathf.Clamp(settings.StorageMultiplierId, 0, multipliers.Length - 1)];

            Logger = modEntry.Logger;

            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;

            return true;
        }

        public static void SetCapacity(Building building, bool increase)
        {
            var value = (increase ? storageMultiplier : 1f);

            string ID = "Unknown";

            try
            {
                ID = building.Def.PrefabID;
                switch (ID)
                {
                    case "StorageLocker":
                        {
                            var component = building.GetComponent<StorageLocker>();
                            var userMaxCapacityNormalized = component.UserMaxCapacity / component.MaxCapacity;
                            var storage = building.GetComponent<Storage>();
                            storage.capacityKg = lockerCapacityKg * value;
                            component.UserMaxCapacity = storage.capacityKg * userMaxCapacityNormalized;
                            break;
                        }
                    case "StorageLockerSmart":
                        {
                            var component = building.GetComponent<StorageLockerSmart>();
                            var userMaxCapacityNormalized = component.UserMaxCapacity / component.MaxCapacity;
                            var storage = building.GetComponent<Storage>();
                            storage.capacityKg = lockerCapacityKg * value;
                            component.UserMaxCapacity = storage.capacityKg * userMaxCapacityNormalized;
                            break;
                        }
                    case "LiquidReservoir":
                        {
                            var conduitConsumer = building.GetComponent<ConduitConsumer>();
                            conduitConsumer.storage.capacityKg = liquidReservoirCapacityKg * value;
                            conduitConsumer.capacityKG = conduitConsumer.storage.capacityKg;
                            var reservoir = building.GetComponent<Reservoir>();
                            if (reservoir.isSpawned)
                                Traverse.Create(reservoir).Method("OnStorageChange", new Type[] { typeof(object) }).GetValue(0);
                            break;
                        }
                    case "GasReservoir":
                        {
                            var conduitConsumer = building.GetComponent<ConduitConsumer>();
                            conduitConsumer.storage.capacityKg = gasReservoirCapacityKg * value;
                            conduitConsumer.capacityKG = conduitConsumer.storage.capacityKg;
                            var reservoir = building.GetComponent<Reservoir>();
                            if (reservoir.isSpawned)
                                Traverse.Create(reservoir).Method("OnStorageChange", new Type[] { typeof(object) }).GetValue(0);
                            break;
                        }
                    case "RationBox":
                        {
                            var component = building.GetComponent<RationBox>();
                            var userMaxCapacityNormalized = component.UserMaxCapacity / component.MaxCapacity;
                            var storage = building.GetComponent<Storage>();
                            storage.capacityKg = rationBoxCapacityKg * value;
                            component.UserMaxCapacity = storage.capacityKg * userMaxCapacityNormalized;
                            break;
                        } 
                    case "Refrigerator":
                        {
                            var component = building.GetComponent<Refrigerator>();
                            var userMaxCapacityNormalized = component.UserMaxCapacity / component.MaxCapacity;
                            var storage = building.GetComponent<Storage>();
                            storage.capacityKg = refrigeratorCapacityKg * value;
                            component.UserMaxCapacity = storage.capacityKg * userMaxCapacityNormalized;
                            break;
                        }
                }
            }
            catch(Exception e)
            {
                Logger.Error($"[{ID}] - {e.Message}");
            }
        }

        public static void SetCapacity(bool increase)
        {
            if (Game.Instance == null || !Game.Instance.GameStarted())
                return;

            var objects = GameObject.FindObjectsOfType<Building>();
            foreach (var building in objects)
            {
                if (building.Def.ObjectLayer != ObjectLayer.Building)
                    continue;

                SetCapacity(building, increase);
            }
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            SetCapacity(value);

            enabled = value;

            return true;
        }

        static int[] multipliers = new int[] { 2, 4, 10, 50, 100 };
        static string[] multipliersText = multipliers.Select(x => x.ToString()).ToArray();

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Storage capacity multiplier: ", GUILayout.ExpandWidth(false));
            var id = GUILayout.Toolbar(settings.StorageMultiplierId, multipliersText, GUILayout.ExpandWidth(false));
            if (id != settings.StorageMultiplierId)
            {
                settings.StorageMultiplierId = id;
                storageMultiplier = multipliers[id];
                if (enabled)
                    SetCapacity(true);
            }
            GUILayout.EndHorizontal();
        }

        static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            settings.Save(modEntry);
        }
    }

    [HarmonyPatch(typeof(GameScreenManager), "OnSpawn")]
    static class GameScreenManager_OnSpawn_Patch
    {
        static void Postfix(GameScreenManager __instance)
        {
            if (!Main.enabled)
                return;

            Main.SetCapacity(true);
        }
    }

    [HarmonyPatch(typeof(StorageLocker), "OnSpawn")]
    static class StorageLocker_OnSpawn_Patch
    {
        static void Postfix(StorageLocker __instance)
        {
            if (!Main.enabled)
                return;

            var building = __instance.GetComponent<Building>();
            Main.SetCapacity(building, true);
        }
    }

    [HarmonyPatch(typeof(StorageLockerSmart), "OnSpawn")]
    static class StorageLockerSmart_OnSpawn_Patch
    {
        static void Postfix(StorageLockerSmart __instance)
        {
            if (!Main.enabled)
                return;

            var building = __instance.GetComponent<Building>();
            Main.SetCapacity(building, true);
        }
    }

    [HarmonyPatch(typeof(Reservoir), "OnSpawn")]
    static class Reservoir_OnSpawn_Patch
    {
        static void Postfix(Reservoir __instance)
        {
            if (!Main.enabled)
                return;

            var building = __instance.GetComponent<Building>();
            Main.SetCapacity(building, true);
        }
    }

    [HarmonyPatch(typeof(RationBox), "OnSpawn")]
    static class RationBox_OnSpawn_Patch
    {
        static void Postfix(RationBox __instance)
        {
            if (!Main.enabled)
                return;

            var building = __instance.GetComponent<Building>();
            Main.SetCapacity(building, true);
        }
    }

    [HarmonyPatch(typeof(Refrigerator), "OnSpawn")]
    static class Refrigerator_OnSpawn_Patch
    {
        static void Postfix(Refrigerator __instance)
        {
            if (!Main.enabled)
                return;

            var building = __instance.GetComponent<Building>();
            Main.SetCapacity(building, true);
        }
    }
}
