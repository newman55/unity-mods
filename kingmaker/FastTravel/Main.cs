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

            return true;
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            enabled = value;

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

        static float GetDeltaTime()
        {
            try
            {
                var player = Game.Instance?.Player;
                if (enabled && player != null)
                {
                    if (Game.Instance.CurrentMode == Kingmaker.GameModes.GameModeType.Default)
                    {
                        if (!Game.Instance.Player.IsInCombat)
                        {
                            return Time.deltaTime * settings.TimeScaleNonCombat;
                        }
                        return Time.deltaTime * settings.TimeScaleInCombat;
                    }
                    if (Game.Instance.CurrentMode == Kingmaker.GameModes.GameModeType.GlobalMap || Game.Instance.CurrentMode == Kingmaker.GameModes.GameModeType.Kingdom)
                    {
                        return Time.deltaTime * settings.TimeScaleInGlobalMap;
                    }
                }
            }
            catch (Exception e)
            {
                //Debug.LogException(e);
            }

            return Time.deltaTime;
        }

        [HarmonyPatch(typeof(TimeController), "Tick")]
        static class TimeController_Tick_Patch
        {
            static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> instructions)
            {
                var from = AccessTools.Method(typeof(Time), "get_deltaTime", new Type[0]);
                var to = SymbolExtensions.GetMethodInfo(() => Main.GetDeltaTime());
                return instructions.MethodReplacer(from, to);
            }
        }
    }
}
