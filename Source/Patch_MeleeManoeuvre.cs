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
    /*
     * In verb melee attack, if you are attacking properly with a bayonet, you should be using the stab manoeuvre. 
     * This manoeuvre also determines the flavour text in the battle log.
     * 
     * If the bayonet is configured properly, the pawn is validly using a bayonet, and the verb is using the bayonet
     * tool as expected in Patch_BayonetWield, this changes the manoeuvre in the verb as it is attacking, into a
     * stab. After the melee attack is cast, it reverts that change.
     */
    [HarmonyPatch(typeof(Verb_MeleeAttack), "TryCastShot")]
    static class Patch_MeleeManoeuvre
    {
        private static bool DEBUGGING_HERE = Mod.DEBUGGING && false;

        internal static ManeuverDef GetBayonetManoeuvreDef()
        {
            ManeuverDef correctManoeuvre = Utilities.GetBayonetTool().Maneuvers
                .Where((ManeuverDef i) => i.defName.ToLower().Equals("stab"))
                .FirstOrDefault();

            if (correctManoeuvre == null)
                Mod.LogError("Bayonet improperly configured without stab manoeuvre! "
                    + "Manoeuvres are [{0}]".Formatted(
                        Utilities.GetBayonetTool().Maneuvers.Join()));

            return correctManoeuvre;
        }

        internal static void Prefix(Verb_MeleeAttack __instance, out Changes __state)
        {
            __state = null;
            if (__instance.CasterIsPawn)
                if (__instance.Caster is Pawn p)
                {
                    ThingWithComps bayonetBelt = Utilities.GetBayonetBeltIfValidWielder(p) as ThingWithComps;
                    if (bayonetBelt != null && __instance.tool != null)
                    {
                        if (__instance.tool.Equals(Utilities.GetBayonetTool()))
                        {
                            __state = new Changes(true, __instance.maneuver, GetBayonetManoeuvreDef());
                            __instance.maneuver = __state.newManoeuvre ?? __instance.maneuver;  // if null, do nothing

                            if (DEBUGGING_HERE)
                                Mod.LogMessage(__state.MakeChangeString());
                        }
                    }
                }
        }

        internal static void Postfix(Verb_MeleeAttack __instance, Changes __state)
        {
            if (__state != null && __state.wasChanged)
                if (__state.ChangeActuallyOccurred())
                    __instance.maneuver = __state.originalManoeuvre;
        }

        internal class Changes
        {
            public bool wasChanged;
            public ManeuverDef originalManoeuvre;
            public ManeuverDef newManoeuvre;

            public Changes(bool wasChanged, ManeuverDef originalManoeuvre, ManeuverDef newManoeuvre)
            {
                this.wasChanged = wasChanged;
                this.originalManoeuvre = originalManoeuvre;
                this.newManoeuvre = newManoeuvre;
            }

            public bool ChangeActuallyOccurred()
            {
                return !originalManoeuvre.defName.Equals(newManoeuvre.defName);
            }

            internal string MakeChangeString()
            {
                return "manoeuvre change: {0} -> {1}".Formatted(
                    (originalManoeuvre == null)
                        ? "manoeuvre_null"
                        : originalManoeuvre.defName ?? "def_null",
                    (newManoeuvre == null)
                        ? "manoeuvre_null"
                        : newManoeuvre.defName ?? "def_null");
            }
        }
    }
}
