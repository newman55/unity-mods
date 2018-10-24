using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Harmony12;
using UnityEngine;
using UnityModManagerNet;
using Kingmaker;
using Kingmaker.Controllers;
using Kingmaker.Visual.Animation;
using UnityEngine.Playables;

namespace FastTravel
{
    public class Settings : UnityModManager.ModSettings
    {
        public float TimeScaleNonCombat = 2f;
        public float TimeScaleInCombat = 1f;
        public float TimeScaleInGlobalMap = 1.5f;

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
                if (Game.Instance.IsPaused)
                    return;
                
                if (Main.enabled && Game.Instance?.Player != null && !Game.Instance.InvertPauseButtonPressed)
                {
                    var scale = Game.Instance.TimeController.PlayerTimeScale;
                    
                    if (Game.Instance.CurrentMode == Kingmaker.GameModes.GameModeType.Default)
                    {
                        if (!Game.Instance.Player.IsInCombat)
                        {
                            scale = settings.TimeScaleNonCombat;
                        }
                        else
                        {
                            scale = settings.TimeScaleInCombat;
                        }
                    }
                    if (Game.Instance.CurrentMode == Kingmaker.GameModes.GameModeType.GlobalMap)
                    {
                        scale = settings.TimeScaleInGlobalMap;
                    }
                    
                    Game.Instance.TimeController.PlayerTimeScale = scale;
                }
            }
        }
        
        public static bool enabled;
        public static Settings settings;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            var harmony = HarmonyInstance.Create(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            settings = Settings.Load<Settings>(modEntry);
            settings.TimeScaleNonCombat = Mathf.Clamp(settings.TimeScaleNonCombat, 1f, 3f);
            settings.TimeScaleInCombat = Mathf.Clamp(settings.TimeScaleInCombat, 0.5f, 1.5f);
            settings.TimeScaleInGlobalMap = Mathf.Clamp(settings.TimeScaleInGlobalMap, 1f, 2f);

            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;
            
            new GameObject(nameof(FastTravelTimeController), typeof(FastTravelTimeController));

            return true;
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            enabled = value;
            
            Game.Instance.TimeController.PlayerTimeScale = 1f;

            return true;
        }

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Non combat", GUILayout.ExpandWidth(false));
            settings.TimeScaleNonCombat = GUILayout.HorizontalSlider(settings.TimeScaleNonCombat, 1f, 3f, GUILayout.Width(300f));
            GUILayout.Label($" {settings.TimeScaleNonCombat:p0}", GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("In combat", GUILayout.ExpandWidth(false));
            settings.TimeScaleInCombat = GUILayout.HorizontalSlider(settings.TimeScaleInCombat, 0.5f, 1.5f, GUILayout.Width(300f));
            GUILayout.Label($" {settings.TimeScaleInCombat:p0}", GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("Global map", GUILayout.ExpandWidth(false));
            settings.TimeScaleInGlobalMap = GUILayout.HorizontalSlider(settings.TimeScaleInGlobalMap, 1f, 2f, GUILayout.Width(300f));
            GUILayout.Label($" {settings.TimeScaleInGlobalMap:p0}", GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();
        }

        static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            settings.Save(modEntry);
        }
    }
}
