using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;

namespace AutoSetup
{
    public class Settings : UnityModManager.ModSettings, IDrawable
    {
        [Draw("Activated")] public bool active = true;
        [Draw("Enable log")] public bool log = false;
        [Space(10)] 
        [Draw("Params", InvisibleOn = "active|False")] public SetupParams setupParams = new SetupParams();
        [Space(10)] 
        [Draw("Display setups")] public bool displaySetups = false;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }

        public void OnChange()
        {
        }
    }

    public class SetupParams
    {
        [Draw("Maximal setup")] public bool maximalSetup = false;
        [Draw("Min-Max range", DrawType.Slider, Min = 0, Max = 1, InvisibleOn = "maximalSetup|True")] public float range = 0.7f;
        [Draw("Rng delta", DrawType.Slider, Min = 0, Max = 1, InvisibleOn = "maximalSetup|True")] public float delta = 0.5f;
        [Draw("Custom time cost")] public bool customTimeCost = true;
        [Draw("Time cost", DrawType.Slider, Min = 0, Max = 2, Precision = 1, VisibleOn = "customTimeCost|True")] public float timeCost = 1.5f;
    }

#if DEBUG
    [EnableReloading]
#endif
    static class Main
    {
        public static bool enabled;
        public static Settings settings;
        public static UnityModManager.ModEntry.ModLogger logger;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            settings = Settings.Load<Settings>(modEntry);
            settings.setupParams.delta = Mathf.Clamp01(settings.setupParams.delta);
            settings.setupParams.range = Mathf.Clamp01(settings.setupParams.range);

            var harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            
            logger = modEntry.Logger;
            modEntry.OnSaveGUI = OnSaveGUI;
            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = OnGUI;
            throw new Exception("qwe");
#if DEBUG
            modEntry.OnUnload = Unload;
#endif
            return true;
        }
#if DEBUG
        static bool Unload(UnityModManager.ModEntry modEntry)
        {
            var harmony = new Harmony(modEntry.Info.Id);
            harmony.UnpatchAll(modEntry.Info.Id);
            return true;
        }
#endif
        static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            settings.Save(modEntry);
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            enabled = value;

            return true;
        }
        
        static FieldInfo mSetupOutputFi = AccessTools.Field(typeof(SetupPerformance), "mSetupOutput");

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            settings.Draw(modEntry);

            if (settings.displaySetups && Game.IsActive() && Game.instance.sessionManager.isSessionActive && Game.instance.sessionManager.isCircuitActive)
            {
                GUILayout.Label("[Value/Time/Driver]");
                int i = 1;
                foreach (var vehicle in Game.instance.sessionManager.standings)
                {
                    var setup = (SessionSetup.SetupOutput)mSetupOutputFi.GetValue(vehicle.performance.setupPerformance);
                    var optimalSetup = vehicle.performance.setupPerformance.GetOptimalSetup().setupOutput;
                    var val = (3f - Mathf.Abs(optimalSetup.handling - setup.handling) - Mathf.Abs(optimalSetup.aerodynamics - setup.aerodynamics)
                        - Mathf.Abs(optimalSetup.speedBalance - setup.speedBalance)) / 3f;
                    GUILayout.Label($"{i++}. {val:p0} {vehicle.performance.setupPerformance.timeCost:n}s {vehicle.driver.lastName}");
                }
                
                if (GUILayout.Button("Update setups", GUILayout.Width(150)))
                {
                    foreach (var vehicle in Game.instance.sessionManager.standings)
                    {
                        if (settings.active)
                        {
                            var setup = vehicle.setup.CreateAutoSetup();
                            mSetupOutputFi.SetValue(vehicle.performance.setupPerformance, setup);
                        }
                        else if (!vehicle.isPlayerDriver)
                        {
                            if (Game.instance.sessionManager.sessionType == SessionDetails.SessionType.Practice)
                            {
                                vehicle.setup.CreateAISetupForPractice();
                            }
                            else
                            {
                                vehicle.setup.CreateAISetupForQualifyingAndRace();
                            }
                            var setup = new SessionSetup.SetupOutput();
                            vehicle.setup.GetSetupOutput(ref setup);
                            mSetupOutputFi.SetValue(vehicle.performance.setupPerformance, setup);
                        }
                        vehicle.performance.setupPerformance.ClearTimeCost();
                    }
                }
            }
        }
    }

    public static class RaceDirector_
    {
        static MechanicStats mHighestMechanicStats;
        static MechanicStats mLowestMechanicStats;
        static float mHighestAverageMechanicStats;
        static float mLowestAverageMechanicStats;
        static Championship mChampionship;

        public static MechanicStats highestMechanicStats
        {
            get
            {
                ValidateStats();

                return mHighestMechanicStats;
            }
        }

        public static MechanicStats lowestMechanicStats
        {
            get
            {
                ValidateStats();

                return mLowestMechanicStats;
            }
        }

        public static float highestAverageMechanicStats
        {
            get
            {
                ValidateStats();

                return mHighestAverageMechanicStats;
            }
        }

        public static float lowestAverageMechanicStats
        {
            get
            {
                ValidateStats();

                return mLowestAverageMechanicStats;
            }
        }

        internal static void ResetStats()
        {
            mHighestMechanicStats = null;
            mLowestMechanicStats = null;
            mHighestAverageMechanicStats = float.MinValue;
            mLowestAverageMechanicStats = float.MaxValue;
        }

        internal static void InitStats()
        {
            mHighestMechanicStats = new MechanicStats();
            mLowestMechanicStats = new MechanicStats();
            mHighestAverageMechanicStats = float.MinValue;
            mLowestAverageMechanicStats = float.MaxValue;
        }

        internal static void CalculateStats()
        {
            #if DEBUG
            Main.logger.Log("RaceDirector.CalculateStats()");
            #endif
            InitStats();
            var inChampionships = Game.instance.sessionManager.championships;
            for (int i = 0; i < inChampionships.Count; i++)
            {
                int driverEntryCount = inChampionships[i].standings.driverEntryCount;
                for (int j = 0; j < driverEntryCount; j++)
                {
                    CalculateStats(inChampionships[i].standings.GetDriverEntry(j).GetEntity<Driver>());
                }
            }
        }

        internal static void CalculateStats(Driver driver)
        {
            if (driver == null)
            {
                Main.logger.Error("Driver is null");
                return;
            }

            var mechanic = driver.contract?.GetTeam()?.GetMechanicOfDriver(driver);
            if (mechanic == null)
            {
                Main.logger.Error($"Mechanic not found for {driver.lastName}");
                return;
            }
            SetHighestMechanicStats(mechanic.stats);
            SetLowestMechanicStats(mechanic.stats);
        }

        internal static void ValidateStats()
        {
            if (mChampionship != Game.instance.sessionManager.championship || mLowestMechanicStats == null)
            {
                mChampionship = Game.instance.sessionManager.championship;
                ResetStats();
                CalculateStats();
            }
        }

        internal static void SetHighestMechanicStats(MechanicStats inStats)
        {
            if (mHighestMechanicStats == null)
                InitStats();

            if (inStats.concentration > mHighestMechanicStats.concentration)
            {
                mHighestMechanicStats.concentration = inStats.concentration;
            }
            if (inStats.leadership > mHighestMechanicStats.leadership)
            {
                mHighestMechanicStats.leadership = inStats.leadership;
            }
            if (inStats.performance > mHighestMechanicStats.performance)
            {
                mHighestMechanicStats.performance = inStats.performance;
            }
            if (inStats.pitStops > mHighestMechanicStats.pitStops)
            {
                mHighestMechanicStats.pitStops = inStats.pitStops;
            }
            if (inStats.reliability > mHighestMechanicStats.reliability)
            {
                mHighestMechanicStats.reliability = inStats.reliability;
            }
            if (inStats.speed > mHighestMechanicStats.speed)
            {
                mHighestMechanicStats.speed = inStats.speed;
            }
            float unitAverage = inStats.GetUnitAverage();
            if (unitAverage > mHighestAverageMechanicStats)
            {
                mHighestAverageMechanicStats = unitAverage;
            }
        }

        internal static void SetLowestMechanicStats(MechanicStats inStats)
        {
            if (mLowestMechanicStats == null)
                InitStats();

            if (MathsUtility.ApproximatelyZero(mLowestMechanicStats.concentration) || inStats.concentration < mLowestMechanicStats.concentration)
            {
                mLowestMechanicStats.concentration = inStats.concentration;
            }
            if (MathsUtility.ApproximatelyZero(mLowestMechanicStats.leadership) || inStats.leadership < mLowestMechanicStats.leadership)
            {
                mLowestMechanicStats.leadership = inStats.leadership;
            }
            if (MathsUtility.ApproximatelyZero(mLowestMechanicStats.performance) || inStats.performance < mLowestMechanicStats.performance)
            {
                mLowestMechanicStats.performance = inStats.performance;
            }
            if (MathsUtility.ApproximatelyZero(mLowestMechanicStats.pitStops) || inStats.pitStops < mLowestMechanicStats.pitStops)
            {
                mLowestMechanicStats.pitStops = inStats.pitStops;
            }
            if (MathsUtility.ApproximatelyZero(mLowestMechanicStats.reliability) || inStats.reliability < mLowestMechanicStats.reliability)
            {
                mLowestMechanicStats.reliability = inStats.reliability;
            }
            if (MathsUtility.ApproximatelyZero(mLowestMechanicStats.speed) || inStats.speed < mLowestMechanicStats.speed)
            {
                mLowestMechanicStats.speed = inStats.speed;
            }
            float unitAverage = inStats.GetUnitAverage();
            if (MathsUtility.ApproximatelyZero(mLowestAverageMechanicStats) || unitAverage < mLowestAverageMechanicStats)
            {
                mLowestAverageMechanicStats = unitAverage;
            }
        }
    }

    public static class SessionSetup_
    {
        public static bool NotDefault(this SessionSetup.SetupOutput instance)
        {
            if (instance.aerodynamics != 0 || instance.handling != 0 || instance.speedBalance != 0)
            {
                return true;
            }

            return false;
        }

        public static string ToString_(this SessionSetup.SetupOutput instance)
        {
            return $"{instance.aerodynamics:f2}; {instance.handling:f2}; {instance.speedBalance:f2}";
        }

        public static void CopyTo(this SessionSetup.SetupOutput instance, SessionSetup.SetupOutput to)
        {
            to.aerodynamics = instance.aerodynamics;
            to.handling = instance.handling;
            to.speedBalance = instance.speedBalance;
        }
        
        static FieldInfo mAISetupOutputFi = AccessTools.Field(typeof(SessionSetup), "mAISetupOutput");

        public static SessionSetup.SetupOutput CreateAutoSetup(this SessionSetup instance)
        {
            instance.CreateAutoSetup(out var setup);
            mAISetupOutputFi.SetValue(instance, setup);
            return setup;
        }

        public static void CreateAutoSetup(this SessionSetup instance, out SessionSetup.SetupOutput setup)
        {
            setup = new SessionSetup.SetupOutput();

            if (Main.settings.setupParams.maximalSetup)
            {
                instance.SetAISetup(1, 1, ref setup);
                return;
            }
            
            float driverFeedback = 0f;
            if (instance.vehicle.driversForCar.Length > 1)
            {
                Driver[] driversForCar = instance.vehicle.driversForCar;
                for (int i = 0; i < driversForCar.Length; i++)
                {
                    float stat = driversForCar[i].GetDriverStats().feedback;
                    driverFeedback = Mathf.Max(driverFeedback, stat);
                }
            }
            else
            {
                driverFeedback = instance.vehicle.driver.GetDriverStats().feedback;
            }

            float lowest = Game.instance.sessionManager.raceDirector.lowestDriverStats.feedback;
            float highest = Game.instance.sessionManager.raceDirector.highestDriverStats.feedback;
            driverFeedback = (driverFeedback - lowest) / Mathf.Max(highest - lowest, Mathf.Epsilon);
            var mechanic = instance.vehicle.driver.contract.GetTeam().GetMechanicOfDriver(instance.vehicle.driver);
            float mechanicBonus;
            float mechanicStat;
            if (mechanic == null)
            {
                Debug.LogWarning("Could not find mechanic for driver, defaulting values", null);
                mechanicStat = 0f;
                mechanicBonus = 0f;
            }
            else
            {
                lowest = RaceDirector_.lowestAverageMechanicStats;
                highest = RaceDirector_.highestAverageMechanicStats;
                mechanicStat = Mathf.Max(lowest, mechanic.stats.GetUnitAverage());
                mechanicStat = (mechanicStat - lowest) / Mathf.Max(highest - lowest, Mathf.Epsilon);
                if (instance.vehicle.driversForCar.Length > 1)
                {
                    mechanicBonus = 0f;
                    Driver[] driversForCar = instance.vehicle.driversForCar;
                    for (int i = 0; i < driversForCar.Length; i++)
                    {
                        var val = mechanic.GetModifiedRelationshipWithDriver(driversForCar[i]).relationshipAmount / 100f;
                        mechanicBonus = Mathf.Max(val, mechanicBonus);
                    }
                }
                else
                {
                    if (!instance.vehicle.driver.IsReserveDriver())
                        mechanicBonus = mechanic.GetModifiedRelationshipWithDriver(instance.vehicle.driver).relationshipAmount / 100f;
                    else
                        mechanicBonus = 0;
                }
            }

            var minusDelta = 1f - Main.settings.setupParams.delta;
            var minusScale = 1f - Main.settings.setupParams.range;
            var halfScale = Main.settings.setupParams.range * 0.5f;
            
            var minMechanic = halfScale * 0.70f * mechanicStat;
            var minDriver = halfScale * 0.30f * driverFeedback;
            var minRelations = halfScale * 0.30f * mechanicBonus;
            
            var maxMechanic = halfScale * 0.40f * mechanicStat;
            var maxDriver = halfScale * 0.60f * driverFeedback;
            
            instance.SetAISetup(minusScale + halfScale * minusDelta + minMechanic + minDriver + minRelations, (1f - halfScale) + maxMechanic + maxDriver, ref setup);
//            instance.SetAISetup(0.5f + 0.25f * mechanicStat + 0.1f * driverFeedback, 0.65f + 0.15f * mechanicStat + 0.2f * driverFeedback, ref setup);
        }

        public static void SetAISetup(this SessionSetup instance, float inMin, float inMax, ref SessionSetup.SetupOutput setup)
        {
            var optimalSetup = instance.vehicle.performance.setupPerformance.GetOptimalSetup().setupOutput;
            inMin = Mathf.Clamp01(inMin);
            inMax = Mathf.Clamp01(inMax);
            inMax = Mathf.Max(inMax, inMin);
            var random = new System.Random((int)(instance.vehicle.championship.GetCurrentEventDetails().eventDate.Ticks / 864000000000L
                + instance.vehicle.driver.dateOfBirth.Ticks / 864000000000L));
            float random2 = RandomUtility.GetRandom(inMin, inMax, random);
            float num = 1f - random2;
            int num2 = (int)Math.Round(num * 100f);
            if (num2 > 0)
            {
                float[] array = new float[]
                {
                    (float) random.Next(num2 / 2, num2 + num2 / 2) / 100f, (float) random.Next(num2 / 2, num2 + num2 / 2) / 100f,
                    (float) random.Next(num2 / 2, num2 + num2 / 2) / 100f
                };
                float num3 = array[0] + array[1] + array[2];
                if (num3 > 0f)
                {
                    array[0] = array[0] / num3 * num * 3f * (float)((RandomUtility.GetRandom01(random) > 0.5f) ? -1 : 1);
                    array[1] = array[1] / num3 * num * 3f * (float)((RandomUtility.GetRandom01(random) > 0.5f) ? -1 : 1);
                    array[2] = array[2] / num3 * num * 3f * (float)((RandomUtility.GetRandom01(random) > 0.5f) ? -1 : 1);
                }

                setup.aerodynamics = optimalSetup.aerodynamics + array[0];
                setup.speedBalance = optimalSetup.speedBalance + array[1];
                setup.handling = optimalSetup.handling + array[2];
            }
            else
            {
                setup.aerodynamics = optimalSetup.aerodynamics;
                setup.speedBalance = optimalSetup.speedBalance;
                setup.handling = optimalSetup.handling;
            }

            if (Main.settings.log)
                Main.logger.Log(
                    $"Setup created {random2:p0} ({(inMin*100f):n0}-{(inMax*100f):n0}) for {instance.vehicle.driver.lastName}");
        }
    }
    
    [HarmonyPatch(typeof(SessionSetup), "GetSetupOutput", new Type[]{typeof(SessionSetup.SetupOutput)}, new ArgumentType[] {ArgumentType.Ref})]
    static class SessionSetup_GetSetupOutput_Patch
    {
        static bool Prefix(SessionSetup __instance, ref SessionSetup.SetupOutput outSetupOutput, ref SessionSetup.SetupOutput ___mAISetupOutput)
        {
            if (!Main.enabled || !Main.settings.active)
                return true;

            if (outSetupOutput != null && outSetupOutput.NotDefault())
            {
                return false;
            }

            if (outSetupOutput == null)
                outSetupOutput = new SessionSetup.SetupOutput();

            if (___mAISetupOutput != null && ___mAISetupOutput.NotDefault())
            {
                ___mAISetupOutput.CopyTo(outSetupOutput);
                return false;
            }

            if (__instance.vehicle.isPlayerDriver && Game.instance.sessionManager.sessionType != SessionDetails.SessionType.Practice)
            {
                SetupStintData setupStintData = null;
                List<SetupStintData> setupStintData2 = Game.instance.persistentEventData.GetSetupStintData(__instance.vehicle);
                for (int i = 0; i < setupStintData2.Count; i++)
                {
                    if (setupStintData == null || setupStintData.GetOverallSetupPercentage() < setupStintData2[i].GetOverallSetupPercentage())
                    {
                        setupStintData = setupStintData2[i];
                    }
                }

                if (setupStintData != null)
                {
                    if (setupStintData.setupOutput.NotDefault())
                    {
                        ___mAISetupOutput = new SessionSetup.SetupOutput();
                        setupStintData.setupOutput.CopyTo(___mAISetupOutput);
                        setupStintData.setupOutput.CopyTo(outSetupOutput);
                        
                        if (Main.settings.log)
                            Main.logger.Log($"Setup loaded {setupStintData.GetOverallSetupPercentage():p0} for {__instance.vehicle.driver.lastName}");
                    }
                    else
                    {
                        __instance.CreateAutoSetup(out ___mAISetupOutput);
                        ___mAISetupOutput.CopyTo(outSetupOutput);
                    }

                    return false;
                }
            }

            if (___mAISetupOutput == null)
            {
                if (Main.settings.log)
                    Main.logger.Log("Setup not found for " + __instance.vehicle.driver.lastName);
                ___mAISetupOutput = new SessionSetup.SetupOutput();
            }

            ___mAISetupOutput.CopyTo(outSetupOutput);

            return false;
        }
    }
    
    [HarmonyPatch(typeof(VehicleManager), "OnSessionStart")]
    static class VehicleManager_OnSessionStart_Patch
    {
        static bool Prefix(VehicleManager __instance, List<RacingVehicle> ___mVehicles)
        {
            if (!Main.enabled || !Main.settings.active)
                return true;
			
            foreach (var t in ___mVehicles)
            {
                t.resultData = null;
                t.performance.OnSessionStart();
                if (!t.isPlayerDriver || Game.instance.sessionManager.sessionType == SessionDetails.SessionType.Practice)
                {
                    t.setup.CreateAutoSetup();
                }
            }

            return false;
        }
    }
    
    [HarmonyPatch(typeof(SessionSetup), "CreateAISetupForPractice")]
    static class SessionSetup_CreateAISetupForPractice_Patch
    {
        static bool Prefix(SessionSetup __instance)
        {
            if (!Main.enabled || !Main.settings.active)
                return true;

            return false;
        }
    }
    
    [HarmonyPatch(typeof(SessionSetup), "CreateAISetupForQualifyingAndRace")]
    static class SessionSetup_CreateAISetupForQualifyingAndRace_Patch
    {
        static bool Prefix(SessionSetup __instance)
        {
            if (!Main.enabled || !Main.settings.active)
                return true;

            return false;
        }
    }
    
    [HarmonyPatch(typeof(SetupPerformance), "SimulationUpdate")]
    static class SetupPerformance_SimulationUpdate_Patch
    {
        private static MethodInfo baseSimulationUpdateMethodInfo = AccessTools.Method(typeof(PerformanceImpact), "SimulationUpdate");
        private static MethodInfo increaseTimeCostMethodInfo = AccessTools.Method(typeof(PerformanceImpact), "IncreaseTimeCost");
        
        static bool Prefix(SetupPerformance __instance, RacingVehicle ___mVehicle, ref SetupPerformance.SetupArea ___mSetupAreaToFocusOn, 
            ref SessionSetup.SetupOutput ___mSetupOutput, CarPerformanceDesignData ___mCarPerformance)
        {
            if (!Main.enabled || !Main.settings.active)
                return true;
            
            var baseSimulationUpdate = (Action) Activator.CreateInstance(typeof(Action), __instance, baseSimulationUpdateMethodInfo.MethodHandle.GetFunctionPointer());
            var increaseTimeCost = (Action<float>) Activator.CreateInstance(typeof(Action<float>), __instance, increaseTimeCostMethodInfo.MethodHandle.GetFunctionPointer());

            baseSimulationUpdate();

            var optimalSetup = __instance.GetOptimalSetup();
            
            __instance.ClearTimeCost();
            ___mVehicle.setup.GetSetupOutput(ref ___mSetupOutput);
            float num = optimalSetup.setupOutput.aerodynamics - ___mSetupOutput.aerodynamics;
            float num2 = optimalSetup.setupOutput.speedBalance -___mSetupOutput.speedBalance;
            float num3 = optimalSetup.setupOutput.handling - ___mSetupOutput.handling;
            float num4 = (Mathf.Abs(num) + Mathf.Abs(num2) + Mathf.Abs(num3)) / 3f;
            increaseTimeCost((Main.settings.setupParams.customTimeCost ? Main.settings.setupParams.timeCost : ___mCarPerformance.setup.maxSetupTimeCost) * num4);
            
            if (Mathf.Abs(num) > Mathf.Abs(num2) && Mathf.Abs(num) > Mathf.Abs(num3))
            {
                if (num < 0f)
                {
                    ___mSetupAreaToFocusOn = SetupPerformance.SetupArea.Cornering;
                }
                else
                {
                    ___mSetupAreaToFocusOn = SetupPerformance.SetupArea.Straights;
                }
            }
            else if (Mathf.Abs(num2) > Mathf.Abs(num) && Mathf.Abs(num2) > Mathf.Abs(num3))
            {
                if (num2 < 0f)
                {
                    ___mSetupAreaToFocusOn = SetupPerformance.SetupArea.Acceleration;
                }
                else
                {
                    ___mSetupAreaToFocusOn = SetupPerformance.SetupArea.TopSpeed;
                }
            }
            else if (num3 < 0f)
            {
                ___mSetupAreaToFocusOn = SetupPerformance.SetupArea.Understeer;
            }
            else
            {
                ___mSetupAreaToFocusOn = SetupPerformance.SetupArea.Oversteer;
            }

            return false;
        }
    }
}
