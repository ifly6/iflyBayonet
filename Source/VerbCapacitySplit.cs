using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using RimWorld;
using Verse;

namespace Bayonet
{
    internal static class VerbCapacitySplit
    {

        private static readonly bool DEBUGGING_HERE = Mod.DEBUGGING && true;

        /*
         * Take list of verbs which looks like this:
         * 
         *  [ Verb  Tool    Capacities    ]
         *  [ --------------------------- ]
         *  [ V1    Stock   [Blunt]       ]
         *  [ V2    Barrel  [Blunt, Poke] ]
         *  [ V3    Barrel  [Blunt, Poke] ]
         * 
         * And turn it into a table (same columns) like this:
         * 
         *  [ V1  Stock   [Blunt] ]
         *  [ V2  Barrel  [Blunt] ]
         *  [ V3  Barrel  [Poke]  ]
         */
        public static void Reassign(ref List<Verb> verbList)
        {
            Dictionary<Tool, List<Verb>> toolGroups = new Dictionary<Tool, List<Verb>>();
            foreach (Verb v in verbList)
                if (toolGroups.ContainsKey(v.tool))
                    toolGroups[v.tool].Add(v);
                else
                {
                    var newList = new List<Verb> { v };
                    toolGroups.Add(v.tool, newList);
                }

            foreach (KeyValuePair<Tool, List<Verb>> entry in toolGroups)
            {
                List<Verb> _verbList = entry.Value;
                                int numVerbs = _verbList.Count();
                if (_verbList.Select(v => v.tool.capacities.Count()).All(i => i == numVerbs))
                {
                    List<ToolCapacityDef> capacityDefs = _verbList[0].tool.capacities;
                    if (DEBUGGING_HERE)
                        Mod.LogMessage("capacities: " + capacityDefs.ToStringSafeEnumerable());

                    for (int i = 0; i < _verbList.Count(); i++)
                    {
                        var singletonList = new List<ToolCapacityDef> { capacityDefs[i] };
                        _verbList[i].tool.capacities = singletonList;
                    }
                }
            }

            // all of this acts in place, so it should work without complex reassignments
        }
    }
}
