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
    internal static class PatchCache
    {
        public struct Changes
        {
            public string pawnName;
            public List<(Verb, Tool)> before;
            public List<(Verb, Tool)> after;

            // Token: 0x0600AAA2 RID: 43682 RVA: 0x00395115 File Offset: 0x00393315
            public Changes(string name, List<(Verb, Tool)> before, List<(Verb, Tool)> after)
            {
                this.pawnName = name;
                this.before = before;
                this.after = after;
            }

            public override string ToString()
            {
                return string.Format("PatchChanges[name: {2}; before: {0}; after: {1}]",
                    before.Select(t => String.Format("({0}, {1})", t.Item1.ToStringSafe(), t.Item2.ToStringSafe())).Join(),
                    after.Select(t => String.Format("({0}, {1})", t.Item1.ToStringSafe(), t.Item2.ToStringSafe())).Join(),
                    pawnName
                );
            }
            public static List<(Verb, Tool)> MakeList(List<Verb> l)
            {
                return l.Select(v => (v, v.tool)).ToList();
            }

            public static string TupleToString(List<(Verb, Tool)> tupleList)
            {
                return tupleList.Select(t => String.Format("({0}, {1})", t.Item1.ToStringSafe(), t.Item2.ToStringSafe())).Join();
            }
        }

        private static readonly Dictionary<Thing, Changes> d = new Dictionary<Thing, Changes>();

        public static bool Contains(Thing weapon)
        {
            if (weapon == null) { throw new NullReferenceException("null weapon provided to PatchChanges.Get"); }
            return d.ContainsKey(weapon);
        }

        public static void Add(Thing weapon, Changes changes)
        {
            if (weapon == null)
                throw new NullReferenceException("thing provided to patch cache was null?");
            if (!weapon.def.IsWeapon)
                throw new ArgumentException("provided thing [" + weapon.ToStringSafe() + "]is not a weapon!");

            d[weapon] = changes;
        }

        public static Changes Get(Thing weapon)
        {
            if (weapon == null) { throw new NullReferenceException("null weapon provided to PatchChanges.Get"); }
            return d[weapon];
        }

        public static string DataString() {
            var lines = d.Select(pair => pair.Key + ": " + pair.Value.ToString());
            return "PatchChanges[\n" + string.Join("\n", lines) + "\n]"; 
        }
    }

    [HarmonyPatch]
    internal static class Patch_BayonetWield
    {
        private static readonly bool DEBUGGING_HERE = Mod.DEBUGGING && false;
        private static readonly ToolCapacityDef POKE_CAPACITY = DefDatabase<ToolCapacityDef>.GetNamed("Poke");
        private static readonly ToolCapacityDef STAB_CAPACITY = DefDatabase<ToolCapacityDef>.GetNamed("Stab");

        internal static bool HasPokeCapacity(Tool tool)
        {
            return tool.capacities.Contains(Patch_BayonetWield.POKE_CAPACITY);
        }
        internal static bool HasStabCapacity(Tool tool)
        {
            return tool.capacities.Contains(Patch_BayonetWield.STAB_CAPACITY);
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

                    List<(Verb, Tool)> oldSet = PatchCache.Changes.MakeList(primaryMeleeVerbs);
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

                    // do caching
                    PatchCache.Add(primaryWeapon, new PatchCache.Changes(
                        pawn.Name.ToStringFull, 
                        oldSet, PatchCache.Changes.MakeList(primaryMeleeVerbs)));
                    if (DEBUGGING_HERE)
                        Mod.LogMessage("created patch cache for weapon {0} stashing changes {1}".Formatted(
                            primaryWeapon.ToStringSafe(), 
                            PatchCache.Get(primaryWeapon).ToString()));
                }
                else { Mod.LogError("Pawn weapon no components or verbs??"); }
            }
        }

        // Patching the equipment tracker's thing verbs means that it will give the right names in the combat log.
        // See Patch_MeleeDamageCalc for the patching to impute BAYONET damage rather than RIFLE damage.
        [HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.AddEquipment))]
        [HarmonyPostfix]
        internal static void Postfix_PawnEquipmentTracker(ref Pawn_EquipmentTracker __instance, ThingWithComps newEq)
        {
            if (newEq != null && newEq.def.IsWeaponUsingProjectiles)
                DoPawnPatch(__instance.pawn);
        }

        [HarmonyPatch(typeof(Pawn_ApparelTracker), nameof(Pawn_ApparelTracker.Wear))]
        [HarmonyPostfix]
        internal static void Postfix_PawnApparelTracker(ref Pawn_ApparelTracker __instance, Apparel newApparel)
        {
            if (newApparel != null && newApparel.def.Equals(ThingDef.Named("Apparel_BayonetBelt")))
                DoPawnPatch(__instance.pawn);
        }
    }

    // only one try drop equipment in the class
    [HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.TryDropEquipment))]
    internal static class Patch_RemoveWeapon
    {
        private static bool DEBUGGING_HERE = Mod.DEBUGGING && false;
        internal static void Prefix(ref Pawn_EquipmentTracker __instance, out bool __state, 
            ref ThingWithComps eq, ref ThingWithComps resultingEq)
        {
            if (DEBUGGING_HERE)
                Mod.LogMessage("Removing weapon prefix triggered");
            
            Pawn pawn = __instance.pawn;
            if (pawn == null)
            {
                __state = false;
                return;
            }

            var bayonetUser = (Utilities.GetBayonetBeltIfValidWielder(pawn) != null);
            if (DEBUGGING_HERE)
                Mod.LogMessage("remove equipment called on thing {0}, bayonet user == {1}"
                    .Formatted(eq.ToStringSafe(), bayonetUser.ToString()));

            __state = bayonetUser;
        }

        internal static void Postfix(ref Pawn_EquipmentTracker __instance, bool __state, 
            ref ThingWithComps eq, ref ThingWithComps resultingEq)
        {
            Pawn pawn = __instance.pawn;
            if (pawn == null) return;

            Thing nullableBayonetBelt = Utilities.GetBayonetBeltIfValidWielder(pawn);
            if (__state != (nullableBayonetBelt != null)) // bayonet is no longer valid; go and reset the weapon
            {
                if (DEBUGGING_HERE)
                    Mod.LogMessage("reverting changes to bayonetted weapon");

                if (PatchCache.Contains(eq))
                {
                    var eqChanges = PatchCache.Get(eq);
                    for (int i = 0; i < eqChanges.after.Count(); i++)
                    {
                        var verb = eqChanges.after[i].Item1;
                        verb.tool = eqChanges.before[i].Item2;
                    }

                    var reqChanges = PatchCache.Get(resultingEq);
                    for (int i = 0; i < reqChanges.after.Count(); i++)
                    {
                        var verb = reqChanges.after[i].Item1;
                        verb.tool = reqChanges.before[i].Item2;
                    }
                }
                else
                    Mod.LogError("cache miss for weapon " + eq.ToStringSafe());
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_ApparelTracker), nameof(Pawn_ApparelTracker.TryDrop),
         new[] { typeof(Apparel), typeof(Apparel) }, new[] {ArgumentType.Normal, ArgumentType.Ref })]
    internal static class Patch_RemoveBelt
    {
        private static bool DEBUGGING_HERE = Mod.DEBUGGING && false;

        internal static void Prefix(ref Pawn_EquipmentTracker __instance, out bool __state, 
            ref Apparel ap)
        {
            Pawn pawn = __instance.pawn;
            if (pawn == null)
            {
                __state = false;
                return;
            }

            var bayonetUser = (Utilities.GetBayonetBeltIfValidWielder(pawn) != null);
            if (DEBUGGING_HERE)
                Mod.LogMessage("remove equipment called on thing {0}, bayonet user == {1}"
                    .Formatted(ap.ToStringSafe(), bayonetUser.ToString()));

            __state = bayonetUser;
        }

        internal static void Postfix(ref Pawn_EquipmentTracker __instance, bool __state)
        {
            Pawn pawn = __instance.pawn;
            if (pawn == null) return;

            Thing nullableBayonetBelt = Utilities.GetBayonetBeltIfValidWielder(pawn);
            if (__state != (nullableBayonetBelt != null)) // bayonet is no longer valid; go and reset the weapon
            {
                if (DEBUGGING_HERE)
                    Mod.LogMessage("reverting changes to bayonetted weapon");

                ThingWithComps eq = pawn.equipment.Primary;
                if (PatchCache.Contains(eq))
                {
                    var changes = PatchCache.Get(eq);
                    for (int i = 0; i < changes.after.Count(); i++)
                    {
                        var verb = changes.after[i].Item1;
                        verb.tool = changes.before[i].Item2;
                    }
                }
                else
                    Mod.LogError("cache miss for weapon " + eq.ToStringSafe());
            }
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
                    Mod.LogMessage("init-patching pawn " + pawn.ToStringSafe());

                Patch_BayonetWield.DoPawnPatch(pawn);
            }

            
            Mod.LogMessage("init with patch cache: " + PatchCache.DataString());
        }
    }
}

