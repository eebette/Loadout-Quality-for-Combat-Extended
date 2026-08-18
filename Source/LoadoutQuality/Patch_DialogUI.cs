using CombatExtended;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LoadoutQuality
{
    /// <summary>
    /// Quality + hit-point range widgets for the selected loadout, drawn as an
    /// extra strip below CE's loadout dialog (the vanilla widgets players know
    /// from outfit dialogs). The dialog itself is untouched — the window grows and
    /// the strip renders in the gained space. Entries prune back to nothing when
    /// reset to wide open, keeping saves clean.
    /// </summary>
    [HarmonyPatch(typeof(Dialog_ManageLoadouts), nameof(Dialog_ManageLoadouts.DoWindowContents))]
    public static class Dialog_ManageLoadouts_DoWindowContents_Patch
    {
        public const float StripHeight = 60f;

        [HarmonyPostfix]
        public static void Postfix(Dialog_ManageLoadouts __instance, Rect canvas)
        {
            Loadout loadout = __instance.CurrentLoadout;
            if (loadout == null || loadout.defaultLoadout || LQGameComponent.Instance == null)
            {
                return;
            }
            LQEntry existing = LQGameComponent.Instance.GetEntry(loadout.uniqueID, create: false);
            LQEntry entry = existing ?? new LQEntry();

            float y = canvas.height - StripHeight + 4f;
            float half = (canvas.width - 16f) / 2f;
            Rect qualityRect = new Rect(0f, y, half, 26f);
            Rect hpRect = new Rect(half + 16f, y, half, 26f);
            Rect labelRect = new Rect(0f, y + 28f, canvas.width, 22f);

            Widgets.QualityRange(qualityRect, 189440612 ^ loadout.uniqueID, ref entry.quality);
            Widgets.FloatRange(hpRect, 720358401 ^ loadout.uniqueID, ref entry.hitPoints,
                0f, 1f, "HitPoints", ToStringStyle.PercentZero);
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            Widgets.Label(labelRect,
                "LQ_QualityLabel".Translate() + " / " + "LQ_HitPointsLabel".Translate()
                + " — weapons outside these ranges are not fetched and are dropped.");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            if (!entry.IsDefault && existing == null)
            {
                // First narrowing: persist the entry.
                LQGameComponent.Instance.GetEntry(loadout.uniqueID, create: true).quality = entry.quality;
                LQGameComponent.Instance.GetEntry(loadout.uniqueID, create: true).hitPoints = entry.hitPoints;
            }
            else if (existing != null && entry.IsDefault)
            {
                LQGameComponent.Instance.PruneDefault(loadout.uniqueID);
            }
        }
    }

    /// <summary>Grow the dialog window to make room for the strip.</summary>
    [HarmonyPatch(typeof(Dialog_ManageLoadouts), nameof(Dialog_ManageLoadouts.InitialSize), MethodType.Getter)]
    public static class Dialog_ManageLoadouts_InitialSize_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ref Vector2 __result)
        {
            __result.y += Dialog_ManageLoadouts_DoWindowContents_Patch.StripHeight;
        }
    }
}
