# First-Person Mobile Spearfighting Game — Comprehensive Build & Deployment Plan

*A working catalog of every category, task, and decision required to make and ship the game. Each section presents the real options, their trade-offs, a recommendation, and — where relevant — the hidden assumption or risk that most people underestimate.*

**Working concept (assumption — confirm before proceeding):** A first-person, arena PvP spear-combat game for iOS/Android. Players move, jump, and place **obstacles** to shape the arena (cover, chokepoints, elevation), then fight with spears. Combat is thrust-centric. If any of that is wrong, sections 1, 2, 4, 5, and 10 change materially.

---

## 0. How to read this document

This is not a linear to-do list. Roughly six decisions are **load-bearing** — they cascade into everything else. Make these first, in this order, because later ones depend on earlier ones:

1. **Genre/loop** (§1) — what game is this, actually?
2. **Combat model**: simulated-physics vs animation-hitbox (§2) — defines "feel" and difficulty.
3. **Determinism required?** (§5, §10) — dictated by netcode; constrains physics and engine.
4. **Multiplayer in MVP, or later?** (§10) — the biggest scope lever you have.
5. **Engine** (§6) — mostly falls out of 2–4.
6. **Art direction concretely defined** (§7) — "enhanced Minecraft, not rectangles" needs to become a spec.

Everything else is execution.

---

## 1. Concept & Design Pillars

**Objective:** Turn a vibe into a testable design. Until this is nailed, no estimate below is trustworthy.

### Decisions to make

- **Core loop.** Options:
  - *(A) Pure duel/brawler:* fight to the death, obstacles are pre-placed. Simplest, tightest.
  - *(B) Build-and-battle:* a build phase then a combat phase (à la Fortnite's cycle but melee). Doubles your systems (a build mode *and* a combat mode).
  - *(C) Parkour-combat arena:* obstacles exist to be traversed; jump/movement is a first-class skill expression. Highest ceiling, hardest to tune.
  - *(D) Objective modes* (capture, king-of-the-hill) layered on any of the above.
  - **Recommendation:** Start with **(A) with light (C)** — pre-placed obstacles that reward movement — and treat player-building as a *fast-follow* feature, not MVP. Rationale: build systems are deceptively expensive (authoring UI, validation, sync, exploit-proofing) and can be added once combat is proven fun.
- **Session length & structure.** Mobile sessions skew short (2–5 min). Best-of-N rounds vs single-life vs timed. Short rounds = more matchmaking churn but better retention on mobile.
- **Player count.** 1v1 (cleanest netcode, easiest to balance) → 2v2 → small FFA (3–6). Every added player multiplies netcode and balance cost. **Recommend 1v1 for MVP.**
- **Win condition & TTK (time-to-kill).** Melee wants a TTK long enough to allow reads/counterplay but short enough to feel decisive. This is a tuning variable, not a launch decision — but decide the *philosophy* now (methodical duel vs frantic brawl).
- **Progression fantasy.** What does the player chase? Skill mastery, cosmetics, unlockable spears/movement tech, arena themes.

### Hidden assumption to check
That "create obstacles" adds fun rather than friction. Player-built content is a *content-generation shortcut* for the developer, but it's a *cognitive load increase* for the player and an *exploit surface* for you. Validate with paper/prototype that players actually want to build here, or you'll build an expensive system nobody uses.

**Deliverables:** one-page design brief, core-loop diagram, "pillars" (3–5 sentences you'll say "no" against), reference/mood board, a paper or Figma mock of a single match's flow.

---

## 2. Combat System Design (the heart of the game)

**Objective:** Make spear combat *feel* precise, weighty, and readable on a touchscreen. This is the make-or-break subsystem — prototype it in week one.

### The load-bearing decision: combat model

| Model | What it is | Pros | Cons | Fit for you |
|---|---|---|---|---|
| **Animation-driven hitbox/hurtbox** | Attacks are authored animations with hitboxes active on certain frames; hits = hitbox∩hurtbox overlap (fighting-game style) | Deterministic-friendly, controllable, tunable, cheaper to net-sync, readable | Less emergent/"physical"; relies on great animation | **Strong default** |
| **Simulated weapon physics** | Spear is a rigid body; hits computed from momentum/velocity (Mordhau/Blade & Sorcery style) | Emergent, physical, satisfying | Hard to balance, hard to net-sync, hard on touch input, hard on mobile perf | High-risk; avoid for MVP |
| **Hybrid** | Hitbox authority for *whether* you hit; physics for *reactions* (ragdoll on death, knockback) | Best of both feels | More systems to integrate | Good v1.1 target |

**Recommendation:** **Animation-driven hitboxes** for the combat resolution, physics only for cosmetic reactions. This keeps determinism on the table (§10) and control tight.

### Spear mechanics to design
- **Thrust** (primary): windup → active → recovery frames. Reach is your defining stat — spears win at range, lose up close.
- **Block / parry:** hold-block (drains stamina) vs timed-parry (skill window). Parry creates the "read" that makes dueling deep.
- **Feint / cancel:** cancel a windup to bait a parry. This is the core mind-game; it's also the hardest thing to express on touch.
- **Footwork / spacing:** lunge/step to control distance; "spacing" is the whole game for spears.
- **Stamina/posture system:** attacks, blocks, sprints cost stamina; running out = vulnerable. Prevents mindless spamming.
- **Damage model:** flat vs locational (head/torso/limbs). Locational adds depth but needs precise hitboxes and complicates touch aiming.

### The real problem: touch input for melee
Options (pick and prototype 2–3):
- **Tap-to-thrust + swipe-to-parry** (directional swipe sets parry angle).
- **Gesture combat:** swipe direction = attack direction. Intuitive but imprecise; fatiguing.
- **Auto-facing + timing buttons:** simplify aiming (lock-on to nearest foe), player focuses on *timing* attacks/parries/feints. **Most mobile-appropriate.**
- **Gyro-assisted aim:** tilt to fine-aim thrust. Novel; motion-sickness and precision risks.
- **Recommendation:** **soft lock-on + timing-based buttons + a single directional parry swipe.** Reduce degrees of freedom until the game is fun with a thumb.

### Hit detection math (animation-hitbox model)
Per tick, an attack is "live" during its active frames. A hit registers when a capsule/sphere weapon collider overlaps an enemy hurtbox. With server-authority + lag comp (§10), the server rewinds the target to the attacker's view time:

$$t_{\text{rewind}} = t_{\text{server}} - \left( \text{RTT}/2 + t_{\text{interp}} \right)$$

where $t_{\text{interp}}$ is the client's interpolation delay. Getting this right is what makes "I clearly hit them" true on both screens.

**Deliverables:** greybox combat prototype (two capsules, one spear, hit/parry/feint, stamina), tunable via a config file; a "feel" checklist you re-test every build.

---

## 3. Movement & Traversal

**Objective:** Movement that is fun *on its own* and legible in first-person on a small screen.

### Decisions
- **Movement feel:** grounded/weighty (dueling) vs snappy/floaty (arena). Spears reward positioning, so lean **grounded with a committed lunge**.
- **Jump:** fixed-height vs variable (hold) vs double-jump/dash. Jump interacts with obstacles (§4) and combat (aerial thrusts?). Decide if jump is *traversal only* or *combat-relevant* — the latter massively increases balance work.
- **Traversal tech:** vault/mantle over obstacles, wall-jump, slide, dash. Each is a physics + animation + netcode cost. Add one at a time.
- **Controls:** left virtual joystick (move) + right screen drag (look) is the mobile default. Consider auto-run, tap-to-move-to-cover, or contextual traversal (auto-vault) to reduce touch load.
- **First-person nausea/readability:** FOV tuning, head-bob toggle, motion reduction settings — accessibility *and* comfort.

### Hidden assumption
That first-person is right at all. First-person maximizes immersion but **hides your own body and the incoming spear**, which is brutal for melee reads and worse on a small screen. A **third-person or "first-person with visible weapon and generous readability cues"** may serve the fantasy better. At least prototype both cameras before committing — this is cheap now and catastrophic to change later.

**Deliverables:** movement prototype (integrated with §2), camera comparison build, control-scheme A/B.

---

## 4. The Obstacle Creation System

**Objective:** Let players place obstacles that meaningfully shape a match — without breaking the game, the netcode, or the framerate.

### Define the obstacles concretely (this is currently undefined and must be)
- **Set:** a small, curated kit beats infinite freedom. E.g.: *low wall (cover), tall wall (line-of-sight block), ramp/platform (elevation for jumps), pillar (spacing tool), destructible crate.* 5–8 pieces is plenty.
- **Look:** ties directly to art direction (§7). A **modular, grid-or-snap kit** reads as "enhanced Minecraft, not rectangles": chunky, readable silhouettes with beveled edges, organic surface detail, and hand-painted or gradient textures rather than pure cubes.
- **Placement model — the key decision:**
  - *Free placement* (anywhere): expressive, but exploit-prone (box yourself in, block spawns) and hard to net-sync.
  - *Snap-to-grid:* the Minecraft-adjacent option; easy to author, validate, and sync; reads cleanly. **Recommended.**
  - *Slot-based:* pre-defined positions players fill. Most constrained, safest, least expressive.
- **When can players build?** Pre-match draft phase (safe, simple) vs live mid-combat (Fortnite-style — powerful, but a huge netcode/exploit/perf problem). **Recommend pre-match for MVP.**
- **Constraints/validation:** max pieces, no blocking spawns/objectives, no un-winnable geometry, budget/economy per match. You *will* need an automated validity check.
- **Destructibility:** static vs damageable vs fully destructible. Destructible = physics + netcode + VFX cost. Start static/damageable.

### Data & sync
- Represent a built layout as a compact list of `{pieceId, gridPos, rotation}` — a few bytes each. This is trivial to serialize, replicate, and validate, which is the whole reason to prefer snap-to-grid.
- Determinism (§5): if netcode is deterministic, the layout is just initial simulation state — clean. If not, you replicate placements as networked objects.

### Hidden assumption
That building and dueling coexist well. In most games, *building mid-fight and precise melee compete for the same thumbs and the same seconds.* If both are live simultaneously, one will feel bad. Separating them into phases is not a compromise — it's probably the correct design.

**Deliverables:** obstacle kit spec (art + gameplay stats per piece), placement UX prototype, layout serialization format, validity checker.

---

## 5. Physics

**Objective:** Use physics for what it's good at; do **not** write your own engine.

### Decisions
- **Build vs buy:** Buy. Every serious engine ships a mature solver (Unity: PhysX + optional/removed Havok; Unreal: Chaos; Godot: Jolt). Writing your own is months of work to arrive at something worse. The only reason to hand-roll is a *deterministic lockstep* requirement — and even then you'd use a deterministic library (e.g., a fixed-point physics lib, or Photon Quantum's built-in deterministic physics), not build from scratch.
- **What physics you actually need:**
  - Character controller (kinematic capsule) — *not* full rigid-body for the player.
  - Collision queries for movement, jump, vault.
  - Hitbox/hurtbox overlap tests (may be your own lightweight system, not the physics engine).
  - Cosmetic: ragdolls on death, knockback, destructible debris.
- **Determinism — the fork that touches everything:**
  - *Non-deterministic* (floating-point, standard engine physics): fine for single-player and for state-sync netcode (Fusion), but two machines can diverge.
  - *Deterministic* (fixed-point or a deterministic engine): required for lockstep/rollback netcode (Quantum). Enables cheap sync + strong anti-cheat, but constrains your math and tooling.
  - **Recommendation:** If you commit to real-time competitive PvP, adopt a **deterministic simulation from day one** (Photon Quantum) — retrofitting determinism later is a rewrite. If MVP is single-player/async, use standard engine physics and keep the door open.

### Hidden assumption
That "physics engine" was ever the hard part. It isn't — the hard part is **determinism + netcode + touch input**. "Should I build a physics engine?" is the wrong question; "does my netcode force determinism?" is the right one.

**Deliverables:** decision memo on determinism; character-controller prototype; hit-detection system (independent of full physics).

---

## 6. Game Engine Selection

**Objective:** Pick the engine that maximizes mobile shipping speed and multiplayer support for a small team.

| Engine | Mobile pipeline | Netcode | Cost | Learning curve | Verdict |
|---|---|---|---|---|---|
| **Unity** | Best-in-class mobile build/deploy/compression | NGO (co-op), Mirror, **Photon Fusion/Quantum** | Free < \$200k rev; per-seat Pro after | Moderate (C#) | **Recommended default** |
| **Unreal** | Heavier binaries, more perf tuning on mobile | Built-in replication (excellent), Fusion (Godot/UE) | 5% royalty after threshold | Steep (C++/BP) | Overkill for stylized mobile |
| **Godot** | Improving (Jolt physics), lighter | High-level multiplayer API; Fusion coming | Free/open-source | Gentle (GDScript/C#) | Viable on budget; netcode/tooling less mature for competitive |
| **Custom** | — | — | Your time | Insane | No |

**Facts grounding this (as of 2026):** Unity's Runtime Fee was cancelled in 2024 and remains cancelled; pricing is per-seat with a free tier under \$200k revenue/funding; Unity is still the strongest mobile build pipeline. Note Havok is being removed from Pro plans in 2026 (irrelevant if you use PhysX or a deterministic lib). Godot has real momentum (Jolt physics, Photon investing in Godot support) but competitive real-time netcode tooling is less mature than Unity's.

**Recommendation:** **Unity**, C#, targeting the current 6.x LTS. If real-time competitive PvP is core, pair it with **Photon Quantum** (deterministic rollback) and design the simulation in Quantum from day one.

**Deliverables:** engine decision memo, project skeleton, source control + CI wired up (§16).

---

## 7. Art & Graphics Direction

**Objective:** Nail "enhanced Minecraft, but not all rectangles" as a concrete, reproducible style — and keep it cheap to produce and cheap to render on mobile.

### Translate the vibe into a spec
"Enhanced Minecraft, not rectangles" most plausibly means: **stylized low-poly with chunky, readable silhouettes; beveled/organic edges instead of hard cubes; flat or gradient shading with hand-painted texture accents; saturated, friendly palette; strong readable lighting.** Reference points to align on: *Monument Valley* (geometry + palette), *Bugsnax / A Short Hike* (chunky-charming), *Astroneer* (soft low-poly), *Valheim* (stylized-but-grounded). Pick 2–3 and write down what specifically you're taking from each.

### Decisions
- **Modeling approach:**
  - *Hand-modeled low-poly* — most control, most labor.
  - *Smooth-voxel / marching cubes* — keeps a "Minecrafty" build logic but rounds it off; great if players build.
  - *Modular kit* — author a set of snap pieces; reuse everywhere (pairs perfectly with §4). **Recommended.**
- **Texturing:** flat-color materials (cheapest, cleanest, great on mobile) vs hand-painted textures (more character, more work) vs stylized PBR (richest, heaviest). **Start flat/gradient + a few hand-painted hero assets.**
- **Shading/lighting:** Unity URP (Universal Render Pipeline) is the mobile-appropriate choice. Baked lighting + light probes for perf; avoid heavy real-time shadows on low-end devices. A custom toon/stylized shader defines your look cheaply.
- **Characters:** first-person means you mostly see *arms + spear* (viewmodel) and the *opponent*. Budget accordingly — the enemy model and the viewmodel are your hero assets.
- **VFX:** hit sparks, parry clashes, dust on landings, obstacle placement "poof." Readability > realism.
- **Make vs buy assets:** asset-store/marketplace kits to prototype fast, custom art for hero pieces and identity. Don't ship a store-kit look.

### Mobile-specific art constraints (decide targets now)
- Polygon budgets per object/scene, texture atlas sizes, draw-call ceilings, LOD levels, texture compression (ASTC on modern mobile).
- Define a **low-end target device** (see §14) and art to *that*, not to your dev phone.

**Deliverables:** style guide (palette, materials, silhouette rules, lighting recipe), the modular obstacle/environment kit, hero viewmodel + enemy model, a stylized shader, per-platform art budgets.

---

## 8. UI / UX

**Objective:** A HUD and menus that are legible with thumbs on a 6" screen and don't obscure combat.

### Decisions
- **Combat HUD:** health, stamina, reach/spacing cues, cooldowns — minimal and peripheral. Melee needs the *center screen clear.*
- **Input UI:** placement, size, and opacity of virtual sticks/buttons; left-hand/right-hand modes; customizable layout (accessibility win, low cost, high goodwill).
- **Menus & flow:** main menu → matchmaking → loadout/obstacle draft → match → results → progression. Keep taps-to-play low.
- **Onboarding/tutorial:** melee timing and the obstacle system both need teaching. Interactive tutorial + bot practice. Undervalued and critical for retention.
- **Visual style:** the UI should echo the art direction (chunky, rounded, friendly) without eating screen space.
- **Accessibility:** colorblind-safe cues, scalable text, motion reduction, one-handed considerations, haptics for hit/parry feedback (huge for melee "feel" on mobile).

**Deliverables:** UX flowchart, HUD mockups, input-layout system, tutorial script, accessibility checklist.

---

## 9. Audio

**Objective:** Sell the impact of thrusts, parries, and landings — audio does half the "feel" work in melee.

### Decisions
- **SFX:** spear whoosh, flesh/armor hit, parry *clang*, footsteps, jump/land, obstacle place/destroy, UI. Layered variants to avoid repetition.
- **Spatial audio:** even in 1v1, directional audio for the opponent's attacks aids reads. Cheap, high value.
- **Music:** menu vs combat vs tension states. Adaptive/stinger system optional.
- **Voice:** grunts/effort sounds (cheap, big feel) vs full VO (expensive, probably unnecessary).
- **Pipeline:** middleware (FMOD/Wwise) vs Unity's built-in audio. Built-in is fine for MVP; FMOD/Wwise if audio design gets ambitious.
- **Mobile constraints:** compression, memory budgets, respecting the mute switch, mixing for tiny speakers *and* headphones.

**Deliverables:** SFX list + sourcing plan (record/library/commission), haptics map, audio-mixer setup.

---

## 10. Networking & Multiplayer

**Objective:** If you go real-time PvP, deliver responsive, fair, cheat-resistant melee over variable mobile connections. This is the highest-risk system in the project.

### The scope decision (revisit before anything else here)
- **Ship single-player / async first** (bots, ghost/async duels, or leaderboards) → prove fun → add real-time PvP.
- **vs. real-time PvP in MVP** → maximal risk, maximal differentiation.
- **Recommendation:** unless real-time PvP *is* the pitch, **defer it.** Melee's latency-intolerance makes it the worst genre to learn netcode on.

### If real-time PvP: topology & netcode model

| Approach | How | Pros | Cons |
|---|---|---|---|
| **P2P listen-server** | one client hosts | cheap, simple | host advantage, cheatable, host-migration pain |
| **Client-server, state-sync + prediction + lag comp** (Photon Fusion) | authoritative state replicated; clients predict | proven for action games, flexible | melee lag comp is hard; "I hit them!" disputes |
| **Deterministic lockstep + rollback** (Photon Quantum) | clients exchange inputs; identical sims; rollback on mispredict | zero-lag feel, strong anti-cheat, unified single/multi code | requires determinism (§5) from day one; steeper model |

**Facts grounding this:** Photon Quantum is a deterministic predict/rollback engine that runs physics-intensive multiplayer on mobile and is cheat-resistant by design (all clients run identical, verifiable simulations); Photon Fusion is the high-end state-sync option with prediction and lag compensation. Both are Unity-verified and cross-platform.

**Recommendation:** for competitive 1v1 melee, **deterministic rollback (Quantum)** is the best technical fit *if* you accept its constraints early. Otherwise **Fusion** with server authority + lag compensation.

### Netcode fundamentals to get right
- **Tick rate:** simulation frequency. Higher = more responsive, more bandwidth/CPU. Interpolation delay to smooth remote entities:
  $$t_{\text{interp}} \approx \frac{k}{f_{\text{tick}}}, \quad k \in \{1,2,3\}$$
  where $f_{\text{tick}}$ is the tick rate; you trade smoothness against responsiveness via $k$.
- **Client prediction + reconciliation** for the local player; **interpolation** for remote players; **lag compensation / server rewind** for hit validation (§2).
- **Matchmaking:** skill-based (needs a rating system), latency-based (region pinning), or hybrid. Melee *demands* low ping — region-lock aggressively.
- **Anti-cheat:** server/deterministic authority, input validation, replay verification. Deterministic sims make this dramatically easier.

### Backend dependencies (see §11)
Matchmaking service, relay/cloud (Photon Cloud handles a lot), session management, reconnection/host-migration.

### Hidden assumption
That you can add real-time PvP "later, easily." You often can't — determinism, authority, and prediction are *architectural*, not features you bolt on. If real-time PvP is even *plausibly* in your future, decide the netcode model now, even if you don't build it yet.

**Deliverables:** netcode decision memo, a two-device latency prototype *early* (before art), matchmaking design, anti-cheat plan.

---

## 11. Backend & Live Services

**Objective:** Everything the client can't be trusted to do or store itself.

### Systems & options
- **Accounts/auth:** platform sign-in (Apple/Google), guest accounts, optional custom. Start with platform + guest.
- **Player data/progression storage:** managed backend-as-a-service (PlayFab, Nakama, Firebase, Unity Gaming Services) vs custom. **Use a BaaS** — don't build accounts/storage from scratch.
- **Matchmaking & sessions:** Photon (if using it), Unity Matchmaker, Nakama, or custom.
- **Leaderboards / ranked / seasons.**
- **Analytics & telemetry:** you need this from day one to tune combat and retention (funnel, match outcomes, quit points).
- **Remote config / feature flags / live tuning:** change balance values without shipping a build — invaluable for a combat game.
- **Server hosting** (if authoritative sim needed): managed game-server hosting vs Photon Cloud vs self-managed. Managed/cloud first.
- **Live-ops:** events, content drops, patch cadence.

### Hidden assumption
That backend is an afterthought. Analytics and remote-config should exist *before* your first playtest, or you'll tune combat blind.

**Deliverables:** BaaS selection, data schema (player, match, progression), analytics event spec, remote-config plan.

---

## 12. Progression, Economy & Monetization

**Objective:** A business model that funds the game without poisoning a skill-based competitive experience.

### Decisions
- **Model:** premium (one-time buy — rare on mobile, but clean) vs free-to-play + cosmetics vs F2P + ads vs hybrid. For competitive skill games, **F2P + cosmetics** is the least corrosive.
- **What's monetized:** cosmetics only (spears skins, arena themes, effects) vs progression boosts vs pay-to-win (avoid — kills competitive integrity).
- **Progression:** cosmetic unlocks, battle pass, ranked seasons, mastery. Keep gameplay-affecting unlocks *earnable*, not purchasable.
- **Ads:** rewarded video (least intrusive) vs interstitial (retention risk). If used, rewarded only.
- **Economy design:** soft/hard currencies, sinks/sources, pricing.
- **Ethics & regulation:** loot-box disclosure laws, gambling regulations, and children's-privacy law (COPPA/GDPR-K) if under-13s can play — this shapes data collection and ad choices and is **legally mandatory**, not optional.

### Hidden assumption
That you can decide monetization at the end. The *data collection, account, and store systems* it requires are architectural. Decide the model early even if you implement it late.

**Deliverables:** monetization model doc, cosmetic pipeline, store integration plan, compliance/legal checklist (age gating, disclosures, privacy).

---

## 13. Content & Arena Design

**Objective:** Arenas that make spacing, jumping, and obstacle-placement sing.

### Decisions
- **Arena count & size for launch:** a few tight, well-tuned arenas beat many mediocre ones. Melee arenas are small; readability and spacing rule.
- **Layout language:** sightlines, chokes, elevation, cover density — designed to interact with the obstacle system (§4) rather than fight it.
- **Handcrafted vs modular vs procedural:** handcraft launch arenas from the modular kit (§7). Procedural is a trap for a launch tuning-critical game.
- **Themes/biomes:** tie to art direction; used for cosmetic variety and progression.
- **Balance:** no dominant spawn/position; symmetric for competitive fairness.

**Deliverables:** 2–3 launch arenas, an arena design guide, a greybox-to-final pipeline.

---

## 14. Platform, Build & Deployment

**Objective:** Ship to iOS and Android and survive device fragmentation and store review.

### Decisions
- **Platforms/order:** iOS-first (higher ARPU, less fragmentation) vs Android-first (reach) vs simultaneous. Small teams often iOS-first for cleaner QA.
- **Min-spec target device:** pick a *concrete low-end phone* and make it the performance bar (see §7). Frame-rate target (30 vs 60 fps — 60 matters for melee timing but costs perf/battery).
- **Performance budgets:** frame time, memory, thermal/battery, binary size, load times. Thermal throttling on mobile *will* affect a 60fps combat game — plan for it.
- **Store compliance:** App Store & Play policies, content ratings (IARC/ESRB/PEGI — a *fighting* game has rating implications), privacy nutrition labels / data-safety forms, required legal docs (privacy policy, terms).
- **Build pipeline:** Unity Build Automation / Fastlane / Codemagic; TestFlight (iOS) & Play Console internal/closed/open testing tracks; staged rollout.
- **Certification/review lead time:** budget for rejections and resubmits.

### Hidden assumption
That your dev phone represents your players. It doesn't — the median device is far weaker and hotter. Test on low-end hardware *continuously*, not at the end.

**Deliverables:** target-device matrix, performance-budget doc, store-compliance checklist, CI build pipeline, beta-testing plan.

---

## 15. Testing & QA

**Objective:** Prove the game is fun, fair, performant, and stable — especially the parts that can't be unit-tested.

### Areas
- **Combat-feel playtesting:** the single most important test loop. Structured sessions, fresh players, watch where timing/reads fail. Re-run every build.
- **Netcode testing:** artificial latency/jitter/packet-loss injection; two-device and cross-region tests; desync detection (critical for deterministic sims).
- **Device/perf testing:** the low-end matrix, thermal soak tests, battery drain, memory pressure.
- **Balance testing:** telemetry-driven (win rates, TTK, obstacle usage), plus expert players.
- **Automated tests:** unit tests for combat math/state machines; simulation/replay tests for determinism; smoke tests in CI.
- **Exploit/abuse testing:** obstacle-placement exploits, out-of-bounds, cheat vectors.
- **Store-compliance & accessibility QA.**

**Deliverables:** playtest protocol + cadence, netcode test harness (latency sim), device lab or cloud-device plan (e.g., device farms), telemetry dashboards, bug-tracking process.

---

## 16. Tooling, Source Control, CI/CD & Project Management

**Objective:** Infrastructure so a small team doesn't lose days to process.

### Decisions
- **Source control:** Git + LFS (for binary assets) or Unity Version Control (formerly Plastic — good for large binaries and non-mergeable art). Unity VC is now more generous on the free tier.
- **CI/CD:** automated builds per commit, TestFlight/Play distribution, versioning. Unity Build Automation, GitHub Actions, or Codemagic.
- **Asset pipeline:** import settings enforcement, texture-compression presets, addressables for content delivery.
- **Project management:** issue tracker (Linear/Jira/GitHub Issues), design docs, build/version conventions.
- **Config-driven design:** expose combat/economy/tuning values in data files or remote config so designers tune without engineering.

**Deliverables:** repo + LFS/VC setup, CI pipeline, branching strategy, PM board, tuning-config system.

---

## 17. Team, Budget & Timeline

**Objective:** Match ambition to resources honestly.

### Roles a competitive mobile PvP game implies
Gameplay/systems engineer, **network engineer** (specialist if real-time PvP), 3D/environment artist, character/animation artist, technical artist (shaders/perf), UI/UX designer, game/combat designer, audio (often contract), backend/live-ops engineer, QA. Plus production and (later) community/marketing.

### Reality check
- As described — first-person, physics, networked, cosmetics, live-ops, two platforms — this is a **multi-discipline, multi-person, 12–36 month effort** at production quality, not a weekend project. A solo dev *can* build a scoped-down version, but not the full spec above.
- The three most expensive/risky line items: **real-time netcode**, **combat feel iteration** (uncapped — you iterate until it's fun), and **art production**.
- Budget ranges swing enormously with team model (solo + contractors vs small studio). The honest answer is: *scope determines budget, and your current scope is large.* The MVP below is how you make it fundable.

**Deliverables:** staffing plan, phased budget, realistic milestone timeline tied to §18.

---

## 18. Consolidated Risk Register

| Risk | Severity | Likelihood | Mitigation |
|---|---|---|---|
| Melee doesn't feel good on touch | Critical | High | Prototype combat feel in week 1; soft lock-on + timing model; kill-fast if it can't be fun |
| Real-time melee netcode too hard/costly | Critical | High | Defer PvP; if kept, commit to determinism (Quantum) day one; latency prototype before art |
| Genre incoherence (fight vs build) | High | Medium | Lock the loop in §1 before production; phase-separate building and combat |
| First-person hurts readability | High | Medium | Prototype both cameras early |
| Scope vs team mismatch | High | High | Ruthless MVP (§19); fast-follow everything non-core |
| Mobile perf/thermal at 60fps | Medium | High | Low-end target device, perf budgets, continuous device testing |
| Building creates exploits | Medium | Medium | Snap-grid + automated validity checks; pre-match only |
| Monetization/compliance retrofitting | Medium | Medium | Decide model + data/legal architecture early |
| Engine/pricing/licensing shifts | Low | Low | Stay under free-tier threshold; keep tuning in data not engine-locked code |

---

## 19. Recommended MVP & Phasing (what to cut, and in what order to build)

**Guiding principle:** prove the two riskiest fun-hypotheses first — *(a) spear combat feels good on a touchscreen*, and *(b) obstacles add fun* — before spending on netcode, art, or backend.

- **Phase 0 — Combat feel prototype (weeks).** Two capsules, one spear, greybox arena. Thrust/parry/feint/stamina, soft lock-on, movement, jump. Placeholder art. *Gate:* is it fun with a thumb? If no, iterate or stop.
- **Phase 1 — Obstacle + single-player loop.** Snap-grid obstacle kit, pre-match placement, bot opponent, one arena, basic HUD, haptics, analytics + remote config. *Gate:* do obstacles + movement + combat form a fun match vs a bot?
- **Phase 2 — Art & polish pass.** Define and apply the "enhanced Minecraft" style to hero assets and the kit; audio; UI; tutorial; accessibility. *Gate:* does it look and feel like a real game on a low-end device?
- **Phase 3 — Networking (only if PvP is core).** Latency prototype → chosen netcode model (Quantum/Fusion) → 1v1 real-time → matchmaking → anti-cheat. *Gate:* fair, responsive 1v1 across regions.
- **Phase 4 — Live services, monetization, store prep.** Accounts, progression, cosmetics, leaderboards, compliance, beta tracks, staged rollout.
- **Phase 5 — Launch & live-ops.** Seasons, content, balance patches driven by telemetry.

**The most important sentence in this document:** if Phase 0 isn't fun, none of Phases 1–5 matter — so build Phase 0 before you build anything else in this plan.

---

*End of catalog. Sections 1, 2, 5, 6, and 10 contain the decisions that constrain all the others; resolve those first.*
