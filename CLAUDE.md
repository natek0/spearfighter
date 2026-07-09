# Spearfighter — Project Context

> This file is read by Claude Code at the start of every session. It carries the decisions made
> during planning so the assistant starts with full context. Keep it concise and current.

## What this is
A first-person **mobile** (iOS + Android) spear-combat game. Players move, jump, throw
**charge-based arced spears** (quick tap = short-range jab), and **build obstacles live during
combat**. Free-to-play, **cosmetics-only** monetization (no ads, no pay-to-win).

## Current status
- **Phase 0 (combat-feel prototype): COMPLETE.** Validated on a real phone using a throwaway
  web/Three.js build. The core charge-aim-throw + jab loop feels good on two thumbs.
- **The web prototype is disposable and does NOT carry over** to the production codebase.
- **Next:** rebuild the Phase 0 / Phase 1 systems in the production stack (Unity).

## Tech stack (decided)
- **Engine:** Unity 6.x LTS, C#, **URP** (mobile render pipeline).
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
- **Building:** **live during combat.** Default object = **slanted ramp-wall** (~1 player-height,
  walkable top, rotatable, chainable for height). **Custom player-authored shapes** via a
  constrained **voxel editor** — gameplay-relevant collision, copyable between players.
  **Place-only** (no editing placed objects). Gated by a **regenerating energy meter**, single
  build cost, and a **cap on simultaneous builds**. Fairness/anti-turtle constraints deferred
  to post-MVP.
- **Projectile miss:** spear **sticks** into build/floor. **No destruction** for now.
- **Format:** **1v1** for MVP; architecture must not preclude solo/FFA/teams later. Real-time
  PvP is the goal; **MVP plays vs bots** running on the real netcode architecture.

## Architecture rules (important)
- Keep the **SIMULATION** (movement, combat, builds, projectiles) **decoupled from rendering,
  input-driven, and fixed-tick.** Bots, replays, and netcode all feed the **same input struct**
  into the **same simulation.** This is what allows server-authoritative PvP to be added later
  WITHOUT a rewrite.
- All tunables (combat, build, economy) are **data-driven** (ScriptableObjects / remote config),
  never hardcoded.

## Reference docs (in /docs)
- `task_catalog.md` — full work breakdown (17 workstreams, phased).
- `decision_register.md` — the 34 load-bearing decisions and their rationale.
- `game_plan.md` — the comprehensive category-by-category plan.

## Conventions
- (To be filled in when the Unity project is scaffolded: folder layout, assembly definitions,
  naming, formatting, test setup.)

## Critical-path risks (protect these)
1. **Combat feel** — validated in Phase 0; keep protecting it through every change.
2. **Mutable-world netcode** — replicating player-built geometry authoritatively in real time on
   mobile is the hardest system in the project. Prototype a two-device build-and-fight EARLY in
   the networking phase, before any netcode polish.
