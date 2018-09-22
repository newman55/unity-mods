using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Harmony12;
using UnityModManagerNet;

namespace ElectrolyzerFix
{
    public class Main
    {
        public static UnityModManager.ModEntry mod;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            mod = modEntry;

            var harmony = HarmonyInstance.Create(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            return true;
        }
    }

    [HarmonyPatch(typeof(Electrolyzer))]
    [HarmonyPatch("RoomForPressure", MethodType.Getter)]
    static class Electrolyzer_RoomForPressure_Patch
    {
        //static SimHashes[] condition = new SimHashes[] { SimHashes.Oxygen };

        static bool Prefix(Electrolyzer __instance, ref bool __result)
        {
            if (!Main.mod.Active)
                return true;

            int num = Grid.PosToCell(__instance.transform.GetPosition());
            num = Grid.CellAbove(num);

            var fn = new Func<HashSet<int>, Electrolyzer, bool>(NormalPressure);
            __result = FloodFillFindAll(fn, __instance, num, 3, true, true);

            return false;
        }

        static bool NormalPressure(HashSet<int> cells, Electrolyzer electrolyzer)
        {
            return cells.Average(x => Grid.Mass[x]) < electrolyzer.maxMass;
        }

        public static bool FloodFillFindAll<ArgType>(Func<HashSet<int>, ArgType, bool> fn, ArgType arg, int start_cell, int max_depth, bool stop_at_solid, bool stop_at_liquid, SimHashes[] only_elements = null, SimHashes[] ignore_elements = null)
        {
            GameUtil.FloodFillNext.Enqueue(new GameUtil.FloodFillInfo
            {
                cell = start_cell,
                depth = 0
            });
            while (GameUtil.FloodFillNext.Count > 0)
            {
                GameUtil.FloodFillInfo floodFillInfo = GameUtil.FloodFillNext.Dequeue();
                if (floodFillInfo.depth < max_depth)
                {
                    if (Grid.IsValidCell(floodFillInfo.cell))
                    {
                        Element element = Grid.Element[floodFillInfo.cell];
                        if (!stop_at_solid || !element.IsSolid)
                        {
                            if (!stop_at_liquid || !element.IsLiquid)
                            {
                                if (only_elements == null || Array.Exists(only_elements, x => x == element.id))
                                {
                                    if (ignore_elements == null || !Array.Exists(ignore_elements, x => x == element.id))
                                    {
                                        if (!GameUtil.FloodFillVisited.Contains(floodFillInfo.cell))
                                        {
                                            GameUtil.FloodFillVisited.Add(floodFillInfo.cell);

                                            GameUtil.FloodFillNext.Enqueue(new GameUtil.FloodFillInfo
                                            {
                                                cell = Grid.CellLeft(floodFillInfo.cell),
                                                depth = floodFillInfo.depth + 1
                                            });
                                            GameUtil.FloodFillNext.Enqueue(new GameUtil.FloodFillInfo
                                            {
                                                cell = Grid.CellRight(floodFillInfo.cell),
                                                depth = floodFillInfo.depth + 1
                                            });
                                            GameUtil.FloodFillNext.Enqueue(new GameUtil.FloodFillInfo
                                            {
                                                cell = Grid.CellAbove(floodFillInfo.cell),
                                                depth = floodFillInfo.depth + 1
                                            });
                                            GameUtil.FloodFillNext.Enqueue(new GameUtil.FloodFillInfo
                                            {
                                                cell = Grid.CellBelow(floodFillInfo.cell),
                                                depth = floodFillInfo.depth + 1
                                            });
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            bool result = fn(GameUtil.FloodFillVisited, arg);
            GameUtil.FloodFillVisited.Clear();
            GameUtil.FloodFillNext.Clear();
            return result;
        }
    }
}
