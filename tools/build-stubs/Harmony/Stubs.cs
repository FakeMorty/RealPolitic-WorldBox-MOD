// Заглушка HarmonyLib (0Harmony) — только для компиляции.

using System;
using System.Reflection;

namespace HarmonyLib
{
    public class Harmony
    {
        public Harmony(string id) { }
        // Реально (Harmony 2.9.0): public MethodInfo Patch(MethodBase, HarmonyMethod, HarmonyMethod, HarmonyMethod, HarmonyMethod, HarmonyMethod)
        public MethodInfo Patch(MethodBase original, HarmonyMethod prefix, HarmonyMethod postfix, HarmonyMethod transpiler, HarmonyMethod finalizer, HarmonyMethod ilmanipulator) => null;
        public static void UnpatchID(string id) { }
    }

    public class HarmonyMethod
    {
        public HarmonyMethod(Type type, string methodName, Type[] argumentTypes) { }
        public HarmonyMethod() { }
    }
}
