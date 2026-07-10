# Spearfighter — Project Context

> This file is read by Claude Code at the start of every session. It carries the decisions made
> during planning so the assistant starts with full context. Keep it concise and current.

## What this is
A first-person **mobile** (iOS + Android) spear-combat game. Players move, jump, throw
**charge-based arced spears** (quick tap = short-range jab), and **build obstacles live during
combat**. Free-to-play, **cosmetics-only** monetization (no ads, no pay-to-win).

## Current status
- **Phase 0 rebuilt in Unity 6.5 (`6000.5.3f1`) and played on iPhone.** Movement,
  charge-aim-throw + jab, arced trajectory preview, live **voxel** building (walk up it), a
  first-person viewmodel, and a basic bot all work on-device. Combat feel is broadly landing.
- **Phase 1 in progress.** Building + energy meter + cap done. **Bot depth + arena tightening
  done** (LOS/arc fire discipline, dodging, reactive cover, stuck-routing; symmetric + bounded
  arena). **"Glitchy map" fixed** (root cause: URP shaders on the built-in pipeline broke depth
  writing — `Mats` now matches shaders to the active pipeline; runs on **built-in + `Standard`**,
  renders correctly. A headless URP-asset attempt broke rendering and was **reverted** — redo URP
  via the editor wizard + Play-verify, never headless.) **NPC now an articulated humanoid (legs)**.
  **Match structure done:** stock-based **3-life** match with win/lose + auto-rematch, HUD life
  pips + banner (sim-owned, unit-tested; 25 sim tests). **Remain:** voxel custom-build editor,
  analytics/remote-config, bot difficulty tiers.
- **Next:** on-device feel test of the full match, then the **voxel custom-build editor** and the
  **analytics/remote-config seam** to close Phase 1. See `spearfighting_task_catalog.md`. The
  catalog got a **comprehensiveness pass** (WS17–WS22: characters/animation, devops/observability,
  persistence/localization, trust-&-safety/anti-cheat, retention, UA).

## Tech stack (decided)
- **Engine:** Unity 6.5 (`6000.5.3f1`), C#, **URP** (mobile render pipeline).
- **Netcode (later phase):** **Photon Fusion** — server-authoritative + client prediction +
  lag compensation. NOT deterministic rollback (Quantum). Server-authoritative ⇒ strict
  determinism is NOT required.
- **Hosting:** managed / outsourced (Photon Cloud or Unity Multiplay). **No self-managed servers.**
- **Backend:** a BaaS (evaluate Unity Gaming Services / PlayFab / Nakama) for accounts, storage,
  matchmaking, leaderboards, analytics, remote config.
- **Min-spec:** 2021 mid-range Android floor (Adreno 619–642L class, ~4GB RAM, Vulkan 1.1 /
  GLES 3.2 / Metal, FHD+). **60 fps target, 30 fps floor, tuned for sustained (throttled) perf.**

## Locked design decisions
- **Camera:** first-person.
- **Combat:** arced projectile spear; **hold to charge** (power scales with charge time),
  release to throw; **quick tap = short-range jab**. **Hitbox/hurtbox** resolution — NOT
  simulated weapon physics. No aim-assist in MVP.
- **Aiming aid:** live **dotted trajectory-preview arc**.
- **Controls (Scheme B, two thumbs):** left = move joystick; right-side drag = look; a
  **draggable attack button** (tap = jab; hold + drag = charge/aim/throw, with **reduced look
  sensitivity while charging**); jump + build buttons on the right.
- **Building:** **live during combat.** Default object = a **walkable voxel staircase**
  (~1 player-height rise), placed via **hold-to-preview, release-to-place**. All builds live on
  a shared **world voxel grid** (a build = local bitmask + origin + rotation) — the same
  representation the (deferred) **custom voxel editor** and the netcode will use.
  **Place-only** (no editing placed objects). Gated by a **regenerating energy meter**, single
  build cost, and a **cap on simultaneous builds** (oldest despawns). Fairness/anti-turtle
  constraints deferred to post-MVP.
- **Projectile miss:** spear **sticks** into build/floor. **No destruction** for now.
- **Characters:** opponent body (NPC bot + future PVP players) = **articulated humanoid with
  legs** on **one shared rig** (skins retarget); local player = **first-person viewmodel**.
  **Animation is sim-driven, never authoritative.** Greybox now; rigged art Phase 2.
- **Format:** **1v1** for MVP; architecture must not preclude solo/FFA/teams later. Real-time
  PvP is the goal; **MVP plays vs bots** running on the real netcode architecture.

## Architecture rules (important)
- Keep the **SIMULATION** (movement, combat, builds, projectiles) **decoupled from rendering,
  input-driven, and fixed-tick.** Bots, replays, and netcode all feed the **same input struct**
  into the **same simulation.** This is what allows server-authoritative PvP to be added later
  WITHOUT a rewrite.
- All tunables (combat, build, economy) are **data-driven** (ScriptableObjects / remote config),
  never hardcoded.

## Reference docs
- `spearfighting_task_catalog.md` — full work breakdown + **live implementation status**.
- `spearfighting_context_and_plan.md` — vision, locked decisions, architecture, rationale
  (merged from the old game_plan + decision_register, which were deleted as outdated).
- `spear_prototype.html` — the validated Phase 0 web prototype (feel/number reference only).

## Conventions
- **Repo layout:** `unity/` = Unity 6.5 project. `unity/Assets/Spearfighter/Simulation/` =
  engine-agnostic sim (**no UnityEngine**, `noEngineReferences`). `unity/Assets/Spearfighter/Game/`
  = Unity glue (view/input/HUD/bootstrap). `sim/` = dotnet solution compiling the sim + xUnit
  tests — run with `cd sim && dotnet test` (fast, no Unity needed; ~18 tests).
- **Run it:** open `unity/`, menu `Spearfighter ▸ Create Play Scene` (or add `Bootstrap` to an
  empty scene), press Play. On device: File ▸ Build Profiles ▸ Build (choose **Append**) → open
  Xcode project → Run. Verify Unity-side changes by batch-compiling (headless) before rebuilding.
- **Core rule:** the sim key type is `SimCore` (class), driven by `SimCore.Tick(InputCommand[])`.
  All tunables live in `SimConfig` (data-driven), never hardcoded.

## Critical-path risks (protect these)
1. **Combat feel** — validated in Phase 0; keep protecting it through every change.
2. **Mutable-world netcode** — replicating player-built geometry authoritatively in real time on
   mobile is the hardest system in the project. Prototype a two-device build-and-fight EARLY in
   the networking phase, before any netcode polish.
