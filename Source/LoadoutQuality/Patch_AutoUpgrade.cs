using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CombatExtended;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace LoadoutQuality
{
    /// <summary>
    /// Auto weapon upgrade. No parallel scheduler: postfix on CE's own
    /// GetUpdateLoadoutJob — when CE has no loadout work for the pawn, scan the
    /// loadout-declared weapons for a strictly higher-quality copy of the SAME def
    /// AND SAME material on the map (no cross-material quality comparison), passing
    /// the loadout's quality/HP filter, and return a swap job. Free-rides CE's
    /// 1800-tick throttle, think priority, and the Assign tab's update-now button.
    /// </summary>
    [HarmonyPatch(typeof(JobGiver_UpdateLoadout), nameof(JobGiver_UpdateLoadout.GetUpdateLoadoutJob))]
    public static class GetUpdateLoadoutJob_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ref Job __result)
        {
            if (__result != null || !LoadoutQualityMod.Settings.autoUpgrade)
            {
                return;
            }
            if (pawn == null || !pawn.IsColonist || pawn.Downed || pawn.Drafted || pawn.Map == null)
            {
                return;
            }
            Loadout loadout = pawn.GetLoadout();
            if (loadout == null || loadout.defaultLoadout)
            {
                return;
            }
            LQEntry entry = LQGameComponent.Instance?.GetEntry(loadout.uniqueID, create: false);

            foreach (LoadoutSlot slot in loadout.Slots)
            {
                if (slot.thingDef == null || !slot.thingDef.IsWeapon)
                {
                    continue;
                }
                ThingWithComps carried = CarriedInstance(pawn, slot.thingDef);
                if (carried == null || !carried.TryGetQuality(out QualityCategory currentQuality))
                {
                    continue;
                }
                Thing best = null;
                QualityCategory bestQuality = currentQuality;
                foreach (Thing candidate in pawn.Map.listerThings.ThingsOfDef(slot.thingDef))
                {
                    if (!candidate.Spawned || candidate.IsForbidden(pawn) || candidate.IsBurning())
                    {
                        continue;
                    }
                    if (candidate.Stuff != carried.Stuff)
                    {
                        continue; // same material only — apples to apples
                    }
                    if (!candidate.TryGetQuality(out QualityCategory q) || q <= bestQuality)
                    {
                        continue;
                    }
                    if (!FilterCore.Allows(entry, candidate))
                    {
                        continue;
                    }
                    if (!pawn.CanReserveAndReach(candidate, PathEndMode.ClosestTouch, Danger.None))
                    {
                        continue;
                    }
                    best = candidate;
                    bestQuality = q;
                }
                if (best != null)
                {
                    Job job = JobMaker.MakeJob(LQDefOf.LQ_SwapWeapon, best, carried);
                    __result = job;
                    return;
                }
            }
        }

        private static ThingWithComps CarriedInstance(Pawn pawn, ThingDef def)
        {
            if (pawn.equipment?.Primary?.def == def)
            {
                return pawn.equipment.Primary;
            }
            return pawn.inventory?.innerContainer?.OfType<ThingWithComps>()
                .FirstOrDefault(t => t.def == def);
        }
    }

    [DefOf]
    public static class LQDefOf
    {
        public static JobDef LQ_SwapWeapon;

        static LQDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(LQDefOf));
        }
    }

    /// <summary>
    /// TargetA = better weapon on the map, TargetB = carried weapon it replaces.
    /// Reservation (not map-wide forbid hacks) prevents pickup races; the old
    /// weapon drops IN PLACE, UNFORBIDDEN — vanilla JobDriver_OptimizeApparel
    /// convention: haulers store it, other pawns may legitimately claim it.
    /// Simple Sidearms, when present, is informed via reflection so its memory
    /// tracks the new instance.
    /// </summary>
    public class JobDriver_SwapWeapon : JobDriver
    {
        private Thing NewWeapon => job.targetA.Thing;
        private Thing OldWeapon => job.targetB.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(NewWeapon, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOnBurningImmobile(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch)
                .FailOnDespawnedNullOrForbidden(TargetIndex.A);
            Toil swap = ToilMaker.MakeToil("LQ_Swap");
            swap.initAction = () =>
            {
                var newWeapon = (ThingWithComps)NewWeapon;
                var oldWeapon = OldWeapon as ThingWithComps;
                if (newWeapon == null || newWeapon.Destroyed)
                {
                    return;
                }
                bool wasEquipped = pawn.equipment?.Primary == oldWeapon;
                if (oldWeapon != null && !oldWeapon.Destroyed)
                {
                    Thing dropped = null;
                    if (wasEquipped)
                    {
                        pawn.equipment.TryDropEquipment(oldWeapon, out ThingWithComps droppedEq, pawn.Position, forbid: false);
                        dropped = droppedEq;
                    }
                    else if (pawn.inventory.innerContainer.Contains(oldWeapon))
                    {
                        pawn.inventory.innerContainer.TryDrop(oldWeapon, pawn.Position, pawn.Map,
                            ThingPlaceMode.Near, out dropped);
                    }
                    // Other mods (e.g. Simple Sidearms' drop handling) may forbid
                    // dropped weapons; the replaced copy is a hand-me-down for
                    // haulers and other pawns — explicitly leave it claimable.
                    dropped?.SetForbidden(false, warnOnFail: false);
                }
                if (newWeapon.Spawned)
                {
                    newWeapon.DeSpawn();
                }
                if (wasEquipped)
                {
                    pawn.equipment.AddEquipment(newWeapon);
                }
                else
                {
                    pawn.inventory.innerContainer.TryAdd(newWeapon, true);
                }
                pawn.TryGetComp<CompInventory>()?.UpdateInventory();
                SidearmsBridge.NotifySwap(pawn, oldWeapon, newWeapon);
            };
            swap.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return swap;
        }
    }

    /// <summary>Soft Simple Sidearms integration — reflection only, no reference.</summary>
    public static class SidearmsBridge
    {
        private static bool initialized;
        private static MethodInfo getMemory;
        private static MethodInfo informAdded;

        public static void NotifySwap(Pawn pawn, Thing oldWeapon, Thing newWeapon)
        {
            if (!initialized)
            {
                initialized = true;
                Type memoryType = GenTypes.GetTypeInAnyAssembly("SimpleSidearms.rimworld.CompSidearmMemory");
                if (memoryType != null)
                {
                    getMemory = memoryType.GetMethod("GetMemoryCompForPawn",
                        BindingFlags.Public | BindingFlags.Static);
                    informAdded = memoryType.GetMethod("InformOfAddedSidearm",
                        BindingFlags.Public | BindingFlags.Instance);
                }
            }
            if (getMemory == null || informAdded == null || newWeapon == null)
            {
                return;
            }
            try
            {
                object memory = getMemory.Invoke(null, new object[] { pawn, true });
                if (memory != null)
                {
                    informAdded.Invoke(memory, new object[] { newWeapon });
                }
            }
            catch
            {
                // SS absent or API drifted — soft integration stays soft.
            }
        }
    }
}
