using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;
using Harmony;
using UnityEngine;
using UnityEngine.UI;
using UnityModManagerNet;

namespace SpeedManager
{
    public class SpeedSet
    {
        [Draw(Precision = 1, Min = 0), Space(5)] public float Frontend = 1f;
        [Draw("Simulation 1", Precision = 1, Min = 0), Space(5)] public float Simulation1 = 20f;
        [Draw("Simulation 2", Precision = 1, Min = 0)] public float Simulation2 = 30f;
        [Draw("Simulation 3", Precision = 1, Min = 0)] public float Simulation3 = 40f;
        [Draw("Race 1", Precision = 1, Min = 0), Space(5)] public float Race1 = 1f;
        [Draw("Race 2", Precision = 1, Min = 0)] public float Race2 = 2f;
        [Draw("Race 3", Precision = 1, Min = 0)] public float Race3 = 4f;

        public static SpeedSet Fast()
        {
            return new SpeedSet { Frontend = 2f, Race1 = 2f, Race2 = 4f, Race3 = 8f, Simulation1 = 30f, Simulation2 = 45f, Simulation3 = 60f };
        }
    }

    public enum SpeedSets { Fast, Slow };

    public class Settings : UnityModManager.ModSettings, IDrawable
    {
        [Draw(DrawType.ToggleGroup)] public SpeedSets Set = SpeedSets.Fast;
        [Draw("", VisibleOn = "Set|Fast")] public SpeedSet Fast = SpeedSet.Fast();
        [Draw("", VisibleOn = "Set|Slow")] public SpeedSet Slow = new SpeedSet();

        public void OnChange()
        {
            Main.ApplySettings(Set);
        }

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }

    static class Main
    {
        public static bool enabled;
        public static Settings settings;

        static readonly float FrontendSpeed = 120000f;
        static float[,] speedMultipliers = null;
        static float[] skipSimSpeed = null;

        static bool Load(UnityModManager.ModEntry modEntry)
        {

            settings = Settings.Load<Settings>(modEntry);

            var harmony = HarmonyInstance.Create(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            modEntry.OnSaveGUI = OnSaveGUI;
            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = OnGUI;

            return true;
        }

        static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            settings.Save(modEntry);
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            try
            {
                if (value)
                {
                    ApplySettings(settings.Set);
                }
                else
                {
                    RestoreSettings();
                }

                enabled = value;
                return true;
            }
            catch (Exception e)
            {
                modEntry.Logger.LogException(e);
                return false;
            }
        }

        public static void ApplySettings(SpeedSets set)
        {
            if (Game.instance?.time == null)
                return;

            var sets = set == SpeedSets.Fast ? settings.Fast : settings.Slow;
            var speedMultipliers = Traverse.Create(Game.instance.time).Field("speedMultipliers").GetValue<float[,]>();
            if (Main.speedMultipliers == null)
                Main.speedMultipliers = speedMultipliers;
            speedMultipliers[2, 0] = sets.Race1;
            speedMultipliers[2, 1] = sets.Race2;
            speedMultipliers[2, 2] = sets.Race3;
            Traverse.Create(Game.instance.time).Field("speedMultipliers").SetValue(speedMultipliers);

            if (Main.skipSimSpeed == null)
                Main.skipSimSpeed = GameTimer.skipSimSpeed;
            GameTimer.skipSimSpeed[1] = sets.Simulation1;
            GameTimer.skipSimSpeed[2] = sets.Simulation2;
            GameTimer.skipSimSpeed[3] = sets.Simulation3;
            GameTimer.minSkipSpeed = sets.Frontend * FrontendSpeed * 0.5f;
            GameTimer.maxSkipSpeed = sets.Frontend * FrontendSpeed;
        }

        static void RestoreSettings()
        {
            if (Game.instance?.time == null)
                return;

            if (Main.speedMultipliers != null)
            {
                Traverse.Create(Game.instance.time).Field("speedMultipliers").SetValue(Main.speedMultipliers);
                GameTimer.skipSimSpeed = Main.skipSimSpeed;
                GameTimer.minSkipSpeed = FrontendSpeed * 0.5f;
                GameTimer.maxSkipSpeed = FrontendSpeed;
            }
        }

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            settings.Draw(modEntry);
        }
    }

    [HarmonyPatch(typeof(GameTimer), "OnLoad")]
    static class GameTimer_OnLoad_Patch
    {
        static void Postfix(GameTimer __instance)
        {
            if (!Main.enabled)
                return;

            try
            {
                Main.ApplySettings(Main.settings.Set);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}
