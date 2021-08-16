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
    internal static class Utilities
    {

        private static Tool bayonetStab;
        internal static Tool GetBayonetTool()
        {
            if (bayonetStab == null)
            {
                List<Tool> l = ThingDef.Named("Apparel_BayonetBelt").tools;
                foreach (var t in l)
                    if (t.capacities.Select(c => c.ToStringSafe()).Contains("Stab"))
                    {
                        bayonetStab = t;
                        return bayonetStab;
                    }

                Mod.LogError("Bayonet belt is misconfigured; no stabbing tool! See tool capacities: "
                    + l.Select(t => t.capacities).SelectMany(i => i).Select(c => c.label));
                throw new IndexOutOfRangeException("Bayonet belt is misconfigured!");
            }

            return bayonetStab;
        }

        // private cache for crap weapon determination
        private static readonly Dictionary<ThingDef, bool> resultCache = new Dictionary<ThingDef, bool>();

        static readonly string[] GOOD_TAGS = new string[] {
            "IndustrialGunAdvanced", "SpacerGun",
            "BayonetGun" // tag for other modders? idk if it works
        };

        static readonly string[] BAYONET_COMPATIBLE = new string[]
        {
            "Gun_BoltActionRifle", "Gun_PumpShotgun"
        };

        static readonly string[] BAYONET_INCOMPATIBLE = new string[]
        {
            "Gun_ChargeLance", "Gun_SniperRifle", "Gun_IncendiaryLauncher", "Gun_EmpLauncher",
            "Gun_SmokeLauncher"
        };

        // Returns true if a bayonet should be theoretically mountable to the weapon
        private static bool __IsNotCrapWeapon(ThingDef weapon)
        {
            if (weapon == null) { throw new NullReferenceException("provided thingdef weapon is null!"); }
            if (GOOD_TAGS == null) { throw new NullReferenceException("weapon GOOD_TAGS are null!"); }
            if (weapon.weaponTags == null) { return false; } // weapon tags == null -> there are no tags
            if (!weapon.IsWeaponUsingProjectiles) { return false; }

            List<string> weaponTags = weapon.weaponTags;
            if (GOOD_TAGS.AsQueryable().Intersect(weaponTags).Any()) { return true; }
            if (weapon.defName.Equals("")) { return true; }
            if (BAYONET_COMPATIBLE.Contains(weapon.defName)) { return true; }
            if (weapon.defName.Equals("Gun_Revolver")) { return true; }  // pritchard bayonet reacts only

            return false;
        }

        // Cached results for determining whether something is a crap weapon
        internal static bool IsNotCrapWeapon(ThingDef weapon)
        {
            if (!resultCache.ContainsKey(weapon))
                resultCache.Add(weapon, __IsNotCrapWeapon(weapon));

            return resultCache[weapon];
        }

        internal static bool IsCrapWeapon(ThingDef weapon)
        {
            return !IsNotCrapWeapon(weapon);
        }

        // Returns the bayonet if the pawn is a valid wielder of the bayonet (ie can use it)
        internal static Thing GetBayonetBeltIfValidWielder(Pawn wielder)
        {
            if (wielder != null && wielder.RaceProps.Humanlike)
            {
                /* if the person is wearing a bayonet belt */
                IEnumerable<Thing> attackApparelList = wielder.apparel.WornApparel;
                Thing theBayonetBelt = attackApparelList.Where(i => i.def.Equals(ThingDef.Named("Apparel_BayonetBelt"))).FirstOrDefault();
                if (theBayonetBelt != null) // is wearing the bayonet belt
                {
                    /* If the pawn is using a gun which is not a crap projectile weapon, then */
                    Thing attackerPrimary = wielder.equipment.Primary;
                    if (attackerPrimary != null && attackerPrimary.def.IsWeaponUsingProjectiles) // is using a projectile weapon
                    {
                        bool weaponIsBayonetMountable = IsNotCrapWeapon(attackerPrimary.def);
                        if (weaponIsBayonetMountable) // the weapon can mount bayonet
                        {
                            return theBayonetBelt;
                        }
                    }
                }
            }

            return null;
        }
    }

    [HarmonyPatch(typeof(VerbProperties), nameof(VerbProperties.AdjustedMeleeDamageAmount),
     new[] { typeof(Tool), typeof(Pawn), typeof(Thing), typeof(HediffComp_VerbGiver) })]
    internal static class Patch_MeleeDamageAmount
    {
        private static readonly bool DEBUGGING_HERE = Mod.DEBUGGING && false;

        private static string ToolCapacitiesToString(Tool t)
        {
            return String.Join(", ", t.capacities.Select(i => i.ToStringSafe()));
        }

        static void Prefix(ref Tool tool, ref Pawn attacker, ref Thing equipment, ref HediffComp_VerbGiver hediffCompSource)
        {
            /* 
             * If using a valid wielder and the wielder is a stabbing type (as defined in the bayonet def),
             * calculate damage as if the tool were the bayonet with the bayonet's equipment state rather than rifle.
             */
            Thing theBayonetBelt = Utilities.GetBayonetBeltIfValidWielder(attacker);
            if (theBayonetBelt != null)
            {
                string originalCapacities = ToolCapacitiesToString(tool);

                /*
                 * Due to verb replacement, in valid bayonet wielders, the weapon should only be using a stab capacity.
                 * However, melee average damage calculations are based on poke capacity which is derived from ThingDef
                 * and is not patched by our overriding the Thing > Verb > Tools.
                 */
                if (Patch_BayonetWield.HasStabCapacity(tool) || Patch_BayonetWield.HasPokeCapacity(tool))
                {
                    equipment = theBayonetBelt; // pass quality from bayonet belt to melee damage
                    tool = Utilities.GetBayonetTool();

                    // this method constantly gets called for some reason, even when paused
                    if (DEBUGGING_HERE)
                        Mod.LogMessage(String.Format(
                            "imputed tool {0} and equipment {1}; orig capacites {2}",
                            tool.ToStringSafe(), equipment.ToStringSafe(), originalCapacities));
                }
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_MeleeVerbs), nameof(Pawn_MeleeVerbs.TryMeleeAttack))]
    internal static class Patch_Pawn_MeleeVerbs_TryMeleeAttack
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
    internal static class Patch_Verb_MeleeAttack
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

    //[HarmonyPatch(typeof(Pawn_MeleeVerbs), "ChooseMeleeVerb")]
    //internal static class Patch_Path_MeleeVerbs_ChooseMeleeVerb
    //{
    //  ...
    //}
}