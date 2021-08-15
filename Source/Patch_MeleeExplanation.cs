using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Bayonet
{

    [HarmonyPatch(typeof(StatWorker_MeleeAverageDPS), nameof(StatWorker_MeleeAverageDPS.GetExplanationUnfinalized))]
    internal static class Patch_MeleeExplanation
    {
        private static readonly bool DEBUGGING_HERE = Mod.DEBUGGING && false;

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

        private static Dictionary<string, string> CreateDictionary(List<Tool> defTools, List<Tool> itemTools)
        {
            Dictionary<string, string> d = new Dictionary<string, string>();
            for (int i = 0; i< defTools.Count; i++)
            {
                var defCapacities = MakeToolCapacityString(defTools[i]);
                var itemCapacities = MakeToolCapacityString(itemTools[i]);
                for (int j = 0; j < defCapacities.Count; j++)
                {
                    d[defCapacities[j]] = (j < itemCapacities.Count)
                        ? itemCapacities[j]
                        : itemCapacities[itemCapacities.Count - 1]; // last element repeats if exceeded
                }
            }

            return d.Count == 0 ? null : d;
        }

        static void Postfix(ref string __result, StatRequest req)
        {
            Pawn pawn = StatWorker_MeleeAverageDPS.GetCurrentWeaponUser(req.Thing);
            if (Utilities.GetBayonetBeltIfValidWielder(pawn) != null)
            {
                bool anyPatched = false;
                bool patchingComplete = false;

                List<string> replace = new List<string>();
                ThingWithComps thing = GetThingFromPawn(pawn, req.Def as ThingDef) as ThingWithComps;
                if (thing != null)
                {
                    //CompEquippable equipComp = thing.GetComp<CompEquippable>();
                    //// derive a mapping between the tooldef strings and the real tool strings
                    //List<Tool> defTools = thing.def.tools.ToList();
                    //List<Tool> itemTools = equipComp.AllVerbs
                    //    .Where(i => i.IsMeleeAttack)
                    //    .Select(i => i.tool)
                    //    .ToList();
                    //Dictionary<string, string> toolPairs = CreateDictionary(defTools, itemTools);

                    if (PatchCache.Contains(thing))
                    {
                        var changes = PatchCache.Get(thing);
                        var toolPairs = CreateDictionary(
                            changes.before.Select(i => i.Item2).ToList(),
                            changes.after.Select(i => i.Item2).ToList());

                        if (toolPairs != null)
                        {
                            if (DEBUGGING_HERE)
                                Mod.LogMessage("tool pairs: " + toolPairs.ToStringFullContents());

                            // replace lines where they match over, reconcat, and display
                            string[] explanationLines = __result.Split('\n');
                            for (int i = 0; i < explanationLines.Count(); i++)
                            {
                                if (toolPairs.ContainsKey(explanationLines[i].Trim()))
                                {
                                    int startingSpaces = explanationLines[i].TakeWhile(c => c == ' ').Count();
                                    explanationLines[i] = String.Concat(Enumerable.Repeat(" ", startingSpaces))
                                        + toolPairs[explanationLines[i].Trim()];
                                    anyPatched = true;
                                }
                                patchingComplete = true;
                            }

                            __result = String.Join("\n", explanationLines);
                        }
                    }
                    else
                        Mod.LogError("cache miss for thing " + thing.ToStringSafe());

                } // null can happen, if checking dps of unequipped obj

                if (patchingComplete == false)
                    __result += "\n\nFailed to calculate weapon damage with bayonet. ";
                if (anyPatched == false)
                    __result += "No explanation lines patched. Is bayonet working? ";

            }

            //if (DEBUGGING_HERE)
            //    Mod.LogMessage(__result);
        }
    }
}
