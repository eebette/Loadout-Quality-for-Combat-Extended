using CombatExtended;
using RimWorld;
using UnityEngine;
using Verse;

namespace LoadoutQuality
{
    /// <summary>
    /// The one Allows() both gates (fetch + drop) and the upgrade scan consult.
    /// Semantics mirror vanilla outfit filters: HP range applies to anything with
    /// hit points, quality range to anything with quality; both default wide open.
    /// Scope is deliberately weapons-only for the quality/drop path — CE loadout
    /// slots for meals/meds/ammo have no quality, and HP-gating consumables would
    /// silently starve pawns.
    /// </summary>
    public static class FilterCore
    {
        public static LQEntry EntryFor(Pawn pawn)
        {
            Loadout loadout = pawn?.GetLoadout();
            if (loadout == null || loadout.defaultLoadout)
            {
                return null;
            }
            return LQGameComponent.Instance?.GetEntry(loadout.uniqueID, create: false);
        }

        public static bool Allows(LQEntry entry, Thing thing)
        {
            if (entry == null || thing == null)
            {
                return true;
            }
            Thing inner = thing.GetInnerIfMinified();
            if (!inner.def.IsWeapon)
            {
                return true;
            }
            if (inner.def.useHitPoints)
            {
                float pct = Mathf.Clamp01(GenMath.RoundedHundredth(
                    inner.HitPoints / (float)inner.MaxHitPoints));
                if (!entry.hitPoints.IncludesEpsilon(pct))
                {
                    return false;
                }
            }
            if (entry.quality != QualityRange.All && inner.def.HasComp(typeof(CompQuality)))
            {
                if (!inner.TryGetQuality(out QualityCategory qc))
                {
                    qc = QualityCategory.Normal;
                }
                if (!entry.quality.Includes(qc))
                {
                    return false;
                }
            }
            return true;
        }

        public static bool Allows(Pawn pawn, Thing thing)
        {
            return Allows(EntryFor(pawn), thing);
        }
    }
}
