using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Harmony12;
using UnityEngine;
using TH20;
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
