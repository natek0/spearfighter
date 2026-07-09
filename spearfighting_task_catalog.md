# Spearfighting Game — Full Task & Objective Catalog

*Every workstream broken into concrete tasks, with all locked decisions baked in. Organized by system (for completeness), then sequenced into phases at the end (for order). Tags: **[P0]–[P6]** = the phase a task primarily lands in. ⚠ = deceptively large or high-risk; respect it early.*

---

# IMPLEMENTATION STATUS — updated 2026-07-09

*Legend: ✅ done · 🟡 partial (started; note says what's left) · ⬜ not started · ⏸ deferred by scope decision. Status covers P0/P1 only (the phases we're building). The task spec itself is unchanged and lives below this section.*

**What runs today:** an engine-agnostic, fixed-tick, input-driven **simulation** (`unity/Assets/Spearfighter/Simulation/`, no `UnityEngine` dependency) with a **17-test xUnit suite that passes** (`cd sim && dotnet test`), plus the **Unity 6.5 view/input/HUD glue** (`unity/Assets/Spearfighter/Game/`) and a code-driven `Bootstrap` that runs the full loop from one component on an empty scene. Not yet feel-tested on a device — that's the open Phase 0 gate.

### WS0 — Foundation & Architecture
- ✅ **⚠ Simulation/rendering separation** — the centerpiece. `Simulation.Tick(commands)` is fixed-tick, engine-agnostic; rendering is strictly downstream. `noEngineReferences` on the sim asmdef *enforces* it at compile time.
- ✅ Input abstraction — one `InputCommand` struct; human, bot, (future) network all produce it, consumed identically.
- ✅ Data-driven config — `SimConfig` POCO + `SimConfigAsset` ScriptableObject. (Remote-config *backing* is WS11.)
- ✅ Code architecture — two asmdefs (Simulation / Game), clear folder split, conventions.
- ✅ Source control — git repo + `.gitignore` (Unity+dotnet), pushed to GitHub `main`. (Git LFS + formal branching strategy: not needed yet.)
- 🟡 Unity project — 6.5 scaffold (manifest w/ URP, `ProjectVersion`, asmdefs) done. **Left:** assign URP pipeline asset, switch on iOS/Android build targets, color space + ASTC. (Materials fall back to Standard so Play works meanwhile.)
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
- ✅ Health + damage + medium TTK. ✅ Death & round-based respawn. ✅ all combat values data-driven.
- 🟡 **Stamina/posture system is NOT implemented** — health-only for now (deliberate; revisit when tuning counterplay).

### WS4 — Building (default ramp-wall)
- ✅ Ramp-wall — procedural walkable wedge mesh (visual reuses the sim's exact collision surface), ~1 player-height.
- ✅ **⚠ Placement** — grid snap, 90° rotate, live ghost preview, reach clamp, no-bury-a-player check, one-tap place.
- ✅ Energy meter (drain/regen + UI bar), ✅ simultaneous-build cap (oldest despawns), ✅ projectile-stick into placed builds.
- 🟡 Build perf (pooling) — meshes pooled per build id. **Left:** off-main-thread gen + LODs (P2).
- ⏸ **Custom voxel authoring (whole sub-workstream) — deferred this pass**: voxel grid, editor UI, bitmask serialization, mesh gen, collider gen, per-player storage, replication. (Copy-build P2, fairness/anti-turtle P3+ also deferred.)

### WS5 — AI / Bots
- ✅ **⚠ Bot emits the same `InputCommand`** as a human → vs-bots exercises the real sim path.
- 🟡 Behaviors — spacing (hold range), ballistic aim solve, charge+throw, jab up close, occasional build: done. **Left:** meaningful reaction to incoming (dodge/block cover), difficulty tiers.
- ⬜ **⚠ Navigation over dynamic geometry** — intentionally naive (straight-line approach/retreat + jump). Real navmesh-over-player-builds is flagged and deferred.

### WS6 — Arena & Level Design
- ✅ Greybox arena — cover wall + pillars (mirrors the validated prototype), built in code by `Bootstrap`.
- 🟡 Spawn points done. **Left:** world bounds + out-of-bounds handling.
- ⬜ Launch arenas + art pass (P2).

### WS9 — UI / UX (P1 items)
- 🟡 Core HUD — health, build energy, charge bar, hit-count, crosshair + flash, arc toggle, respawn banner (IMGUI greybox). **Left:** production uGUI/UI-Toolkit, FP-peripheral layout, customizable layout.
- 🟡 Bot practice — you already fight a bot; dedicated practice-mode UI later.

### WS11 — Backend & Live Services (P1 items)
- 🟡 Remote config — `SimConfig` is the data-driven seam it plugs into; **no remote backing yet**.
- ⏸ BaaS selection, player-data schema/storage, **⚠ analytics/telemetry** — vendor-blocked; deferred until there's a running game to measure. (`SimEvent` stream is the natural analytics hook.)

### WS14 — Testing & QA (P0/P1 items)
- ✅ Automated tests — 17 xUnit tests: charge curve, jab-vs-throw (+drag tiebreak), reduced-sens-while-charging, trajectory↔flight parity, arced stick, ballistic-solved bot aim, movement/landing, ramp walking, wall block, energy drain/regen, build-cap eviction, full determinism (bot in the loop).
- ⏳ **⚠ Combat-feel playtest (every build)** — the primary gate. **Needs you, on a device.** Not yet done.
- ⏳ **⚠ Build+fight feel test in first-person** (the "can you fight inside geometry you can't see around" check) — needs device.
- ⬜ Balance-via-telemetry (needs analytics). ⬜ Unity PlayMode smoke tests for the glue layer.

### Immediate deferred set (by our scope decision, not oversights)
Voxel custom-build editor · all backend/analytics/remote-config *backing* · netcode (WS10, Phase 3) · art/audio/perf (Phase 2) · store/monetization/compliance (Phase 4).

### How to proceed → see the **Phase sequencing** and **Critical path** sections at the bottom of this file.
The gating next step is **Phase 0's feel gate**: open the Unity project on your phone (or Device Simulator) and confirm charge-aim-throw + jab + movement feels good with two thumbs. Everything else waits on that answer.

---

## Locked spec (decisions from our discussion)

- **Perspective:** first-person.
- **Combat:** thrust-spear as an **arced, charge-to-throw projectile**; **tap = short-range jab**, hold = charge & throw. Hitbox/hurtbox resolution (not simulated weapon physics). No aim-assist in MVP.
- **Aiming aid:** live **dotted trajectory-preview arc**.
- **Building:** **live during combat** (Fortnite-style). Default object = **slanted ramp-wall** (~1 player-height, walkable top, rotatable, chainable for height). **Custom player-authored shapes** via constrained voxel editor, gameplay-relevant collision, copyable between players; fairness/anti-turtle constraints deferred to post-MVP. **Place-only** (no editing placed objects). Gated by a **regenerating energy meter**, single build cost, **cap on simultaneous builds**.
- **Projectile-miss:** spear **sticks** into build or floor. **No destruction** for now.
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

# Phase sequencing (how the workstreams stack into milestones)

- **Phase 0 — Combat-feel prototype.** WS0 foundation + WS1 controls + WS2 movement + WS3 combat, all vs a dumb bot, greybox art. **Gate: is charge-aim-throw + jab + movement fun with two thumbs in first-person?** Everything stops here if no.
- **Phase 1 — Core loop vs bots.** WS4 building (default ramp-wall + meter + cap, then voxel editor) + WS5 bots + WS6 greybox arena + WS9 core HUD + WS11 analytics/remote-config/data. **Gate: does live building + spear combat form a fun 1v1 match vs a real bot, in FP, without feeling blind?**
- **Phase 2 — Art, audio, polish, perf.** WS7 art + WS8 audio + WS9 menus/tutorial + WS13 optimization to the min-spec floor + WS14 device/exploit testing. **Gate: looks and runs like a real game on a 2021 mid-range phone.**
- **Phase 3 — Real-time multiplayer.** WS10 netcode + mutable-world sync + matchmaking + anti-cheat + WS11 accounts. **Gate: fair, responsive 1v1 across regions, builds and projectiles in sync.**
- **Phase 4 — Store, monetization, compliance, launch prep.** WS12 economy/store + WS15 platform/compliance/beta. **Gate: submittable, compliant, monetized.**
- **Phase 5+ — Launch & live-ops.** WS16.

# Critical path — the two things that gate the whole project

1. **Combat feel (Phase 0).** The novel control scheme is unproven until a thumb proves it. Build it first; be willing to kill or pivot fast.
2. **Mutable-world netcode (Phase 3, WS10).** Live building + destruction-free stick mechanics are fine offline; replicating a *player-mutated world* authoritatively in real time on mobile is where Fortnite spent its fortune. It's the hardest system here and the one most likely to blow estimates — prototype a two-device build-and-fight the moment Phase 3 opens, before any netcode polish.

Everything else is execution around those two.

---

*Reference tasks by workstream number. When you're ready, we can expand any single workstream into a sprint-level task breakdown — WS10 (netcode) and WS4 (custom voxel builds) are the two I'd detail first, since they carry the most hidden work.*
