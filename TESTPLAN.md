# Test plan — Loadout Quality for Combat Extended

Automated end-to-end in this repo's own **CE-only profile**
(Core + Harmony + Combat Extended + this mod — no Simple Sidearms, no suite;
that's the "works with CE alone" claim):

```
./test/run-lq-stage.sh                     # regenerate QUAL saves (quit after letter)
./test/run-lq-assert.sh qual1 QUAL-1-filter
./test/run-lq-assert.sh qual2 QUAL-2-upgrade
```

Results: `test/SaveData/test-results-qual*.json`. Green passes recorded
2026-08-18 (6/6 checks):

- **qual1 (filter)**: with ranges wide open CE fetches whatever it fetches
  (default inertness); the runner then sets the quality range to EXCLUDE the
  carried rifle and admit the other one — the drop gate sheds the carried
  instance and the fetch gate brings the admitted one. Symmetric by design:
  which rifle CE grabs first is CE's pathing business, not the test's.
- **qual2 (upgrade)**: toggle-off negative control (1800 ticks, no swap);
  toggle on → equipped Normal rifle swaps to the Excellent map copy; the old
  one lies dropped UNFORBIDDEN at the pawn's feet; the steel gladius ignores an
  excellent PLASTEEL gladius (same-material rule).

Findings the harness surfaced along the way:

- **The suite profile contaminates LQ scenarios** (first runs): the Loadouts
  module's refetch automation + Simple Sidearms' own logistics fetched
  quality-preferred instances and re-forbade drops. LQ tests moved to their own
  CE-only profile — which is also the honest test of the mod's claim.
- **Race**: a save that loads unpaused ticks before LoadedGame callbacks run —
  the toggle-off reset lost to the think tree once. Settings are now killed in
  the test assembly's static constructor (mod-init time), before any save
  can tick.
- **Quicktest maps scatter their own random weapons**: a map-wide
  "find the dropped Normal rifle" once sampled a pre-existing FORBIDDEN rifle
  from across the map (thingID fingerprinting caught the mismatch; the actual
  drop was unforbidden all along). Assertions search pawn-adjacent instances
  only.
- Vanilla auto-forbids drops outside the home area — the staging paints a home
  area over the scene so the dropped-unforbidden contract is testable at all.
