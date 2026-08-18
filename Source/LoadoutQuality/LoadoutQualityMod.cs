using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LoadoutQuality
{
    public class LQSettings : ModSettings
    {
        // Install-as-consent: upgrade ships ON. The filter needs no toggle — its
        // ranges default wide open, so it is inert until a loadout narrows them.
        public bool autoUpgrade = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref autoUpgrade, "autoUpgrade", true);
        }
    }

    public class LoadoutQualityMod : Mod
    {
        public static LQSettings Settings { get; private set; }

        public LoadoutQualityMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<LQSettings>();
        }

        public override string SettingsCategory()
        {
            return "Loadout Quality";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled("Auto weapon upgrade", ref Settings.autoUpgrade,
                "Pawns swap loadout weapons for a strictly higher-quality copy of the same weapon and material when one is available on the map. The replaced weapon is dropped in place, unforbidden. Quality/hit-point floors are set per loadout in the loadout dialog and need no global switch.");
            listing.End();
        }
    }

    /// <summary>Per-loadout quality/HP ranges, keyed by Loadout.uniqueID. A loadout
    /// with wide-open ranges has no entry — absent means inert.</summary>
    public class LQEntry : IExposable
    {
        public QualityRange quality = QualityRange.All;
        public FloatRange hitPoints = FloatRange.ZeroToOne;

        public bool IsDefault => quality == QualityRange.All
                                 && hitPoints.min <= 0f && hitPoints.max >= 1f;

        public void ExposeData()
        {
            Scribe_Values.Look(ref quality, "quality", QualityRange.All);
            Scribe_Values.Look(ref hitPoints, "hitPoints", FloatRange.ZeroToOne);
        }
    }

    public class LQGameComponent : GameComponent
    {
        public static LQGameComponent Instance { get; private set; }

        private Dictionary<int, LQEntry> entries = new Dictionary<int, LQEntry>();
        private List<int> scribeKeys;
        private List<LQEntry> scribeVals;

        public LQGameComponent(Game game)
        {
            Instance = this;
        }

        public LQEntry GetEntry(int loadoutId, bool create)
        {
            if (entries.TryGetValue(loadoutId, out LQEntry entry))
            {
                return entry;
            }
            if (!create)
            {
                return null;
            }
            entry = new LQEntry();
            entries[loadoutId] = entry;
            return entry;
        }

        public void PruneDefault(int loadoutId)
        {
            if (entries.TryGetValue(loadoutId, out LQEntry entry) && entry.IsDefault)
            {
                entries.Remove(loadoutId);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                foreach (int id in new List<int>(entries.Keys))
                {
                    PruneDefault(id);
                }
            }
            Scribe_Collections.Look(ref entries, "entries", LookMode.Value, LookMode.Deep,
                ref scribeKeys, ref scribeVals);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && entries == null)
            {
                entries = new Dictionary<int, LQEntry>();
            }
        }
    }

    [StaticConstructorOnStartup]
    public static class Bootstrap
    {
        static Bootstrap()
        {
            new Harmony("eebette.CELoadoutQuality").PatchAll(typeof(Bootstrap).Assembly);
            Log.Message("[LoadoutQuality] Patches installed.");
        }
    }
}
