using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Harmony;
using UnityModManagerNet;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TH20;
using FullInspector;
using TMPro;

namespace JobAssign
{
    public class Settings : UnityModManager.ModSettings
    {
        public bool UnassignJobsOnHire = false;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }

    static class Main
    {
        public static bool enabled;
        public static Settings settings;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            var harmony = HarmonyInstance.Create(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            settings = Settings.Load<Settings>(modEntry);

            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;

            return true;
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            enabled = value;

            return true;
        }

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            settings.UnassignJobsOnHire = GUILayout.Toggle(settings.UnassignJobsOnHire, " Cancel jobs when hiring.");
        }

        static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            settings.Save(modEntry);
        }
    }

    [HarmonyPatch(typeof(HospitalEventStaffHired.Config), "OnStaffHiredEvent")]
    static class HospitalEventStaffHired_OnStaffHiredEvent_Patch
    {
        static void Postfix(HospitalEventStaffHired.Config __instance, Staff staff, JobApplicant applicant)
        {
            if (!Main.enabled || !Main.settings.UnassignJobsOnHire)
                return;

            if (staff.Definition._type != StaffDefinition.Type.Doctor && staff.Definition._type != StaffDefinition.Type.Nurse)
                return;

            var jobs = RoomAlgorithms.GetAllJobs(staff.Level.Metagame, staff.Level.WorldState, staff.Definition._type);
            staff.JobExclusions.Clear();
            staff.JobExclusions.AddRange(jobs);
        }
    }

    [HarmonyPatch(typeof(StaffMenuJobAssignRow), "Setup")]
    static class StaffMenuJobAssignRow_Setup_Patch
    {
        static bool Prefix(StaffMenuJobAssignRow __instance, Staff staff, List<JobDescription> jobs, GameObject ____jobTogglePrefab)
        {
            if (!Main.enabled)
                return true;

            if (!____jobTogglePrefab.GetComponent<JobAssignToggle>())
                ____jobTogglePrefab.AddComponent<JobAssignToggle>();

            return true;
        }
    }

    [HarmonyPatch(typeof(StaffMenu), "CreateJobIcons")]
    static class StaffMenu_CreateJobIcons_Patch
    {
        static void Postfix(StaffMenu __instance, StaffDefinition.Type staffType, StaffMenu.StaffMenuSettings ____staffMenuSettings, WorldState ____worldState, List<JobDescription>[] ____jobs, float ____titleWidth)
        {
            if (!Main.enabled)
                return;

            if (staffType != StaffDefinition.Type.Doctor && staffType != StaffDefinition.Type.Nurse)
                return;

            int i = 0;
            foreach(Transform t in ____staffMenuSettings.JobsListContainer)
            {
                var job = (JobRoomDescription)____jobs[(int)staffType][i];
                TMP_Text obj = UnityEngine.Object.Instantiate(____staffMenuSettings.TitleText.gameObject).GetComponent<TMP_Text>();
                obj.rectTransform.SetParent(t);
                obj.text = ____worldState.AllRooms.Count(x => x.Definition == job.Room).ToString();
                obj.enableAutoSizing = false;
                obj.fontSize = 24f;
                obj.enableWordWrapping = false;
                obj.overflowMode = TextOverflowModes.Overflow;
                obj.alignment = TextAlignmentOptions.Midline;
                obj.color = Color.white;
                obj.rectTransform.anchorMin = new Vector2(0.5f, 0f);
                obj.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                obj.rectTransform.anchoredPosition = new Vector2(0f, -15f);
                obj.rectTransform.sizeDelta = t.GetComponent<RectTransform>().sizeDelta;
                i++;
            }
        }
    }

    public class JobAssignToggle : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData e)
        {
            if (!Main.enabled)
                return;

            var jobToggles = Traverse.Create(GetComponentInParent<StaffMenuJobAssignRow>()).Field("_jobToggles").GetValue<List<StaffJobToggle>>();
            if (jobToggles.Count == 0)
                return;

            if (e.button == PointerEventData.InputButton.Right)
            {
                var toggle = GetComponent<Toggle>();
                if (toggle.interactable)
                {
                    foreach (var obj in jobToggles)
                    {
                        obj.GetComponent<Toggle>().isOn = false;
                    }
                    toggle.isOn = true;
                }
            }
            else if (Input.GetKey(KeyCode.LeftShift) || e.button == PointerEventData.InputButton.Middle)
            {
                var staff = Traverse.Create(jobToggles.First()).Field("_staff").GetValue<Staff>();
                if (staff.Definition._type != StaffDefinition.Type.Doctor && staff.Definition._type != StaffDefinition.Type.Nurse || staff.Qualifications.Count == 0)
                    return;

                var recommendedRooms = new List<RoomDefinition.Type>();

                foreach (var qualification in staff.Qualifications)
                {
                    if (qualification.Definition.RequiredRoomUnlocked != null)
                    {
                        recommendedRooms.Add(qualification.Definition.RequiredRoomUnlocked.Instance._type);
                    }

                    foreach (var modifier in qualification.Definition.Modifiers)
                    {
                        var validRooms = new List<RoomDefinition.Type>();
                        if (modifier is QualificationBaseModifier)
                        {
                            var roomDefinition = Traverse.Create(((QualificationBaseModifier)modifier)).Field("_validRooms").GetValue<SharedInstance<RoomDefinition>[]>();
                            if (roomDefinition.Length > 0)
                            {
                                validRooms.AddRange(roomDefinition.Select(x => x.Instance._type));
                            }
                        }

                        if (validRooms.Count > 0)
                        {
                            recommendedRooms.AddRange(validRooms);
                        }
                        else
                        {
                            if (modifier is QualificationDiagnosisModifier)
                            {
                                recommendedRooms.AddRange(RoomDefinition.DiagnosisRooms);
                            }
                            else if (modifier is QualificationResearchModifier)
                            {
                                recommendedRooms.AddRange(new[] {
                                        RoomDefinition.Type.Research
                                    });
                            }
                            else if (modifier is QualificationTreatmentModifier)
                            {
                                recommendedRooms.AddRange(new[] {
                                        RoomDefinition.Type.Chromatherapy,
                                        RoomDefinition.Type.ClownClinic,
                                        RoomDefinition.Type.DNAAnalysis,
                                        RoomDefinition.Type.FractureWard,
                                        RoomDefinition.Type.InjectionRoom,
                                        RoomDefinition.Type.PandemicClinic,
                                        RoomDefinition.Type.Pharmacy,
                                        RoomDefinition.Type.Psychiatry,
                                        RoomDefinition.Type.ElectricShockClinic,
                                        RoomDefinition.Type.Ward,
                                        RoomDefinition.Type.OperatingTheater,
                                        RoomDefinition.Type.MummyClinic,
                                        RoomDefinition.Type.ClinicCubism,
                                        RoomDefinition.Type.LightHeaded,
                                        RoomDefinition.Type.AnimalMagnetismClinic,
                                        RoomDefinition.Type.TurtleHeadClinic,
                                        RoomDefinition.Type.ClinicVI10,
                                        RoomDefinition.Type.EightBitClinic,
                                    });
                            }
                        }
                    }
                }

                if (recommendedRooms.Count > 0)
                {
                    foreach (var obj in jobToggles)
                    {
                        var toggle = obj.GetComponent<Toggle>();
                        if (toggle.interactable && obj.Job is JobRoomDescription)
                        {
                            var room = ((JobRoomDescription)obj.Job).Room;
                            toggle.isOn = recommendedRooms.Exists(x => x == room._type);
                        }
                    }
                }
            }
        }
    }
}
