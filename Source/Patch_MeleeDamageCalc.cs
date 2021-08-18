using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

using System.Reflection;
using HarmonyLib;
using System.Text.RegularExpressions;

namespace Bayonet
{
    /*
     * This patch is necessary for two reasons. First, it changes the calculations used in the average melee DPS data so that it
     * accounts for use of the bayonet. Second, it passes the **bayonet** equipment quality to the melee damage calculation. We 
     * want the bayonet equipment quality to be controlling, not the quality of the stick the bayonet is on.
     */
    [HarmonyPatch(typeof(VerbProperties), nameof(VerbProperties.AdjustedMeleeDamageAmount),
     new[] { typeof(Tool), typeof(Pawn), typeof(Thing), typeof(HediffComp_VerbGiver) })]
    internal static class Patch_MeleeDamageAmount
    {
        private static readonly bool DEBUGGING_HERE = Mod.DEBUGGING && false;

        private static string ToolCapacitiesToString(Tool t)
        {
            return t.capacities.Select(i => i.ToStringSafe()).Join();
        }

        internal static void Prefix(ref Tool tool, ref Pawn attacker, ref Thing equipment, ref HediffComp_VerbGiver hediffCompSource)
        {
            /* 
             * If using a valid wielder and the wielder is a stabbing type (as defined in the bayonet def),
             * calculate damage as if the tool were the bayonet with the bayonet's equipment state rather than rifle.
             */
            Thing theBayonetBelt = Utilities.GetBayonetBeltIfValidWielder(attacker);
            if (theBayonetBelt != null)
                if (Patch_BayonetWield.HasStabCapacity(tool) || Patch_BayonetWield.HasPokeCapacity(tool))
                {
                    equipment = theBayonetBelt; // pass quality from bayonet belt to melee damage
                    tool = Utilities.GetBayonetTool();

                    // this method constantly gets called for some reason, even when paused
                    if (DEBUGGING_HERE)
                        Mod.LogMessage(String.Format(
                            "imputed tool {0} and equipment {1}; orig capacites {2}",
                            tool.ToStringSafe(), equipment.ToStringSafe(),
                            ToolCapacitiesToString(tool)));
                }
        }
    }

    [HarmonyPatch(typeof(VerbProperties), nameof(VerbProperties.AdjustedArmorPenetration),
        new[] { typeof(Tool), typeof(Pawn), typeof(Thing) , typeof(HediffComp_VerbGiver) })]
    internal static class Patch_AdjustedArmorPenetration
    {
        private static readonly bool DEBUGGING_HERE = Mod.DEBUGGING && false;

        static void Prefix(ref Tool tool, ref Pawn attacker, ref Thing equipment, ref HediffComp_VerbGiver hediffCompSource)
        {
            Patch_MeleeDamageAmount.Prefix(ref tool, ref attacker, ref equipment, ref hediffCompSource);
        }
    }

    [HarmonyPatch(typeof(Pawn_MeleeVerbs), nameof(Pawn_MeleeVerbs.TryMeleeAttack))]
    internal static class Patch_ConfirmPatchEffective
    {
        // static readonly bool DEBUGGING_HERE = false;

        static MethodInfo getMeleeVerbMethod = AccessTools.Method(typeof(Pawn_MeleeVerbs), "TryGetMeleeVerb");
        static ToolCapacityDef POKE_CAPACITY = DefDatabase<ToolCapacityDef>.GetNamed("Poke");

        // This is meant to 
        static void Prefix(ref Pawn_MeleeVerbs __instance, ref Thing target, ref Verb verbToUse)
        {
            if (Utilities.GetBayonetBeltIfValidWielder(__instance.Pawn) != null)
            {
                if (verbToUse != null && verbToUse.tool != null
                    && verbToUse.tool.capacities != null)
                {
                    if (verbToUse.tool.capacities.Contains(POKE_CAPACITY))
                    {
                        // weapon verb should not have a poke if it was properly replaced
                        // check whether it was properly replaced here
                        Mod.LogWarning("Replacement of poke capacity ineffective?");
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(Verb_MeleeAttack), nameof(Verb_MeleeAttack.CreateCombatLog))]
    internal static class Patch_LogAttackTool
    {
        private static readonly bool DEBUGGING_HERE = false;

        static void Postfix(ref Verb_MeleeAttack __instance, ref BattleLogEntry_MeleeCombat __result)
        {
            if (Mod.DEBUGGING && DEBUGGING_HERE)
            {
                Mod.LogMessage("tool label used: " + __instance.tool.label
                    + "; passed to battle log as: " + Traverse.Create(__result).Field("toolLabel"));
            }
        }
    }
}