using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CombatExtended;
using LoadoutQuality;
using RimWorld;
using Verse;
using Verse.AI;

namespace LQTestStaging
{
    /// <summary>
    /// Stages QUAL saves (-quicktest -lqstage):
    ///  QUAL-1-filter: colonist "Filty", empty-handed, loadout with one rifle slot;
    ///    AWFUL rifle near, EXCELLENT rifle farther (both plain steel-less defs).
    ///  QUAL-2-upgrade: colonist "Uppy" with NORMAL rifle equipped + steel gladius
    ///    sidearm, loadout declaring both; EXCELLENT rifle and EXCELLENT PLASTEEL
    ///    gladius on the ground (the plasteel one must NOT tempt the same-stuff rule).
    /// </summary>
    public class LQStagingComponent : GameComponent
    {
        private readonly List<Thing> staged = new List<Thing>();
        private readonly List<Loadout> stagedLoadouts = new List<Loadout>();
        private IntVec3 anchor = IntVec3.Invalid;

        public LQStagingComponent(Game game)
        {
        }

        public override void StartedNewGame()
        {
            if (!GenCommandLine.CommandLineArgPassed("lqstage"))
            {
                return;
            }
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                try
                {
                    StageAll();
                }
                catch (Exception e)
                {
                    Log.Error("[LQStaging] Staging failed: " + e);
                }
            });
        }

        private void StageAll()
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                Log.Error("[LQStaging] No map; launch with -quicktest -lqstage.");
                return;
            }
            anchor = ComputeAnchor(map);
            Log.Message($"[LQStaging] anchor {anchor}.");
            // Home area over the whole scene: without it, vanilla auto-forbids
            // anything pawns drop (outside-home-area convention), which would
            // false-fail the dropped-unforbidden contract that only exists to
            // hold inside a colony's home area.
            foreach (IntVec3 c in GenRadial.RadialCellsAround(anchor, 25f, useCenter: true))
            {
                if (c.InBounds(map))
                {
                    map.areaManager.Home[c] = true;
                }
            }

            Stage1(map);
            SaveAndReset("QUAL-1-filter");
            Stage2(map);
            SaveAndReset("QUAL-2-upgrade");

            Find.TickManager.Pause();
            Log.Message("[LQStaging] All QUAL saves created.");
            Find.LetterStack.ReceiveLetter("QUAL saves created",
                "QUAL-1-filter, QUAL-2-upgrade written.", LetterDefOf.PositiveEvent);
        }

        private void Stage1(Map map)
        {
            Pawn pawn = SpawnColonist(map, "Filty", new IntVec3(-4, 0, 0));
            ThingDef rifle = ThingDef.Named("Gun_BoltActionRifle");
            SpawnWithQuality(map, rifle, null, QualityCategory.Awful, anchor + new IntVec3(3, 0, 2));
            SpawnWithQuality(map, rifle, null, QualityCategory.Excellent, anchor + new IntVec3(10, 0, 6));
            SpawnAmmoFor(map, rifle);

            var loadout = new Loadout("QUAL filter test");
            loadout.AddSlot(new LoadoutSlot(rifle, 1));
            LoadoutManager.AddLoadout(loadout);
            stagedLoadouts.Add(loadout);
            pawn.SetLoadout(loadout);
        }

        private void Stage2(Map map)
        {
            Pawn pawn = SpawnColonist(map, "Uppy", new IntVec3(4, 0, 0));
            ThingDef rifle = ThingDef.Named("Gun_BoltActionRifle");
            ThingDef gladius = ThingDef.Named("MeleeWeapon_Gladius");

            ThingWithComps carriedRifle = MakeWithQuality(rifle, null, QualityCategory.Normal);
            pawn.equipment.AddEquipment(carriedRifle);
            LoadMag(pawn, carriedRifle);
            ThingWithComps carriedGladius = MakeWithQuality(gladius, ThingDefOf.Steel, QualityCategory.Normal);
            pawn.inventory.innerContainer.TryAdd(carriedGladius, true);

            SpawnWithQuality(map, rifle, null, QualityCategory.Excellent, anchor + new IntVec3(8, 0, 4));
            SpawnWithQuality(map, gladius, ThingDefOf.Plasteel, QualityCategory.Excellent, anchor + new IntVec3(8, 0, -4));
            SpawnAmmoFor(map, rifle);

            var loadout = new Loadout("QUAL upgrade test");
            loadout.AddSlot(new LoadoutSlot(rifle, 1));
            loadout.AddSlot(new LoadoutSlot(gladius, 1));
            LoadoutManager.AddLoadout(loadout);
            stagedLoadouts.Add(loadout);
            pawn.SetLoadout(loadout);
        }

        // ---- helpers -------------------------------------------------------

        private ThingWithComps MakeWithQuality(ThingDef def, ThingDef stuff, QualityCategory quality)
        {
            var thing = (ThingWithComps)ThingMaker.MakeThing(def, stuff ?? (def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null));
            thing.TryGetComp<CompQuality>()?.SetQuality(quality, ArtGenerationContext.Colony);
            return thing;
        }

        private void SpawnWithQuality(Map map, ThingDef def, ThingDef stuff, QualityCategory quality, IntVec3 near)
        {
            ThingWithComps thing = MakeWithQuality(def, stuff, quality);
            GenSpawn.Spawn(thing, FindCell(map, near), map);
            staged.Add(thing);
        }

        private void SpawnAmmoFor(Map map, ThingDef weapon)
        {
            AmmoDef ammo = weapon.GetCompProperties<CompProperties_AmmoUser>()?.ammoSet?.ammoTypes?.FirstOrDefault()?.ammo;
            if (ammo == null)
            {
                return;
            }
            Thing stack = ThingMaker.MakeThing(ammo);
            stack.stackCount = 120;
            GenSpawn.Spawn(stack, FindCell(map, anchor + new IntVec3(2, 0, -2)), map);
            staged.Add(stack);
        }

        private void LoadMag(Pawn pawn, ThingWithComps weapon)
        {
            CompAmmoUser user = weapon.TryGetComp<CompAmmoUser>();
            if (user != null && user.UseAmmo)
            {
                user.ResetAmmoCount();
            }
        }

        private void SaveAndReset(string name)
        {
            GameDataSaveLoader.SaveGame(name);
            foreach (Thing thing in staged)
            {
                if (thing is Pawn pawn)
                {
                    LoadoutManager._current?._assignedLoadouts?.Remove(pawn);
                    LoadoutManager._current?._assignedTrackers?.Remove(pawn);
                }
                if (thing != null && !thing.Destroyed)
                {
                    thing.Destroy(DestroyMode.Vanish);
                }
            }
            staged.Clear();
            foreach (Loadout loadout in stagedLoadouts)
            {
                LoadoutManager._current?._loadouts?.Remove(loadout);
            }
            stagedLoadouts.Clear();
        }

        private static IntVec3 ComputeAnchor(Map map)
        {
            bool Valid(IntVec3 c) => c.Standable(map) && !c.Fogged(map);
            if (CellFinder.TryFindRandomCellNear(map.Center, map, 30, Valid, out IntVec3 cell))
            {
                return cell;
            }
            CellFinderLoose.TryGetRandomCellWith(Valid, map, 1000, out cell);
            return cell.IsValid ? cell : map.Center;
        }

        private IntVec3 FindCell(Map map, IntVec3 near)
        {
            IntVec3 root = near.ClampInsideMap(map);
            if (CellFinder.TryFindRandomCellNear(root, map, 15, c => c.Standable(map) && !c.Fogged(map), out IntVec3 cell))
            {
                return cell;
            }
            return anchor;
        }

        private Pawn SpawnColonist(Map map, string nick, IntVec3 offset)
        {
            var request = new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer,
                          PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true,
                          canGeneratePawnRelations: false, colonistRelationChanceFactor: 0f);
            Pawn pawn = PawnGenerator.GeneratePawn(request);
            pawn.Name = new NameTriple("Test", nick, "QUAL");
            pawn.equipment?.DestroyAllEquipment();
            pawn.inventory?.DestroyAll();
            GenSpawn.Spawn(pawn, FindCell(map, anchor + offset), map);
            staged.Add(pawn);
            return pawn;
        }
    }

    [StaticConstructorOnStartup]
    public static class LQTestBoot
    {
        static LQTestBoot()
        {
            Log.Message("[LQStaging] assembly loaded.");
            if (!GenCommandLine.TryGetCommandLineArg("ceassert", out string scenario)
                || scenario.NullOrEmpty() || !scenario.StartsWith("qual"))
            {
                return;
            }
            // Kill the default-ON upgrade BEFORE any save can load and tick — the
            // LoadedGame-callback reset loses a race against the think tree on a
            // save that loads unpaused (each scenario re-enables explicitly).
            LoadoutQuality.LoadoutQualityMod.Settings.autoUpgrade = false;
            if (GenCommandLine.TryGetCommandLineArg("celoadsave", out string save) && !save.NullOrEmpty())
            {
                LongEventHandler.ExecuteWhenFinished(() =>
                {
                    Log.Message($"[LQTest] Auto-loading save '{save}'.");
                    GameDataSaveLoader.LoadGame(save);
                });
            }
        }
    }

    public class LQTestRunnerComponent : GameComponent
    {
        private bool active;
        private bool done;
        private int startTick;
        private int phase;
        private string scenario;
        private Pawn subject;
        private readonly List<string> results = new List<string>();
        private bool failed;

        public LQTestRunnerComponent(Game game)
        {
        }

        public override void LoadedGame()
        {
            if (!GenCommandLine.TryGetCommandLineArg("ceassert", out scenario)
                || scenario.NullOrEmpty() || !scenario.StartsWith("qual"))
            {
                return;
            }
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                string nick = scenario == "qual1" ? "Filty" : "Uppy";
                subject = Find.CurrentMap.mapPawns.FreeColonistsSpawned
                    .FirstOrDefault(p => p.Name is NameTriple nt && nt.Nick == nick);
                if (subject == null)
                {
                    Check("setup", false, "subject pawn missing");
                    Finish();
                    return;
                }
                LoadoutQualityMod.Settings.autoUpgrade = false; // each scenario opts in explicitly
                active = true;
                startTick = Find.TickManager.TicksGame;
                Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
                Log.Message($"[LQTest] {scenario} started.");
            });
        }

        private void Check(string name, bool pass, string detail)
        {
            results.Add($"{{\"name\": \"{name}\", \"passed\": {(pass ? "true" : "false")}, \"detail\": \"{detail.Replace("\"", "'")}\"}}");
            if (!pass)
            {
                failed = true;
            }
            Log.Message($"[LQTest] {name}: {(pass ? "PASS" : "FAIL")} - {detail}");
        }

        private IEnumerable<(ThingWithComps thing, QualityCategory q)> CarriedWeapons()
        {
            if (subject.equipment?.Primary != null)
            {
                subject.equipment.Primary.TryGetQuality(out QualityCategory q);
                yield return (subject.equipment.Primary, q);
            }
            foreach (ThingWithComps t in subject.inventory.innerContainer.OfType<ThingWithComps>().Where(t => t.def.IsWeapon))
            {
                t.TryGetQuality(out QualityCategory q);
                yield return (t, q);
            }
        }

        public override void GameComponentTick()
        {
            if (!active || done)
            {
                return;
            }
            if (Find.TickManager.Paused || Find.TickManager.CurTimeSpeed != TimeSpeed.Superfast)
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
            }
            int tick = Find.TickManager.TicksGame;
            if (tick % 30 != 0)
            {
                return;
            }
            if (scenario == "qual1")
            {
                TickQual1(tick);
            }
            else
            {
                TickQual2(tick);
            }
        }

        // QUAL-1: filter, symmetric. Phase 0 (no entry, wide open): CE fetches
        // SOME rifle — which one is CE's pathing business, not ours; any fetch with
        // ranges wide open proves default inertness. Phase 1: set the range to
        // exclude whatever the pawn holds and admit only the OTHER rifle — the drop
        // gate must shed the carried one and the fetch gate must bring the other.
        private QualityCategory fetchedQuality;

        private void TickQual1(int tick)
        {
            ThingDef rifle = ThingDef.Named("Gun_BoltActionRifle");
            if (phase == 0)
            {
                var carried = CarriedWeapons().Where(c => c.thing.def == rifle).ToList();
                if (carried.Count > 0)
                {
                    fetchedQuality = carried[0].q;
                    Check("default-inert-fetches", true, $"fetched {fetchedQuality} rifle with ranges wide open");
                    var comp = LQGameComponent.Instance.GetEntry(subject.GetLoadout().uniqueID, create: true);
                    comp.quality = fetchedQuality == QualityCategory.Awful
                        ? new QualityRange(QualityCategory.Good, QualityCategory.Legendary)
                        : new QualityRange(QualityCategory.Awful, QualityCategory.Poor);
                    phase = 1;
                    startTick = tick;
                    return;
                }
                if (tick - startTick > 20000)
                {
                    Check("default-inert-fetches", false,
                        $"never fetched a rifle; job={subject.CurJobDef?.defName}");
                    Finish();
                }
                return;
            }
            if (phase == 1)
            {
                QualityCategory wanted = fetchedQuality == QualityCategory.Awful
                    ? QualityCategory.Excellent : QualityCategory.Awful;
                var carried = CarriedWeapons().Where(c => c.thing.def == rifle).ToList();
                bool hasWanted = carried.Any(c => c.q == wanted);
                bool hasOld = carried.Any(c => c.q == fetchedQuality);
                if (hasWanted && !hasOld)
                {
                    Check("floor-drops-and-refetches", true, $"{fetchedQuality} shed, {wanted} fetched");
                    Finish();
                    return;
                }
                if (tick - startTick > 30000)
                {
                    Check("floor-drops-and-refetches", false,
                        $"carried={string.Join(",", carried.Select(c => c.q.ToString()))} job={subject.CurJobDef?.defName}");
                    Finish();
                }
            }
        }

        // QUAL-2: upgrade. Phase 0 (toggle OFF): 1800 ticks, normal rifle stays.
        // Phase 1 (ON): equipped rifle becomes excellent, the normal one lies
        // dropped unforbidden, and the steel gladius is NOT swapped for the
        // plasteel one (same-stuff rule).
        private void TickQual2(int tick)
        {
            ThingDef rifle = ThingDef.Named("Gun_BoltActionRifle");
            if (phase == 0)
            {
                subject.equipment.Primary.TryGetQuality(out QualityCategory q);
                if (q != QualityCategory.Normal)
                {
                    Check("off-no-upgrade", false, $"swapped with toggle OFF: quality={q}");
                    Finish();
                    return;
                }
                if (tick - startTick > 1800)
                {
                    Check("off-no-upgrade", true, "normal rifle retained for 1800 ticks");
                    LoadoutQualityMod.Settings.autoUpgrade = true;
                    phase = 1;
                    startTick = tick;
                }
                return;
            }
            if (phase == 1)
            {
                subject.equipment.Primary.TryGetQuality(out QualityCategory q);
                if (subject.equipment.Primary?.def == rifle && q == QualityCategory.Excellent)
                {
                    Check("upgrade-swaps-equipped", true, "excellent rifle equipped");
                    // The pawn-adjacent instance ONLY — quicktest maps scatter their own
                    // random weapons, and a map-wide FirstOrDefault once sampled a
                    // pre-existing forbidden rifle from across the map (id mismatch
                    // caught by fingerprinting).
                    Thing droppedNormal = subject.Map.listerThings.ThingsOfDef(rifle)
                        .Where(t => t.Spawned && t.Position.DistanceTo(subject.Position) < 8f)
                        .FirstOrDefault(t => t.TryGetQuality(out QualityCategory dq) && dq == QualityCategory.Normal);
                    Check("old-dropped-unforbidden",
                        droppedNormal != null && !droppedNormal.IsForbidden(Faction.OfPlayer),
                        $"dropped={(droppedNormal != null)} id={droppedNormal?.thingIDNumber} forbidden={droppedNormal?.IsForbidden(Faction.OfPlayer)}");
                    ThingWithComps gladius = subject.inventory.innerContainer.OfType<ThingWithComps>()
                        .FirstOrDefault(t => t.def.defName == "MeleeWeapon_Gladius");
                    Check("same-stuff-rule-holds",
                        gladius != null && gladius.Stuff == ThingDefOf.Steel,
                        $"gladius stuff={gladius?.Stuff?.defName ?? "missing"} (plasteel bait must be ignored)");
                    Finish();
                    return;
                }
                if (tick - startTick > 30000)
                {
                    Check("upgrade-swaps-equipped", false,
                        $"primary={subject.equipment.Primary?.def?.defName}:{q} job={subject.CurJobDef?.defName}");
                    Finish();
                }
            }
        }

        private void Finish()
        {
            done = true;
            var sb = new StringBuilder();
            sb.Append($"{{\n  \"scenario\": \"{scenario}\",\n");
            sb.Append($"  \"passed\": {(!failed ? "true" : "false")},\n");
            sb.Append("  \"checks\": [\n    ");
            sb.Append(string.Join(",\n    ", results));
            sb.Append("\n  ]\n}\n");
            File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, $"test-results-{scenario}.json"), sb.ToString());
            Log.Message("[LQTest] Results written; shutting down.");
            Root.Shutdown();
        }
    }
}
