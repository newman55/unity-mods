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
        public float TimeScale = 2f;

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
            settings.TimeScale = Mathf.Clamp(settings.TimeScale, 1f, 3f);

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

            GUILayout.Label("Time scale ", GUILayout.ExpandWidth(false));
            settings.TimeScale = GUILayout.HorizontalSlider(settings.TimeScale, 1f, 3f, GUILayout.Width(300f));
            GUILayout.Label($" {settings.TimeScale:p0}", GUILayout.ExpandWidth(false));

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
                if (enabled && Game.Instance != null && (Game.Instance.CurrentMode == Kingmaker.GameModes.GameModeType.Default || Game.Instance.CurrentMode == Kingmaker.GameModes.GameModeType.Kingdom
                    || Game.Instance.CurrentMode == Kingmaker.GameModes.GameModeType.KingdomSettlement) && Game.Instance.Player != null && !Game.Instance.Player.IsInCombat)
                {
                    return Time.deltaTime * settings.TimeScale;
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
