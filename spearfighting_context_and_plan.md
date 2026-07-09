# Spearfighter — Context & Game Plan

> The single source of truth for **what we're building and why**. Supersedes the old
> `spearfighting_game_plan.md` and `spearfighting_decision_register.md` (early
> exploration whose recommendations were largely overtaken by our actual, locked
> decisions). For the concrete task list and live build status, see
> **`spearfighting_task_catalog.md`**. Last updated 2026-07-09.

---

## 1. What the game is

A first-person **mobile** (iOS + Android) spear-combat game. Players move, jump, throw
**charge-based arced spears** (quick tap = short-range jab), and **build obstacles live
during combat** to shape cover and verticality. Free-to-play, **cosmetics-only**
monetization (no ads, no pay-to-win). Visual target: "enhanced Minecraft, not
rectangles" — chunky, readable, stylized; voxel builds smoothed so they don't read as
raw cubes.

**Design pillars (say "no" against these):**
1. **Spacing + reads over twitch.** Arced projectiles + a live trajectory preview make
   throws a positioning/prediction game, not a reflex test — the right fit for mobile.
2. **The world is player-shaped.** Building mid-fight is the novelty; everything must
   survive a mutable arena.
3. **Two thumbs, honest.** Every mechanic has to feel good on a touchscreen first.
4. **Competitive integrity.** Cosmetics only; gameplay is earnable, never buyable.

---

## 2. Locked design decisions (these are settled)

These are the *actual* decisions we build to. Changing one is a rewrite, not a tweak.

- **Perspective:** first-person, with a visible viewmodel (right arm + held spear).
- **Combat:** arced projectile spear — **hold to charge** (power scales with charge
  time), release to throw; **quick tap = short-range jab**. **Hitbox/hurtbox**
  resolution, *not* simulated weapon physics. **No aim-assist / no lock-on** in MVP —
  free aim with a live **dotted trajectory-preview arc** as the aiming aid.
  *(This replaces the old thrust/block/parry/feint melee concept entirely.)*
- **Health model:** health only for now; stamina/posture deferred (revisit if
  counterplay needs it). Medium-long TTK so a landed throw matters but counterplay exists.
- **Building:** **live during combat** (not pre-match, not between-rounds). Default
  object = a **walkable voxel staircase** (~1 player-height rise). Custom player-authored
  shapes via a constrained **voxel editor** (deferred). **Place-only** (no editing placed
  objects). Gated by a **regenerating energy meter**, single build cost, and a **cap on
  simultaneous builds** (oldest despawns). Fairness/anti-turtle constraints deferred to
  post-MVP.
- **Projectile miss:** spear **sticks** into build/floor. **No destruction** for now.
- **Format:** **1v1** for MVP; architecture must not preclude solo/FFA/teams later.
  Real-time PvP is the goal; **MVP plays vs bots** on the real architecture.
- **Business:** F2P, cosmetics-only in-game store. No ads. No pay-to-win.
- **Min-spec:** 2021 mid-range Android floor (Adreno 619–642L class, ~4GB RAM, Vulkan
  1.1 / GLES 3.2 / Metal, FHD+). **60 fps target, 30 fps floor**, tuned for sustained
  (throttled) performance.

---

## 3. Tech stack (decided)

- **Engine:** Unity **6.5** (`6000.5.3f1`), C#, **URP** (mobile render pipeline).
  *(Plan said "6.x LTS"; 6.5 is what's installed and used. 6000.0.x is the LTS branch if
  a third-party SDK ever requires it — trivial to drop back to.)*
- **Netcode (later, Phase 3):** **Photon Fusion** — server-authoritative + client
  prediction + lag compensation. **NOT** deterministic rollback (Quantum). Server
  authority is the anti-cheat backbone; strict determinism is not required. (We happen to
  keep the sim deterministic anyway — it's free insurance and makes tests/replays exact.)
- **Hosting:** managed / outsourced (Photon Cloud or Unity Multiplay). No self-managed servers.
- **Backend (BaaS):** evaluate Unity Gaming Services / PlayFab / Nakama for accounts,
  storage, matchmaking, leaderboards, analytics, remote config.
- **Source control:** Git (GitHub: `natek0/spearfighter`), pushed over SSH.

---

## 4. Architecture principles (load-bearing)

These are *why the code is shaped the way it is*; protect them.

- **Simulation ↔ rendering separation.** The game logic is an engine-agnostic,
  **fixed-tick, input-driven simulation** (`unity/Assets/Spearfighter/Simulation/`, no
  `UnityEngine` dependency — enforced by `noEngineReferences`). Unity is a thin
  view/input shell on top. This is what lets bots, replays, and server-authoritative
  netcode all feed the **same simulation** without a rewrite.
- **One input struct.** A human (touch/desktop), a bot, or the network all produce the
  same `InputCommand`, consumed identically by `SimCore.Tick(commands)`.
- **Sim-owned physics.** Collision lives *in the sim* (not Unity PhysX) because the
  server must reproduce it authoritatively and rebuild player builds identically.
- **Voxel coordinate system.** All solid geometry is cells on a shared **world voxel
  grid**. A build = a local voxel bitmask + world origin + rotation → deterministic and
  **server-reconstructable** (exactly what the voxel editor and netcode need). The
  default staircase and future custom shapes use the *same* representation.
- **Data-driven tunables.** All combat/build/economy values are `SimConfig` (a
  ScriptableObject / future remote-config), never hardcoded.
- **Testability.** The sim compiles under plain .NET and is covered by an xUnit suite
  (`sim/`, run with `dotnet test`) — gameplay math is verified without Unity.

---

## 5. Key engineering decisions made during the build (rationale kept)

- **Collision = voxel swept-AABB + eased step-up.** Player is a swept box resolved per
  axis; auto step-up climbs stepped ramps (eased vertically so it feels smooth); walls
  block; jump-over emerges from jump-height vs obstacle-height. Chosen over
  capsule-vs-triangle collide-and-slide because it's deterministic, cheap on mobile,
  server-reconstructable, and unifies the default + custom builds. Ramps are physically
  stepped, hidden by a smoothed render mesh (later).
- **Build placement = hold-to-preview, release to place.** The translucent ghost only
  shows while BUILD is held (no always-on clutter); quick tap still places.
- **Viewmodel** currently renders on the main camera (a URP overlay-camera version threw
  at runtime and broke startup, so it was pulled). No-clip overlay is a later refinement.
  Any non-essential visual is now built **last, in a try/catch**, so it can never abort
  game startup.
- **Controls:** Scheme B (left move joystick, right-side look drag, draggable
  attack/BUILD buttons that also aim, jump/build/rotate). Look/move X are negated at the
  input boundary to correct a camera↔sim handedness mirror. HUD respects `Screen.safeArea`
  (notch), forced landscape.
- **Open question:** the **rotate-build** button — keep it, or force builds to face the
  player? Leaning keep (matters once custom shapes exist), low-stakes either way.

---

## 6. What still applies from the older plans (kept, for later phases)

- **Art direction spec ("enhanced Minecraft").** Turn the vibe into rules: chunky
  readable silhouettes, beveled/organic edges (not raw cubes), flat/gradient + toon
  shading, saturated palette, baked lighting + light probes, minimal real-time shadows.
  Hero assets = viewmodel (arms+spear), enemy, spear projectile, build blocks, stuck
  spear. Reference 2–3 games and write what you take from each. (Phase 2.)
- **Backend is not an afterthought.** Analytics + remote-config should exist *by the
  first real playtest* or you tune combat blind. `SimConfig`/`SimEvent` are the seams.
- **Onboarding is retention-critical.** A novel control scheme *and* a novel build loop
  both need teaching (charge/throw/jab, arc aiming, building + meter). (Phase 2.)
- **Compliance is mandatory, not optional.** Content rating (IARC — a combat game),
  privacy/data-safety labels, and COPPA/GDPR-K **must** be revisited before launch; if
  under-13s can play it reshapes data collection. (Phase 4.)
- **Scope reality check.** As specified (FP, mobile, live building, networked,
  cosmetics, live-ops, two platforms) this is a large, multi-discipline effort. The
  phasing below is how it stays tractable; cosmetics-only is a *volume* business, so
  retention/onboarding are load-bearing for the business, not just the fun.

---

## 7. Phasing (milestones) — mirrors the task catalog

- **Phase 0 — Combat-feel prototype.** ✅ Validated (web prototype), then rebuilt in
  Unity. Charge-aim-throw + jab + movement in first-person with two thumbs.
- **Phase 1 — Core loop vs bots.** *In progress.* Live building (default voxel staircase
  + meter + cap done; voxel editor deferred), bots (basic done, needs depth), greybox
  arena, core HUD, analytics/remote-config (todo). **Gate:** is a 1v1 vs a real bot fun,
  in FP, without feeling blind?
- **Phase 2 — Art, audio, polish, perf.** Style spec + shaders, build-mesh smoothing,
  audio, menus/tutorial, optimization to the min-spec floor, device/exploit testing.
- **Phase 3 — Real-time multiplayer.** Photon Fusion server-auth + prediction + lag
  comp; **mutable-world sync** (the hardest system — prototype a two-device
  build-and-fight early); matchmaking; anti-cheat; accounts.
- **Phase 4 — Store, monetization, compliance, launch prep.**
- **Phase 5+ — Launch & live-ops** (seasons, cosmetics, balance, new modes).

---

## 8. Critical-path risks (protect these)

1. **Combat feel** — validated; keep protecting it through every change (feel-test each build).
2. **Mutable-world netcode (Phase 3)** — replicating player-built geometry authoritatively
   in real time on mobile is the hardest system in the project and the most likely to blow
   estimates. The voxel coordinate system (§4) is deliberately built to make this
   reconstructable; still, prototype a two-device build-and-fight **before** any netcode polish.
3. **First-person build-fight legibility** — can you fight competently inside geometry you
   can't see around? The must-test question of Phase 1.
