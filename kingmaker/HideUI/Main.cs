using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Harmony12;
using Kingmaker;
using Kingmaker.GameModes;
using Kingmaker.UI.Common;
using Kingmaker.UI.Selection;
using UnityEngine;
using UnityModManagerNet;

namespace HideUI
{
    static class Main
    {
        public static bool enabled;
        public static UnityModManager.ModEntry.ModLogger logger;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            var harmony = HarmonyInstance.Create(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            modEntry.OnToggle = OnToggle;

            logger = modEntry.Logger;

            return true;
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            enabled = value;

            return true;
        }
    }
    
    [HarmonyPatch(typeof(UnityModManager.UI), "Update")]
    static class UnityModManager_UI_Update_Patch
    {
        static void Postfix(UnityModManager.UI __instance)
        {
            if (!Main.enabled)
                return;

            try
            {
                if (Input.GetKeyUp(KeyCode.H) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) 
                    && Game.Instance.Player != null && (Game.Instance.CurrentMode == GameModeType.Default || Game.Instance.CurrentMode == GameModeType.Pause))
                {
//                    Game.Instance.UI.Canvas.HudAnimation(Game.Instance.UI.Canvas.HUDController.CurrentState == UISectionHUDController.HUDState.AllVisible, false, false);
                    Game.Instance.UI.MainCanvas.CanvasGroup.gameObject.SetActive(!Game.Instance.UI.MainCanvas.CanvasGroup.gameObject.activeSelf);
                }
            }
            catch (Exception)
            {
            }
        }
    }
    
    [HarmonyPatch(typeof(CharacterUIDecal), "Update")]
    static class CharacterUIDecal_Update_Patch
    {
        static void Postfix(CharacterUIDecal __instance, GameObject ___m_Container)
        {
            if (!Main.enabled)
                return;

            try
            {
                if (!Game.Instance.UI.MainCanvas.CanvasGroup.gameObject.activeSelf)
                    ___m_Container.SetActive(false);
            }
            catch (Exception)
            {
            }
        }
    }
}
