using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Harmony12;
using UnityEngine;
using UnityModManagerNet;
using Kingmaker;
using Kingmaker.Blueprints.Root;
using Kingmaker.Controllers;
using Kingmaker.Visual.Animation;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace FastTravel
{
    public class Settings : UnityModManager.ModSettings
    {
        public float TimeScaleNonCombat = 2f;
        public float TimeScaleInCombat = 1f;
        public float TimeScaleInGlobalMap = 1.5f;
        public float SpeedScaleInGlobalMap = 1f;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }

    static class Main
    {
        class FastTravelTimeController : MonoBehaviour
        {
            private void Awake()
            {
                DontDestroyOnLoad(this);
            }

            private void LateUpdate()
            {
                if (!Main.enabled || Game.Instance.IsPaused || Game.Instance.InvertPauseButtonPressed || Game.Instance.Player == null)
                    return;
                
                if (Game.Instance.CurrentMode == Kingmaker.GameModes.GameModeType.Default)
                {
                    if (!Game.Instance.Player.IsInCombat)
                    {
                        Game.Instance.TimeController.PlayerTimeScale = settings.TimeScaleNonCombat;
                    }
                    else
                    {
                        Game.Instance.TimeController.PlayerTimeScale = settings.TimeScaleInCombat;
                    }
                    return;
                }
                Game.Instance.TimeController.PlayerTimeScale = 1f;
            }
        }
        
        public static bool enabled;
        public static Settings settings;
        public static float mechanicsSpeedBase;
        public static float visualSpeedBase;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            var harmony = HarmonyInstance.Create(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            settings = Settings.Load<Settings>(modEntry);
            settings.TimeScaleNonCombat = Mathf.Clamp(settings.TimeScaleNonCombat, 1f, 3f);
            settings.TimeScaleInCombat = Mathf.Clamp(settings.TimeScaleInCombat, 0.5f, 1.5f);
            settings.TimeScaleInGlobalMap = Mathf.Clamp(settings.TimeScaleInGlobalMap, 1f, 2f);
            settings.SpeedScaleInGlobalMap = Mathf.Clamp(settings.SpeedScaleInGlobalMap, 1f, 3f);

            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;

            SceneManager.sceneLoaded += (scene, mode) =>
            {
                if (scene.name == "Globalmap")
                {
                    UpdateSpeed();
                }
            };
            
            new GameObject(nameof(FastTravelTimeController), typeof(FastTravelTimeController));

            return true;
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            enabled = value;

            UpdateSpeed();

            return true;
        }

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            GUILayout.Space(5);
            
            GUILayout.Label("Increasing game speed", UnityModManager.UI.bold, GUILayout.ExpandWidth(false));
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("Exploration", GUILayout.ExpandWidth(false));
            GUILayout.Space(10);
            settings.TimeScaleNonCombat = GUILayout.HorizontalSlider(settings.TimeScaleNonCombat, 1f, 3f, GUILayout.Width(300f));
            GUILayout.Label($" {settings.TimeScaleNonCombat:p0}", GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("Battle", GUILayout.ExpandWidth(false));
            GUILayout.Space(10);
            settings.TimeScaleInCombat = GUILayout.HorizontalSlider(settings.TimeScaleInCombat, 0.5f, 1.5f, GUILayout.Width(300f));
            GUILayout.Label($" {settings.TimeScaleInCombat:p0}", GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("Global map", GUILayout.ExpandWidth(false));
            GUILayout.Space(10);
            settings.TimeScaleInGlobalMap = GUILayout.HorizontalSlider(settings.TimeScaleInGlobalMap, 1f, 2f, GUILayout.Width(300f));
            GUILayout.Label($" {settings.TimeScaleInGlobalMap:p0}", GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();
            
            GUILayout.Space(5);
            
            GUILayout.Label("Increasing unit speed (Cheating)", UnityModManager.UI.bold, GUILayout.ExpandWidth(false));
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("Global map", GUILayout.ExpandWidth(false));
            GUILayout.Space(10);
            settings.SpeedScaleInGlobalMap = GUILayout.HorizontalSlider(settings.SpeedScaleInGlobalMap, 1f, 3f, GUILayout.Width(300f));
            GUILayout.Label($" {settings.SpeedScaleInGlobalMap:p0}", GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();
            
            UpdateSpeed();
        }

        static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            settings.Save(modEntry);
        }

        private static bool mInit;

        static void UpdateSpeed()
        {
            if (BlueprintRoot.Instance == null || BlueprintRoot.Instance.GlobalMap == null)
            {
                return;
            }

            if (enabled)
            {
                if (!mInit)
                {
                    mechanicsSpeedBase = BlueprintRoot.Instance.GlobalMap.MechanicsSpeedBase;
                    visualSpeedBase = BlueprintRoot.Instance.GlobalMap.VisualSpeedBase;
                    mInit = true;
                }

                BlueprintRoot.Instance.GlobalMap.MechanicsSpeedBase = mechanicsSpeedBase * settings.SpeedScaleInGlobalMap;
                BlueprintRoot.Instance.GlobalMap.VisualSpeedBase = visualSpeedBase * settings.TimeScaleInGlobalMap;
            }
            else if (mInit)
            {
                BlueprintRoot.Instance.GlobalMap.MechanicsSpeedBase = mechanicsSpeedBase;
                BlueprintRoot.Instance.GlobalMap.VisualSpeedBase = visualSpeedBase;
            }
        }
    }
}
