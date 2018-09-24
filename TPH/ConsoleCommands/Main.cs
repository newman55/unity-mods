/*
 * https://www.dropbox.com/s/035cxp4dfix5bhl/ConsoleCommands.zip?dl=0
 * 
 * Dev console opens with a tilde.
 * 
 * Modify the UnityConsole.ConsoleCommandsDatabase class in Assembly-CSharp-firstpass.dll.
 * 
 *  public static void RegisterCommand(string command, string description, string usage, ConsoleCommandCallback callback)
 *  {
 *		Console.WriteLine(command);
 *	}
 *
 *	public static void RegisterSimpleCommand(string command, string description, SimpleConsoleCommandCallback callback)
 *	{
 *		Console.WriteLine(command);
 *	}
 * 
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using Harmony12;
using UnityEngine;
using UnityConsole;

namespace ConsoleCommands
{
    static class Main
    {
        static bool Load()
        {
            var harmony = HarmonyInstance.Create(nameof(ConsoleCommands));
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            return true;
        }

        [HarmonyPatch(typeof(ConsoleController), "Update")]
        static class ConsoleController_Update_Patch
        {
            static void Postfix(ConsoleController __instance)
            {
                if (Input.GetKeyDown(KeyCode.BackQuote))
                {
                    __instance.UI.ToggleConsole();
                }
            }
        }

        [HarmonyPatch(typeof(ConsoleCommandsDatabase), "RegisterCommand")]
        static class ConsoleCommandsDatabase_RegisterCommand_Patch
        {
            static void Postfix(string command, string description, string usage, ConsoleCommandCallback callback, Dictionary<string, ConsoleCommand> ___Database)
            {
                if (!___Database.ContainsKey(command))
                    ___Database.Add(command, new ConsoleCommand(command, description, usage, callback));
            }
        }

        [HarmonyPatch(typeof(ConsoleCommandsDatabase), "RegisterSimpleCommand")]
        static class ConsoleCommandsDatabase_RegisterSimpleCommand_Patch
        {
            static void Postfix(string command, string description, SimpleConsoleCommandCallback callback, Dictionary<string, ConsoleCommand> ___Database)
            {
                if (!___Database.ContainsKey(command))
                    ___Database.Add(command, new ConsoleCommand(command, description, "", new ConsoleCommandCallback((args) => { callback(); return ConsoleCommandResult.Succeeded(null); })));
            }
        }

        [HarmonyPatch(typeof(ConsoleCommandsDatabase), "ExecuteCommand")]
        static class ConsoleCommandsDatabase_ExecuteCommand_Patch
        {
            static void Postfix(string command, params string[] args)
            {
                Console.WriteLine($"ExecuteCommand {command}");
            }
        }
    }
}
