using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CombatExtended;
using HarmonyLib;
using RimWorld;
using Verse;

namespace LoadoutQuality
{
    /// <summary>
    /// Fetch gate: CE's JobGiver_UpdateLoadout.FindPickup validates candidates with
    /// a compiler-generated `search` lambda (the only one in the method that calls
    /// ForbidUtility.IsForbidden). Its generated name is BRITTLE across CE releases,
    /// so it is resolved dynamically: scan the giver's nested display classes for a
    /// bool(Thing) method whose body references IsForbidden; Prepare() aborts the
    /// patch loudly (rather than silently mis-patching) if the shape ever changes.
    /// The postfix reads the display class's captured `pawn` field and vetoes
    /// candidates the pawn's loadout filter disallows.
    /// </summary>
    [HarmonyPatch]
    public static class FindPickup_Validator_Patch
    {
        private static FieldInfo pawnField;

        public static bool Prepare()
        {
            MethodBase target = TargetMethod();
            if (target == null)
            {
                Log.Error("[LoadoutQuality] Could not locate CE's FindPickup validator lambda — the fetch gate is DISABLED. Report this with your CE version.");
                return false;
            }
            pawnField = AccessTools.Field(target.DeclaringType, "pawn");
            if (pawnField == null)
            {
                Log.Error("[LoadoutQuality] FindPickup display class has no 'pawn' field — the fetch gate is DISABLED. Report this with your CE version.");
                return false;
            }
            return true;
        }

        public static MethodBase TargetMethod()
        {
            MethodInfo isForbidden = AccessTools.Method(typeof(ForbidUtility),
                nameof(ForbidUtility.IsForbidden), new[] { typeof(Thing), typeof(Pawn) });
            foreach (System.Type nested in typeof(JobGiver_UpdateLoadout)
                         .GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
            {
                foreach (MethodInfo method in nested.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    if (method.ReturnType != typeof(bool))
                    {
                        continue;
                    }
                    ParameterInfo[] pars = method.GetParameters();
                    if (pars.Length != 1 || pars[0].ParameterType != typeof(Thing))
                    {
                        continue;
                    }
                    List<CodeInstruction> body;
                    try
                    {
                        body = PatchProcessor.GetOriginalInstructions(method);
                    }
                    catch
                    {
                        continue;
                    }
                    if (body.Any(ci => ci.Calls(isForbidden)))
                    {
                        return method;
                    }
                }
            }
            return null;
        }

        [HarmonyPostfix]
        public static void Postfix(object __instance, Thing t, ref bool __result)
        {
            if (!__result)
            {
                return;
            }
            Pawn pawn = pawnField.GetValue(__instance) as Pawn;
            if (pawn != null && !FilterCore.Allows(pawn, t))
            {
                __result = false;
            }
        }
    }

    /// <summary>
    /// Drop gate: when CE's excess scan found nothing, report a carried weapon the
    /// loadout filter disallows (outfit-filter parity: below-floor gear gets shed
    /// and the fetch gate finds an allowed replacement). Stable public targets —
    /// the same composition the old Extended Loadout mod proved.
    /// </summary>
    [HarmonyPatch(typeof(Utility_HoldTracker), nameof(Utility_HoldTracker.GetExcessEquipment))]
    public static class GetExcessEquipment_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ref ThingWithComps dropEquipment, ref bool __result)
        {
            if (__result)
            {
                return;
            }
            LQEntry entry = FilterCore.EntryFor(pawn);
            if (entry == null)
            {
                return;
            }
            ThingWithComps primary = pawn.equipment?.Primary;
            if (primary != null && primary.def.IsWeapon && !FilterCore.Allows(entry, primary))
            {
                dropEquipment = primary;
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(Utility_HoldTracker), nameof(Utility_HoldTracker.GetExcessThing))]
    public static class GetExcessThing_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ref Thing dropThing, ref int dropCount, ref bool __result)
        {
            if (__result)
            {
                return;
            }
            LQEntry entry = FilterCore.EntryFor(pawn);
            if (entry == null || pawn.inventory?.innerContainer == null)
            {
                return;
            }
            foreach (Thing thing in pawn.inventory.innerContainer)
            {
                Thing inner = thing.GetInnerIfMinified();
                if (inner.def.IsWeapon && !FilterCore.Allows(entry, inner))
                {
                    dropThing = thing;
                    dropCount = 1;
                    __result = true;
                    return;
                }
            }
        }
    }
}
