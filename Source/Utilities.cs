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
        internal static VerbProperties GetBayonetVerbProperty()
        {
            IEnumerable<VerbProperties> verbProps = ThingDef.Named("Apparel_BayonetBelt").tools
                .Select(i => i.Maneuvers)
                .SelectMany(i => i)
                .Select(i => i.verb);

            if (Mod.DEBUGGING)
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
                String s = String.Join(", ", Utilities.GetBayonetTool().Maneuvers);
                Mod.LogError("Bayonet improperly configured without stab manoeuvre! "
                    + "Valid manoeuvres are [{0}]".Formatted(s));
                return null;
            }
        }

        // private cache for crap weapon determination
        private static readonly Dictionary<ThingDef, bool> resultCache = new Dictionary<ThingDef, bool>();

        static readonly string[] GOOD_TAGS = new string[] {
            "IndustrialGunAdvanced", "SpacerGun",
            "BayonetGun" // tag for other modders (apparently works)
        };

        static readonly string[] BAYONET_COMPATIBLE = new string[]
        {
            "Gun_BoltActionRifle", "Gun_PumpShotgun",
            "Gun_Revolver" // pritchard bayonet reacts only
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
            if (weapon.defName == null) { throw new NullReferenceException("weapon defname is null!"); }
            
            if (weapon.weaponTags == null) { return false; } // weapon tags == null -> there are no tags
            if (!weapon.IsWeaponUsingProjectiles) { return false; }

            if (GOOD_TAGS.AsQueryable().Intersect(weapon.weaponTags).Any()) { return true; } // identify by tag
            if (BAYONET_COMPATIBLE.Contains(weapon.defName)) { return true; } // identify on these names always

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
