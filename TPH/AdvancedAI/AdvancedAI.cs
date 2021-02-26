using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Harmony12;
using UnityModManagerNet;
using UnityEngine;
using TH20;
using FullInspector;
using TH20.BTA;
using BehaviorDesigner.Runtime.Tasks;
using TH20.BT_Types;

namespace AdvancedAI
{
    public class Settings : UnityModManager.ModSettings
    {
        public int SendtoHomeTreatmentChance = 70;
        public int QueueOrder = 1;
        public bool SendtoHomeIfHospitalFull = false;
        public bool SendtoHomeIfRoomNotExists = false;
        public bool TakeBreakAfterTraining = true;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }

    static class Main
    {
        public static bool enabled;
        public static Settings settings;
        public static UnityModManager.ModEntry.ModLogger Logger;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            var harmony = HarmonyInstance.Create(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            settings = Settings.Load<Settings>(modEntry);
            settings.SendtoHomeTreatmentChance = Mathf.Clamp(settings.SendtoHomeTreatmentChance, 0, 100);
            settings.QueueOrder = Mathf.Clamp(settings.QueueOrder, 0, queueOrder.Length - 1);

            Logger = modEntry.Logger;

            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;

            return true;
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
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

        static string[] queueOrder = new string[]
        {
            "Normal",
            "Health",
            "Time in hospital"
        };

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("GP: Send home if treatment chance less than (0(disabled)-100) ", GUILayout.ExpandWidth(false));
            var inString = settings.SendtoHomeTreatmentChance.ToString();
            var outString = GUILayout.TextField(inString, 3, GUILayout.Width(50));
            if (outString != inString && int.TryParse(outString, out var result))
            {
                settings.SendtoHomeTreatmentChance = Mathf.Clamp(result, 0, 100);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("GP: Send home if room does not exist ", GUILayout.ExpandWidth(false));
            settings.SendtoHomeIfRoomNotExists = GUILayout.Toggle(settings.SendtoHomeIfRoomNotExists, "", GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Reception: Send home if GP offices are full (based on queue length warning) ", GUILayout.ExpandWidth(false));
            settings.SendtoHomeIfHospitalFull = GUILayout.Toggle(settings.SendtoHomeIfHospitalFull, "", GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Queue order: ", GUILayout.ExpandWidth(false));

            for (int i = 0; i < queueOrder.Length; i++)
            {
                var value = settings.QueueOrder == i;
                var @new = GUILayout.Toggle(value, queueOrder[i], GUILayout.ExpandWidth(false));
                if (@new != value && @new == true)
                {
                    settings.QueueOrder = i;
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Send staff on break after training ", GUILayout.ExpandWidth(false));
            settings.TakeBreakAfterTraining = GUILayout.Toggle(settings.TakeBreakAfterTraining, "", GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();
        }

        static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            settings.Save(modEntry);
        }

        public static void UpdateGameAlgorithmsConfig()
        {
            if (!enabled)
                return;

            GameAlgorithmsConfig config = null;

            var obj = GameObject.FindObjectOfType<MainScript>();
            if (obj)
            {
                var appConfig = Traverse.Create(obj).Field("_appConfig").GetValue<SharedInstance<AppConfig>>();
                config = appConfig?.Instance?.GameAlgorithmsConfig?.Instance;
            }

            if (config == null)
                return;

            Main.Logger.Log("Updating GameAlgorithmsConfig");

            var traverse = Traverse.Create(config);
            traverse.Field("MaxRoomQueueDistance").SetValue(15);
        }
    }

    [HarmonyPatch(typeof(DiagnosisTreatmentComponent), "ProcessDiagnosis")]
    static class DiagnosisTreatmentComponent_ProcessDiagnosis_Patch
    {
        static bool Prefix(DiagnosisTreatmentComponent __instance, Patient patient, Staff ____doctor, Room ____room)
        {
            if (!Main.enabled || (Main.settings.SendtoHomeTreatmentChance == 0 && !Main.settings.SendtoHomeIfRoomNotExists))
                return true;
            try
            {
                Level level = patient.Level;
                ResearchManager researchManager = level.ResearchManager;
                if (____doctor.ModifiersComponent != null)
                {
                    ____doctor.ModifiersComponent.ApplyInteractWithOtherModifiers(patient);
                }
                float certainty = GameAlgorithms.GetDiagnosisCertainty(patient, ____room, ____doctor, researchManager).Certainty;
                patient.ReceiveDiagnosis(____room, ____doctor, certainty);
                ____room.OnUnitProcessed();
                if (__instance.Level.HospitalPolicy.AutoSendForTreatment && patient.FullyDiagnosed())
                {
                    var roomDef = patient.Illness.GetTreatmentRoom(patient, researchManager);
                    patient.SendToTreatmentRoom(roomDef, immediately: true);
                    bool haveRoom = GameAlgorithms.DoesHospitalHaveRoom(patient.Level.WorldState, roomDef._type);
                    var knownnIllness = patient.Level.GameplayStatsTracker.HasIllnessBeenDiagnosedBefore(patient.Illness);
                    var treatmentChance = 100f; //this is just so the check against send home on treatmeant chance is always false if it's set to 0 (disabled)

                    if (Main.settings.SendtoHomeTreatmentChance > 0)
                        treatmentChance = CalculateAverageTreatmentChance(patient, roomDef);
                    if (knownnIllness && (haveRoom && treatmentChance < Main.settings.SendtoHomeTreatmentChance || !haveRoom && Main.settings.SendtoHomeIfRoomNotExists))
                    {
                        SendHome(patient);
                        //Main.Logger.Log($"send home {patient.Name} room {roomDef._type} diagnosis {patient.DiagnosisCertainty} treatment {treatmentChance} room exists {haveRoom}");
                    }
                    return false;
                }
                if (____room.Definition._type == RoomDefinition.Type.GPOffice)
                {
                    if (patient.FullyDiagnosed())
                    {
                        var roomDef = patient.Illness.GetTreatmentRoom(patient, researchManager);
                        patient.SendToTreatmentRoom(roomDef, true);

                        bool haveRoom = GameAlgorithms.DoesHospitalHaveRoom(patient.Level.WorldState, roomDef._type);
                        var knownnIllness = patient.Level.GameplayStatsTracker.HasIllnessBeenDiagnosedBefore(patient.Illness);
                        var treatmentChance = 100f; //this is just so the check against send home on treatmeant chance is always false if it's set to 0 (disabled)

                        if (Main.settings.SendtoHomeTreatmentChance > 0)
                            treatmentChance = CalculateAverageTreatmentChance(patient, roomDef);
                        if (knownnIllness && (haveRoom && treatmentChance < Main.settings.SendtoHomeTreatmentChance || !haveRoom && Main.settings.SendtoHomeIfRoomNotExists))
                        {
                            SendHome(patient);
                            //Main.Logger.Log($"send home {patient.Name} room {roomDef._type} diagnosis {patient.DiagnosisCertainty} treatment {treatmentChance} room exists {haveRoom}");
                        }
                    }
                    else
                    {
                        patient.SendToDiagnosisRoom(Traverse.Create(__instance).Method("GetDiagnosisRoom", new Type[] { typeof(Patient), typeof(Staff) }).GetValue<Room>(patient, ____doctor));
                    }
                }
                else
                {
                    Room bestRoomOfType = GameAlgorithms.GetBestRoomOfType(level.WorldState, RoomDefinition.Type.GPOffice, RoomUseType.Any, patient);
                    if (bestRoomOfType != null)
                    {
                        patient.GotoRoom(bestRoomOfType, ReasonUseRoom.Diagnosis, false, -1);
                    }
                    else
                    {
                        patient.WaitForRoomToBeBuilt(RoomDefinition.Type.GPOffice, ReasonUseRoom.Diagnosis, GameAlgorithms.Config.PatientWaitLongTime);
                    }
                }
            }
            catch (Exception e)
            {
                Main.Logger.Error(e.ToString());
                return true;
            }
            return false;
        }

        public static void SendHome(this Patient patient)
        {
            try
            {
                var IModeChange = typeof(Character).GetNestedType("IModeChange", BindingFlags.NonPublic);
                var SendHomeModeChange = typeof(Patient).GetNestedType("SendHomeModeChange", BindingFlags.NonPublic);
                var obj = Activator.CreateInstance(SendHomeModeChange, patient);
                if (patient.CurrentMode != Patient.Mode.Dead && patient.CurrentMode != Patient.Mode.SentHome && Traverse.Create((Character)patient).Method("ChangeMode", new Type[] { IModeChange }).GetValue<bool>(obj))
                {
                    patient.Level.StatusIconManager.ShowStatusIcon(patient, StatusIcon.Type.SentHome);
                    Traverse.Create(obj).Field("LeaveNow").SetValue(true);
                }
            }
            catch (Exception e)
            {
                Main.Logger.Error(e.ToString());
            }
        }

        static List<Room> _roomsCache = new List<Room>();

        public static float CalculateAverageTreatmentChance(this Patient patient, RoomDefinition roomDef)
        {
            if (roomDef == null)
                return 0;

            Level level = patient.Level;
            level.WorldState.GetRoomsOfType(roomDef._type, false, _roomsCache);
            int count = 0;
            float chanceOfSuccess = 0;
            foreach (Room room in _roomsCache)
            {
                if (room.WhoCanUse.IsMember(patient) && room.IsFunctional())
                {
                    try
                    {
                        var staff = room.AssignedStaff.FirstOrDefault(x => x.Definition._type == StaffDefinition.Type.Doctor);
                        staff = staff ?? room.AssignedStaff.FirstOrDefault(x => x.Definition._type == StaffDefinition.Type.Nurse);
                        if (staff == null || staff.Definition._type == StaffDefinition.Type.Nurse && !room.RequiredStaffAssigned())
                        {
                            var leaveHistory = Room_StaffLeaveRoom_Patch.leaveHistory;
                            for (int i = leaveHistory.Count - 1; i >= 0; i--)
                            {
                                if (leaveHistory.ElementAt(i).roomId == room.ID)
                                {
                                    staff = patient.Level.CharacterManager.StaffMembers.Find(x => x.ID == leaveHistory.ElementAt(i).staffId);
                                    if (staff != null)
                                        break;
                                }
                            }
                        }

                        var breakdown = GameAlgorithms.CalculateEstimatedTreatmentOutcome(patient, staff, room);
                        chanceOfSuccess += breakdown.ChanceOfSuccess;
                        count++;
                    }
                    catch (Exception e)
                    {
                        Main.Logger.Error(e.ToString());
                    }
                }
            }

            _roomsCache.Clear();

            return count > 0 ? chanceOfSuccess / count : 0;
        }
    }

    [HarmonyPatch(typeof(Room), "StaffLeaveRoom")]
    static class Room_StaffLeaveRoom_Patch
    {
        public static Queue<LeaveHistory> leaveHistory = new Queue<LeaveHistory>(100);

        public class LeaveHistory
        {
            public int staffId;
            public int roomId;
        }

        static void Postfix(Room __instance, Staff staff)
        {
            if (__instance.Definition.IsHospital)
                return;

            if (staff.Definition._type == StaffDefinition.Type.Doctor
                || staff.Definition._type == StaffDefinition.Type.Nurse && !__instance.Definition.GetRequiredStaff().Exists(x => x.Definition._type == StaffDefinition.Type.Doctor))
            {
                leaveHistory.Enqueue(new LeaveHistory { staffId = staff.ID, roomId = __instance.ID });
            }

            var count = staff.Level.CharacterManager.StaffMembers.Count * 2;
            while (leaveHistory.Count > count)
                leaveHistory.Dequeue();
        }
    }

    [HarmonyPatch(typeof(Room), "GetFrontOfQueue")]
    static class Room_GetFrontOfQueue_Patch
    {
        static bool Prefix(Room __instance)
        {
            if (!Main.enabled)
                return true;

            __instance.Queue.Sort(Compare);

            return true;
        }

        const string vip = "VIP";

        private static int Compare(Character l, Character r)
        {
            if (l.StandInQueue && !r.StandInQueue)
            {
                return -1;
            }
            if (!l.StandInQueue && r.StandInQueue)
            {
                return 1;
            }

            var lVip = l.Name.Contains(vip);
            var rVip = r.Name.Contains(vip);
            if (lVip && !rVip)
            {
                return -1;
            }
            if (!lVip && rVip)
            {
                return 1;
            }
            if (lVip && rVip)
            {
                return 0;
            }

            if (Main.settings.QueueOrder == 1) //Health
            {
                if ((l is Patient lp) && (r is Patient rp))
                {
                    var a = Mathf.Min(lp.Health.Value(), lp.Happiness.Value() * 2f);
                    var b = Mathf.Min(rp.Health.Value(), rp.Happiness.Value() * 2f);
                    if (a < b)
                    {
                        return -1;
                    }
                    if (a > b)
                    {
                        return 1;
                    }
                }
            }
            else if (Main.settings.QueueOrder == 2) //Time in hospital
            {
                if (l.TotalTimeInHospital > r.TotalTimeInHospital)
                {
                    return -1;
                }
                if (l.TotalTimeInHospital < r.TotalTimeInHospital)
                {
                    return 1;
                }
            }

            return 0;
        }
    }

    [HarmonyPatch(typeof(ReceptionistSeePatient), "OnUpdate")]
    static class ReceptionistSeePatient_OnUpdate_Patch
    {
        static List<Room> _roomsCache = new List<Room>();

        static void Postfix(ReceptionistSeePatient __instance, TaskStatus __result, SharedCharacterRef ___Character)
        {
            if (!Main.enabled || !Main.settings.SendtoHomeIfHospitalFull)
                return;
            try
            {
                if (__result == TaskStatus.Success && ___Character.Get is Patient patient)
                {
                    var level = patient.Level;
                    level.WorldState.GetRoomsOfType(RoomDefinition.Type.GPOffice, false, _roomsCache);
                    int minQueueLength = int.MaxValue;
                    foreach (Room room in _roomsCache)
                    {
                        if (!room.IsOpen) continue;
                        if (minQueueLength > room.QueueLength)
                            minQueueLength = room.QueueLength;
                    }
                    if (_roomsCache.Any() && minQueueLength >= level.HospitalPolicy.QueueWarningLength)
                    {
                        //Main.Logger.Log($"send home {patient.Name} min Queue Length {minQueueLength}");
                        DiagnosisTreatmentComponent_ProcessDiagnosis_Patch.SendHome(patient);
                    }
                    _roomsCache.Clear();
                }
            }
            catch (Exception e)
            {
                Main.Logger.Error(e.ToString());
            }
        }
    }

    [HarmonyPatch(typeof(AdvisorTriggerDiagnosisRoomRequired), "OnPatientDiagnosisExhausted")]
    static class AdvisorTriggerDiagnosisRoomRequired_OnPatientDiagnosisExhausted_Patch
    {
        static void Postfix(AdvisorTriggerDiagnosisRoomRequired __instance, Patient patient)
        {
            if (!Main.enabled || Main.settings.SendtoHomeTreatmentChance == 0)
                return;
            try
            {
                if (!patient.FullyDiagnosed())
                {
                    Level level = patient.Level;
                    ResearchManager researchManager = level.ResearchManager;

                    var roomDef = patient.Illness.GetTreatmentRoom(patient, researchManager);
                    patient.SendToTreatmentRoom(roomDef, true);

                    bool haveRoom = GameAlgorithms.DoesHospitalHaveRoom(patient.Level.WorldState, roomDef._type);
                    var knownedIllness = patient.Level.GameplayStatsTracker.HasIllnessBeenDiagnosedBefore(patient.Illness);
                    var treatmentChance = patient.CalculateAverageTreatmentChance(roomDef);
                    if (knownedIllness && (treatmentChance < Main.settings.SendtoHomeTreatmentChance))
                    {
                        patient.SendHome();
                    }
                }
            }
            catch (Exception e)
            {
                Main.Logger.Error(e.ToString());
            }
        }
    }

    [HarmonyPatch(typeof(RoomLogicTrainingRoom), "RemovePupil")]
    static class RoomLogicTrainingRoom_RemovePupil_Patch
    {
        static void Postfix(RoomLogicTrainingRoom __instance, Staff pupil)
        {
            if (!Main.enabled || !Main.settings.TakeBreakAfterTraining)
                return;

            pupil.TakeBreak();
        }
    }

    //[HarmonyPatch(typeof(GameAlgorithms), "FindBestNeedInteractionWithinRadius")]
    //static class GameAlgorithms_FindBestNeedInteractionWithinRadius_Patch
    //{
    //    static bool Prefix(Character character, int needIndex, Vector3 characterPosition, float radius, out ObjectInteraction interactionOut, out Room roomOut)
    //    {
    //        roomOut = null;
    //        interactionOut = null;
    //        if (!Main.enabled)
    //            return true;
    //        try
    //        {
    //            float num = float.MaxValue;
    //            Room roomUsing = character.RoomUsing;
    //            int queuePosition = character.GetQueuePosition();
    //            NavMesh navMesh = character.Level.WorldState.NavMesh;
    //            List<KeyValuePair<RoomItem, float>>[] needSatisfyingRoomItems = character.Level.WorldState.NeedSatisfyingRoomItems;

    //            foreach (KeyValuePair<RoomItem, float> item in needSatisfyingRoomItems[needIndex])
    //            {
    //                RoomItem key = item.Key;
    //                if (CharacterCanUseInteractionsInRoom(character, key.OwningRoom) && key.IsFunctional() && key.Definition.ValidQueuePositionForNeed(queuePosition))
    //                {
    //                    Room owningRoom = key.OwningRoom;
    //                    foreach (ObjectInteraction interaction in key.Interactions)
    //                    {
    //                        float pathDistance;
    //                        if (interaction.Type == InteractionAttributeModifier.Type.Use && interaction.Valid && (radius <= 0f || Vector3.Distance(characterPosition, interaction.WorldStartPosition) <= radius) && InteractionAlgorithms.InteractionReachable(navMesh, characterPosition, interaction.WorldStartPosition, roomUsing, owningRoom, out pathDistance) && pathDistance <= radius)
    //                        {
    //                            bool isInSamePlot = true;
    //                            if (character is Staff)
    //                            {
    //                                var roomPlot = character.Level.WorldState.GetHospitalPlotFromRoom(owningRoom);
    //                                if (roomPlot != null)
    //                                {
    //                                    Main.Logger.Log($"need {Enum.GetName(typeof(CharacterAttributes.Type), needIndex)} room {owningRoom.GetRoomName()} room plot {roomPlot.Definition.NameLocalised}");
    //                                    var map = character.Level.WorldState.GetHospitalMapAtWorldPosition(character.Position);
    //                                    if (map != null)
    //                                    {
    //                                        var staffPlot = map.Plot;                                            
    //                                        isInSamePlot = roomPlot == staffPlot;
    //                                        Main.Logger.Log($"staff {character.Name} staff plot {staffPlot.Definition.NameLocalised} samePlot {isInSamePlot}");
    //                                    }
    //                                }

    //                            }
    //                            if (isInSamePlot)
    //                            {
    //                                float value = item.Value;
    //                                float num2 = pathDistance / value;
    //                                if (interaction.IsAvailable(character))
    //                                {
    //                                    num2 *= GameAlgorithms.Config.NeedScoreInteractionAvailable;
    //                                }
    //                                int queueLength = interaction.GetQueueLength();
    //                                if (queueLength != 0)
    //                                {
    //                                    num2 *= (float)queueLength * GameAlgorithms.Config.NeedScoreQueueLengthMultiplier;
    //                                }
    //                                if (owningRoom != roomUsing)
    //                                {
    //                                    num2 *= GameAlgorithms.Config.NeedScoreInDifferentRoomMultiplier;
    //                                }
    //                                if (num2 <= num)
    //                                {
    //                                    num = num2;
    //                                    interactionOut = interaction;
    //                                    roomOut = key.OwningRoom;
    //                                }
    //                            }
    //                        }
    //                    }
    //                }
    //            }
    //        }
    //        catch (Exception e)
    //        {
    //            Main.Logger.Error(e.ToString());
    //            return true;
    //        }
    //        if (roomOut == null)
    //            return true;
    //        return false;
    //    }
    //    private static bool CharacterCanUseInteractionsInRoom(Character character, Room room)
    //    {
    //        if (room == null || !room.IsOpen)
    //        {
    //            return false;
    //        }
    //        if (room.WhoCanUse.IsMember(character) && (room.Definition.IsHospital || (!room.IsAtMaxCapacity() && room.FloorPlan.Door != null)))
    //        {
    //            if (room.Definition._allowPatientsNeedsSatisfaction && character is Patient)
    //            {
    //                return true;
    //            }
    //            Staff staff = character as Staff;
    //            if (staff != null && (room.Definition._allowStaffNeedsSatisfaction || room.IsStaffMember(staff)) && (!staff.IsIdleInWorkRoom() || room == staff.RoomUsing))
    //            {
    //                return true;
    //            }
    //        }
    //        return false;
    //    }
    //}
}
