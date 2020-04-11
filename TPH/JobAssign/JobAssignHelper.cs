using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Harmony12;
using UnityModManagerNet;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TH20;
using FullInspector;
using TMPro;
using System.Xml.Serialization;
using TH20.UI;

namespace JobAssign
{
    public class Settings : UnityModManager.ModSettings
    {
        public bool UnassignJobsOnHire = false;
        public bool AssignJobsOnTrainingComplete = false;

        public List<RoomDefinition.Type> TreatmentRooms = new List<RoomDefinition.Type>
        {
            RoomDefinition.Type.Chromatherapy, RoomDefinition.Type.ClownClinic, RoomDefinition.Type.DNAAnalysis,
            RoomDefinition.Type.ElectricShockClinic, RoomDefinition.Type.MummyClinic, RoomDefinition.Type.ClinicCubism,
            RoomDefinition.Type.LightHeaded, RoomDefinition.Type.AnimalMagnetismClinic, RoomDefinition.Type.TurtleHeadClinic,
            RoomDefinition.Type.ClinicVI10, RoomDefinition.Type.EightBitClinic, RoomDefinition.Type.PandemicClinic,
            RoomDefinition.Type.FrankensteinClinic, RoomDefinition.Type.DogClinic, RoomDefinition.Type.RobotMonsterClinic,
            RoomDefinition.Type.BlankLooksClinic, RoomDefinition.Type.EightBallClinic, RoomDefinition.Type.ExplorerClinic,
            RoomDefinition.Type.CardboardClinic, RoomDefinition.Type.FrogClinic, RoomDefinition.Type.AstroClinic,
            RoomDefinition.Type.PinocchioClinic, RoomDefinition.Type.ScarecrowClinic, RoomDefinition.Type.TechClinic, RoomDefinition.Type.PlantWardClinic,
        };

        public List<RoomDefinition.Type> DiagnosisRooms = new List<RoomDefinition.Type>
        {
            RoomDefinition.Type.GeneralDiagnosis, RoomDefinition.Type.Cardiography, RoomDefinition.Type.FluidAnalysis,
            RoomDefinition.Type.DNAAnalysis, RoomDefinition.Type.XRay, RoomDefinition.Type.MRIScanner,
        };

        public List<RoomDefinition.Type> CustomerServiceRooms = new List<RoomDefinition.Type>
        {
            RoomDefinition.Type.Reception, RoomDefinition.Type.Cafe,
        };

        [XmlIgnore]
        public readonly RoomDefinition.Type[] BannedRooms = new RoomDefinition.Type[]
        {
            RoomDefinition.Type.Invalid,
            RoomDefinition.Type.Hospital,
            RoomDefinition.Type.HospitalUnbuilt,
        };

        public List<JobMaintenance.JobDescription> MiscJobs = new List<JobMaintenance.JobDescription>
        {
            JobMaintenance.JobDescription.WiltedPlant,
            JobMaintenance.JobDescription.Litter,
            JobMaintenance.JobDescription.MedicalWaste,
        };

        public List<JobMaintenance.JobDescription> MaintenanceJobs = new List<JobMaintenance.JobDescription>
        {
            JobMaintenance.JobDescription.BrokenMachine,
            JobMaintenance.JobDescription.BlockedToilet,
            JobMaintenance.JobDescription.OutOfStock,
        };

        [XmlIgnore]
        public readonly JobMaintenance.JobDescription[] GhostJobs = new JobMaintenance.JobDescription[]
        {
            JobMaintenance.JobDescription.Ghost
        };

        [XmlIgnore]
        public readonly JobMaintenance.JobDescription[] UpgradeJobs = new JobMaintenance.JobDescription[]
        {
            JobMaintenance.JobDescription.Max
        };

        [XmlIgnore]
        public readonly JobMaintenance.JobDescription[] IgnoreJobs = new JobMaintenance.JobDescription[]
        {
            JobMaintenance.JobDescription.None, JobMaintenance.JobDescription.Ghost, JobMaintenance.JobDescription.Max
        };

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

            Logger = modEntry.Logger;

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

        static IEnumerable<RoomDefinition.Type> _roomDefinitionValues = ((IEnumerable<RoomDefinition.Type>)Enum.GetValues(typeof(RoomDefinition.Type)));
        static RoomDefinition.Type _lastRoomDefinitionValue = (RoomDefinition.Type)((IEnumerable<int>)_roomDefinitionValues).Max();

        static IEnumerable<JobMaintenance.JobDescription> _jobDescriptionValues = ((IEnumerable<JobMaintenance.JobDescription>)Enum.GetValues(typeof(JobMaintenance.JobDescription)));
        static JobMaintenance.JobDescription _lastJobDescription = (JobMaintenance.JobDescription)((IEnumerable<int>)_jobDescriptionValues).Max();

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            settings.UnassignJobsOnHire = GUILayout.Toggle(settings.UnassignJobsOnHire, " Unassign jobs when hiring.");
            settings.AssignJobsOnTrainingComplete = GUILayout.Toggle(settings.AssignJobsOnTrainingComplete, " Assign jobs when training complete.");

            GUILayout.Space(10);
            GUILayout.Label("Jobs (Treatment/Diagnosis/CustomerService)", UnityModManager.UI.bold);
            GUILayout.BeginHorizontal();

            var columns = 3;
            var rows = Mathf.CeilToInt((_roomDefinitionValues.Count() - settings.BannedRooms.Length) / (float)columns);
            int i = 0;
            foreach (var room in _roomDefinitionValues)
            {
                if (Array.Exists(settings.BannedRooms, (x) => x == room))
                    continue;

                if (i == 0)
                {
                    GUILayout.BeginVertical();
                }

                GUILayout.BeginHorizontal();
                var value = settings.TreatmentRooms.Exists(x => x == room);
                var result = GUILayout.Toggle(value, "", GUILayout.ExpandWidth(false));
                if (result != value)
                {
                    if (result)
                    {
                        settings.TreatmentRooms.Add(room);
                    }
                    else
                    {
                        settings.TreatmentRooms.Remove(room);
                    }
                }

                value = settings.DiagnosisRooms.Exists(x => x == room);
                result = GUILayout.Toggle(value, "", GUILayout.ExpandWidth(false));
                if (result != value)
                {
                    if (result)
                    {
                        settings.DiagnosisRooms.Add(room);
                    }
                    else
                    {
                        settings.DiagnosisRooms.Remove(room);
                    }
                }

                value = settings.CustomerServiceRooms.Exists(x => x == room);
                result = GUILayout.Toggle(value, $"   {room}", GUILayout.ExpandWidth(false));
                if (result != value)
                {
                    if (result)
                    {
                        settings.CustomerServiceRooms.Add(room);
                    }
                    else
                    {
                        settings.CustomerServiceRooms.Remove(room);
                    }
                }
                GUILayout.EndHorizontal();

                if (i == rows - 1 || room == _lastRoomDefinitionValue)
                {
                    GUILayout.EndVertical();
                    i = 0;
                }
                else
                {
                    i++;
                }
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.Label("Jobs for Janitar (Maintenance/Misc)", UnityModManager.UI.bold);

            foreach (var job in _jobDescriptionValues)
            {
                if (Array.Exists(settings.IgnoreJobs, (x) => x == job))
                    continue;

                GUILayout.BeginHorizontal();
                var value = settings.MaintenanceJobs.Exists(x => x == job);
                var result = GUILayout.Toggle(value, "", GUILayout.ExpandWidth(false));
                if (result != value)
                {
                    if (result)
                    {
                        settings.MaintenanceJobs.Add(job);
                    }
                    else
                    {
                        settings.MaintenanceJobs.Remove(job);
                    }
                }

                value = settings.MiscJobs.Exists(x => x == job);
                result = GUILayout.Toggle(value, $"   {job}", GUILayout.ExpandWidth(false));
                if (result != value)
                {
                    if (result)
                    {
                        settings.MiscJobs.Add(job);
                    }
                    else
                    {
                        settings.MiscJobs.Remove(job);
                    }
                }
                GUILayout.EndHorizontal();
            }
        }

        static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            settings.TreatmentRooms = settings.TreatmentRooms.Distinct().ToList();
            settings.DiagnosisRooms = settings.DiagnosisRooms.Distinct().ToList();
            settings.CustomerServiceRooms = settings.CustomerServiceRooms.Distinct().ToList();
            settings.MaintenanceJobs = settings.MaintenanceJobs.Distinct().ToList();
            settings.MiscJobs = settings.MiscJobs.Distinct().ToList();
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

            try
            {
                if (staff.Definition._type != StaffDefinition.Type.Doctor && staff.Definition._type != StaffDefinition.Type.Nurse)
                    return;

                var jobs = RoomAlgorithms.GetAllJobs(staff.Level.Metagame, staff.Level.WorldState, staff.Definition._type);
                staff.JobExclusions.Clear();
                staff.JobExclusions.AddRange(jobs);
            }
            catch (Exception e)
            {
                Main.Logger.Error(e.ToString());
            }
        }
    }

    [HarmonyPatch(typeof(HospitalEventTraineeCompletedCourse.Config), "OnStaffQualificationComplete")]
    static class HospitalEventTraineeCompletedCourse_OnStaffQualificationComplete_Patch
    {
        static void Postfix(HospitalEventTraineeCompletedCourse.Config __instance, Staff staff, QualificationDefinition qualification, Staff teacher)
        {
            if (!Main.enabled || !Main.settings.AssignJobsOnTrainingComplete)
                return;

            try
            {
                if (staff.Definition._type != StaffDefinition.Type.Doctor && staff.Definition._type != StaffDefinition.Type.Nurse)
                    return;

                var recommended = staff.GetRecommendedJobRooms();
                var jobs = RoomAlgorithms.GetAllJobs(staff.Level.Metagame, staff.Level.WorldState, staff.Definition._type);
                for (int i = jobs.Count - 1; i >= 0; i--)
                {
                    if (jobs[i] is JobRoomDescription job && recommended.Exists(x => x == job.Room._type))
                    {
                        jobs.RemoveAt(i);
                    }
                }

                staff.JobExclusions.Clear();
                staff.JobExclusions.AddRange(jobs);
            }
            catch (Exception e)
            {
                Main.Logger.Error(e.ToString());
            }
        }
    }


    [HarmonyPatch(typeof(StaffMenuJobAssignRow), "Setup")]
    static class StaffMenuJobAssignRow_Setup_Patch
    {
        static bool Prefix(StaffMenuJobAssignRow __instance, Staff staff, List<JobDescription> jobs, StaffMenu staffMenu, GameObject ____jobTogglePrefab,
            QualificationIcons ___QualificationIcons)
        {
            if (!Main.enabled)
                return true;

            if (staff == null)
                return true;

            try
            {
                var icons = Traverse.Create(___QualificationIcons).Field("_qualificationImages").GetValue<Image[]>();
                for (int i = 0; i < icons.Length; i++)
                {
                    var button = icons[i].gameObject.GetOrAddComponent<Button>();
                    button.enabled = true;
                    var obj = icons[i].gameObject.GetOrAddComponent<JobAssignQualificationToggle>();
                    obj.staff = staff;
                    obj.staffMenu = staffMenu;
                    obj.id = i;
                }
            }
            catch (Exception e)
            {
                Main.Logger.Error(e.ToString());
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(StaffMenu), "CreateJobIcons")]
    static class StaffMenu_CreateJobIcons_Patch
    {
        static void Postfix(StaffMenu __instance, StaffDefinition.Type staffType, StaffMenu.StaffMenuSettings ____staffMenuSettings, WorldState ____worldState, List<JobDescription>[] ____jobs, CharacterManager ____characterManager)
        {
            if (!Main.enabled)
            {
                return;
            }
            try
            {
                int num = 0;
                foreach (object obj in ____staffMenuSettings.JobsListContainer)
                {
                    Transform transform = (Transform)obj;
                    JobDescription job = ____jobs[(int)staffType][num];
                    TMP_Text component = UnityEngine.Object.Instantiate<GameObject>(____staffMenuSettings.TitleText.gameObject).GetComponent<TMP_Text>();
                    component.rectTransform.SetParent(transform);
                    int staffCount = ____characterManager.StaffMembers.Count((Staff s) => !s.HasResigned() && !s.HasBeenFired() && job.IsSuitable(s) && !s.JobExclusions.Contains(job));
                    int roomCount = 0;
                    if (job is JobRoomDescription)
                    {
                        roomCount = ____worldState.AllRooms.Count((Room x) => x.Definition == ((JobRoomDescription)job).Room);
                        component.text = staffCount.ToString() + "/" + roomCount.ToString();
                        StaffJobIcon[] componentsInChildren = ____staffMenuSettings.JobsListContainer.gameObject.GetComponentsInChildren<StaffJobIcon>();
                        if (componentsInChildren != null && num < componentsInChildren.Length)
                        {
                            componentsInChildren[num].Tooltip.SetDataProvider(delegate (Tooltip tooltip)
                            {
                                tooltip.Text = string.Concat(new string[]
                                {
                                    job.GetJobAssignmentTooltipString(),
                                    "\n\nSatff Assigned: ",
                                    staffCount.ToString(),
                                    "\nRooms Built: ",
                                    roomCount.ToString()
                                });
                            });
                        }
                    }
                    else if (job is JobItemDescription)
                    {
                        component.text = staffCount.ToString();
                        StaffJobIcon[] componentsInChildren2 = ____staffMenuSettings.JobsListContainer.gameObject.GetComponentsInChildren<StaffJobIcon>();
                        if (componentsInChildren2 != null && num < componentsInChildren2.Length)
                        {
                            componentsInChildren2[num].Tooltip.SetDataProvider(delegate (Tooltip tooltip)
                            {
                                tooltip.Text = job.GetJobAssignmentTooltipString() + "\n\nSatff Assigned: " + staffCount.ToString();
                            });
                        }
                    }
                    else if (job is JobMaintenanceDescription)
                    {
                        string text = staffCount.ToString();
                        int itemCount = (from mj in ____worldState.GetRoomItemsWithMaintenanceDescription(((JobMaintenanceDescription)job).Description)
                                         where mj.Definition.Interactions.Count((InteractionDefinition inter) => inter.Type == InteractionAttributeModifier.Type.Maintain) > 0
                                         select mj).Count<RoomItem>();
                        text = text + "/" + itemCount.ToString();
                        component.text = text;
                        StaffJobIcon[] componentsInChildren3 = ____staffMenuSettings.JobsListContainer.gameObject.GetComponentsInChildren<StaffJobIcon>();
                        if (componentsInChildren3 != null && num < componentsInChildren3.Length)
                        {
                            componentsInChildren3[num].Tooltip.SetDataProvider(delegate (Tooltip tooltip)
                            {
                                tooltip.Text = string.Concat(new string[]
                                {
                                    job.GetJobAssignmentTooltipString(),
                                    "\n\nSatff Assigned: ",
                                    staffCount.ToString(),
                                    "\nMaintenance Items: ",
                                    itemCount.ToString()
                                });
                            });
                        }
                    }
                    else if (job is JobUpgradeDescription)
                    {
                        int upgradeCount = 0;
                        string machinesForUpgarde = "";
                        foreach (Room room in ____worldState.AllRooms)
                        {
                            foreach (RoomItem roomItem in room.FloorPlan.Items)
                            {
                                RoomItemUpgradeDefinition nextUpgrade = roomItem.Definition.GetNextUpgrade(roomItem.UpgradeLevel);
                                if (nextUpgrade != null && roomItem.Level.Metagame.HasUnlocked(nextUpgrade) && roomItem.GetComponent<RoomItemUpgradeComponent>() == null)
                                {
                                    upgradeCount++;
                                    machinesForUpgarde = machinesForUpgarde + "\n" + roomItem.Name;
                                }
                            }
                        }
                        if (!string.IsNullOrEmpty(machinesForUpgarde))
                        {
                            StaffJobIcon[] componentsInChildren4 = ____staffMenuSettings.JobsListContainer.gameObject.GetComponentsInChildren<StaffJobIcon>();
                            if (componentsInChildren4 != null && num < componentsInChildren4.Length)
                            {
                                componentsInChildren4[num].Tooltip.SetDataProvider(delegate (Tooltip tooltip)
                                {
                                    tooltip.Text = string.Concat(new string[]
                                    {
                                        job.GetJobAssignmentTooltipString(),
                                        "\n\nSatff Assigned: ",
                                        staffCount.ToString(),
                                        "\n",
                                        machinesForUpgarde
                                    });
                                });
                            }
                        }
                        component.text = staffCount.ToString() + "/" + upgradeCount;
                    }
                    else
                    {
                        if (!(job is JobGhostDescription) && !(job is JobFireDescription))
                        {
                            continue;
                        }
                        component.text = staffCount.ToString();
                    }
                    component.enableAutoSizing = false;
                    component.fontSize = 18f;
                    component.enableWordWrapping = false;
                    component.overflowMode = 0;
                    component.alignment = TextAlignmentOptions.Midline;
                    component.color = Color.white;
                    component.outlineColor = Color.black;
                    component.outlineWidth = 1f;
                    component.rectTransform.anchorMin = new Vector2(0.5f, 0f);
                    component.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                    component.rectTransform.anchoredPosition = new Vector2(0f, -15f);
                    component.rectTransform.sizeDelta = transform.GetComponent<RectTransform>().sizeDelta;
                    num++;
                }
            }
            catch (Exception ex)
            {
                Main.Logger.Error(ex.ToString() + ": " + ex.StackTrace.ToString());
            }
        }
    }
    /// <summary>
    /// controls left clicking on the job qualification icon
    /// left clicking on a qualification will toggle jobs so only those related to that qualification are assigned
    /// middle of shift-click any qualification toggles on matching jobs for all qualifications
    /// </summary>
    public class JobAssignQualificationToggle : MonoBehaviour, IPointerClickHandler
    {
        public Staff staff;
        public StaffMenu staffMenu;
        public int id;

        public void OnPointerClick(PointerEventData e)
        {
            if (!Main.enabled || staff == null || staff.Qualifications.Count <= id)
                return;
            try
            {
                var jobToggles = Traverse.Create(GetComponentInParent<StaffMenuJobAssignRow>()).Field("_jobToggles").GetValue<List<StaffJobToggle>>();
                if (jobToggles.Count == 0)
                    return;

                if (e.button == PointerEventData.InputButton.Left)
                {
                    if (Input.GetKey(KeyCode.LeftShift))
                    {
                        ToggleAllQualifications(jobToggles);
                    }
                    else
                    {
                        var recommendedRooms = staff.GetRecommendedJobRooms(staff.Qualifications[id]);
                        if (recommendedRooms.Count > 0)
                        {
                            foreach (var obj in jobToggles)
                            {
                                var toggle = obj.GetComponent<Toggle>();
                                if (toggle.interactable && obj.Job is JobRoomDescription description)
                                {
                                    if (recommendedRooms.Exists(x => x == description.Room._type))
                                        staff.JobExclusions.Remove(description);
                                    else
                                        staff.JobExclusions.AddUnique(description);
                                }
                                else if (staff.Definition._type == StaffDefinition.Type.Assistant && toggle.interactable && obj.Job is JobItemDescription)
                                {
                                    if (staff.Qualifications[id].Definition.Modifiers.Count((CharacterModifier m) => m is QualificationServiceModifier) > 0)
                                        staff.JobExclusions.Remove(obj.Job);
                                    else
                                        staff.JobExclusions.AddUnique(obj.Job);
                                }
                            }
                        }

                        List<JobMaintenance.JobDescription> recommendedJobs = staff.GetRecommendedJobs(staff.Qualifications[id]);
                        if (recommendedJobs.Count > 0)
                        {
                            foreach (var obj in jobToggles)
                            {
                                var toggle = obj.GetComponent<Toggle>();
                                JobMaintenanceDescription description;
                                if (toggle.interactable && (description = (obj.Job as JobMaintenanceDescription)) != null)
                                {
                                    if (recommendedJobs.Exists((JobMaintenance.JobDescription x) => x == description.Description))
                                        staff.JobExclusions.Remove(description);
                                    else
                                        staff.JobExclusions.AddUnique(description);
                                }
                                else if (toggle.interactable && obj.Job is JobGhostDescription)
                                {
                                    if (recommendedJobs.Exists((JobMaintenance.JobDescription x) => x == JobMaintenance.JobDescription.Ghost))
                                        staff.JobExclusions.Remove(obj.Job);
                                    else
                                        staff.JobExclusions.AddUnique(obj.Job);
                                }
                                else if (toggle.interactable && obj.Job is JobUpgradeDescription)
                                {
                                    if (recommendedJobs.Exists((JobMaintenance.JobDescription x) => x == JobMaintenance.JobDescription.Max))
                                        staff.JobExclusions.Remove(obj.Job);
                                    else
                                        staff.JobExclusions.AddUnique(obj.Job);
                                }
                            }
                        }
                    }
                    if (staffMenu != null)
                    {
                        var p = staffMenu.GetType().GetField("_staffMenuRowProvider", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(staffMenu);
                        if (p is StaffMenuRowProvider provider)
                        {
                            provider.RefreshRowJobs();
                        }

                        MethodInfo method = staffMenu.GetType().GetMethod("CreateJobIcons", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (method != null)
                        {
                            method.Invoke(staffMenu, new object[]
                            {
                                staff.Definition._type
                            });
                        }
                    }
                }
                else if (e.button == PointerEventData.InputButton.Middle)
                {
                    ToggleAllQualifications(jobToggles);
                    if (staffMenu != null)
                    {
                        var p = staffMenu.GetType().GetField("_staffMenuRowProvider", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(staffMenu);
                        if (p is StaffMenuRowProvider provider)
                        {
                            provider.RefreshRowJobs();
                        }

                        MethodInfo method = staffMenu.GetType().GetMethod("CreateJobIcons", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (method != null)
                        {
                            method.Invoke(staffMenu, new object[]
                            {
                                staff.Definition._type
                            });
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Main.Logger.Error(exception.ToString());
            }
        }
        public void ToggleAllQualifications(List<StaffJobToggle> jobToggles)
        {
            if (staff.Qualifications.Count == 0)
            {
                return;
            }
            List<RoomDefinition.Type> recommendedJobRooms = staff.GetRecommendedJobRooms();
            if (recommendedJobRooms.Count > 0)
            {
                foreach (StaffJobToggle staffJobToggle2 in jobToggles)
                {
                    Toggle toggle = staffJobToggle2.GetComponent<Toggle>();
                    JobRoomDescription description;
                    if (toggle.interactable && (description = (staffJobToggle2.Job as JobRoomDescription)) != null)
                    {
                        if (recommendedJobRooms.Exists((RoomDefinition.Type x) => x == description.Room._type))
                            staff.JobExclusions.Remove(staffJobToggle2.Job);
                        else
                            staff.JobExclusions.AddUnique(staffJobToggle2.Job);
                    }
                    else if (staff.Definition._type == StaffDefinition.Type.Assistant && toggle.interactable && staffJobToggle2.Job is JobItemDescription)
                    {
                        if (recommendedJobRooms.Exists((RoomDefinition.Type x) => x == RoomDefinition.Type.Reception))
                            staff.JobExclusions.Remove(staffJobToggle2.Job);
                        else
                            staff.JobExclusions.AddUnique(staffJobToggle2.Job);
                    }
                }
            }
            if (staff.Definition._type == StaffDefinition.Type.Janitor)
            {
                List<JobMaintenance.JobDescription> recommendedJobs = staff.GetRecommendedJobs();
                if (recommendedJobs.Count > 0)
                {
                    foreach (StaffJobToggle staffJobToggle3 in jobToggles)
                    {
                        Toggle toggle = staffJobToggle3.GetComponent<Toggle>();
                        JobMaintenanceDescription description;
                        if (toggle.interactable && (description = (staffJobToggle3.Job as JobMaintenanceDescription)) != null)
                        {
                            if (recommendedJobs.Exists((JobMaintenance.JobDescription x) => x == description.Description))
                                staff.JobExclusions.Remove(staffJobToggle3.Job);
                            else
                                staff.JobExclusions.AddUnique(staffJobToggle3.Job);
                        }
                        if (toggle.interactable && staffJobToggle3.Job is JobGhostDescription)
                        {
                            if (recommendedJobs.Exists((JobMaintenance.JobDescription x) => x == JobMaintenance.JobDescription.Ghost))
                                staff.JobExclusions.Remove(staffJobToggle3.Job);
                            else
                                staff.JobExclusions.AddUnique(staffJobToggle3.Job);
                        }
                        if (toggle.interactable && staffJobToggle3.Job is JobUpgradeDescription)
                        {
                            if (recommendedJobs.Exists((JobMaintenance.JobDescription x) => x == JobMaintenance.JobDescription.Max))
                                staff.JobExclusions.Remove(staffJobToggle3.Job);
                            else
                                staff.JobExclusions.AddUnique(staffJobToggle3.Job);
                        }
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(StaffJobToggle), "OnToggled")]
    static class StaffJobToggle_OnToggled_Patch
    {
        static void Postfix(StaffJobToggle __instance, Staff ____staff)
        {
            if (!Main.enabled)
            {
                return;
            }
            try
            {
                StaffMenu staffMenu = ____staff.Level.HUD.FindMenu<StaffMenu>(true);
                if (staffMenu != null)
                {
                    MethodInfo method = staffMenu.GetType().GetMethod("CreateJobIcons", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (method != null)
                    {
                        method.Invoke(staffMenu, new object[]
                        {
                                ____staff.Definition._type
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Main.Logger.Error(ex.ToString() + ": " + ex.StackTrace.ToString());
            }
        }
    }
    [HarmonyPatch(typeof(StaffMenu), "OnJobRowPressed")]
    static class StaffMenu_OnJobRowPressed_Patch
    {
        static void Postfix(StaffMenu __instance, Staff staff)
        {
            if (!Main.enabled)
            {
                return;
            }
            try
            {
                MethodInfo method = __instance.GetType().GetMethod("CreateJobIcons", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (method != null)
                {
                    method.Invoke(__instance, new object[]
                    {
                            staff.Definition._type
                    });
                }
            }
            catch (Exception ex)
            {
                Main.Logger.Error(ex.ToString() + ": " + ex.StackTrace.ToString());
            }
        }
    }

    public static class Extensions
    {
        private static void GetRecommendedJobRooms(this Staff staff, QualificationSlot qualificationSlot, List<RoomDefinition.Type> recommendedRooms)
        {
            if (qualificationSlot.Definition.RequiredRoomUnlocked != null)
            {
                recommendedRooms.Add(qualificationSlot.Definition.RequiredRoomUnlocked.Instance._type);
            }

            if (qualificationSlot.Definition.RequiredIllnessWithTreatmentRoom != null)
            {
                recommendedRooms.Add(qualificationSlot.Definition.RequiredIllnessWithTreatmentRoom.Instance._type);
            }

            foreach (var modifier in qualificationSlot.Definition.Modifiers)
            {
                var validRooms = new List<RoomDefinition.Type>();
                if (modifier is QualificationBaseModifier baseModifier)
                {
                    var roomDefinition = Traverse.Create(baseModifier).Field("_validRooms").GetValue<SharedInstance<RoomDefinition>[]>();
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
                        recommendedRooms.AddRange(Main.settings.DiagnosisRooms);
                    }
                    else if (modifier is QualificationTreatmentModifier)
                    {
                        recommendedRooms.AddRange(Main.settings.TreatmentRooms);
                    }
                    else if (modifier is QualificationResearchModifier)
                    {
                        recommendedRooms.Add(RoomDefinition.Type.Research);
                    }
                    else if (modifier is QualificationServiceModifier)
                    {
                        recommendedRooms.AddRange(Main.settings.CustomerServiceRooms);
                    }
                    else if (modifier is QualificationMarketModifier)
                    {
                        recommendedRooms.Add(RoomDefinition.Type.Marketing);
                    }
                }
            }
        }

        private static void GetRecommendedJobs(this Staff staff, QualificationSlot qualificationSlot, List<JobMaintenance.JobDescription> recommendedJobs)
        {
            foreach (CharacterModifier characterModifier in qualificationSlot.Definition.Modifiers)
            {
                if (characterModifier is QualificationMaintenanceModifier)
                {
                    recommendedJobs.AddRange(Main.settings.MaintenanceJobs);
                }
                else
                {
                    if (!(characterModifier is QualificationUpgradeItemModifier))
                    {
                        LocalisedString nameLocalised = qualificationSlot.Definition.NameLocalised;
                        if (!nameLocalised.ToString().ToLowerInvariant().StartsWith("mechanics"))
                        {
                            if (characterModifier is CharacterModifierIgnoreStatusEffect)
                            {
                                recommendedJobs.AddRange(Main.settings.GhostJobs);
                            }
                            else
                            {
                                recommendedJobs.AddRange(Main.settings.MiscJobs);
                            }
                        }
                    }
                    else
                    {
                        recommendedJobs.AddRange(Main.settings.UpgradeJobs);
                    }
                }
            }
            if (qualificationSlot.Definition.Modifiers.Count<CharacterModifier>() == 0 && qualificationSlot.Definition.ToString().ToLowerInvariant().StartsWith("mechanics"))
            {
                recommendedJobs.AddRange(Main.settings.UpgradeJobs);
            }
        }

        public static List<RoomDefinition.Type> GetRecommendedJobRooms(this Staff staff, QualificationSlot qualificationSlot)
        {
            List<RoomDefinition.Type> list = new List<RoomDefinition.Type>();
            if (staff.Definition._type == StaffDefinition.Type.Janitor)
            {
                return list;
            }
            staff.GetRecommendedJobRooms(qualificationSlot, list);
            return list;
        }

        public static List<RoomDefinition.Type> GetRecommendedJobRooms(this Staff staff)
        {
            List<RoomDefinition.Type> list = new List<RoomDefinition.Type>();
            if (staff.Definition._type == StaffDefinition.Type.Janitor)
            {
                return list;
            }
            foreach (QualificationSlot qualificationSlot in staff.Qualifications)
            {
                staff.GetRecommendedJobRooms(qualificationSlot, list);
            }
            return list;
        }

        public static List<JobMaintenance.JobDescription> GetRecommendedJobs(this Staff staff, QualificationSlot qualificationSlot)
        {
            List<JobMaintenance.JobDescription> list = new List<JobMaintenance.JobDescription>();
            if (staff.Definition._type != StaffDefinition.Type.Janitor)
            {
                return list;
            }
            staff.GetRecommendedJobs(qualificationSlot, list);
            return list;
        }

        public static List<JobMaintenance.JobDescription> GetRecommendedJobs(this Staff staff)
        {
            List<JobMaintenance.JobDescription> list = new List<JobMaintenance.JobDescription>();
            if (staff.Definition._type != StaffDefinition.Type.Janitor)
            {
                return list;
            }
            foreach (QualificationSlot qualificationSlot in staff.Qualifications)
            {
                staff.GetRecommendedJobs(qualificationSlot, list);
            }
            return list;
        }


    }
}
