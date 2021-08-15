using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using HarmonyLib;
using Verse;
using RimWorld;

namespace Bayonet
{

    [HarmonyPatch(typeof(StatWorker_MeleeAverageDPS), nameof(StatWorker_MeleeAverageDPS.GetExplanationUnfinalized))]
    internal static class Patch_MeleeExplanation
    {
        private static readonly bool DEBUGGING_HERE = Mod.DEBUGGING && true;

        // From a ThingDef, get the version of that thing held by the pawn, if it has Comps.
        // If it cannot be found for any reason, return null.
        private static ThingWithComps GetThingFromPawn(Pawn p, ThingDef d)
        {
            try
            {
                return p.equipment.AllEquipmentListForReading
                    .Where(i => i.def.Equals(d)).FirstOrDefault() as ThingWithComps;
            }
            catch (NullReferenceException)
            {
                Mod.LogWarning(
                    String.Format(
                        "could not get thing from pawn: {0}, {1}, {2}",
                        p.ToStringSafe(), p.equipment.ToStringSafe(), p.equipment.AllEquipmentListForReading.ToStringSafeEnumerable()
                    ));
                return null;
            }
        }

        // Make all combinations of a given tool's label and the capacities thereof
        private static List<String> MakeToolCapacityString(Tool t)
        {
            string toolLabel = t.LabelCap;
            List<ToolCapacityDef> capacities = t.capacities;
            if (capacities != null)
                return capacities
                    .Select(c => String.Format("{0} ({1})", t.LabelCap, c.label))
                    .ToList();

            return new List<string>(0);
        }

        private static Dictionary<string, string> CreateDictionary(List<Tool> tList1, List<Tool> tList2)
        {
            List<string> capacityList1 = tList1.Select(MakeToolCapacityString).SelectMany(i => i).ToList();
            List<string> capacityList2 = tList2.Select(MakeToolCapacityString).SelectMany(i => i).ToList();
            if (capacityList1.Count() == capacityList2.Count())
            {
                Dictionary<string, string> d = new Dictionary<string, string>();
                for (int i = 0; i < capacityList1.Count(); i++)
                    d.Add(capacityList1[i], capacityList2[i]);

                return d;
            }
            else
            {
                if (DEBUGGING_HERE)
                    Mod.LogError("defTools has different num of capacities than itemTools? "
                            + String.Format(
                                "def: {0}; item: {1}",
                                capacityList1.ToStringSafeEnumerable(),
                                capacityList2.ToStringSafeEnumerable()));
            }

            return null;
        }

        static void Postfix(ref string __result, StatRequest req)
        {
            Pawn pawn = StatWorker_MeleeAverageDPS.GetCurrentWeaponUser(req.Thing);
            if (Utilities.GetBayonetBeltIfValidWielder(pawn) != null)
            {
                List<string> replace = new List<string>();
                ThingWithComps thing = GetThingFromPawn(pawn, req.Def as ThingDef) as ThingWithComps;
                if (thing != null)
                {
                    CompEquippable equipComp = thing.GetComp<CompEquippable>();

                    // derive a mapping between the tooldef strings and the real tool strings
                    List<Tool> defTools = thing.def.tools.ToList();
                    List<Tool> itemTools = equipComp.AllVerbs
                        .Where(i => i.IsMeleeAttack)
                        .Select(i => i.tool).ToList();

                    Dictionary<string, string> toolPairs = CreateDictionary(defTools, itemTools);
                    if (toolPairs != null)
                    {
                        // replace lines where they match over, reconcat, and display
                        string[] explanationLines = __result.Split('\n');
                        for (int i = 0; i < explanationLines.Count(); i++)
                        {
                            if (toolPairs.ContainsKey(explanationLines[i].Trim()))
                            {
                                int startingSpaces = explanationLines[i].TakeWhile(c => c == ' ').Count();
                                explanationLines[i] = String.Concat(Enumerable.Repeat(" ", startingSpaces))
                                    + toolPairs[explanationLines[i].Trim()];
                            }
                        }

                        __result = String.Join("\n", explanationLines);
                    }
                }
            }

            //if (DEBUGGING_HERE)
            //    Mod.LogMessage(__result);
        }
    }
}
