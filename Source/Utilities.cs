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
    internal static class Utilities
    {

        private static readonly bool DEBUGGING_HERE = Mod.DEBUGGING && true;

        internal static VerbProperties GetBayonetVerbProperty()
        {
            IEnumerable<VerbProperties> verbProps = ThingDef.Named("Apparel_BayonetBelt").tools
                .Select(i => i.Maneuvers)
                .SelectMany(i => i)
                .Select(i => i.verb);

            if (DEBUGGING_HERE)
                Mod.LogMessage("found verb properties: " + verbProps.ToStringSafeEnumerable());

            return verbProps.First();
        }

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

        private static ManeuverDef bayonetManoeuvreDef;
        internal static ManeuverDef GetBayonetManoeuvre()
        {
            try
            {
                if (bayonetManoeuvreDef == null)
                {
                    bayonetManoeuvreDef = Utilities.GetBayonetTool().Maneuvers
                        .Where((ManeuverDef i) => i.defName.ToLower().Equals("stab"))
                        .FirstOrDefault();
                }
                return bayonetManoeuvreDef;
            }
            catch (Exception)
            {
                Mod.LogError("Bayonet improperly configured without stab manoeuvre! "
                    + "Manoeuvres are [{0}]".Formatted(
                        Utilities.GetBayonetTool().Maneuvers.Join()));
                return null;
            }
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
}
