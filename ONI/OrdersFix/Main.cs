using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Harmony12;
using UnityModManagerNet;

namespace OrdersFix
{
    static class Main
    {
        public static bool enabled;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            var harmony = HarmonyInstance.Create(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            modEntry.OnToggle = OnToggle;

            return true;
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            enabled = value;

            return true;
        }
    }

    [HarmonyPatch(typeof(Fabricator), "OnCompleteWork")]
    static class Fabricator_OnCompleteWork_Patch
    {
        static bool Prefix(Fabricator __instance, Worker worker, List<Fabricator.UserOrder> ___userOrders, List<Fabricator.MachineOrder> ___machineOrders)
        {
            if (!Main.enabled)
                return true;

            if (___userOrders.Count > 1)
            {
                var machineOrder = ___machineOrders[0];
                if (!machineOrder.parentOrder.infinite)
                {
                    ___userOrders.Remove(machineOrder.parentOrder);
                    ___userOrders.Insert(0, machineOrder.parentOrder);
                }
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Fabricator), "UpdateOrderQueue")]
    static class Fabricator_UpdateOrderQueue_Patch
    {
        static bool Prefix(Fabricator __instance, bool force_update, Operational ___operational, List<Fabricator.UserOrder> ___userOrders,
            List<Fabricator.MachineOrder> ___machineOrders)
        {
            if (!Main.enabled)
                return true;

            if (!force_update && !___operational.IsOperational)
                return false;

            if (___userOrders.Count <= 1)
                return true;

            foreach (var userOrder in ___userOrders)
            {
                int count = ___machineOrders.Count(x => x.parentOrder == userOrder);
                if (userOrder.infinite && count < 2)
                {
                    for (int i = count; i < 2; i++)
                    {
                        ___machineOrders.Add(new Fabricator.MachineOrder() { parentOrder = userOrder });
                    }
                }
                else if (count == 0)
                {
                    ___machineOrders.Add(new Fabricator.MachineOrder() { parentOrder = userOrder });
                }
            }

            var ingregientsAvailable = new bool[___userOrders.Count];
            for (int i = 0, c = ___userOrders.Count; i < c; i++)
            {
                ingregientsAvailable[i] = true;
                var userOrder = ___userOrders[i];
                var allIngredients = userOrder.recipe.GetAllIngredients(userOrder.orderTags);
                foreach(var ingredient in allIngredients)
                {
                    if (__instance.inStorage.GetMassAvailable(ingredient.tag) < ingredient.amount)
                    {
                        ingregientsAvailable[i] = false;
                        break;
                    }
                }
            }

            Comparison<Fabricator.MachineOrder> comparison = delegate (Fabricator.MachineOrder x, Fabricator.MachineOrder y) 
            {
                if (x.chore != null)
                {
                    return -1;
                }
                else if (y.chore != null)
                {
                    return 1;
                }
                else
                {
                    int idx1 = ___userOrders.FindIndex(n => n == x.parentOrder);
                    int idx2 = ___userOrders.FindIndex(n => n == y.parentOrder);

                    if (idx1 != -1 && idx2 != -1)
                    {
                        if (ingregientsAvailable[idx1] && !ingregientsAvailable[idx2])
                        {
                            return -1;
                        }
                        else if (!ingregientsAvailable[idx1] && ingregientsAvailable[idx2])
                        {
                            return 1;
                        }
                    }
                    
                    if (idx1 == idx2)
                    {
                        return 0;
                    }
                    else
                    {
                        return idx1 < idx2 ? -1 : 1;
                    }
                }
            };
            ___machineOrders.Sort(comparison);

            return true;
        }
    }

    [HarmonyPatch(typeof(Refinery), "OnCompleteWork")]
    static class Refinery_OnCompleteWork_Patch
    {
        static bool Prefix(Refinery __instance, List<Fabricator.UserOrder> ___userOrders, List<Fabricator.MachineOrder> ___machineOrders)
        {
            if (!Main.enabled)
                return true;

            if (___userOrders.Count > 1)
            {
                var machineOrder = ___machineOrders[0];
                if (!machineOrder.parentOrder.infinite)
                {
                    ___userOrders.Remove(machineOrder.parentOrder);
                    ___userOrders.Insert(0, machineOrder.parentOrder);
                }
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Refinery), "UpdateOrderQueue")]
    static class Refinery_UpdateOrderQueue_Patch
    {
        static bool Prefix(Refinery __instance, bool force_update, Operational ___operational, List<Refinery.UserOrder> ___userOrders,
            List<Refinery.MachineOrder> ___machineOrders)
        {
            if (!Main.enabled)
                return true;

            if (!force_update && !___operational.IsOperational)
                return false;

            if (___userOrders.Count <= 1)
                return true;

            foreach (var userOrder in ___userOrders)
            {
                int count = ___machineOrders.Count(x => x.parentOrder == userOrder);
                if (userOrder.infinite && count < 2)
                {
                    for (int i = count; i < 2; i++)
                    {
                        ___machineOrders.Add(new Refinery.MachineOrder() { parentOrder = userOrder });
                    }
                }
                else if (count == 0)
                {
                    ___machineOrders.Add(new Refinery.MachineOrder() { parentOrder = userOrder });
                }
            }

            var ingregientsAvailable = new bool[___userOrders.Count];
            for (int i = 0, c = ___userOrders.Count; i < c; i++)
            {
                ingregientsAvailable[i] = true;
                var userOrder = ___userOrders[i];
                var allIngredients = userOrder.recipe.ingredients;
                foreach (var ingredient in allIngredients)
                {
                    if (__instance.inStorage.GetUnitsAvailable(ingredient.material) < ingredient.amount)
                    {
                        ingregientsAvailable[i] = false;
                        break;
                    }
                }
            }

            Comparison<Refinery.MachineOrder> comparison = delegate (Refinery.MachineOrder x, Refinery.MachineOrder y)
            {
                if (x.chore != null)
                {
                    return -1;
                }
                else if (y.chore != null)
                {
                    return 1;
                }
                else
                {
                    int idx1 = ___userOrders.FindIndex(n => n == x.parentOrder);
                    int idx2 = ___userOrders.FindIndex(n => n == y.parentOrder);

                    if (idx1 != -1 && idx2 != -1)
                    {
                        if (ingregientsAvailable[idx1] && !ingregientsAvailable[idx2])
                        {
                            return -1;
                        }
                        else if (!ingregientsAvailable[idx1] && ingregientsAvailable[idx2])
                        {
                            return 1;
                        }
                    }

                    if (idx1 == idx2)
                    {
                        return 0;
                    }
                    else
                    {
                        return idx1 < idx2 ? -1 : 1;
                    }
                }
            };
            ___machineOrders.Sort(comparison);

            return true;
        }
    }
}
