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
using UnityEngine.SceneManagement;

namespace CheatMenu
{
    static class Main
    {
        public static bool enabled;
        
        static bool Load(UnityModManager.ModEntry modEntry)
        {
            var harmony = HarmonyInstance.Create(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = OnGUI;

            return true;
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            enabled = value;

            return true;
        }

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name == "Start" || scene.name == "MainMenu" || Game.Instance?.Player == null)
                return;
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("Gold ", GUILayout.ExpandWidth(false));
            for (int i = 1000; i <= 100000; i=i*10)
            {
                if (GUILayout.Button($"{i}", GUILayout.Width(100)))
                {
                    Game.Instance.Player.GainMoney(i);
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("LevelUp ", GUILayout.ExpandWidth(false));
            foreach (var i in new int[] {5,10,20})
            {
                if (GUILayout.Button($"{i}", GUILayout.Width(50)))
                {
                    var exp = BlueprintRoot.Instance.Progression.XPTable.GetBonus(i);
                    foreach (var character in Game.Instance.Player.AllCharacters)
                    {
                        character.Descriptor.Progression.AdvanceExperienceTo(exp, true);
                    }
                }
            }
            GUILayout.EndHorizontal();
        }
    }
}
