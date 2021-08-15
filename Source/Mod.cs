using HarmonyLib;
using System;
using System.Reflection;
using Verse;

namespace Bayonet
{
    [StaticConstructorOnStartup]
    internal static class Mod
    {
        internal static readonly bool DEBUGGING = true;

        static readonly string VERSION = "v0.1";
        static readonly string NAME = "ifly Bayonet";
        static readonly string LOG_PREFIX = "[" + NAME + " " + VERSION + "] ";

        static Mod()
        {
            Harmony harmony = new Harmony("com.github.ifly6.RimworldBayonet");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            Mod.LogMessage("ifly Bayonet initalised");
        }

        internal static void LogMessage(string s) {
            Log.Message(LOG_PREFIX + s);
        }

        internal static void LogWarning(string s)
        {
            Log.Warning(LOG_PREFIX + s);
        }

        internal static void LogError(string s)
        {
            Log.Error(LOG_PREFIX + s);
        }
    }
}