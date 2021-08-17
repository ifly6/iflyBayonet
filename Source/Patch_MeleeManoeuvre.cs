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
            try
            {
                return Utilities.GetBayonetTool().Maneuvers
                    .Where((ManeuverDef i) => i.defName.ToLower().Equals("stab"))
                    .FirstOrDefault();
            }
            catch (Exception)
            {
                Mod.LogError("Bayonet improperly configured without stab manoeuvre! "
                    + "Manoeuvres are [{0}]".Formatted(
                        Utilities.GetBayonetTool().Maneuvers.Join()));
                return null;
            }
        }

        internal static DamageDef GetBayonetDamageDef()
        {
            // use name matching
            string defName = GetBayonetManoeuvreDef().defName;  // should be same name as manoeuvre
            try { return DefDatabase<DamageDef>.GetNamed(defName); }
            catch (Exception) { return null; }
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
                            __state = new Changes(true,
                                __instance.maneuver, GetBayonetManoeuvreDef(),
                                __instance.GetDamageDef(), GetBayonetDamageDef());

                            // if null, do nothing
                            __instance.maneuver = __state.newManoeuvre ?? __instance.maneuver;
                            __instance.verbProps.meleeDamageDef = __state.newDamageDef ?? __instance.GetDamageDef();

                            if (DEBUGGING_HERE)
                                Mod.LogMessage(__state.MakeChangeString());
                        }
                    }
                }
        }

        internal static void Postfix(Verb_MeleeAttack __instance, Changes __state)
        {
            if (__state != null && __state.wasChanged)
                if (__state.ManoeuvreActuallyChanged())
                {
                    __instance.maneuver = __state.oldManoeuvre;
                    __instance.verbProps.meleeDamageDef = __state.oldDamageDef;
                }
        }

        internal class Changes
        {
            public bool wasChanged;
            public ManeuverDef oldManoeuvre;
            public ManeuverDef newManoeuvre;
            public DamageDef oldDamageDef;
            public DamageDef newDamageDef;

            public Changes(bool wasChanged, ManeuverDef oldManoeuvre, ManeuverDef newManoeuvre,
                DamageDef oldDamage, DamageDef newDamage)
            {
                this.wasChanged = wasChanged;
                this.oldManoeuvre = oldManoeuvre;
                this.newManoeuvre = newManoeuvre;
                this.oldDamageDef = oldDamage;
                this.newDamageDef = newDamage;
            }

            public bool ManoeuvreActuallyChanged()
            {
                return !oldManoeuvre.defName.Equals(newManoeuvre.defName);
            }

            private string DefToStringSafe(Def def)
            {
                return (def == null)
                        ? def.GetType().Name + "_null"
                        : def.defName ?? "def_null";
            }

            internal string MakeChangeString()
            {
                return "manoeuvre change: {0} -> {1} && damage def: {2} -> {3}".Formatted(
                    DefToStringSafe(oldManoeuvre),
                    DefToStringSafe(newManoeuvre),
                    DefToStringSafe(oldDamageDef),
                    DefToStringSafe(newDamageDef));
            }
        }
    }
}
