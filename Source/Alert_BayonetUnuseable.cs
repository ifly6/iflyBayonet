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
    public class Alert_BayonetUnuseable : Alert
    {
        private List<Pawn> unuseableBayonetPawnList = new List<Pawn>();

        private readonly ThingDef BAYONET_BELT = ThingDef.Named("Apparel_BayonetBelt");
        private readonly string[] LIST_VALID_WEAPONS = DefDatabase<ThingDef>.AllDefs
            .Where(i => i != null)
            .Where(i => i.IsWeapon)
            .Where(i => Utilities.IsNotCrapWeapon(i))
            .Select(i => i.label).ToArray();

        private List<Pawn> UnuseableBayonetPawns
        {
            get
            {
                this.unuseableBayonetPawnList.Clear();
                foreach (Pawn pawn in PawnsFinder.AllMaps_FreeColonistsSpawned)
                {
                    ThingWithComps primaryWeapon = pawn.equipment.Primary;
                    if (primaryWeapon != null && Utilities.IsCrapWeapon(primaryWeapon.def))
                    {
                        List<Apparel> wornApparel = pawn.apparel.WornApparel;
                        for (int i = 0; i < wornApparel.Count; i++)
                        {
                            if (wornApparel[i].def.Equals(BAYONET_BELT))
                            {
                                this.unuseableBayonetPawnList.Add(pawn);
                                break;
                            }
                        }
                    }
                }
                return this.unuseableBayonetPawnList;
            }
        }

        public Alert_BayonetUnuseable()
        {
            this.defaultLabel = "Bayonet user does not have mountable weapon";
            this.defaultExplanation = "Bayonets can only be mounted on certain weapons. They can only be used with ranged weapons with lugs: "
                + String.Join(", ", LIST_VALID_WEAPONS);
        }

        public override AlertReport GetReport()
        {
            return AlertReport.CulpritsAre(this.UnuseableBayonetPawns);
        }
    }
}

