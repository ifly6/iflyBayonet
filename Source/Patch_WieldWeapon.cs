using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using RimWorld;
using Verse;

namespace Bayonet
{
    [HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.AddEquipment))]
    internal static class Patch_WieldWeapon
    {
        private static readonly bool DEBUGGING_HERE = Mod.DEBUGGING && true;
        private static readonly ToolCapacityDef POKE_CAPACITY = DefDatabase<ToolCapacityDef>.GetNamed("Poke");
        private static readonly ToolCapacityDef STAB_CAPACITY = DefDatabase<ToolCapacityDef>.GetNamed("Stab");

        internal static bool HasPokeCapacity(Tool tool)
        {
            return tool.capacities.Contains(Patch_WieldWeapon.POKE_CAPACITY);
        }
        internal static bool HasStabCapacity(Tool tool)
        {
            return tool.capacities.Contains(Patch_WieldWeapon.STAB_CAPACITY);
        }
        private static string __MakeMessage(List<Verb> verbs)
        {
            IEnumerable<Verb> meleeVerbs = verbs
                .Where(v => v != null)
                .Where(v => v.IsMeleeAttack);

            List<string> strings = new List<string>();
            foreach (Verb v in meleeVerbs)
                strings.Add(String.Format(
                    "Verb({0}, {1}: {2})",
                    v.ToStringSafe(),
                    v.tool.label,
                    v.tool.capacities.ToStringSafeEnumerable()));

            return String.Join(", ", strings);
        }

        internal static void DoPawnPatch(Pawn pawn)
        {
            /* 
             * Go and patch the verbs in the rifle if it is compatible. Check first whether it is
             * compatible with bayonet belt. Then, for each weapon verb which displays a poke
             * capacity, replace it with the bayonet belt's STABBY STABBY capacity.
             */
            Thing bayonetBelt = Utilities.GetBayonetBeltIfValidWielder(pawn);
            if (bayonetBelt != null && pawn.equipment.Primary != null) // is compatible and has weapon
            {
                ThingWithComps primaryWeapon = pawn.equipment.Primary;
                CompEquippable equippableComp = primaryWeapon.GetComp<CompEquippable>();
                if (equippableComp != null && equippableComp.AllVerbs != null // weapon has melee capacity
                    && equippableComp.AllVerbs.Count() > 0)
                {
                    List<Verb> primaryMeleeVerbs = equippableComp.AllVerbs
                        .Where(v => v.IsMeleeAttack)
                        .ToList();

                    if (DEBUGGING_HERE)
                        Mod.LogMessage("original verbs: " + __MakeMessage(primaryMeleeVerbs));

                    // replace melee verbs
                    for (int i = 0; i < primaryMeleeVerbs.Count(); i++) // for each melee verb
                    {
                        Verb weaponVerb = primaryMeleeVerbs[i]; // no foreach so we can modify ref
                        if (weaponVerb.tool != null && weaponVerb.tool.capacities != null)
                        {
                            // if the verb relates to poking people, replace it with bayonetting people
                            if (HasPokeCapacity(weaponVerb.tool))
                            {
                                string oldToolName = weaponVerb.tool.ToStringSafe();
                                weaponVerb.tool = Utilities.GetBayonetTool();
                                if (DEBUGGING_HERE)
                                    Mod.LogMessage("Replaced old melee tool [" + oldToolName 
                                        + "] with bayonet tool [" + weaponVerb.tool.ToStringSafe() + "]");
                                
                                /*
                                 * MUST replace all elements with POKE. Otherwise, replacement of the melee damage
                                 * calculations does not work properly. Bayonet added, but the blunt/poke barrel of
                                 * a rifle causes mismatch in number of entries in the melee average damage 
                                 * calculation.
                                 */
                            }
                        }
                    }
                }
                else { Mod.LogError("Pawn weapon no components or verbs??"); }
            }
        }

        // Patching the equipment tracker's thing verbs means that it will give the right names in the combat log.
        // See Patch_MeleeDamageCalc for the patching to impute BAYONET damage rather than RIFLE damage.
        internal static void Postfix(ref Pawn_EquipmentTracker __instance, ThingWithComps newEq)
        {
            if (newEq != null && newEq.def.IsWeaponUsingProjectiles)
                DoPawnPatch(__instance.pawn);
        }
    }

    [HarmonyPatch(typeof(Map), nameof(Map.FinalizeInit))]
    internal static class Patch_MapInit
    {
        private static readonly bool DEBUGGING_HERE = Mod.DEBUGGING && false;

        // Patching needed on initialisation to patch verbs when the game is loaded in.
        internal static void Postfix(ref Map __instance)
        {
            // on spawn map init, patch all the verbs
            // do some checks here too because it's cheaper than doing it for all pawns
            List<Pawn> ourPawns = PawnsFinder.AllMaps
                .Where(p => p != null)
                .Where(p => p.RaceProps.Humanlike) // animals cannot use bayonet
                .Where(p => p.equipment.Primary != null)
                .ToList();
            foreach (Pawn pawn in ourPawns)
            {
                if (DEBUGGING_HERE)
                    Mod.LogMessage("patch pawn " + pawn.ToStringSafe());
                Patch_WieldWeapon.DoPawnPatch(pawn);
            }
        }
    }
}

