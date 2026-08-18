# Releasing

Manual local builds (workshop-local CE reference; CE is CC BY-NC-SA — no CI,
never vendored); `Assemblies/LoadoutQuality.dll` committed.

## Checklist

1. `dotnet build Source/LoadoutQuality/LoadoutQuality.csproj -c Release`
2. Automated pass: `./test/run-lq-stage.sh`, then both scenarios per
   TESTPLAN.md — all `test-results-qual*.json` must be `"passed": true`.
3. Manual UI check: loadout dialog shows the quality + hit-point strip; ranges
   persist across save/load; resetting to wide open prunes the entry.
4. Composition sanity: one load in the CE+SS suite profile (SS notify path).
5. Demo GIF (owner): QUAL-2 scene — pawn walks to the excellent rifle, swaps,
   drops the old one. Clip to `Media/`, embed README + description slot.
6. Blessing issues (posterity, non-blocking): bananasss00/RW-CombatExtended_ExtendedLoadout
   and linyaDev/CEQuickLoadout — behavioral reference credit.
7. CE upstream pitch for the FILTER half (parity argument: outfit policies have
   these ranges, loadouts don't) — file with the working patch as evidence.
8. Record CE version tested; tag v1.0.0; upload via in-game Mods → Upload.

## Versioning & save compatibility

Semver. Save footprint: one GameComponent storing per-loadout ranges (pruned
to nothing at defaults). Safe to ADD mid-save. REMOVING mid-save leaves a
one-time unknown-GameComponent load warning, then nothing. Breaking either =
major bump.
