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

        private static readonly Dictionary<Thing, List<BayonetPatch>> d = new Dictionary<Thing, List<BayonetPatch>>();

        public class BayonetPatch
        {
            public Verb v;
            public VerbProperties oldProperties;
            public Tool oldTool;
            public ManeuverDef oldManoeuvre;
            public VerbProperties newProperties;
            public Tool newTool;
            public ManeuverDef newManoeuvre;

            public BayonetPatch(Verb v, VerbProperties oldProperties, Tool oldTool, ManeuverDef oldManoeuvre, VerbProperties newProperties,
                Tool newTool, ManeuverDef newManoeuvre)
            {
                this.v = v;
                this.oldProperties = oldProperties;
                this.oldTool = oldTool;
                this.oldManoeuvre = oldManoeuvre;
                this.newProperties = newProperties;
                this.newTool = newTool;
                this.newManoeuvre = newManoeuvre;
            }

            public void Apply()
            {
                v.verbProps = newProperties;
                v.tool = newTool;
                v.maneuver = newManoeuvre;
            }

            public void Revert()
            {
                v.verbProps = oldProperties;
                v.tool = oldTool;
                v.maneuver = oldManoeuvre;
            }

            private static string Coalesce(string s, string i)
            {
                return (s.NullOrEmpty()) ? i : s;
            }

            public override string ToString()
            {
                return "Patch[Verb {0}; Properties {1} -> {2}; Tool {3} -> {4}; Manoeuvre {5} -> {6}]"
                    .Formatted(
                        v.ToStringSafe(),
                        oldProperties.ToStringSafe(),
                        newProperties.ToStringSafe(),
                        oldTool.label,
                        newTool.label,
                        Coalesce(oldManoeuvre.label, oldManoeuvre.defName),
                        Coalesce(newManoeuvre.label, newManoeuvre.defName));
            }
        }

        public static bool Contains(Thing weapon)
        {
            if (weapon == null) { throw new NullReferenceException("null weapon provided to PatchChanges.Get"); }
            return d.ContainsKey(weapon);
        }

        public static void Add(Thing weapon, List<BayonetPatch> changes)
        {
            if (weapon == null)
                throw new NullReferenceException("thing provided to patch cache was null?");
            if (!weapon.def.IsWeapon)
                throw new ArgumentException($"provided thing [{weapon.ToStringSafe()}] is not a weapon!");

            d[weapon] = changes;
        }

        public static List<BayonetPatch> Get(Thing weapon)
        {
            if (weapon == null) { throw new NullReferenceException("null weapon provided to PatchChanges.Get"); }
            return d[weapon];
        }

        public static void ApplyChanges(Thing weapon)
        {
            var l = Get(weapon);
            foreach (var p in l)
                p.Apply();
        }

        public static void RevertChanges(Thing weapon)
        {
            var l = Get(weapon);
            foreach (var p in l)
                p.Revert();
        }

        public static string DataString()
        {
            var lines = d.Select(pair => pair.Key + ": " + pair.Value.ToStringSafeEnumerable());
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
             * 
             * Note that the bayonet belt itself does not have any valid verbs attached directly.
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

                    var patches = new List<PatchCache.BayonetPatch>();
                    foreach (Verb v in primaryMeleeVerbs)
                        if (HasPokeCapacity(v.tool))
                            patches.Add(new PatchCache.BayonetPatch(v, 
                                v.verbProps, v.tool, v.maneuver,
                                Utilities.GetBayonetVerbProperty(), Utilities.GetBayonetTool(), Utilities.GetBayonetManoeuvre()));


                    PatchCache.Add(primaryWeapon, patches);
                    PatchCache.ApplyChanges(primaryWeapon);

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
                    PatchCache.RevertChanges(eq);

                else
                    Mod.LogError("cache miss for weapon " + eq.ToStringSafe());
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_ApparelTracker), nameof(Pawn_ApparelTracker.TryDrop),
         new[] { typeof(Apparel), typeof(Apparel) }, new[] { ArgumentType.Normal, ArgumentType.Ref })]
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
                    PatchCache.RevertChanges(eq);

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

