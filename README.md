# Spearfighter — Phase 0 + Phase 1 (sim layer)

Production rebuild of the validated Phase 0 combat-feel prototype, plus Phase 1's
simulation-layer systems, in the decided stack (Unity 6.x LTS + C#). The disposable
web prototype (`spear_prototype.html`) is kept only as a feel/number reference.

## The one architectural idea

The game logic is an **engine-agnostic, fixed-tick, input-driven simulation** with
**no `UnityEngine` dependency**. Unity is only a view/input shell on top.

```
unity/Assets/Spearfighter/
  Simulation/   <- pure C# sim (System.Numerics only). THE game. No UnityEngine.
  Game/         <- Unity glue: input -> InputCommand, views, HUD, bootstrap.
sim/
  Spearfighter.Simulation/        <- dotnet lib that compiles the SAME Simulation/ files
  Spearfighter.Simulation.Tests/  <- xUnit tests (run without Unity)
```

A human, a bot, or (later) the network all drive the sim through the **same
`InputCommand` struct**, consumed identically by `Simulation.Tick(commands)`. This
is what lets server-authoritative PvP (Photon Fusion, WS10) be added later without
a rewrite — the only thing that changes is where each `commands[i]` comes from.

The `Simulation` asmdef sets `"noEngineReferences": true`, so the compiler
**enforces** the decoupling: any accidental `using UnityEngine` in the sim fails the
build. Collision/physics lives **inside the sim** (not Unity PhysX) because the
server must reproduce it authoritatively and rebuild player builds identically.

## Run the tests (no Unity needed)

```bash
cd sim
dotnet test
```

17 tests cover: charge-power curve, jab-vs-throw disambiguation (incl. the aim-drag
tiebreak), reduced look-sensitivity while charging, trajectory-preview ↔ real-flight
parity, arced spear stick, ballistic-solved aim landing on target, movement/landing,
**walking up a ramp**, wall blocking, build energy drain/regen, build-cap eviction,
and full **determinism** (same inputs + seed ⇒ identical world, bot included).

## Run it in Unity

1. Install **Unity 6.x LTS** via Unity Hub (only 2022.3 is on this machine; the plan
   locks 6.x). Open the `unity/` folder as the project. Unity resolves packages and
   generates `.meta` files on first open.
2. `Spearfighter ▸ Create Play Scene` (menu), or add an empty GameObject to an empty
   scene and add the **`Bootstrap`** component.
3. Press **Play**. `Bootstrap` builds the greybox arena, spawns you + one bot, and
   wires everything in code — no prefab/Canvas wiring required.

### Controls
- **Desktop (editor):** WASD move · mouse look · LMB (tap = jab, hold = charge/throw) ·
  Space jump · **B** build · **R** rotate build · **T** toggle arc.
- **Touch (device — Scheme B):** left half = move joystick · right side = look drag ·
  **THROW/JAB** button (tap = jab, hold = charge; drag while holding to aim) ·
  **JUMP / BUILD / ROT** buttons.

To feel the real touch controls, do a mobile build (or Device Simulator). Combat
tunables live on a `SimConfig` asset (`Create ▸ Spearfighter ▸ Sim Config`); assign
it to `Bootstrap`, or leave empty to use the validated prototype defaults.

## What's in / what's deferred

**In (this pass):** fixed-tick sim; Scheme B input; first-person movement + jump +
ramp/slope traversal; charge→arced-throw + jab; hitbox/hurtbox damage; projectile
stick-on-miss; dotted trajectory preview; ramp-wall building with grid-snap/rotate/
ghost, regenerating energy meter, and simultaneous-build cap; a bot that emits the
same `InputCommand`; greybox arena; greybox HUD; round-based respawn.

**Deferred (by scope decision):**
- Voxel custom-build editor + mesh/collider generation (WS4 P1) — Editor/art-heavy,
  and its colliders must be *server-reconstructable*, which is really netcode work.
- Backend / analytics / remote-config / BaaS selection (WS11) — vendor-blocked; only
  meaningful once there's a running game to measure. `SimConfig` is already the seam
  remote config will plug into.
- Netcode (WS10, Phase 3). The sim is built for it but nothing here is networked yet.

**Known prototype-grade shortcuts (intentional, flagged for later):**
- Input uses the legacy Input Manager for zero-wiring; migrate to the Input System
  (multitouch/palm rejection) in WS1 P1. If look/keys don't respond, set
  *Project Settings ▸ Player ▸ Active Input Handling* to **Both**.
- HUD is IMGUI (`OnGUI`); production HUD is uGUI/UI Toolkit (WS9).
- URP is in the manifest but the pipeline asset isn't assigned; materials fall back
  to Standard so Play works regardless. Assigning the URP asset is a WS0 setup step.
- No render interpolation yet (sim tick = 60, render reads latest state).

## CI/CD, store pipelines, signing (WS0/WS15)
Not set up here — they need Apple/Google accounts, signing, and a cloud-build vendor.
Left as an explicit external-dependency task, not silently skipped.
