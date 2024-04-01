using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;

namespace Assistant
{
    public class DriverAssistOptions
    {
        [Header("Driving")]
        [Draw("Enabled")] public bool driving = false;
        [Draw("Max on the 1st lap", VisibleOn = "driving|True")] public bool boostDrive = false;
        [Draw("Hold tyre temperature", DrawType.Slider, Min = 0, Max = 100, Precision = 0, VisibleOn = "driving|True")] public float temperature = 70f;
        [Header("Engine")]
        [Draw("Enabled")] public bool engine = false;
        [Draw("Max on the 1st lap", VisibleOn = "engine|True")] public bool boostEngine = false;
        [Draw("Planned pitstop", VisibleOn = "engine|True")] public bool plannedPitstop = false;
        [Draw("Hold fuel lap delta", DrawType.Slider, Min = -1, Max = 1, Precision = 2, VisibleOn = "#HoldfuelVisible|True")] public float fuel = 0f;
        [Draw("On lap", DrawType.Slider, Min = 1, Max = 100, Precision = 0, VisibleOn = "#OnlapVisible|True")] public float pitstopOnLap = 100f;

        bool OnlapVisible => engine && plannedPitstop;
        bool HoldfuelVisible => engine && !plannedPitstop;
    }

    public class ManagementAssistOptions
    {
        [Header("Performance")]
        [Draw("Improve")] public bool improvePerformance = false;
        [Space(10)]
        [Header("Reliability")]
        [Draw("Improve")] public bool improveReliability = false;
        [Draw("Improve to", DrawType.Slider, Min = 0, Max = 100, Precision = 0, VisibleOn = "improveReliability|True")] public float improveReliabilityTo = 80f;
    }

    public class PracticeAssistOptions
    {
        [Header("Practice")]
        [Draw("Enabled")] public bool enabled = false;
        [Header("Knowledge")]
        [Draw("Priority")] public KnowledgePriority knowledgePriority = new KnowledgePriority();
    }

    public class KnowledgePriority
    {
        [Draw("Qualification")] public bool qualification;
        [Draw("Race")] public bool race;
        [Draw("First tyre")] public bool firstTyre;
        [Draw("Second tyre")] public bool secondTyre;
        [Draw("Third tyre")] public bool thirdTyre;
    }

    public class Settings : UnityModManager.ModSettings, IDrawable
    {
        [Draw("AI race control")] public bool aiControl = false;
        [Draw("Manual pitstop", VisibleOn = "aiControl|True")] public bool manualPitstop = false;
        [Draw("Driver 1 Assistance Options", Box = true, Collapsible = true, InvisibleOn = "aiControl|True")] public DriverAssistOptions driver1AssistOptions = new DriverAssistOptions();
        [Draw("Driver 2 Assistance Options", Box = true, Collapsible = true, InvisibleOn = "aiControl|True")] public DriverAssistOptions driver2AssistOptions = new DriverAssistOptions();
        [Draw("Practice Assistance Options", Box = true, Collapsible = true)] public PracticeAssistOptions practiceAssistOptions = new PracticeAssistOptions();
        [Draw("Management Assistance Options", Box = true, Collapsible = true)] public ManagementAssistOptions managementAssistOptions = new ManagementAssistOptions();

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }

        public void OnChange()
        {
        }
    }

#if DEBUG
    [EnableReloading]
#endif
    static class Main
    {
        public static bool enabled;
        public static Settings settings;
        public static UnityModManager.ModEntry.ModLogger logger;
        public static bool hasEnduranceSeries2;
        public static Championship.Series EnduranceSeries2;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            settings = Settings.Load<Settings>(modEntry);

            var harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            logger = modEntry.Logger;
            modEntry.OnSaveGUI = OnSaveGUI;
            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = OnGUI;
#if DEBUG
            modEntry.OnUnload = Unload;
#endif
            var names = Enum.GetNames(typeof(Championship.Series));
            if (names.Contains("EnduranceSeries2"))
            {
                hasEnduranceSeries2 = true;
                EnduranceSeries2 = (Championship.Series)Enum.Parse(typeof(Championship.Series), "EnduranceSeries2");
            }

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

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            settings.Draw(modEntry);

        }

        public static bool IsEnduranceSeries(Championship.Series series)
        {
            return series == Championship.Series.EnduranceSeries || hasEnduranceSeries2 && series == EnduranceSeries2;
        }
    }

    [HarmonyPatch(typeof(PartImprovement), "Update")]
    static class PartImprovement_Update_Patch
    {
        static void Prefix(PartImprovement __instance, Team ___mTeam)
        {
            if (Game.instance.time.isPaused || !___mTeam.IsPlayersTeam()) return;

            if (Main.settings.managementAssistOptions.improvePerformance)
            {
                var partList = new List<CarPart>();

                for (int i = 0; i < CarManager.carCount; i++)
                {
                    var car = ___mTeam.carManager.GetCar(i);

                    for (int index = 0; index < car.seriesCurrentParts.Length; ++index)
                    {
                        var p = car.seriesCurrentParts[index];
                        if (p != null && !p.isBanned && p.stats.performance < p.stats.maxPerformance)
                        {
                            partList.Add(p);
                        }
                    }
                }

                if (partList.Count == 0)
                {
                    goto Next;
                }

                __instance.RemoveAllPartImprove(CarPartStats.CarPartStat.Performance);

                var parts = partList.OrderBy(x => x.stats.performance).ToArray();

                foreach (var part in parts)
                {
                    if (__instance.HasAvailableSlot(CarPartStats.CarPartStat.Performance))
                    {
                        __instance.AddPartToImprove(CarPartStats.CarPartStat.Performance, part);
                        continue;
                    }
                    break;
                }
            }

        Next:

            if (Main.settings.managementAssistOptions.improveReliability)
            {
                var partList = new List<CarPart>();

                for (int i = 0; i < CarManager.carCount; i++)
                {
                    var car = ___mTeam.carManager.GetCar(i);

                    for (int index = 0; index < car.seriesCurrentParts.Length; ++index)
                    {
                        var p = car.seriesCurrentParts[index];
                        if (p != null && !p.isBanned && p.stats.reliability < p.stats.maxReliability)
                        {
                            partList.Add(p);
                        }
                    }
                }

                if (partList.Count == 0)
                {
                    return;
                }

                __instance.RemoveAllPartImprove(CarPartStats.CarPartStat.Reliability);

                var parts = partList.OrderBy(x => x.stats.reliability).ToArray();

                var improveReliabilityTo = Main.settings.managementAssistOptions.improveReliabilityTo / 100f;

                __instance.SplitMechanics(0f);
                int limit = Mathf.FloorToInt(__instance.GetWorkRate(CarPartStats.CarPartStat.Reliability) / (improveReliabilityTo - parts[0].stats.reliability));
                int j = 1;
                foreach (var part in parts)
                {
                    __instance.AddPartToImprove(CarPartStats.CarPartStat.Reliability, part);
                    j++;
                    if (j > limit || !__instance.HasAvailableSlot(CarPartStats.CarPartStat.Reliability))
                    {
                        break;
                    }
                }

                if (__instance.partsToImprove[1].Count > 0 && __instance.partsToImprove[3].Count > 0)
                {
                    if (__instance.partsToImprove[1].Count(x => x.stats.reliability < improveReliabilityTo) > 0)
                    {
                        __instance.SplitMechanics(0f);
                        var workRate = __instance.GetWorkRate(CarPartStats.CarPartStat.Reliability) + __instance.GetChiefMechanicWorkRate(CarPartStats.CarPartStat.Reliability);
                        var val = __instance.partsToImprove[1].Select(x => (improveReliabilityTo - x.stats.reliability) / workRate).OrderByDescending(x => x).First();
                        __instance.SplitMechanics(1f - Mathf.Clamp01(val));
                    }
                    else
                    {
                        __instance.SplitMechanics(1f);
                    }
                }
            }
        }
    }

    static class Assistant
    {
        internal class TyreLog
        {
            public TyreSet tyre;
            public float temp;
            public DateTime time;
        }

        internal static readonly TyreLog tyre1 = new TyreLog();
        internal static readonly TyreLog tyre2 = new TyreLog();

        internal static void AssistDrive(DriverAssistOptions options, RacingVehicle vehicle, TyreLog tyreLog)
        {
            if (!options.driving) return;

            if ((Game.instance.time.now - tyreLog.time).TotalSeconds < 20)
                return;

            var mode = vehicle.performance.drivingStyleMode;
            var tyre = vehicle.setup.tyreSet;
            float temp = tyre.GetTemperature();
            if (tyreLog.tyre != tyre)
            {
                tyreLog.tyre = tyre;
                tyreLog.temp = temp;
                tyreLog.time = Game.instance.time.now;
                mode = DrivingStyle.Mode.Neutral;
            }

            var m = mode;
            var changeRate = 0.08f;
            var changeRate2 = changeRate * 2;
            var tempChangeRate = (float)((temp - tyreLog.temp) / (Game.instance.time.now - tyreLog.time).TotalMinutes);
            var t = options.temperature / 100f;

            if (temp < t + 0.04f && temp > t - 0.04f && tempChangeRate > -changeRate && tempChangeRate < changeRate)
            {
            }
            else if (temp < t - 0.2f)
            {
                if (tempChangeRate < changeRate2)
                    mode = GetIncreaseDrivingStyle(mode);
            }
            else if (temp > t + 0.2f)
            {
                if (tempChangeRate > -changeRate2)
                    mode = GetDecreaseDrivingStyle(mode);
            }
            else if (temp < t)
            {
                if (tempChangeRate < changeRate)
                    mode = GetIncreaseDrivingStyle(mode);
            }
            else if (temp > t)
            {
                if (tempChangeRate > -changeRate)
                    mode = GetDecreaseDrivingStyle(mode);
            }

            if (vehicle.timer.lap == 0 && options.boostDrive)
            {
                mode = DrivingStyle.Mode.Attack;
            }

            vehicle.performance.drivingStyle.SetDrivingStyle(mode);

            //            Main.logger.Log($"{m}->{mode} {tempChangeRate} {temp} {tyreLog.temp} {(Game.instance.time.now - tyreLog.time).TotalMinutes}");

            tyreLog.temp = temp;
            tyreLog.time = Game.instance.time.now;
        }

        public static DrivingStyle.Mode GetIncreaseDrivingStyle(DrivingStyle.Mode mode)
        {
            if (mode != DrivingStyle.Mode.Attack)
            {
                mode--;
            }

            return mode;
        }

        public static DrivingStyle.Mode GetDecreaseDrivingStyle(DrivingStyle.Mode mode)
        {
            if (mode != DrivingStyle.Mode.BackUp)
            {
                mode++;
            }

            return mode;
        }

        internal static void AssistEngine(DriverAssistOptions options, RacingVehicle vehicle)
        {
            if (!options.engine || Game.instance.sessionManager.eventDetails.currentSession.sessionType != SessionDetails.SessionType.Race) return;

            var mode = Fuel.EngineMode.Medium;

            float fuelLapsRemainingDecimal = vehicle.performance.fuel.GetFuelLapsRemainingDecimal();
            float fuelLapDelta = vehicle.performance.fuel.GetTargetFuelLapDelta();


            //if (vehicle.championship.rules.isRefuelingOn)
            //{
            //    var delta = fuelLapsRemainingDecimal - tyreLapRange;
            //    if (delta < 0 && fuelLapsRemainingDecimal > lapsRemaining || delta > 0 && tyreLapRange > lapsRemaining)
            //        delta = fuelLapDelta;

            //    if (delta > options.fuel - 0.05f && delta < options.fuel + 0.05f)
            //    {
            //        mode = Fuel.EngineMode.Medium;
            //    }
            //    else if (delta > options.fuel + 0.2f)
            //    {
            //        mode = Fuel.EngineMode.Overtake;
            //    }
            //    else if (delta > options.fuel)
            //    {
            //        mode = Fuel.EngineMode.High;
            //    }
            //    else if (delta < options.fuel)
            //    {
            //        mode = Fuel.EngineMode.Low;
            //    }
            //}
            //else if (fuelLapDelta > options.fuel - 0.05f && fuelLapDelta < options.fuel + 0.05f)
            //{
            //    mode = Fuel.EngineMode.Medium;
            //}
            //else if (fuelLapDelta > options.fuel + 0.2f)
            //{
            //    mode = Fuel.EngineMode.Overtake;
            //}
            //else if (fuelLapDelta > options.fuel)
            //{
            //    mode = Fuel.EngineMode.High;
            //}
            //else if (fuelLapDelta < options.fuel)
            //{
            //    mode = Fuel.EngineMode.Low;
            //}

            if (vehicle.championship.rules.isRefuelingOn)
            {
                if (options.plannedPitstop)
                {
                    var currentLap = vehicle.timer.lap;
                    var distanceAlongTrackPath01 = vehicle.pathController.distanceAlongTrackPath01;
                    if (distanceAlongTrackPath01 == 1)
                        distanceAlongTrackPath01 = 0;
                    var delta = fuelLapsRemainingDecimal - (options.pitstopOnLap - currentLap - distanceAlongTrackPath01);
                    if (options.pitstopOnLap < currentLap)
                    {
                        options.plannedPitstop = false;
                        options.pitstopOnLap = 0;
                        delta = 0;
                    }
                    if (delta > -0.05f && delta < 0.05f)
                    {
                        mode = Fuel.EngineMode.Medium;
                    }
                    else if (delta > 0.2f)
                    {
                        mode = Fuel.EngineMode.Overtake;
                    }
                    else if (delta > 0)
                    {
                        mode = Fuel.EngineMode.High;
                    }
                    else if (delta < 0)
                    {
                        mode = Fuel.EngineMode.Low;
                    }
                }
                else
                {
                    float lapLength = GameUtility.MilesToMeters(Game.instance.sessionManager.eventDetails.circuit.trackLengthMiles);
                    var tyreLapRange = TyreSet.CalculateLapRangeOfTyre(vehicle.setup.tyreSet, lapLength) * Math.Max(0, vehicle.setup.tyreSet.GetCondition() /*- vehicle.setup.tyreSet.GetCliffCondition()*/);
                    var lapsRemaining = vehicle.GetLapsRemainingDecimal();

                    var delta = fuelLapsRemainingDecimal - tyreLapRange;
                    if (delta < 0 && fuelLapsRemainingDecimal > lapsRemaining || delta > 0 && tyreLapRange > lapsRemaining)
                        delta = fuelLapDelta;

                    if (delta > options.fuel - 0.05f && delta < options.fuel + 0.05f)
                    {
                        mode = Fuel.EngineMode.Medium;
                    }
                    else if (delta > options.fuel + 0.2f)
                    {
                        mode = Fuel.EngineMode.Overtake;
                    }
                    else if (delta > options.fuel)
                    {
                        mode = Fuel.EngineMode.High;
                    }
                    else if (delta < options.fuel)
                    {
                        mode = Fuel.EngineMode.Low;
                    }
                }
            }
            else if (fuelLapDelta > options.fuel - 0.05f && fuelLapDelta < options.fuel + 0.05f)
            {
                mode = Fuel.EngineMode.Medium;
            }
            else if (fuelLapDelta > options.fuel + 0.2f)
            {
                mode = Fuel.EngineMode.Overtake;
            }
            else if (fuelLapDelta > options.fuel)
            {
                mode = Fuel.EngineMode.High;
            }
            else if (fuelLapDelta < options.fuel)
            {
                mode = Fuel.EngineMode.Low;
            }

            if (vehicle.car.seriesCurrentParts[1].partCondition.IsOnRed() && mode < Fuel.EngineMode.Medium)
            {
                mode = Fuel.EngineMode.Medium;
            }

            if (vehicle.timer.lap == 0 && options.boostEngine)
            {
                mode = Fuel.EngineMode.Overtake;
            }

            //            Main.logger.Log($"{mode} {fuelLapDelta} {fuelLapsRemainingDecimal - tyreLapRange}");

            vehicle.performance.fuel.SetEngineMode(mode);
        }

        public static float GetLapsRemainingDecimal(this RacingVehicle instance)
        {
            if (Main.IsEnduranceSeries(instance.championship.series))
            {
                return Mathf.Clamp01(1f - Game.instance.sessionManager.GetNormalizedSessionTime()) * Game.instance.sessionManager.duration / instance.performance.estimatedBestLapTime;
            }
            return (float)Game.instance.sessionManager.lapCount * Mathf.Clamp01(1f - instance.pathController.GetRaceDistanceTraveled01()) - (float)instance.GetLapsBehindLeader();
        }

        public static int GetLapsRemaining(this RacingVehicle instance)
        {
            return Mathf.FloorToInt(instance.GetLapsRemainingDecimal());
        }
    }

    [HarmonyPatch(typeof(SessionStrategy), "OnEnterGate")]
    static class SessionStrategy_OnEnterGate_Patch
    {
        static void Prefix(SessionStrategy __instance, RacingVehicle ___mVehicle, int inGateID, PathData.GateType inGateType)
        {
            if (!Main.enabled || inGateID % 5 != 0) return;

            var sessionType = Game.instance.sessionManager.eventDetails.currentSession.sessionType;
            var vehicle = ___mVehicle;

            var usesAI = vehicle.driver.personalityTraitController.UsesAIForStrategy(vehicle) || Game.instance.sessionManager.isUsingAIForPlayerDrivers;

            if (!Main.settings.aiControl && (sessionType == SessionDetails.SessionType.Race || sessionType == SessionDetails.SessionType.Practice))
            {
                if (usesAI || !vehicle.isPlayerDriver)
                    return;

                if (vehicle.pathState.IsInPitlaneArea() || vehicle.timer.hasSeenChequeredFlag || Game.instance.sessionManager.isSafetyCarFlag)
                {
                    vehicle.performance.drivingStyle.SetDrivingStyle(DrivingStyle.Mode.BackUp);
                    vehicle.performance.fuel.SetEngineMode(Fuel.EngineMode.Low);
                    return;
                }

                if (vehicle.carID == 0)
                {
                    Assistant.AssistDrive(Main.settings.driver1AssistOptions, vehicle, Assistant.tyre1);
                    if (inGateID % 30 == 0)
                        Assistant.AssistEngine(Main.settings.driver1AssistOptions, vehicle);
                }
                else if (vehicle.carID == 1)
                {
                    Assistant.AssistDrive(Main.settings.driver2AssistOptions, vehicle, Assistant.tyre2);
                    if (inGateID % 30 == 0)
                        Assistant.AssistEngine(Main.settings.driver2AssistOptions, vehicle);
                }
            }

            if (Main.settings.practiceAssistOptions.enabled && sessionType == SessionDetails.SessionType.Practice
                && usesAI && vehicle.isPlayerDriver && vehicle.pathController.GetCurrentPath().pathType == PathController.PathType.Track)
            {
                if (vehicle.strategy.IsGoingToPit())
                    return;

                bool qualifyingBasedActive = vehicle.driver.contract.GetTeam().championship.rules.qualifyingBasedActive;
                int qualificationLevel = vehicle.practiceKnowledge.practiceReport.GetKnowledgeOfType(PracticeReportSessionData.KnowledgeType.QualifyingTrim).lastUnlockedLevel;
                int raceLevel = vehicle.practiceKnowledge.practiceReport.GetKnowledgeOfType(PracticeReportSessionData.KnowledgeType.RaceTrim).lastUnlockedLevel;
                var trim = vehicle.setup.currentSetup.trim;
                var needToPit = false;

                if (qualifyingBasedActive)
                {
                    if (Main.settings.practiceAssistOptions.knowledgePriority.qualification)
                    {
                        if (trim == SessionSetup.Trim.Qualifying && qualificationLevel == 3 && qualificationLevel != raceLevel)
                            needToPit = true;
                    }
                    if (Main.settings.practiceAssistOptions.knowledgePriority.race)
                    {
                        if (trim == SessionSetup.Trim.Race && raceLevel == 3 && qualificationLevel != raceLevel)
                            needToPit = true;
                    }
                }

                var compounds = vehicle.driver.contract.GetTeam().championship.rules.compoundsAvailable;
                int firstTyreLevel = vehicle.practiceKnowledge.practiceReport.GetKnowledgeOfType(PracticeReportSessionData.KnowledgeType.FirstOptionTyres).lastUnlockedLevel;
                int secondTyreLevel = vehicle.practiceKnowledge.practiceReport.GetKnowledgeOfType(PracticeReportSessionData.KnowledgeType.SecondOptionTyres).lastUnlockedLevel;
                int thirdTyreLevel = compounds == 2 ? 3 : vehicle.practiceKnowledge.practiceReport.GetKnowledgeOfType(PracticeReportSessionData.KnowledgeType.ThirdOptionTyres).lastUnlockedLevel;

                if (Main.settings.practiceAssistOptions.knowledgePriority.firstTyre)
                {
                    var firstTyreCompound = __instance.GetTyre(SessionStrategy.TyreOption.First, 0).GetCompound();
                    if (vehicle.setup.tyreSet.GetCompound() == firstTyreCompound && firstTyreLevel == 3
                        && (firstTyreLevel != secondTyreLevel || firstTyreLevel != thirdTyreLevel))
                        needToPit = true;
                }
                if (Main.settings.practiceAssistOptions.knowledgePriority.secondTyre)
                {
                    var secondTyreCompound = __instance.GetTyre(SessionStrategy.TyreOption.Second, 0).GetCompound();
                    if (vehicle.setup.tyreSet.GetCompound() == secondTyreCompound && secondTyreLevel == 3
                        && (secondTyreLevel != firstTyreLevel || secondTyreLevel != thirdTyreLevel))
                        needToPit = true;
                }
                if (Main.settings.practiceAssistOptions.knowledgePriority.thirdTyre && compounds == 3)
                {
                    var thirdTyreCompound = __instance.GetTyre(SessionStrategy.TyreOption.Third, 0).GetCompound();
                    if (vehicle.setup.tyreSet.GetCompound() == thirdTyreCompound && thirdTyreLevel == 3
                        && (thirdTyreLevel != firstTyreLevel || thirdTyreLevel != secondTyreLevel))
                        needToPit = true;
                }
                if (needToPit)
                {
                    __instance.ReturnToGarage();
                }
            }
        }
    }

    [HarmonyPatch(typeof(SessionSetup), "SetTargetTrim", new Type[] { })]
    static class SessionSetup_SetTargetTrim_Patch
    {
        public static void Postfix(SessionSetup __instance)
        {
            if (!Main.enabled || !Main.settings.practiceAssistOptions.enabled || !__instance.vehicle.isPlayerDriver
                || !(__instance.vehicle.driver.personalityTraitController.UsesAIForStrategy(__instance.vehicle) || Game.instance.sessionManager.isUsingAIForPlayerDrivers))
                return;

            var sessionType = Game.instance.sessionManager.eventDetails.currentSession.sessionType;

            switch (sessionType)
            {
                case SessionDetails.SessionType.Practice:
                    bool qualifyingBasedActive = __instance.vehicle.driver.contract.GetTeam().championship.rules.qualifyingBasedActive;
                    if (qualifyingBasedActive)
                    {
                        int qualificationLevel = 0;
                        int raceLevel = 0;
                        if (__instance.vehicle.practiceKnowledge.practiceReport != null)
                        {
                            qualificationLevel = __instance.vehicle.practiceKnowledge.practiceReport.GetKnowledgeOfType(PracticeReportSessionData.KnowledgeType.QualifyingTrim).lastUnlockedLevel;
                            raceLevel = __instance.vehicle.practiceKnowledge.practiceReport.GetKnowledgeOfType(PracticeReportSessionData.KnowledgeType.RaceTrim).lastUnlockedLevel;
                        }

                        if (Main.settings.practiceAssistOptions.knowledgePriority.qualification && qualificationLevel < 3 || raceLevel == 3)
                        {
                            __instance.sessionPitStop.currentSetup.trim = SessionSetup.Trim.Qualifying;
                            __instance.sessionPitStop.targetSetup.trim = SessionSetup.Trim.Qualifying;
                            __instance.vehicle.practiceKnowledge.knowledgeType = PracticeReportSessionData.KnowledgeType.QualifyingTrim;
                        }
                        else
                        {
                            __instance.sessionPitStop.currentSetup.trim = SessionSetup.Trim.Race;
                            __instance.sessionPitStop.targetSetup.trim = SessionSetup.Trim.Race;
                            __instance.vehicle.practiceKnowledge.knowledgeType = PracticeReportSessionData.KnowledgeType.RaceTrim;
                        }
                    }
                    else
                    {
                        __instance.sessionPitStop.currentSetup.trim = SessionSetup.Trim.Race;
                        __instance.sessionPitStop.targetSetup.trim = SessionSetup.Trim.Race;
                        __instance.vehicle.practiceKnowledge.knowledgeType = PracticeReportSessionData.KnowledgeType.RaceTrim;
                    }

                    break;

                    //				case SessionDetails.SessionType.Qualifying:
                    //					__instance.sessionPitStop.currentSetup.trim = SessionSetup.Trim.Qualifying;
                    //					__instance.sessionPitStop.targetSetup.trim = SessionSetup.Trim.Qualifying;
                    //					break;
                    //				
                    //				case SessionDetails.SessionType.Race:
                    //					__instance.sessionPitStop.currentSetup.trim = SessionSetup.Trim.Race;
                    //					__instance.sessionPitStop.targetSetup.trim = SessionSetup.Trim.Race;
                    //					break;
            }
        }
    }

    [HarmonyPatch(typeof(SessionSetup), "SetTargetTrim", typeof(SessionSetup.Trim))]
    static class SessionSetup_SetTargetTrim2_Patch
    {
        static void Postfix(SessionSetup __instance, SessionSetup.Trim inTrim)
        {
            if (!Main.enabled || !Main.settings.practiceAssistOptions.enabled || !__instance.vehicle.isPlayerDriver
                || !(__instance.vehicle.driver.personalityTraitController.UsesAIForStrategy(__instance.vehicle) || Game.instance.sessionManager.isUsingAIForPlayerDrivers))
                return;

            var sessionType = Game.instance.sessionManager.eventDetails.currentSession.sessionType;
            switch (sessionType)
            {
                case SessionDetails.SessionType.Practice:
                    bool qualifyingBasedActive = __instance.vehicle.driver.contract.GetTeam().championship.rules.qualifyingBasedActive;
                    if (qualifyingBasedActive)
                    {
                        int qualificationLevel = 0;
                        int raceLevel = 0;
                        if (__instance.vehicle.practiceKnowledge.practiceReport != null)
                        {
                            qualificationLevel = __instance.vehicle.practiceKnowledge.practiceReport.GetKnowledgeOfType(PracticeReportSessionData.KnowledgeType.QualifyingTrim).lastUnlockedLevel;
                            raceLevel = __instance.vehicle.practiceKnowledge.practiceReport.GetKnowledgeOfType(PracticeReportSessionData.KnowledgeType.RaceTrim).lastUnlockedLevel;
                        }

                        if (Main.settings.practiceAssistOptions.knowledgePriority.qualification && qualificationLevel < 3 || raceLevel == 3)
                        {
                            __instance.sessionPitStop.targetSetup.trim = SessionSetup.Trim.Qualifying;
                        }
                        else
                        {
                            __instance.sessionPitStop.targetSetup.trim = SessionSetup.Trim.Race;
                        }
                    }
                    else
                    {
                        __instance.sessionPitStop.targetSetup.trim = SessionSetup.Trim.Race;
                    }

                    break;
            }
        }
    }

    [HarmonyPatch(typeof(SessionStrategy), "GetSlickTyre")]
    static class SessionStrategy_GetSlickTyre_Patch
    {
        static bool Prefix(SessionStrategy __instance, ref TyreSet __result, RacingVehicle ___mVehicle, SessionDetails.SessionType inSessionType, float inNormalizedTime)
        {
            var vehicle = ___mVehicle;
            if (!Main.enabled || !Main.settings.practiceAssistOptions.enabled || !vehicle.isPlayerDriver || inSessionType != SessionDetails.SessionType.Practice)
                return true;

            var compounds = vehicle.driver.contract.GetTeam().championship.rules.compoundsAvailable;
            int firstTyreLevel = vehicle.practiceKnowledge.practiceReport.GetKnowledgeOfType(PracticeReportSessionData.KnowledgeType.FirstOptionTyres).lastUnlockedLevel;
            int secondTyreLevel = vehicle.practiceKnowledge.practiceReport.GetKnowledgeOfType(PracticeReportSessionData.KnowledgeType.SecondOptionTyres).lastUnlockedLevel;
            int thirdTyreLevel = compounds == 2 ? 3 : vehicle.practiceKnowledge.practiceReport.GetKnowledgeOfType(PracticeReportSessionData.KnowledgeType.ThirdOptionTyres).lastUnlockedLevel;

            var changeOn = SessionStrategy.TyreOption.First;
            var change = false;
            if (Main.settings.practiceAssistOptions.knowledgePriority.thirdTyre)
            {
                if (thirdTyreLevel < 3)
                {
                    changeOn = SessionStrategy.TyreOption.Third;
                    change = true;
                }
            }
            if (Main.settings.practiceAssistOptions.knowledgePriority.secondTyre)
            {
                if (secondTyreLevel < 3)
                {
                    changeOn = SessionStrategy.TyreOption.Second;
                    change = true;
                }
            }
            if (Main.settings.practiceAssistOptions.knowledgePriority.firstTyre)
            {
                if (firstTyreLevel < 3)
                {
                    changeOn = SessionStrategy.TyreOption.First;
                    change = true;
                }
            }
            if (!change)
            {
                if (firstTyreLevel < 3)
                    changeOn = SessionStrategy.TyreOption.First;
                else if (secondTyreLevel < 3)
                    changeOn = SessionStrategy.TyreOption.Second;
                else if (thirdTyreLevel < 3)
                    changeOn = SessionStrategy.TyreOption.Third;
            }

            var tyres = __instance.GetTyres(changeOn);
            foreach (var t in tyres)
            {
                if (t.GetCondition() > 0.4f)
                {
                    __result = t;
                    return false;
                }
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(SessionStrategy), "DoesVehicleNeedToPit")]
    static class SessionStrategy_DoesVehicleNeedToPit_Patch
    {
        static bool Prefix(SessionStrategy __instance, ref bool __result, bool inIsPitlaneEntryGate, RacingVehicle ___mVehicle)
        {
            if (Main.enabled && Main.settings.aiControl && Main.settings.manualPitstop
                && ___mVehicle.isPlayerDriver && !Game.instance.sessionManager.IsSessionEnding())
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(PersonalityTraitController_v2), "UsesAIForStrategy")]
    static class PersonalityTraitController_v2_UsesAIForStrategy_Patch
    {
        static bool Prefix(PersonalityTraitController_v2 __instance, ref bool __result, RacingVehicle inVehicle)
        {
            if (Main.enabled && Main.settings.aiControl)
            {
                __result = true;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(SessionManager), "isUsingAIForPlayerDrivers", MethodType.Getter)]
    static class SessionManager_isUsingAIForPlayerDrivers_Patch
    {
        static bool Prefix(SessionManager __instance, ref bool __result)
        {
            if (Main.enabled && Main.settings.aiControl)
            {
                __result = true;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(SessionStrategy), "OnSafetyCarEvent")]
    static class SessionStrategy_OnSafetyCarEvent_Patch
    {
        static bool Prefix(SessionStrategy __instance, RacingVehicle ___mVehicle)
        {
            if (Main.enabled && Main.settings.aiControl && Main.settings.manualPitstop
                && ___mVehicle.isPlayerDriver && !Game.instance.sessionManager.IsSessionEnding())
            {
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(SessionStrategy), "OnExitPitlane")]
    static class SessionStrategy_OnExitPitlane_Patch
    {
        static void Postfix(SessionStrategy __instance, RacingVehicle ___mVehicle)
        {
            var vehicle = ___mVehicle;
            if (Main.enabled && vehicle.isPlayerDriver && !Main.settings.aiControl && vehicle.championship.rules.isRefuelingOn
                && !Game.instance.sessionManager.IsSessionEnding() && Game.instance.sessionManager.sessionType == SessionDetails.SessionType.Race)
            {
                /*var value = 0f;
                if (Main.IsEnduranceSeries(vehicle.championship.series))
                {
                    value = Mathf.RoundToInt(vehicle.timer.lap + vehicle.pathController.distanceAlongTrackPath01 + vehicle.performance.fuel.GetFuelLapsRemainingDecimal());
                }
                else
                {
                    value = Mathf.RoundToInt(Game.instance.sessionManager.lapCount * vehicle.pathController.GetRaceDistanceTraveled01() + vehicle.performance.fuel.GetFuelLapsRemainingDecimal());
                }

                value = Mathf.Min(value, vehicle.GetLapsRemaining());

                if (vehicle.carID == 0 && Main.settings.driver1AssistOptions.engine && Main.settings.driver1AssistOptions.plannedPitstop)
                {
                    Main.settings.driver1AssistOptions.pitstopOnLap = value;
                }
                if (vehicle.carID == 1 && Main.settings.driver2AssistOptions.engine && Main.settings.driver2AssistOptions.plannedPitstop)
                {
                    Main.settings.driver2AssistOptions.pitstopOnLap = value;
                }*/

                if (vehicle.carID == 0 && Main.settings.driver1AssistOptions.engine && Main.settings.driver1AssistOptions.plannedPitstop)
                {
                    Main.settings.driver1AssistOptions.plannedPitstop = false;
                    Main.settings.driver1AssistOptions.pitstopOnLap = 0;
                }
                if (vehicle.carID == 1 && Main.settings.driver2AssistOptions.engine && Main.settings.driver2AssistOptions.plannedPitstop)
                {
                    Main.settings.driver2AssistOptions.plannedPitstop = false;
                    Main.settings.driver2AssistOptions.pitstopOnLap = 0;
                }
            }
        }
    }

    [HarmonyPatch(typeof(GridPathState), "OnEnter")]
    static class GridPathState_OnEnter_Patch
    {
        static void Prefix(GridPathState __instance)
        {
            if (__instance.vehicle is RacingVehicle)
            { 
                var vehicle = (RacingVehicle)__instance.vehicle;
                if (Main.enabled && vehicle.isPlayerDriver && !Main.settings.aiControl && vehicle.championship.rules.isRefuelingOn
                    && !Game.instance.sessionManager.IsSessionEnding() && Game.instance.sessionManager.sessionType == SessionDetails.SessionType.Race)
                {
                    /*var value = 0f;
                    if (Main.IsEnduranceSeries(vehicle.championship.series))
                    {
                        value = Mathf.RoundToInt(vehicle.timer.lap + vehicle.pathController.distanceAlongTrackPath01 + vehicle.performance.fuel.GetFuelLapsRemainingDecimal());
                    }
                    else
                    {
                        value = Mathf.RoundToInt(Game.instance.sessionManager.lapCount * vehicle.pathController.GetRaceDistanceTraveled01() + vehicle.performance.fuel.GetFuelLapsRemainingDecimal());
                    }

                    value = Mathf.Min(value, vehicle.GetLapsRemaining());*/

                    if (vehicle.carID == 0 && Main.settings.driver1AssistOptions.engine && Main.settings.driver1AssistOptions.plannedPitstop)
                    {
                        Main.settings.driver1AssistOptions.plannedPitstop = false;
                        Main.settings.driver1AssistOptions.pitstopOnLap = 0;
                    }
                    if (vehicle.carID == 1 && Main.settings.driver2AssistOptions.engine && Main.settings.driver2AssistOptions.plannedPitstop)
                    {
                        Main.settings.driver2AssistOptions.plannedPitstop = false;
                        Main.settings.driver2AssistOptions.pitstopOnLap = 0;
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(SessionManager), "PrepareForSessionAfterLoad")]
    static class SessionManager_PrepareForSessionAfterLoad_Patch
    {
        static void Postfix(SessionManager __instance)
        {
            if (Main.enabled)
            {
                Main.settings.driver1AssistOptions.plannedPitstop = false;
                Main.settings.driver1AssistOptions.pitstopOnLap = 0;
                Main.settings.driver2AssistOptions.plannedPitstop = false;
                Main.settings.driver2AssistOptions.pitstopOnLap = 0;
            }
        }
    }
}