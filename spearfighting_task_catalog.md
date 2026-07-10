# Spearfighting Game — Full Task & Objective Catalog

*Every workstream broken into concrete tasks, with all locked decisions baked in. Organized by system (for completeness), then sequenced into phases at the end (for order). Tags: **[P0]–[P6]** = the phase a task primarily lands in. ⚠ = deceptively large or high-risk; respect it early.*

---

# IMPLEMENTATION STATUS — updated 2026-07-09 (after on-device iterations)

*Legend: ✅ done · 🟡 partial (started; note says what's left) · ⬜ not started · ⏸ deferred by scope decision. Status covers P0/P1 only (the phases we're building). The task spec itself is unchanged and lives below this section. Full rationale/architecture is in `spearfighting_context_and_plan.md`.*

**What runs today (and has been played on an iPhone):** an engine-agnostic, fixed-tick, input-driven **simulation** (`unity/Assets/Spearfighter/Simulation/`, no `UnityEngine` dependency) with an **18-test xUnit suite that passes** (`cd sim && dotnet test`), plus the **Unity 6.5 view/input/HUD glue** (`unity/Assets/Spearfighter/Game/`) and a code-driven `Bootstrap` that runs the full loop from one component on an empty scene. **Built and playable on-device (iPhone) via a manual Unity→Xcode pipeline.** Movement, charge-throw + jab, arced trajectory, voxel building (walk up it), first-person viewmodel, and a basic bot all work. Combat feel is broadly landing; tuning + Phase-1 depth remain. See the "build iterations" subsection below for the newest work.

### WS0 — Foundation & Architecture
- ✅ **⚠ Simulation/rendering separation** — the centerpiece. `Simulation.Tick(commands)` is fixed-tick, engine-agnostic; rendering is strictly downstream. `noEngineReferences` on the sim asmdef *enforces* it at compile time.
- ✅ Input abstraction — one `InputCommand` struct; human, bot, (future) network all produce it, consumed identically.
- ✅ Data-driven config — `SimConfig` POCO + `SimConfigAsset` ScriptableObject. (Remote-config *backing* is WS11.)
- ✅ Code architecture — two asmdefs (Simulation / Game), clear folder split, conventions.
- ✅ Source control — git repo + `.gitignore` (Unity+dotnet), pushed to GitHub `main`. (Git LFS + formal branching strategy: not needed yet.)
- 🟡 Unity project — 6.5 scaffold (manifest w/ URP, `ProjectVersion`, asmdefs) done. **Runs on the built-in pipeline** with `Standard` materials (via `Mats`, which auto-selects the shader for the active pipeline) — renders correctly on device. **URP was attempted but REVERTED:** a *programmatically*-created URP renderer (`UniversalRenderPipelineAsset.Create` + bare `UniversalRendererData`) was missing internal resource refs, so URP rendered nothing but the skybox (sim + IMGUI HUD still ran). **Redo URP via Unity's editor wizard** (`Assets ▸ Create ▸ Rendering ▸ URP Asset (with Universal Renderer)`) so the renderer initializes correctly, and verify in Play *before* building. `Mats` will auto-switch to URP/Lit once a pipeline is assigned. **Also left:** iOS/Android build targets, color space + ASTC.
- ⬜ CI/CD cloud builds → TestFlight/Play internal — external deps (Apple/Google accounts, cloud vendor).
- ⬜ Project-management tracker / milestone board — using in-session task list only.
- ⏸ Early SDK stubs (Fusion/BaaS/analytics no-ops) — seams exist (`InputCommand`, `SimConfig`, `SimEvent`); actual stubs deferred with those systems.

### WS1 — Input & Controls (Scheme B)
- ✅ Left move joystick (touch floating + WASD desktop).
- ✅ Right-side look drag with two sensitivity profiles (base + reduced-while-charging, applied *in-sim* off authoritative charge state).
- ✅ **⚠ Draggable attack button** — tap = jab, hold = charge/throw, drag-while-held aims; tap-vs-charge disambiguated by hold time + drag distance. The core gesture.
- ✅ Charge state machine (c∈[0,1]), ✅ jump button, ✅ build button (+ rotate).
- 🟡 Multitouch/latency — legacy-Input multitouch works. **Left:** palm rejection + migrate to the Input System (P1 polish).
- 🟡 Haptics — hit-confirm vibrate wired. **Left:** full haptics map (charge tick, land, place, meter-full).
- ⬜ Customizable HUD layout / left-right-hand mode.

### WS2 — Character & Movement
- ✅ First-person camera rig + FOV (74). **Left:** head-bob/comfort/motion-reduction toggles.
- 🟡 Kinematic capsule controller — move/jump/gravity/grounding done and sim-owned. **Left:** acceleration/friction/air-control feel curves (currently instant velocity), coyote time.
- ✅ **⚠ Slope/ramp traversal** — walk up ramps, gain height; unit-tested.
- 🟡 Movement tuning harness — bots + data-driven config give the loop; dedicated tuning UI later.
- ⬜ Vault/mantle (P1). ⬜ Animation (P2).

### WS3 — Spear Combat
- ✅ Charge→power model, ✅ **⚠ arced projectile**, ✅ **⚠ trajectory-preview arc** (dotted, live, togglable, parity-tested vs real flight).
- ✅ Throw spawn (locally predicted), ✅ jab, ✅ hitbox/hurtbox hit detection (server rewind = WS10).
- ✅ **⚠ projectile-miss = stick** (into floor/build/pillar, no destruction).
- ✅ Health + damage + medium TTK. ✅ Death + **stock-based match (3 lives, win/lose, auto-rematch)**. ✅ all combat values data-driven.
- 🟡 **Stamina/posture system is NOT implemented** — health-only for now (deliberate; revisit when tuning counterplay).

### WS4 — Building (default ramp-wall)
- ✅ Ramp-wall — procedural walkable wedge mesh (visual reuses the sim's exact collision surface), ~1 player-height.
- ✅ **⚠ Placement** — grid snap, 90° rotate, live ghost preview, reach clamp, no-bury-a-player check, one-tap place.
- ✅ Energy meter (drain/regen + UI bar), ✅ simultaneous-build cap (oldest despawns), ✅ projectile-stick into placed builds.
- 🟡 Build perf (pooling) — meshes pooled per build id. **Left:** off-main-thread gen + LODs (P2).
- 🟡 **Custom voxel authoring (WS4 marquee) — DONE for MVP**: `VoxelTemplate` (bounded grid + compact string serialization, engine-agnostic + unit-tested); a greybox **layer-by-layer IMGUI editor** (`BuildEditorGui` — tap cells per Y-slice, save/clear/use-default, pauses the sim while authoring); saved per player via **PlayerPrefs**; `PlayerState.BuildTemplate` drives `TryGetBuildPlacement`, so the normal BUILD button now places YOUR shape (mesh + collision reuse the same cells; ghost preview works). **Deferred (later phases):** production editor UI + 3D preview (WS9/P2), copy-a-placed-build (P2), backend template storage + replication (WS10/WS11), fairness/anti-turtle (P3+).

### WS5 — AI / Bots
- ✅ **⚠ Bot emits the same `InputCommand`** as a human → vs-bots exercises the real sim path.
- 🟡 Behaviors — spacing (hold range), ballistic aim solve, charge+throw, jab up close: done. **NEW this pass:** LOS/arc-aware **fire discipline** (only commits a throw when the solved arc actually reaches the foe — lobs over cover, won't fling into walls), **spear dodging** (reads spears in flight, side-steps/jumps the incoming line), and **reactive cover-building** (drops a cover build when exposed *and* recently hit, on a cooldown). All data-driven via new `Bot*` config knobs and unit-tested. **Left:** difficulty tiers, smarter build *use* (climbing for height).
- 🟡 **⚠ Navigation over dynamic geometry** — still no real navmesh (deferred), but the bot is no longer purely naive: **stuck-detection** (no-progress → strafe + jump to route around/over walls, pillars, and builds) means it stops grinding on geometry. Full pathing over player-builds remains the flagged WS5 ⚠ item.

### WS6 — Arena & Level Design
- ✅ Greybox arena — central cover wall + pillars, built in code by `Bootstrap`. **NEW:** now **symmetric about z=0** (mirrored pillars + equal-distance spawns) so a 1v1 is fair from either side.
- ✅ Spawn points + **world bounds** — a closed sim-owned perimeter wall (same static-box collision) keeps player and bot inside the greybox (out-of-bounds handling: floor is flat + walls closed, so no fall-out volume needed).
- ⬜ Launch arenas + art pass (P2).

### WS9 — UI / UX (P1 items)
- 🟡 Core HUD — health, build energy, charge bar, hit-count, crosshair + flash, arc toggle, respawn banner (IMGUI greybox). **Left:** production uGUI/UI-Toolkit, FP-peripheral layout, customizable layout.
- 🟡 Bot practice — you already fight a bot; dedicated practice-mode UI later.

### WS11 — Backend & Live Services (P1 items)
- ✅ **BaaS selected: Firebase** (Analytics + Remote Config + later Crashlytics). Best-in-class
  mobile analytics/config, cheap, reversible; pairs with Photon for netcode. Rationale in-session.
- ✅ **Analytics + remote-config SEAM built + unit-tested** (provider-agnostic `IAnalyticsService`
  / `IRemoteConfigService`; `NullBackend` default so the game runs with zero setup and logs events
  to the console). `SimConfigRemote` maps ~28 keys ⇄ `SimConfig` and live-overrides the running
  config (no rebuild to tune). Milestones wired: `app_open`, `match_start/over`, `build_placed`,
  `build_editor_opened`, `custom_template_saved`. 35 sim tests pass.
- 🟡 **Firebase impl written** ([FirebaseBackend.cs], guarded by `SPEARFIGHTER_FIREBASE`). **Left =
  the account/console/SDK/device steps only the owner can do** — see **`firebase_setup.md`** (create
  project, register apps + config files, import SDK, set the define, enter keys, build). Flip the
  define and it's live.
- ⏸ Player-data schema/storage, accounts, economy — Phase 3–4 (add a game backend then; Firebase
  stays for analytics/config).

### WS14 — Testing & QA (P0/P1 items)
- ✅ Automated tests — 17 xUnit tests: charge curve, jab-vs-throw (+drag tiebreak), reduced-sens-while-charging, trajectory↔flight parity, arced stick, ballistic-solved bot aim, movement/landing, ramp walking, wall block, energy drain/regen, build-cap eviction, full determinism (bot in the loop).
- ⏳ **⚠ Combat-feel playtest (every build)** — the primary gate. **Needs you, on a device.** Not yet done.
- ⏳ **⚠ Build+fight feel test in first-person** (the "can you fight inside geometry you can't see around" check) — needs device.
- ⬜ Balance-via-telemetry (needs analytics). ⬜ Unity PlayMode smoke tests for the glue layer.

### Build iterations — delta since the first Unity build (2026-07-09)
*These supersede the matching lines above where they conflict.*
- ✅ **On-device (iPhone) build pipeline (manual).** Unity → Xcode → device works (free
  Apple account, 7-day provisioning). Not automated (that's CI/CD, WS0/WS15, later).
- ✅ **Collision + character controller rewrite → voxel swept-AABB + eased step-up**
  (WS2/WS4). Replaces the special-cased ramp collider. Player is a swept box; auto
  step-up climbs stepped ramps (walk-up now works on device); walls block; jump-over
  emerges. Shared **world voxel grid** = the coordinate system custom builds + netcode use.
- ✅ **Default build → voxel staircase**, generated facing the player; **hold-to-preview,
  release-to-place** with a translucent ghost (WS4). Energy meter + simultaneous cap over cells.
- ✅ **First-person viewmodel** (greybox arm + held spear, lower-right) (WS7/WS2). Muzzle +
  aim-arc originate from the spear tip. *No-clip overlay-camera version deferred* (it threw
  at runtime; non-essential visuals now build last, in try/catch, so they can't abort startup).
- ✅ **HUD**: `Screen.safeArea`-aware (clears the notch), forced landscape, buttons
  bottom-right, resolution-scaled, HP + build-energy bars (WS9).
- ✅ **Controls**: look/move corrected (handedness), and **look-while-holding-BUILD** works
  (drag the build button to aim), matching the attack button.
- ✅ **Bot → real opponent (first pass)** (WS5): LOS/arc-aware **fire discipline** (throws
  only when the solved arc reaches the foe — lobs over cover, no flinging into walls),
  **spear dodging** (side-step/jump the incoming line), **reactive cover-building** (build
  when exposed + recently hit), and **stuck-detection routing** (strafe/jump around
  walls/pillars/builds). New `VoxelWorld.SegmentBlocked` LOS query; new data-driven `Bot*`
  config knobs; +4 xUnit tests (22 total, all green). Full navmesh still deferred.
- ✅ **Arena tightened** (WS6): **symmetric about z=0** (mirrored pillars + equal-distance
  spawns) and a **closed sim-owned bounds perimeter** so nobody walks into the void.
- ✅ **"Glitchy map" ROOT CAUSE found + fixed** (WS7 render): the real bug was a **pipeline/
  shader mismatch** — `Mats` assigned URP (`Universal Render Pipeline/Lit`) shaders, but **no URP
  pipeline asset is assigned**, so the game runs on the **built-in** pipeline. URP shaders under
  built-in don't run their depth pass → opaque geometry stopped writing depth → the ground plane
  sorted over walls in flat chunks that flipped with the camera ("map overlaps itself"). The two
  earlier passes (ground nudge, buried boxes) were band-aids on a broken depth buffer and were
  **reverted**. **Fix:** `Mats` now picks the shader that matches the ACTIVE pipeline — built-in
  `Standard` (opaque, ZWrite on) when no SRP is assigned — so depth writes correctly. Kept:
  interior-face culling (greedy-mesh win), far clip 1000→250, a 2 cm ground nudge. *Needs an
  on-device re-test.*
- ⏸ **URP pipeline — attempted headless, broke rendering, REVERTED** (WS0). A programmatically
  created URP asset (`UniversalRenderPipelineAsset.Create` + bare `UniversalRendererData`) rendered
  only the skybox on device (renderer missing internal resource refs); sim + HUD kept running, so it
  read as "everything invisible, still taking damage." Fully reverted to built-in + `Standard`
  (the confirmed-working state). **Lesson: render-pipeline changes MUST be verified in Play/on device
  before shipping — they can't be validated headlessly.** Redo URP via the **editor wizard** (not
  code) when there's a live-verify loop; `Mats` already auto-switches to URP/Lit when a pipeline is
  active. Built-in is fine for Phase 1.
- ✅ **NPC → articulated greybox humanoid** (WS17): legs + torso + arms + head instead of a
  capsule blob, so the figure has a readable facing/pose. Placeholder for the rigged/animated
  character and the shared PVP body. *(Bot "jumps too much / too strong" is knob-tuning,
  deferred by your call.)*
- ✅ **Match structure — stocks/lives** (WS3): each player has **3 lives** (data-driven
  `MatchLives`); a death costs a life + respawns while stocks remain; lose all 3 → eliminated,
  opponent **wins the match**. Results freeze → **auto-rematch** (resets health/lives/positions,
  clears builds + spears). HUD shows life pips (you + enemy) and a WIN/LOSE banner. All
  sim-owned + unit-tested (25 sim tests). `RespawnDelay` moved into `SimConfig`.

### New tasks & open items (added from this build phase)
- ✅ **Voxel custom-build editor** (WS4 P1) — DONE (MVP): constrained grid + tap-to-fill
  IMGUI editor, bitmask serialization (`VoxelTemplate`), mesh/collider from the same cells,
  per-player PlayerPrefs storage. Production UI + 3D preview + backend storage = later.
- ⬜ **Build-mesh smoothing / greedy-mesh + bevel** so voxel builds read as "enhanced
  Minecraft," not raw cubes (WS7). Collision stays the cells.
- ⬜ **No-clip viewmodel via URP overlay camera** — revisit carefully (it crashed once);
  optional polish (WS7).
- ⬜ **Terrain / world generator** (hills/valleys) — future; generalize the sim's flat
  ground to a heightfield sampler; matching Unity terrain mesh. Architecture left open.
- 🟡 **Bot depth** (WS5) — big pass done: LOS/arc-aware fire discipline, spear dodging,
  reactive cover-building, and stuck-detection routing (strafe/jump around geometry), all
  data-driven + unit-tested. **Left to be a *full* opponent:** difficulty tiers, real
  navmesh over player-builds, and using builds offensively (climb for height). **Needs an
  on-device feel test** to confirm the Phase-1 gate.
- 🔲 **Decision — rotate-build button:** keep it or force builds to face the player?
  (Leaning keep; low-stakes.)
- 🟡 **Movement feel** (WS2) — voxel controller solid; accel/friction curves, coyote time,
  and step-ease tuning still open for feel.

### Still deferred (by scope decision, not oversights)
All backend/analytics/remote-config *backing* (WS11) · netcode + mutable-world sync (WS10,
Phase 3) · art/audio/onboarding/perf (Phase 2) · store/monetization/compliance (Phase 4) ·
stamina/posture (may stay cut).

### Where to continue (recommended next)
Phase 0's feel gate is effectively **passed** (played on-device, mechanics feel good). The
highest-leverage Phase 1 work now, in rough priority:
1. **Bot depth + arena tightening — DONE (2026-07-09).** Fire discipline, dodging, reactive
   cover, stuck-routing; symmetric + bounded arena; all unit-tested. Also **fixed the z-fighting
   "glitchy map"** and gave the **NPC legs**.
2. **Match structure (3-life stocks + win/lose + auto-rematch) — DONE (2026-07-09).** The loop
   now has a beginning/middle/end; it reads as a game, not a sandbox. **Next on the Phase-1
   thread:** on-device feel test of the whole match, then tune `Bot*`/`Match*` knobs.
3. **Voxel custom-build editor (WS4) — DONE (2026-07-09).** `VoxelTemplate` + layer-by-layer
   IMGUI editor + PlayerPrefs storage; the BUILD button now places your authored shape. Needs a
   device visual test of the editor UI.
4. **WS11 analytics + remote-config — the LAST Phase-1 item.** The *seam* (log `SimEvent`s
   locally, load `SimConfig` from JSON) is easy + decision-free; the *backing* needs a
   **load-bearing vendor pick** (Unity Gaming Services / PlayFab / Nakama) + account setup.
   With WS4 done + WS5/WS6/WS9/match-state done, this is all that remains before the Phase-1
   gate ("is 1v1 vs a bot fun in FP without feeling blind?") is fully evaluable.
Plus any small feel/visual tweaks. Full context: `spearfighting_context_and_plan.md`.

---

## Locked spec (decisions from our discussion)

- **Perspective:** first-person.
- **Combat:** thrust-spear as an **arced, charge-to-throw projectile**; **tap = short-range jab**, hold = charge & throw. Hitbox/hurtbox resolution (not simulated weapon physics). No aim-assist in MVP.
- **Aiming aid:** live **dotted trajectory-preview arc**.
- **Building:** **live during combat** (Fortnite-style). Default object = **slanted ramp-wall** (~1 player-height, walkable top, rotatable, chainable for height). **Custom player-authored shapes** via constrained voxel editor, gameplay-relevant collision, copyable between players; fairness/anti-turtle constraints deferred to post-MVP. **Place-only** (no editing placed objects). Gated by a **regenerating energy meter**, single build cost, **cap on simultaneous builds**.
- **Projectile-miss:** spear **sticks** into build or floor. **No destruction** for now.
- **Characters:** the opponent body (NPC bot *and* future PVP players — identical to the sim)
  is an **articulated humanoid with legs**, not a blob, on **one shared humanoid rig** so
  cosmetic skins retarget cleanly. The local player is seen as a **first-person viewmodel**
  (arms + held spear). **Animation is sim-driven** (pose follows sim position/velocity/phase/
  health) and **never authoritative** — it can't feed back into movement or hits (protects the
  sim/render split). Greybox segmented primitives now; rigged + skinned art is Phase 2.
- **Netcode:** **server-authoritative** state-sync + client prediction + lag compensation. **Outsourced/managed hosting** (no self-managed servers). Real-time PvP is the goal; **MVP plays vs bots** on the real netcode architecture.
- **Format:** **1v1** MVP; architecture must not preclude solo/FFA/teams later.
- **Business:** **F2P, cosmetics-only** in-game store. No ads. No pay-to-win.
- **Min-spec:** **2021 mid-range and up** (Adreno 619–642L class, 4GB RAM floor, Vulkan 1.1/GLES 3.2/Metal, FHD+). 60 fps target, 30 fps floor, tuned for sustained (throttled) perf.

## Recommended tech stack (baked into tasks below)

- **Engine:** Unity 6.x LTS, C#, **URP** (mobile-appropriate render pipeline).
- **Netcode:** **Photon Fusion** (server/host topology — server-authoritative, prediction, interpolation, lag comp). *Not* Photon Quantum: Quantum is for deterministic rollback, which your non-deterministic server-authoritative model doesn't need. Fusion is the fit.
- **Hosting:** Photon Cloud / Fusion-hosted, or Unity Multiplay — evaluate; both are managed (no servers to run yourself).
- **Backend (BaaS):** evaluate Unity Gaming Services vs PlayFab vs Nakama for accounts, storage, matchmaking, leaderboards, analytics, remote config.
- **Source control:** Git + Git LFS, or Unity Version Control for large binaries.

---

# WORKSTREAM 0 — Project Foundation & Architecture

- **[P0]** Create Unity 6.x LTS project on the URP mobile template; configure iOS + Android build targets, graphics APIs (Vulkan/GLES3.2/Metal), color space, texture compression (ASTC).
- **[P0]** Source control: repo, Git LFS (or Unity VC), branching strategy, `.gitignore`, large-binary policy.
- **[P0]** CI/CD: automated cloud builds (Unity Build Automation / GitHub Actions / Codemagic) → TestFlight + Play internal track; versioning convention.
- **[P0]** Project management: issue tracker, milestone board, task naming, definition-of-done.
- **[P0]** Code architecture: assembly definitions, folder structure, coding standards.
- **[P0]** ⚠ **Simulation/rendering separation.** Structure the game so the *simulation* (movement, combat, builds, projectiles) is a self-contained, input-driven, fixed-tick system decoupled from rendering. This is the single most important architectural task — it's what lets bots, replays, and netcode all feed the same simulation, and what keeps server-authoritative PvP addable without a rewrite. Do this before any gameplay code.
- **[P0]** Input abstraction: a single input struct that a human, a bot, or the network can all produce, consumed identically by the simulation.
- **[P0]** Data-driven config: expose all tunables (combat, build, economy) as ScriptableObjects / remote-config-backed values so designers tune without code changes.
- **[P0]** Integrate SDK stubs early: Fusion, BaaS, analytics — even as no-ops — so their assumptions shape the architecture from the start.

# WORKSTREAM 1 — Input & Controls (Scheme B)

- **[P0]** Left virtual movement stick (thumb-friendly, dead zone, floating vs fixed).
- **[P0]** Right-side look: touch-drag → camera delta, with two sensitivity profiles.
- **[P0]** ⚠ **Draggable attack button with sensitivity swap.** Press = start charge; while held, dragging the right thumb aims at **reduced (fine-aim) sensitivity**; release = throw; quick tap (no meaningful drag/hold) = jab. Disambiguate tap-vs-charge with a hold threshold. This gesture *is* the core control feel — prototype and tune it first.
- **[P0]** Charge state machine: charge fraction $c \in [0,1]$ over hold time, feeding throw power (WS3).
- **[P0]** Jump button (right side).
- **[P0]** Build button (right side, adjacent to attack).
- **[P1]** Multitouch handling, palm rejection, input latency minimization.
- **[P1]** Customizable HUD layout: reposition/resize/opacity for all controls; left/right-hand mode.
- **[P1]** Haptics hooks: charge tick, throw, hit-confirm, land, build-place, meter-full.

# WORKSTREAM 2 — Character & Movement

- **[P0]** First-person camera rig: FOV tuning, head-bob toggle, comfort/motion-reduction options.
- **[P0]** Kinematic capsule character controller: move, acceleration/friction, air control, grounded feel (grounded + committed lunge).
- **[P0]** Jump: fixed or variable height, gravity, coyote/land handling.
- **[P0]** ⚠ **Slope/ramp traversal.** Your default build is a *walkable ramp*, so the controller must climb slopes smoothly and let the jump gain height off ramps. Get this right or the whole build-for-verticality loop breaks.
- **[P1]** Contextual vault/mantle over builds.
- **[P0]** Movement tuning harness (iterate feel against bots).
- **[P2]** Animation: viewmodel (arms + spear) idle/locomotion/jump/jab/throw-windup/release; enemy character locomotion + hit reactions + death ragdoll.

# WORKSTREAM 3 — Spear Combat System

- **[P0]** Charge → power model: $v_0 = v_{\min} + (v_{\max}-v_{\min})\,c$; optionally charge also scales damage/range.
- **[P0]** ⚠ **Arced projectile.** Spawn from viewmodel on release; gravity-driven; range $R = \dfrac{v_0^2 \sin(2\theta)}{g}$. Spin/orient the spear along its velocity.
- **[P0]** ⚠ **Trajectory-preview arc.** Predict landing path from current aim + charge each frame; render as a dotted line (pooled dots / line renderer); live-update while charging; togglable. This is your primary aiming aid given arc + touch — it's not optional polish, it's core usability.
- **[P0]** Throw release → spawn projectile (predicted locally; server-authoritative once networked).
- **[P0]** Jab (tap): short-range instant thrust hitbox, no projectile.
- **[P0]** Hit detection: spear-tip hitbox vs enemy hurtbox overlap; damage on hit; (lag-compensated server rewind added in WS10).
- **[P0]** Health + stamina/posture systems; TTK philosophy (medium-long; counterplay exists).
- **[P0]** ⚠ **Projectile-miss = stick.** On miss, embed the spear into the hit build/floor surface (spawn stuck prop, align to surface normal). No destruction. (Decide later if stuck spears are retrievable/ammo-relevant.)
- **[P1]** Death & round-based respawn.
- **[P0]** All combat values data-driven for live tuning.

# WORKSTREAM 4 — Building System

### Default ramp-wall
- **[P0]** Ramp-wall prefab: ~1 player-height, walkable top face, collision, placeholder art.
- **[P0]** ⚠ **Placement system:** grid/snap, ghost preview, rotate, valid-placement checks (no overlap into players/illegal geometry, within reach), confirm-place.
- **[P0]** **Energy meter:** drain per build, regen rate, meter UI.
- **[P0]** **Simultaneous-build cap:** oldest build despawns when the player exceeds the cap (also bounds worst-case scene cost for perf).
- **[P0]** Projectile-stick interaction with placed builds.

### Custom build authoring (voxel editor) ⚠
- **[P1]** Constrained voxel grid (e.g. $8^3$) inside the standard bounding box.
- **[P1]** Editor UI: tap voxels to fill/clear, orbit/preview, save template.
- **[P1]** Serialization: compact bitmask format for a template.
- **[P1]** ⚠ Mesh generation from voxels (greedy meshing + bevel/stylized shader so it reads as "enhanced Minecraft, not raw cubes").
- **[P1]** ⚠ Collider generation from voxels (merged/box colliders); **must be reconstructable identically on the server** for authoritative hit-validation.
- **[P1]** Template storage per player (backend, WS11).
- **[P1]** Replication: send template/bitmask on placement; client + server rebuild identical mesh + collider.
- **[P2]** "Copy this build": inspect a placed build and clone it into your editor.
- **[P3+]** ⚠ **Fairness / anti-turtle validation** (deferred): min-opening-size, no-fully-enclosed-volume, build HP/decay, etc. Stub the hook now; implement as a real balance workstream post-MVP. (Note: arc projectiles + meter + build cap already provide partial natural mitigation.)
- **[P2]** Performance: mesh/collider pooling, generation off the main thread, LODs.

# WORKSTREAM 5 — AI / Bots (MVP opponent)

- **[P1]** ⚠ **Bots feed the same input struct as humans** and run inside the real simulation/netcode path — so playing vs bots validates the actual architecture, not a throwaway.
- **[P1]** Behaviors: navigate, manage spacing (approach/retreat for spear range), aim + charge + throw arcs, jab up close, build cover, react to incoming.
- **[P1]** ⚠ **Navigation over dynamic geometry.** Players build mid-fight, so the walkable space changes at runtime — requires runtime navmesh updates or a dynamic-avoidance approach. Non-trivial; scope early.
- **[P1]** Difficulty tuning + practice-mode bots.

# WORKSTREAM 6 — Arena & Level Design

- **[P0]** Greybox arena: size for 1v1, symmetric/fair, sightlines, cover, verticality that rewards ramp-building + jumping.
- **[P0]** Spawn points, bounds, out-of-bounds handling.
- **[P2]** 1–3 launch arenas.
- **[P2]** Arena art pass (after style is locked in WS7).

# WORKSTREAM 7 — Art Production ("enhanced Minecraft, not rectangles")

- **[P1]** Reference board (PureRef) + concept/mood exploration (AI mood-gen for 3D feel; Claude Design for UI/HUD/store mockups). *Internal reference only — check licensing before any AI-generated art ships in-product.*
- **[P1]** ⚠ **Concrete style spec:** palette (hex), silhouette rules (bevel/chunk), shading model (toon/flat/gradient), lighting recipe, per-object poly/texture budgets tied to min-spec. Budget a contract concept/technical artist to convert mood → reproducible rules + first shader if you lack the expertise.
- **[P2]** Custom stylized/toon URP shader (defines the look cheaply).
- **[P2]** Modular environment kit (bevelled/organic, not pure cubes).
- **[P2]** Hero assets: viewmodel (arms+spear), enemy character, spear projectile, ramp-wall, stuck-spear prop.
- **[P2]** Voxel-build render styling (smoothing/bevel so custom builds match the look).
- **[P2]** VFX: charge glow, throw trail, hit spark, stick impact, build-spawn poof, landing dust — readability over realism.
- **[P2]** LODs, texture atlases, ASTC compression.
- **[P2]** Lighting: baked + light probes; minimal real-time shadows (perf).

# WORKSTREAM 8 — Audio

- **[P2]** SFX: charge, throw whoosh, spear-stick variants (flesh/wood/ground), jab, footsteps, jump/land, build-place, meter-full, UI.
- **[P2]** 3D/spatial audio for opponent cues (attack/build direction).
- **[P2]** Music: menu, combat, tension states.
- **[P2]** Haptics map (paired with SFX).
- **[P2]** Middleware decision (Unity audio for MVP; FMOD/Wwise if ambitions grow); mobile mixing, mute-switch respect, memory budget.

# WORKSTREAM 9 — UI / UX

- **[P1]** HUD: health, stamina, build meter, charge indicator, trajectory toggle — minimal, peripheral, FP-friendly (center clear).
- **[P1]** Customizable control layout (from WS1).
- **[P2]** Menus: main, mode-select (1v1 now, extensible), loadout (spear skins, build templates), settings, store shell.
- **[P2]** ⚠ **Onboarding/tutorial:** teach look/charge/throw/jab, jab-vs-throw, building + meter, and **arc aiming via the trajectory preview**. Undervalued and retention-critical — a novel control scheme + novel build loop both need teaching.
- **[P1]** Bot practice mode.
- **[P2]** Accessibility: colorblind-safe cues, text scaling, motion reduction, haptic toggles, one-handed considerations.
- **[P2]** Results / progression screens.

# WORKSTREAM 10 — Networking & Multiplayer (server-authoritative) ⚠

> **Sprint-level breakdown: `spearfighting_netcode_plan.md`** — why it's the hardest, the
> mutable-world sync approach (replicate build *events*, rebuild deterministically), the
> server-auth/prediction/reconciliation/lag-comp model, the load-bearing decisions (D1–D6),
> and the phased plan WS10.0–10.8 with the two-device build-and-fight spike done EARLY.

- **[P3]** Integrate Photon Fusion in server/host mode: server-authoritative state, client prediction + reconciliation (local player), interpolation (remote), lag compensation (hit validation via server rewind).
- **[P3]** Managed hosting integration (Photon Cloud/Fusion-hosted or Unity Multiplay); region deployment.
- **[P3]** Network the systems: player state, predicted movement, **projectiles (predicted client-side, hits validated server-side with rewind)**, meter.
- **[P3]** ⚠⚠ **Mutable-world sync — the project's hardest technical core.** Replicate player-built geometry (voxel templates) fast; keep authoritative collision on the server; prevent build-desync; handle placement/despawn under the simultaneous-build cap; support late-join/reconnect world state. This — not the spear — is where difficulty concentrated once you chose live building. Prototype a two-device build-and-fight *before* investing in polish.
- **[P3]** Matchmaking + lobbies (region-locked for latency), session management, reconnection/host-migration.
- **[P3]** Anti-cheat: server authority as the backbone, input validation, movement/build sanity checks. (Deterministic replay isn't available in this model — server authority is your defense.)
- **[P3]** ⚠ **Netcode test harness:** artificial latency/jitter/packet-loss injection; two-device and cross-region tests; desync detection. Build this alongside the netcode, not after.
- **[P3]** Confirm bots run inside the netcode path (so vs-bots exercises the real stack).

# WORKSTREAM 11 — Backend & Live Services

- **[P1]** BaaS selection (Unity Gaming Services / PlayFab / Nakama).
- **[P3]** Accounts/auth: Apple + Google sign-in + guest.
- **[P1]** Player-data schema & storage: progression, owned cosmetics, **build templates**, settings.
- **[P1]** ⚠ **Analytics/telemetry from the first playtest:** match outcomes, TTK, build usage, quit points, funnel. You cannot tune combat/build balance blind.
- **[P1]** ⚠ **Remote config from day one:** live-tune combat/build/economy values without shipping a build.
- **[P4]** Leaderboards, seasons; live-ops content flags/tooling.

# WORKSTREAM 12 — Progression, Economy, Store, Monetization

- **[P4]** Cosmetics catalog: spear skins, build-template skins, viewmodel/character skins, VFX/trails, arena themes, emotes.
- **[P4]** In-game store: catalog, currencies (soft-earn / hard-buy), purchase flow.
- **[P4]** IAP integration (App Store / Play Billing). *Payment entry handled entirely by the platform, never by us.*
- **[P4]** Progression: cosmetic unlocks + season/battle-pass; **gameplay unlocks are earnable, never buyable** (no pay-to-win).
- **[P4]** Economy balancing (sinks/sources). **No ads.**
- **[P4]** ⚠ Reality check baked in: cosmetics-only is a **volume model** — it only funds the game at scale, so retention/UA (WS9 onboarding, WS16 live-ops) are load-bearing for the *business*, not just the fun.

# WORKSTREAM 13 — Performance & Optimization (min-spec: 2021 mid-range)

- **[P2]** Target tier: Adreno 619–642L, 4GB RAM floor, Vulkan 1.1/GLES 3.2/Metal, FHD+. **60 fps target, 30 fps floor, tuned for sustained (post-throttle) perf.** Bar = "no heavier than a competitive mobile shooter at medium."
- **[P2]** **Starting perf budgets (validate on a real device, not gospel):** ~80–150k visible tris; ~60–120 draw calls; a few hundred MB texture memory; minimal real-time lights/shadows; near-zero per-frame GC allocation. Adjust against profiling.
- **[P2]** Quality-settings toggle: resolution scale, effect density, shadow on/off, build render detail — one build serves flagships and the floor.
- **[P2]** Worst-case scene control: simultaneous-build cap, projectile pooling, voxel-mesh/collider pooling, LOD, occlusion.
- **[P2]** ⚠ Thermal + battery profiling; **frame-time stability** (jitter wrecks charge/throw timing far more than a lower stable framerate).
- **[P2]** Memory/load-time budgets; Addressables for content.

# WORKSTREAM 14 — Testing & QA

- **[P0→all]** ⚠ **Combat-feel playtest loop every build** — the primary gate. If the charge-aim-throw + jab don't feel good, nothing downstream matters.
- **[P1]** ⚠ **Build+fight feel test, in first-person specifically** — the claustrophobia/awareness check (can you fight competently inside geometry you can't see around?). This is where FP either survives or gets reconsidered.
- **[P3]** Netcode testing (latency sim, desync, cross-region).
- **[P2]** Device/perf testing on the named min-spec device + a small matrix; thermal soak.
- **[P2]** Exploit testing: turtle/tent builds, out-of-bounds, build-spam, projectile edge cases.
- **[P1]** Balance via telemetry.
- **[P1]** Automated tests: simulation/combat math, input, state machines, smoke tests in CI.
- **[P4]** Store-compliance + accessibility QA.

# WORKSTREAM 15 — Platform, Build, Deployment, Compliance, Launch

- **[P4]** iOS + Android build pipelines (lead-platform-first for cleaner QA).
- **[P4]** Store setup: App Store Connect, Play Console; listings, art, **content ratings (IARC — a combat game has implications)**.
- **[P4]** ⚠ **Compliance:** privacy policy, data-safety / privacy-nutrition labels, IAP rules. **Age rating / COPPA / GDPR-K is deferred by your call but MUST be revisited before launch** — if under-13s can play, it reshapes data collection, analytics, and store behavior, and it's legally non-optional.
- **[P4]** Beta tracks: TestFlight, Play internal/closed/open; staged rollout.
- **[P4]** Certification lead time + resubmit buffer; launch checklist; rollback plan; live monitoring.

# WORKSTREAM 16 — Live-Ops (post-launch)

- **[P5+]** Seasons, cosmetic drops, new arenas, new build primitives/skins.
- **[P5+]** ⚠ Balance patches — **the build-fairness/anti-turtle constraints land here** as a real, ongoing workstream; remote-config tuning.
- **[P5+]** Community + feedback loop; anti-cheat iteration.
- **[P5+]** New modes (solo / FFA / teams) — architecture already supports 1v1→N.

---

# COMPREHENSIVENESS PASS — added 2026-07-09

*Gap analysis of WS0–WS16 vs "everything a shipped, networked F2P mobile game actually needs."
WS0–WS16 covered the game systems well but under-specified the **production spine**: character
art/animation, devops/observability, persistence/localization, trust-&-safety/anti-cheat depth,
retention/notifications, and UA/marketing. Those are WS17–WS22 below. Nothing here changes the
locked design; it makes the plan complete. Phase tags fold these into the existing milestones.*

# WORKSTREAM 17 — Characters, Rigs & Animation ⚠

*The opponent body is shared by the NPC bot and (later) PVP humans — they're identical to the
sim — so this is built once and reused. Legs, not a blob. Animation is **sim-driven and never
authoritative** (pose follows sim state; it must not feed back into movement/hits).*

- **[P1]** Greybox articulated humanoid (legs/torso/arms/head) with a readable facing/pose. ✅ *done (primitives).*
- **[P2]** ⚠ **Character concept + silhouette design** for the "enhanced Minecraft" look: chunky,
  beveled, saturated/toon-shaded, with a silhouette readable **at distance in first-person**
  (you must instantly parse the enemy's facing, stance, and charge/throw telegraph).
- **[P2]** ⚠ **One shared humanoid rig** (Unity Humanoid/Mecanim, mobile-appropriate bone count)
  so cosmetic skins **retarget** onto the same skeleton. Skinned mesh + weights; hands that hold a spear.
- **[P2]** ⚠ **Animation set**, all driven by sim state (position/velocity/`AttackPhase`/grounded/health):
  idle, locomotion **blend tree** (walk/run + strafe/back keyed off sim velocity), jump/fall/land,
  **charge-windup → throw-release**, jab, hit-react, death (ragdoll or canned). **Root motion OFF** — the sim owns movement.
- **[P2]** First-person **viewmodel animation** (arms + spear): idle sway, charge windup, throw,
  jab, walk bob — must line up with the muzzle/arc origin. (Greybox viewmodel exists.)
- **[P2]** ⚠ **Third-person legibility hooks** the sim exposes for animation/VFX: a clean read of
  charge fraction, jab vs throw, grounded/airborne, hit/death — so the visual telegraph is honest.
- **[P2]** LOD + bone/poly budgets tied to min-spec; skin/attachment system for cosmetics (WS12).
- **[P3]** Network the pose seam: remote players animate from **replicated sim state**, not local input
  (interpolated) — confirm no animation path can desync authoritative state.

# WORKSTREAM 18 — DevOps, Infrastructure & Observability ⚠

*The catalog had CI/CD as a stub. A live, networked, F2P game needs a real production spine.*

- **[P1]** CI: PR gate runs `dotnet test` (sim) + Unity EditMode/PlayMode smoke on push; block merge on red.
- **[P2]** CD: cloud builds (Unity Build Automation / GitHub Actions / Codemagic) → TestFlight + Play internal; **semantic versioning + build numbers**; signed builds; symbol upload.
- **[P2]** Environments: **dev / staging / prod** separation for backend + remote config; per-env secrets management (never in the repo); config to point a build at an env.
- **[P2]** ⚠ **Crash + error reporting** (Unity Cloud Diagnostics / Sentry / Backtrace): client crashes, exceptions, ANRs, with symbolication. **Non-negotiable before a public beta.**
- **[P3]** Server/service observability (Photon/BaaS): dashboards, alerting (latency, error rate, CCU, match-fail rate), log aggregation, on-call/runbook.
- **[P3]** ⚠ **Cost monitoring + budget alerts** (Photon CCU, BaaS ops, bandwidth) — a cosmetics-only volume model dies if infra cost/DAU is unmodeled.
- **[P2]** Feature flags / kill switches (ties into remote config, WS11) so a bad system can be disabled without a store resubmit.

# WORKSTREAM 19 — Persistence, Settings & Localization

- **[P1]** Local settings persistence: control layout, sensitivity, audio, accessibility, quality — survive reinstall via cloud save where possible.
- **[P1]** ⚠ **Save-data schema versioning + migration** (client and server): never brick a returning player when the data shape changes.
- **[P3]** Cloud save + **account linking** (guest → Apple/Google), conflict resolution, device transfer.
- **[P2]** ⚠ **Localization / i18n**: externalize ALL strings, font/glyph coverage (CJK), locale formatting, RTL check, and localized store listings; pick priority locales. Retrofitting this late is expensive.
- **[P2]** Content/asset pipeline: **Addressables** for cosmetics + arenas; author → ingest → remote content delivery, with versioning so cosmetics ship without a client update.

# WORKSTREAM 20 — Trust, Safety, Anti-Cheat & Legal ⚠

*Server authority (WS10) is the backbone; this is the depth around it. Much is legally mandatory once real players + real money are involved.*

- **[P3]** ⚠ **Anti-cheat plan** beyond server authority: input/rate sanity limits, movement/build validity checks server-side, speed/teleport/build-spam detection, and **replay/report review** (the deterministic sim makes server-side replay verification cheap — leverage it).
- **[P3]** Player reporting + block; if any names/UGC (build-template names, display names) exist, a **moderation/profanity** path and a ban/enforcement system.
- **[P4]** ⚠ **Legal/compliance (mandatory):** Privacy Policy, ToS/EULA, data-safety / privacy-nutrition labels, IARC content rating (combat game), IAP rules. **COPPA/GDPR-K age-gate MUST be resolved before launch** — if under-13s can play it reshapes analytics + data collection.
- **[P4]** ⚠ **IAP receipt validation server-side** (fraud), refund/chargeback handling, purchase-restore.
- **[P2]** Data-minimization + consent (analytics opt-in where required); a data-deletion path (GDPR/CCPA "delete my data").

# WORKSTREAM 21 — Retention, Notifications & Social

*Cosmetics-only is a **volume** business (§WS12) — retention is load-bearing for the business, not a nicety.*

- **[P4]** Push notifications + re-engagement (season start, daily reward, "your rival is online") with opt-in/quiet-hours; deep links back into the right screen.
- **[P4]** Daily/weekly engagement loops: login rewards, quests/challenges, streaks — all **cosmetic/earnable, never pay-to-win**.
- **[P4]** Lightweight social: friends/invite, "play again", rematch; party/1v1-challenge-a-friend (small for MVP, architecture already supports 1v1→N).
- **[P5+]** Leaderboards/seasons (already in WS11/WS16), spectate/replay-share (deterministic sim → cheap replays), clips.

# WORKSTREAM 22 — Marketing, UA & Store Optimization (business)

*Out of "make the game work," but part of "ship a functioning F2P product." Called out so it isn't a launch-week surprise.*

- **[P4]** Store presence: **ASO** (title, keywords, screenshots, preview video), App Store Connect + Play Console listings, feature-graphic/press kit.
- **[P4]** Capture/marketing tooling: in-engine screenshot/replay capture, a "cinematic" camera for trailers.
- **[P4]** UA plan + attribution SDK (with privacy constraints: ATT on iOS, Play install referrer), creative testing; model **LTV vs CPI** for a cosmetics volume business.
- **[P5+]** Community: Discord/socials, feedback intake, content-creator/beta program.

---

# Phase sequencing (how the workstreams stack into milestones)

- **Phase 0 — Combat-feel prototype.** WS0 foundation + WS1 controls + WS2 movement + WS3 combat, all vs a dumb bot, greybox art. **Gate: is charge-aim-throw + jab + movement fun with two thumbs in first-person?** Everything stops here if no.
- **Phase 1 — Core loop vs bots.** WS4 building (default staircase + meter + cap, then voxel editor) + WS5 bots + WS6 greybox arena + WS9 core HUD + WS11 analytics/remote-config/data + **match/round state (win-lose, score, match flow)** + WS18 CI gate + WS17 greybox humanoid. **Gate: does live building + spear combat form a fun 1v1 match vs a real bot, in FP, without feeling blind?**
- **Phase 2 — Art, audio, polish, perf.** WS7 art + **WS17 character rig + animation** + WS8 audio + WS9 menus/tutorial + WS13 optimization to the min-spec floor + WS19 localization/persistence + WS18 crash reporting + WS14 device/exploit testing. **Gate: looks and runs like a real game on a 2021 mid-range phone.**
- **Phase 3 — Real-time multiplayer.** WS10 netcode + mutable-world sync + matchmaking + WS20 anti-cheat + WS11 accounts + WS18 server observability/cost. **Gate: fair, responsive 1v1 across regions, builds and projectiles in sync.**
- **Phase 4 — Store, monetization, compliance, launch prep.** WS12 economy/store + WS15 + WS20 legal/compliance + WS21 retention/notifications + WS22 ASO/UA. **Gate: submittable, compliant, monetized.**
- **Phase 5+ — Launch & live-ops.** WS16 (+ WS21 social/seasons, WS22 community).

# Critical path — the two things that gate the whole project

1. **Combat feel (Phase 0).** The novel control scheme is unproven until a thumb proves it. Build it first; be willing to kill or pivot fast.
2. **Mutable-world netcode (Phase 3, WS10).** Live building + destruction-free stick mechanics are fine offline; replicating a *player-mutated world* authoritatively in real time on mobile is where Fortnite spent its fortune. It's the hardest system here and the one most likely to blow estimates — prototype a two-device build-and-fight the moment Phase 3 opens, before any netcode polish.

Everything else is execution around those two.

---

*Reference tasks by workstream number. When you're ready, we can expand any single workstream into a sprint-level task breakdown — WS10 (netcode) and WS4 (custom voxel builds) are the two I'd detail first, since they carry the most hidden work.*
