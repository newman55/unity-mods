using System;
using System.Reflection;
using Harmony12;
using UnityModManagerNet;
using UnityEngine;
using TH20;
using FullInspector;
using System.IO;
using System.Xml.Serialization;
using static Difficulty.Settings;

namespace Difficulty
{
    public class Settings : UnityModManager.ModSettings
    {
        public enum DifficultyType { Easy, Normal, Hard };
        public DifficultyType Difficulty = DifficultyType.Normal;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }

    public class Config : UnityModManager.ModSettings
    {
        [Serializable]
        public class GameConfig
        {
            public float ReputationTreatmentSuccess = 1f;
            public float ReputationTreatmentIneffective = -0.5f;
            public float ReputationDeath = -1f;
            [XmlIgnore]
            public float ReputationSendHome = -0.25f;
            public float ReputationDecayMultiplier = 1f;
            public float GlobalSellValueMultiplier = 1f;
            public float PatientUpdateHealthMultiplier = 1f;
            public float StaffUpdateEnergyMultiplier = 1f;
            public float ResearchMultiplier = 1f;
            public float PatientSpawnRateMultiplier = 0.66f;
            public bool HideButtonSendtoTreatment = false;
        }

        public GameConfig Easy = new GameConfig
        {
            PatientUpdateHealthMultiplier = 0.5f,
            StaffUpdateEnergyMultiplier = 0.5f,
            ResearchMultiplier = 2f,
        };
        public GameConfig Normal = new GameConfig
        {
            GlobalSellValueMultiplier = 0.9f,
            ReputationDeath = -1.5f,
            ReputationDecayMultiplier = 1.5f,
            PatientUpdateHealthMultiplier = 0.75f,
            StaffUpdateEnergyMultiplier = 0.75f,
            ResearchMultiplier = 1.5f,
        };
        public GameConfig Hard = new GameConfig
        {
            GlobalSellValueMultiplier = 0.75f,
            ReputationTreatmentSuccess = 0.5f,
            ReputationDeath = -1.5f,
            ReputationDecayMultiplier = 2f,
            HideButtonSendtoTreatment = true,
        };

        public override string GetPath(UnityModManager.ModEntry modEntry)
        {
            return Path.Combine(modEntry.Path, "Config.xml");
        }

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }

    static class Main
    {
        public static bool enabled;
        public static Settings settings;
        public static Config config;
        public static UnityModManager.ModEntry.ModLogger Logger;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            var harmony = HarmonyInstance.Create(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            settings = Settings.Load<Settings>(modEntry);
            config = Settings.Load<Config>(modEntry);

            Logger = modEntry.Logger;

            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;
            
            return true;
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            if (!value)
                return false;

            try
            {
                enabled = value;
                UpdateGameAlgorithmsConfig();
            }
            catch (Exception e)
            {
                enabled = !enabled;
                return false;
            }

            return true;
        }

        static string[] difficultyStrings = Enum.GetNames(typeof(Settings.DifficultyType));

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Difficulty: ", GUILayout.ExpandWidth(false));

            for (Settings.DifficultyType i = 0; i <= Settings.DifficultyType.Hard; i++)
            {
                var value = settings.Difficulty == i;
                var @new = GUILayout.Toggle(value, difficultyStrings[(int)i], GUILayout.ExpandWidth(false));
                if (@new != value && @new == true)
                {
                    settings.Difficulty = i;
                    UpdateGameAlgorithmsConfig();
                }
            }
            GUILayout.EndHorizontal();
        }

        static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            settings.Save(modEntry);
            config.Save(modEntry);
        }

        public static Config.GameConfig GetConfig()
        {
            switch (settings.Difficulty)
            {
                case DifficultyType.Easy:
                    return config.Easy;
                case DifficultyType.Normal:
                    return config.Normal;
                case DifficultyType.Hard:
                    return config.Hard;
                default:
                    return config.Normal;
            }
        }

        public static void UpdateGameAlgorithmsConfig()
        {
            if (!enabled)
                return;

            GameAlgorithmsConfig gameConfig = null;

            var obj = GameObject.FindObjectOfType<MainScript>();
            if (obj)
            {
                var appConfig = Traverse.Create(obj).Field("_appConfig").GetValue<SharedInstance<AppConfig>>();
                gameConfig = appConfig?.Instance?.GameAlgorithmsConfig?.Instance;
            }

            if (gameConfig == null)
                return;

            Main.Logger.Log("Updating GameAlgorithmsConfig");

            var traverse = Traverse.Create(gameConfig);
            traverse.Field("GlobalSellValueMultiplier").SetValue(GetConfig().GlobalSellValueMultiplier);
        }
    }

    [HarmonyPatch(typeof(IllnessDefinition), "GetTreatmentReputationModifier")]
    static class IllnessDefinition_GetTreatmentReputationModifier_Patch
    {
        static bool Prefix(IllnessDefinition __instance, ref float __result, Treatment.Outcome outcome)
        {
            if (!Main.enabled)
                return true;

            switch (outcome)
            {
                case Treatment.Outcome.Unknown:
                    __result = Main.GetConfig().ReputationSendHome; // this._reputationTreatmentSentHome;
                    break;
                case Treatment.Outcome.Ineffective:
                    __result = Main.GetConfig().ReputationTreatmentIneffective; // this._reputationTreatmentIneffective;
                    break;
                case Treatment.Outcome.Cured:
                    __result = Main.GetConfig().ReputationTreatmentSuccess; // this._reputationTreatmentSuccess;
                    break;
                case Treatment.Outcome.Death:
                    __result = Main.GetConfig().ReputationDeath; // this._reputationTreatmentDeath;
                    break;
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(Character), "GetAttributeMultiplier", typeof(CharacterAttributes.Type))]
    static class Character_GetAttributeMultiplier_Patch
    {
        static void Postfix(Character __instance, ref float __result, CharacterAttributes.Type type)
        {
            if (!Main.enabled)
                return;

            if (type == CharacterAttributes.Type.Health && __instance is Patient)
            {
                var multiplier = Traverse.Create(__instance).Field("_getAttributeMultiplierParam").Field("Multiplier").GetValue<float>();
                __result = Mathf.Max(multiplier * Main.GetConfig().PatientUpdateHealthMultiplier, 0f);
            }
            else if (type == CharacterAttributes.Type.Energy && __instance is Staff)
            {
                var multiplier = Traverse.Create(__instance).Field("_getAttributeMultiplierParam").Field("Multiplier").GetValue<float>();
                __result = Mathf.Max(multiplier * Main.GetConfig().StaffUpdateEnergyMultiplier, 0f);
            }
        }
    }

    [HarmonyPatch(typeof(Staff), "GetResearchRate")]
    static class Staff_GetResearchRate_Patch
    {
        static void Postfix(Staff __instance, ref float __result)
        {
            if (!Main.enabled)
                return;

            __result = Mathf.Max(__result * Main.GetConfig().ResearchMultiplier, 0f);
        }
    }

    [HarmonyPatch(typeof(ReputationTracker), "DecayValue")]
    static class ReputationTracker_DecayValue_Patch
    {
        static bool Prefix(ref float __result, float value, float rate, float deltaTime)
        {
            if (!Main.enabled)
                return true;

            __result = (value >= 0f) ? Mathf.Max(value - rate * deltaTime * Main.GetConfig().ReputationDecayMultiplier, 0f) : Mathf.Min(value + rate * deltaTime, 0f);

            return false;
        }
    }

    [HarmonyPatch(typeof(CharacterManager), "CalculateSpawnTime")]
    static class CharacterManager_CalculateSpawnTime_Patch
    {
        static bool Prefix(CharacterManager __instance, ref float __result, CharacterManager.Config ____config, ReputationTracker ____reputationTracker, PrestigeTracker ____prestigeTracker)
        {
            if (!Main.enabled)
                return true;

            float num = ____config._patientSpawnRate * Main.GetConfig().PatientSpawnRateMultiplier / Mathf.Lerp(1, 3, ____reputationTracker.OverallReputation) / (____prestigeTracker.Data.PatientArrivalRate * 0.5f);
            __result = num + num * RandomUtils.GlobalRandomInstance.NextFloat(-0.25f, 0.25f);

            return false;
        }
    }

    [HarmonyPatch(typeof(InspectorDataPatient), "IsFooterButtonVisible")]
    static class InspectorDataPatient_IsFooterButtonVisible_Patch
    {
        static bool Prefix(InspectorDataPatient __instance, ref bool __result, int buttonIndex)
        {
            if (!Main.enabled || !Main.GetConfig().HideButtonSendtoTreatment)
                return true;

            if (buttonIndex == 2)
            {
                __result = false;
                return false;
            }

            return true;
        }
    }
}
