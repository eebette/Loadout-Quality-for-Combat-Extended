# Loadout Quality for Combat Extended

**Status: BUILT and machine-verified 2026-08-18 — both milestones (filter +
upgrade) implemented, 6/6 automated checks green in a CE-only test profile (see
TESTPLAN.md). Remaining: owner feel-pass, demo GIF, blessing issues, CE upstream
pitch, publish (RELEASING.md).** The brief below is kept as design
documentation.

## Objective

Standalone CE enhancement (NOT part of the CombatExtended-SimpleSidearms compat
family — works with CE alone, no SimpleSidearms involvement):

1. **Per-loadout quality/HP floors** — a loadout gains a quality range and a
   hit-point-percent range; loadout fetching skips items outside the ranges, and
   carried weapons outside them are dropped. Closes a vanilla-parity gap: outfit
   (apparel) policies have quality+HP filters, CE loadouts have none (CE 1.6 loadout
   code contains zero `QualityCategory` references; fetch matches def only).
2. **Auto weapon upgrade (toggleable)** — pawns swap loadout-declared weapons for
   strictly-higher-quality instances available on the map.

Name idiom: "X for Combat Extended" = established third-party positioning (never
"Combat Extended - X", which mimics the CE team's own naming).

- Display name: `Loadout Quality for Combat Extended`
- packageId: `eebette.CELoadoutQuality`
- Dependencies: Harmony, Combat Extended only. SimpleSidearms is an OPTIONAL soft
  integration (reflection). RimWorld 1.6.

## Prior art — behavioral reference ONLY (both unlicensed; copying code is forbidden)

- **https://github.com/bananasss00/RW-CombatExtended_ExtendedLoadout** (author
  PirateBY) — proved the filter mechanism. Per-loadout side-table
  `{HpRange, QualityRange, RefillThreshold}`, `Allows(Thing)` enforced by (a) a
  transpiler into CE `JobGiver_UpdateLoadout.FindPickup`'s candidate-validation
  lambda, (b) postfixes on `Utility_HoldTracker.GetExcessThing` /
  `GetExcessEquipment` declaring disallowed carried weapons excess, (c) loadout
  dialog UI. Upstream dead (1.3, last commit 2022-07); the only living fork is
  inside the Hardcore SK modpack, 1.5-only, compiled against HSK's pinned CE —
  unusable on mainline CE 1.6. That unusability is this mod's reason to exist.
- **https://github.com/linyaDev/CEQuickLoadout** — proved the upgrade concept
  (8-hour MapComponent scan, def-match, quality compare, queued swap job) but is
  hard-locked to the HSK modpack and uses a temp-forbid-all-of-def hack to prevent
  pickup races. We re-engineer rather than follow (see Mechanics).
- Ask both authors for blessing/license via GitHub issues at M3 — non-blocking,
  for posterity.

## Approved design decisions (do not relitigate without the owner)

- **Below-floor carried weapon: DROP it** (vanilla outfit-filter parity, same as
  ExtendedLoadout). Documented caveat: a floor with zero allowed instances on the
  map leaves the pawn without that weapon — same as apparel outfits.
- **Upgrade stuff matching: SAME STUFF ONLY** for `MadeFromStuff` weapons. No
  cross-material quality comparison (awful plasteel vs excellent steel is
  apples-to-oranges), and it keeps SS def+stuff memory pairs stable.
- **Defaults:** ranges ship wide-open (mod inert until narrowed); upgrade toggle
  default ON — installing this mod is the opt-in.
- Quality comparison is strictly-greater, same def (+ same stuff per above). HP as a
  tiebreak is explicitly deferred (open question).

## Mechanics

**Filter:**
- Side-table `{QualityRange, HpRange}` keyed by `Loadout.uniqueID`, scribed in a
  GameComponent (pattern: `SupplyGameComponent` in the Loadouts sibling repo,
  https://github.com/eebette/CombatExtended-SimpleSidearms-Compatibility-Loadouts).
- Fetch gate: inject an `Allows(thing)` check into `JobGiver_UpdateLoadout.FindPickup`'s
  candidate predicate. ExtendedLoadout transpiled the compiler-generated lambda
  (`<>c__DisplayClass6_0.<FindPickup>b__3` in its era) — that name is BRITTLE across
  CE releases: resolve the inner type/method dynamically (scan for the lambda
  containing the `ForbidUtility.IsForbidden` call), guard with Harmony `Prepare()`,
  and log loudly on resolution failure rather than silently not patching.
- Drop gate: postfixes on `Utility_HoldTracker.GetExcessThing` and
  `GetExcessEquipment` (public statics — stable targets): when CE found no excess,
  report a below-floor carried weapon as excess (one per call). Drop-then-refetch
  replaces junk with allowed instances. No patches to slot counting needed —
  ExtendedLoadout proved this composition.
- UI: quality-range + HP-range widgets in CE's loadout management dialog, using the
  vanilla widget idiom players know from outfit dialogs (`Widgets.QualityRange` etc.).

**Upgrade:**
- NO parallel scheduler. Postfix `JobGiver_UpdateLoadout.GetUpdateLoadoutJob` (public
  static): when CE returns no job, scan the pawn's loadout-declared weapon defs for a
  map instance with strictly higher quality that passes `Allows()`, is reachable,
  unclaimed, unforbidden → return a swap job. This rides CE's own 1800-tick
  throttle, think-tree priority, AND the Assign-tab "Rearm"/update-now button for
  free.
- Swap JobDriver: reserve the target in `TryMakePreToilReservations` (kills the race
  CEQuickLoadout handled with map-wide temp-forbids — CE's own fetch respects
  reservations, cf. its `GetUnreservedStackCount`), goto, drop the old weapon **in
  place, unforbidden** (vanilla `JobDriver_OptimizeApparel` convention: haulers store
  it; other pawns may legitimately claim it), pick up the new one, then
  `CompInventory.UpdateInventory()`.
- SS soft integration: if SimpleSidearms is loaded, notify its `CompSidearmMemory`
  of the swap via reflection (forget old pair if no other instance carried, inform
  of the new one). The Loadouts sibling self-heals template pairs anyway when
  present; this covers SS-without-that-module users.

## Interactions with the compat suite (verified reasoning, keep true)

- The fetch-gate applies automatically to the Loadouts module's virtual refetch
  slots — they flow through the same `FindPickup` pipeline. No coordination needed.
- Upgrade swaps preserve ThingDef (and stuff, per decision), so SS memory pairs and
  the Loadouts module's template records do not churn.

## Build

Same pattern as the sibling repos (SDK net48, `Krafs.Rimworld.Ref 1.6.*`,
`Lib.Harmony 2.3.3` ExcludeAssets=runtime, `Krafs.Publicizer` over
`~/.local/share/Steam/steamapps/workshop/content/294100/2890901044/Assemblies/CombatExtended.dll`).
No CI possible (local Steam refs; CE is CC BY-NC-SA — never vendor it).

## Milestones

- **M1**: scaffold + filter (side-table, scribing, dialog UI, fetch gate, drop gate)
  + staging save QUAL-1: colonist with a loadout, awful+excellent instances of the
  same weapon on the ground; verify fetch respects the floor and a carried
  below-floor weapon is dropped. Staging-mod pattern: copy `test/StagingMod/` from a
  sibling repo (CLI-arg-gated GameComponent; reuse its anchor/teardown fixes).
- **M2**: upgrade (job-giver postfix, swap JobDriver, SS reflection notify) +
  QUAL-2: pawn carrying normal quality, excellent on map → swaps once, no loops,
  old weapon dropped unforbidden.
- **M3**: README provenance section, blessing-request issues on both reference
  repos, Workshop prep. Also: pitch the FILTER half to CE upstream as a feature
  request (parity argument) — if CE takes it natively, this mod shrinks to the
  upgrade half.

## Open questions

1. HP-percent as upgrade tiebreak at equal quality — later setting or never?
2. Upgrade scope: loadout-declared weapons only (current decision) — extend to
   SS-remembered sidearms someday? (Would make SS a semi-real dependency; probably a
   separate setting, default off.)
3. Dialog UI placement: inline rows per loadout vs a small settings popout — decide
   against CE 1.6's actual dialog layout when building.
4. Drop-then-refetch churn: if the only replacement is ALSO below floor, pawn ends
   up weaponless by design — is a "keep worst rather than none" escape hatch wanted?
   (Current answer: no — outfit parity. Revisit only on playtest pain.)
5. CE upstream pitch timing: before M1 (risk: wait), after M3 (risk: wasted work if
   accepted). Current lean: after M1 works locally, file the CE issue with the
   working patch as evidence.
