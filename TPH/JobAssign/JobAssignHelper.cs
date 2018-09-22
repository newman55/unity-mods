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

		static void OnGUI(UnityModManager.ModEntry modEntry)
		{
			settings.UnassignJobsOnHire = GUILayout.Toggle(settings.UnassignJobsOnHire, " Unassign jobs when hiring.");
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
		static bool Prefix(StaffMenuJobAssignRow __instance, Staff staff, List<JobDescription> jobs, GameObject ____jobTogglePrefab,
			QualificationIcons ___QualificationIcons)
		{
			if (!Main.enabled)
				return true;

			____jobTogglePrefab.GetOrAddComponent<JobAssignToggle>();

			if (staff == null)
				return true;

			try
			{
				var icons = Traverse.Create(___QualificationIcons).Field("_qualificationImages").GetValue<Image[]>();
				for (int i = 0; i < icons.Length; i++)
				{
					var button = icons[i].gameObject.GetOrAddComponent<Button>();
					button.enabled = staff.Definition._type == StaffDefinition.Type.Doctor || staff.Definition._type == StaffDefinition.Type.Nurse;
					var obj = icons[i].gameObject.GetOrAddComponent<JobAssignQualificationToggle>();
					obj.staff = staff;
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
		static void Postfix(StaffMenu __instance, StaffDefinition.Type staffType, StaffMenu.StaffMenuSettings ____staffMenuSettings,
			WorldState ____worldState, List<JobDescription>[] ____jobs)
		{
			if (!Main.enabled)
				return;

			if (staffType != StaffDefinition.Type.Doctor && staffType != StaffDefinition.Type.Nurse)
				return;

			try
			{
				int i = 0;
				foreach (Transform t in ____staffMenuSettings.JobsListContainer)
				{
					var job = (JobRoomDescription) ____jobs[(int) staffType][i];
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
			catch (Exception e)
			{
				Main.Logger.Error(e.ToString());
			}
		}
	}

	public class JobAssignQualificationToggle : MonoBehaviour, IPointerClickHandler
	{
		public Staff staff;
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
					var recommendedRooms = staff.GetRecommendedJobRooms(staff.Qualifications[id]);

					if (recommendedRooms.Count > 0)
					{
						foreach (var obj in jobToggles)
						{
							var toggle = obj.GetComponent<Toggle>();
							if (toggle.interactable && obj.Job is JobRoomDescription description)
							{
								toggle.isOn = recommendedRooms.Exists(x => x == description.Room._type);
							}
						}
					}
				}
			}
			catch (Exception exception)
			{
				Main.Logger.Error(exception.ToString());
			}
		}
	}

	public class JobAssignToggle : MonoBehaviour, IPointerClickHandler
	{
		public void OnPointerClick(PointerEventData e)
		{
			if (!Main.enabled)
				return;

			try
			{
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
					if (staff.Definition._type != StaffDefinition.Type.Doctor && staff.Definition._type != StaffDefinition.Type.Nurse
						|| staff.Qualifications.Count == 0)
						return;

					var recommendedRooms = staff.GetRecommendedJobRooms();

					if (recommendedRooms.Count > 0)
					{
						foreach (var obj in jobToggles)
						{
							var toggle = obj.GetComponent<Toggle>();
							if (toggle.interactable && obj.Job is JobRoomDescription description)
							{
								toggle.isOn = recommendedRooms.Exists(x => x == description.Room._type);
							}
						}
					}
				}
			}
			catch (Exception exception)
			{
				Main.Logger.Error(exception.ToString());
			}
		}
	}

	public static class Extensions
	{
		private static readonly RoomDefinition.Type[] TreatmentRooms = new[]
		{
			RoomDefinition.Type.Chromatherapy, RoomDefinition.Type.ClownClinic, RoomDefinition.Type.DNAAnalysis, RoomDefinition.Type.PandemicClinic,
			RoomDefinition.Type.ElectricShockClinic, RoomDefinition.Type.MummyClinic, RoomDefinition.Type.ClinicCubism,
			RoomDefinition.Type.LightHeaded, RoomDefinition.Type.AnimalMagnetismClinic, RoomDefinition.Type.TurtleHeadClinic,
			RoomDefinition.Type.ClinicVI10, RoomDefinition.Type.EightBitClinic
		};

		public static readonly RoomDefinition.Type[] DiagnosisRooms = new[]
		{
			RoomDefinition.Type.GeneralDiagnosis, RoomDefinition.Type.Cardiography, RoomDefinition.Type.FluidAnalysis, RoomDefinition.Type.DNAAnalysis
		};

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
						recommendedRooms.AddRange(DiagnosisRooms);
					}
					else if (modifier is QualificationTreatmentModifier)
					{
						recommendedRooms.AddRange(TreatmentRooms);
					}
					else if (modifier is QualificationResearchModifier)
					{
						recommendedRooms.Add(RoomDefinition.Type.Research);
					}
				}
			}
		}

		public static List<RoomDefinition.Type> GetRecommendedJobRooms(this Staff staff, QualificationSlot qualificationSlot)
		{
			var recommendedRooms = new List<RoomDefinition.Type>();
			staff.GetRecommendedJobRooms(qualificationSlot, recommendedRooms);
			return recommendedRooms;
		}

		public static List<RoomDefinition.Type> GetRecommendedJobRooms(this Staff staff)
		{
			var recommendedRooms = new List<RoomDefinition.Type>();
			foreach (var qualification in staff.Qualifications)
			{
				staff.GetRecommendedJobRooms(qualification, recommendedRooms);
			}

			return recommendedRooms;
		}
	}
}
