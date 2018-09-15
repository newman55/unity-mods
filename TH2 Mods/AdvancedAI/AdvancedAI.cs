using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Harmony;
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
        public bool SendtoHomeIfHospitalFull = true;

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
            GUILayout.Label("GP: Send to home if treatment chance less than (0-100) ", GUILayout.ExpandWidth(false));
            var inString = settings.SendtoHomeTreatmentChance.ToString();
            var outString = GUILayout.TextField(inString, 3, GUILayout.Width(50));
            if (outString != inString && int.TryParse(outString, out var result))
            {
                settings.SendtoHomeTreatmentChance = Mathf.Clamp(result, 0, 100);
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Reception: Send to home if GP offices is full ", GUILayout.ExpandWidth(false));
            settings.SendtoHomeIfHospitalFull = GUILayout.Toggle(settings.SendtoHomeIfHospitalFull, "", GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Queue order: ", GUILayout.ExpandWidth(false));

            for(int i = 0; i < queueOrder.Length; i++)
            {
                var value = settings.QueueOrder == i;
                var @new = GUILayout.Toggle(value, queueOrder[i], GUILayout.ExpandWidth(false));
                if (@new != value && @new == true)
                {
                    settings.QueueOrder = i;
                }
            }
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
            if (!Main.enabled)
                return true;

            Level level = patient.Level;
            ResearchManager researchManager = level.ResearchManager;
            if (____doctor.ModifiersComponent != null)
            {
                ____doctor.ModifiersComponent.ApplyInteractWithOtherModifiers(patient);
            }
            float certainty = GameAlgorithms.GetDiagnosisCertainty(patient, ____room, ____doctor, researchManager).Certainty;
            patient.ReceiveDiagnosis(____room, ____doctor, certainty);
            ____room.OnUnitProcessed();
            if (____room.Definition._type == RoomDefinition.Type.GPOffice)
            {
                if (patient.FullyDiagnosed())
                {
                    var roomDef = patient.Illness.GetTreatmentRoom(patient, researchManager);
                    patient.SendToTreatmentRoom(roomDef, true);
                    
                    bool haveRoom = GameAlgorithms.DoesHospitalHaveRoom(patient.Level.WorldState, roomDef._type);
                    var knownedIllness = patient.Level.GameplayStatsTracker.HasIllnessBeenDiagnosedBefore(patient.Illness);
                    var treatmentChance = CalculateAverageTreatmentChance(patient, roomDef);
                    if (haveRoom && knownedIllness && treatmentChance < Main.settings.SendtoHomeTreatmentChance)
                    {
                        SendHome(patient);
                        //Main.Logger.Log($"send to home {patient.Name} room {roomDef._type} diagnosis {patient.DiagnosisCertainty} treatment {treatmentChance}");
                    }
                }
                else
                {
                    patient.SendToDiagnosisRoom(Traverse.Create(__instance).Method("GetDiagnosisRoom", new Type[] { typeof(Patient), typeof(Staff) }).GetValue<Room>(patient, ____doctor));
                }
            }
            else
            {
                Room bestRoomOfType = GameAlgorithms.GetBestRoomOfType(level.WorldState, RoomDefinition.Type.GPOffice, patient);
                if (bestRoomOfType != null)
                {
                    patient.GotoRoom(bestRoomOfType, ReasonUseRoom.Diagnosis, false, -1);
                }
                else
                {
                    patient.WaitForRoomToBeBuilt(RoomDefinition.Type.GPOffice, ReasonUseRoom.Diagnosis, GameAlgorithms.Config.PatientWaitLongTime);
                }
            }

            return false;
        }

        public static void SendHome(Patient patient)
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
            catch(Exception e)
            {
                Main.Logger.Error(e.ToString());
            }
        }

        static List<Room> _roomsCache = new List<Room>();

        static float CalculateAverageTreatmentChance(Patient patient, RoomDefinition roomDef)
        {
            if (roomDef == null)
                return 0;

            Level level = patient.Level;
            level.WorldState.GetRoomsOfType(roomDef._type, true, _roomsCache);
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
                    catch(Exception e)
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
            while(leaveHistory.Count > count)
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
            else if (!l.StandInQueue && r.StandInQueue)
            {
                return 1;
            }

            var lVip = l.Name.Contains(vip);
            var rVip = r.Name.Contains(vip);
            if (lVip && !rVip)
            {
                return -1;
            }
            else if (!lVip && rVip)
            {
                return 1;
            }
            else if (lVip && rVip)
            {
                return 0;
            }

            if (Main.settings.QueueOrder == 1)
            {
                var a = Mathf.Min(((Patient)l).Health.Value(), ((Patient)l).Happiness.Value() * 2f);
                var b = Mathf.Min(((Patient)r).Health.Value(), ((Patient)r).Happiness.Value() * 2f);
                if (a < b)
                {
                    return -1;
                }
                else if (a > b)
                {
                    return 1;
                }
            }
            else if (Main.settings.QueueOrder == 2)
            {
                if (l.TotalTimeInHospital > r.TotalTimeInHospital)
                {
                    return -1;
                }
                else if (l.TotalTimeInHospital < r.TotalTimeInHospital)
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

            if (__result == TaskStatus.Success && ___Character.Get is Patient)
            {
                var level = ___Character.Get.Level;
                level.WorldState.GetRoomsOfType(RoomDefinition.Type.GPOffice, true, _roomsCache);
                int minQueueLength = int.MaxValue;
                foreach (Room room in _roomsCache)
                {
                    if (minQueueLength > room.QueueLength)
                        minQueueLength = room.QueueLength;
                }
                if (_roomsCache.Any() && minQueueLength >= 5)
                {
                    DiagnosisTreatmentComponent_ProcessDiagnosis_Patch.SendHome(___Character.Get as Patient);
                }
                _roomsCache.Clear();
            }
        }
    }

    [HarmonyPatch(typeof(KioskSeePatient), "OnUpdate")]
    static class KioskSeePatient_OnUpdate_Patch
    {
        static List<Room> _roomsCache = new List<Room>();

        static void Postfix(KioskSeePatient __instance, TaskStatus __result, SharedCharacterRef ___Character)
        {
            if (!Main.enabled || !Main.settings.SendtoHomeIfHospitalFull)
                return;

            if (__result == TaskStatus.Success && ___Character.Get is Patient)
            {
                var level = ___Character.Get.Level;
                level.WorldState.GetRoomsOfType(RoomDefinition.Type.GPOffice, true, _roomsCache);
                int minQueueLength = int.MaxValue;
                foreach (Room room in _roomsCache)
                {
                    if (minQueueLength > room.QueueLength)
                        minQueueLength = room.QueueLength;
                }
                if (_roomsCache.Any() && minQueueLength >= 5)
                {
                    DiagnosisTreatmentComponent_ProcessDiagnosis_Patch.SendHome(___Character.Get as Patient);
                }
                _roomsCache.Clear();
            }
        }
    }
}
