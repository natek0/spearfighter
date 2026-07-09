# Spearfighting Game — Load-Bearing Decision Register

*The exhaustive set of foundational decisions that dictate what tasks the project requires. Each is a decision you make once and live with — changing them later is a rewrite, not a tweak. Ordered by dependency: Tier 1 decisions constrain Tier 2, which constrain Tier 3. Locked facts from our discussion are marked ✔.*

**Locked so far:** ✔ Thrust-based combat (no slash arcs). ✔ Combat resolution = animation-driven hitboxes; world = real physics. ✔ Do not build a physics engine — build on an existing one. ✔ MVP likely single-player, but architect toward possible online multiplayer. ✔ Players can create and (probably) destroy obstacles.

Reference decisions by ID (D1…D34) so we can work through them individually.

---

## How to read the dependency structure

Three roots dominate everything:

- **D1 core loop** — defines what the player *does*, which defines every input, system, and mode.
- **D7 combat model** (✔ hitbox) + **D19 determinism** — together define your simulation architecture.
- **D22 multiplayer target** — the single biggest scope and architecture lever.

If those three are set, most other decisions have an obvious "correct-for-you" answer. That's why I recommend locking Tier 1 first, in order, before touching Tier 2.

```
D1 core loop ─┬─> D3 player count ─> D22 netcode scope ─> D23 netcode model ─> D19 determinism ─> D7/D6 combat & engine
              ├─> D17 build timing ─> D13 input model ─> D14 input assist ─> D2 camera
              ├─> D16 placement model ─> D18 destructibility ─> D19 physics/determinism
              └─> D32 business model ─> D33 progression ─> D34 audience/rating ─> D25 backend
```

---

# TIER 1 — Root decisions (lock these first)

### D1. Core loop & phase structure
**The decision:** what is the moment-to-moment shape of a match?
**Options:**
- *(A) Pure duel* — fixed/pre-placed obstacles, fight to win. Tightest, cheapest.
- *(B) Build phase → combat phase* — discrete building, then discrete fighting. Resolves temporal contention cleanly.
- *(C) Live building during combat* — build and fight simultaneously (Fortnite-melee). Highest ceiling, hardest everything.
- *(D) Rounds with re-build between* — hybrid of B; build, fight, adjust, fight again.
**Weigh this:** live building (C) is where your novelty is most extreme *and* where input/temporal/attention contention is worst on mobile. Phased building (B/D) keeps the novelty (player-shaped arenas) while sidestepping the "two thumbs doing two jobs at once" problem.
**Dictates:** input model, netcode complexity, UI, exploit surface, session length.
**Recommendation:** **(D)** — round-based with a build/adjust beat between fights. *Counterpoint:* if "build *while* fighting" is the specific magic you're chasing, (C) is the only way to get it, and you should prototype it early precisely because it's the make-or-break novelty.

### D2. Camera perspective
**The decision:** first-person, third-person, or hybrid?
**Options:** FP (immersion, your stated goal) / TP (readability, spacing, sells melee) / hybrid (FP combat, TP for building, or pull-out on lock-on).
**Weigh this:** FP maximizes immersion but hides your body/reach/wind-up and hurts depth judgment — brutal for a spacing-driven spear game on a small screen. TP is why almost all successful melee is third-person.
**Dictates:** readability, art focus (viewmodel vs full character), control feel, nausea/comfort work.
**Recommendation:** prototype **both** in Phase 0 before committing; if FP survives a spacing playtest, keep it. *Counterpoint:* FP is genuinely more novel and immersive for a spear thrust down the camera axis — it may be worth fighting for if the readability problems can be solved with telegraph/VFX.

### D3. Player count & match format
**Options:** 1v1 / small FFA (3–6) / teams (2v2, 3v3).
**Weigh this:** every added player multiplies netcode cost, balance surface, and arena-design complexity. 1v1 is the cleanest place to prove combat and to build netcode later.
**Dictates:** netcode, matchmaking, balance, arena size/count.
**Recommendation:** **1v1** for MVP and first PvP. *Counterpoint:* FFA/teams are stickier socially and more forgiving of individual skill gaps — better long-term retention, worse first-version risk.

### D7. Combat resolution model ✔ (decided: hitbox)
**Locked:** animation-driven hitbox/hurtbox for whether a hit lands; real physics only for reactions (ragdoll, knockback) and the world.
**Still to sub-decide:** hitbox representation (capsule sweep along the spear vs discrete frame hitboxes), and whether you do continuous vs discrete hit-checks for fast thrusts (to avoid tunneling).
**Dictates:** animation pipeline, netcode sync, determinism feasibility, balance tooling.

### D13. Control scheme & input model
**The decision:** how do the player's two thumbs express move, look, thrust, block, jump, build?
**Options:**
- *Dual virtual sticks + buttons* (shooter-standard): familiar, but occlusion-heavy and input-cramped for melee.
- *Gesture combat* (swipe = thrust/parry direction): intuitive, imprecise, fatiguing.
- *Soft lock-on + timing buttons* (auto-face nearest foe; thumbs handle timing not aiming): removes the aiming DOF, best fit for mobile melee.
- *Hybrid* (lock-on for combat, free look for building/traversal).
**Weigh this:** this is *the* mobile-viability decision. The Infinity Blade lesson: successful FP touch melee wins by reducing inputs. Every verb you keep free-aimed costs you a thumb.
**Dictates:** whether the game is playable at all; HUD; tutorial; combat depth ceiling.
**Recommendation:** **soft lock-on + timing-based thrust/parry + a single directional dodge/step**, with building on a separate phase (D1-D). *Counterpoint:* lock-on caps the skill ceiling on aiming/spacing — hardcore players may want free-aim; consider an optional "advanced" free-aim mode later.

### D19. Determinism requirement
**The decision:** must your simulation be bit-for-bit reproducible across devices?
**Options:** *Non-deterministic* (standard float physics) / *Deterministic* (fixed timestep + fixed-point or tightly controlled float).
**Weigh this:** determined almost entirely by D23 netcode model. Rollback/lockstep ⇒ deterministic mandatory. State-sync or single-player ⇒ not required. Retrofitting determinism later is a rewrite.
**Dictates:** physics library choice, math, engine, anti-cheat strength, ability to add rollback netcode later.
**Recommendation:** since MVP is single-player but you want the multiplayer door open, make a **conscious call now**: either (a) commit to determinism from day one (build combat in a deterministic sim like Photon Quantum, harder now, cheap PvP later), or (b) stay non-deterministic for a fast single-player MVP and accept that real-time PvP will mean state-sync netcode (Fusion) or a partial rewrite. *Counterpoint:* (a) front-loads pain for a multiplayer future that may never ship; (b) is faster to fun but caps your netcode options. Your call hinges on how sure you are that real-time PvP is the destination.

### D22. Multiplayer scope & timing
**The decision:** is the MVP single-player, and is multiplayer real-time or async, and when?
**Options:** SP-only / SP + async (ghosts, leaderboards, async duels) / real-time PvP in MVP / real-time PvP as fast-follow.
**Weigh this:** ✔ you've leaned SP-first, which is the right risk posture. The remaining question is what *kind* of multiplayer you're targeting, because it sets D19/D23 now even though you build it later.
**Dictates:** determinism, backend, anti-cheat, matchmaking, engine, entire architecture.
**Recommendation:** **SP + bots for MVP; decide the *target* netcode model now (D23) so you don't architect yourself into a corner.** *Counterpoint:* deciding netcode before you know the game is fun risks over-engineering — a valid alternative is "SP-only, ignore netcode entirely, and accept a possible rewrite if PvP proves worth it."

### D32. Business model
**The decision:** how does this make money (if at all)?
**Options:** premium (one-time) / F2P + cosmetics / F2P + ads / hybrid.
**Weigh this:** for a competitive skill game, cosmetics-only F2P protects integrity; ads and pay-to-win corrode it. This decision reaches back into *design* (do you need a grind/economy at all?) and into architecture (accounts, store, data collection, compliance).
**Dictates:** accounts, store, progression, data/analytics, legal compliance, and some core design.
**Recommendation:** **F2P + cosmetics**, decided now, implemented late. *Counterpoint:* premium is cleaner and less corrosive but commercially brutal on mobile; a paid game needs a much stronger marketing hook.

---

# TIER 2 — Major decisions (fall out of Tier 1, still shape many tasks)

### D4. Session model & length
Rounds vs single-life vs timed; target 2–5 min mobile session. Dictates matchmaking churn, retention loops, arena pacing.

### D5. Skill-expression philosophy
Twitch/execution vs read/prediction vs positioning/spacing. A spear game leans **spacing + reads**. Dictates which combat systems (feint, parry windows, footwork) you invest in. *Recommendation:* spacing + reads over twitch, since twitch is exactly what mobile latency/occlusion punishes.

### D6. Game engine
Unity / Unreal / Godot. Mostly falls out of D7 (✔ hitbox — any engine fine), D19 (determinism — favors Unity+Quantum), D22 (mobile pipeline — favors Unity). *Recommendation:* **Unity 6.x LTS**; pair with Photon Quantum *if* you go deterministic rollback. *Counterpoint:* Godot if budget/open-source is a hard constraint and you accept less mature competitive netcode.

### D8. Spear moveset scope
Thrust only? + block + parry + feint + lunge + charged thrust + aerial? Each verb = animation + input slot + balance work. *Recommendation:* MVP = **thrust, block, parry, lunge/step**; add feint and charged thrust once the base loop is fun. Dictates animation budget, input model (D13), depth.

### D9. Defensive model
Hold-block (stamina drain) / timed-parry (skill window) / dodge-step / positioning-only. This *is* the core mind-game. *Recommendation:* **timed-parry + dodge-step**, because parry creates the read that makes dueling deep and is expressible as a timed tap. Dictates combat depth, input, animation.

### D10. Targeting/aiming model
Free-aim / soft lock-on / hard lock-on. Tightly coupled to D13. *Recommendation:* soft lock-on (auto-face, player still controls spacing/timing). Dictates hitbox precision needs, input, feel.

### D11. Damage & health model
Flat vs locational damage; health vs posture/stamina vs both. Locational adds depth but demands precise hitboxes and aiming (fights lock-on). *Recommendation:* **health + stamina/posture, non-locational** for MVP. Dictates hitbox granularity, HUD, balance.

### D12. Time-to-kill philosophy
Methodical duel (long TTK, more reads) vs frantic (short TTK). *Recommendation:* medium-long — a thrust that lands should matter, but not instant-kill, so counterplay exists. Dictates balance, session length, feel.

### D14. Input-assist / automation level
Auto-face, auto-approach-to-range, assisted parry timing windows, aim magnetism. This is the dial that makes melee mobile-viable without gutting depth. *Recommendation:* generous assists by default, with a competitive/reduced-assist mode later. Dictates accessibility, skill ceiling, matchmaking fairness.

### D15. Frame-rate target
30 / 60 / 120 fps. Melee timing wants 60; mobile thermal makes sustained 60 hard on low-end. *Recommendation:* **target 60, with a 30fps fallback path**; design timing windows forgiving enough to survive frame dips. Dictates perf budgets, art fidelity, device targets.

### D16. Obstacle placement model
Free placement / snap-to-grid / slot-based. Snap-grid is the "Minecraft-adjacent," easy-to-validate, easy-to-sync choice. *Recommendation:* **snap-to-grid.** Dictates authoring UI, netcode payload, exploit surface, art (modular kit).

### D17. Obstacle build timing
Pre-match / between-rounds / live-during-combat. Directly tied to D1. *Recommendation:* between-rounds (matches D1-D). Dictates input contention, netcode, UI.

### D18. Destructibility model ✔ (you want destruction)
Static / damageable-then-removed / chunk-based fracture / full-sim destruction. Full sim is expensive and hard to net-sync. *Recommendation:* **damageable → pre-authored fracture pieces** (looks physical, stays cheap and syncable), not real-time fracture. Dictates physics load, VFX, netcode, perf.

### D20. Movement & traversal scope
Jump only / + vault-mantle / + dash / + wall-run. And: is jump *combat-relevant* or traversal-only? Combat-relevant jump massively increases balance work. *Recommendation:* **move + jump + contextual vault** for MVP; jump is traversal-first. Dictates animation, physics, balance, netcode.

### D23. Netcode model (target, even if deferred)
Deterministic rollback (Quantum) / state-sync + prediction + lag-comp (Fusion) / P2P listen-server. Sets D19. *Recommendation:* if real-time competitive PvP is the destination, **target deterministic rollback**; if PvP is a maybe, keep it undecided but stay architecturally clean (pure simulation, inputs separable from rendering) so either path stays open. Dictates determinism, backend, anti-cheat, engine.

### D25. Backend / live-services approach
BaaS (PlayFab / Nakama / Unity Gaming Services / Firebase) vs custom. *Recommendation:* **BaaS** — never hand-build accounts/storage/matchmaking. Even for SP MVP, wire analytics + remote-config early. Dictates accounts, progression storage, matchmaking, live-ops, tuning workflow.

### D26. Target platforms & minimum-spec device
iOS-first / Android-first / simultaneous; and the concrete low-end phone you treat as the performance floor. *Recommendation:* pick one lead platform for cleaner QA and a *specific* low-end device as the bar. Dictates perf budgets, art budgets, testing matrix, store timelines.

### D29. Art production strategy
Modular kit / bespoke hand-modeled / smooth-voxel (marching cubes) / procedural. Ties to D16 (grid ⇒ modular kit is natural) and to whether players build. *Recommendation:* **modular snap kit + a few bespoke hero assets.** Dictates pipeline, budget, obstacle system, identity.

---

# TIER 3 — Foundational but more contained

### D21. Movement feel
Grounded/weighty vs snappy/floaty. Spears reward positioning ⇒ grounded with a committed lunge. Dictates animation, physics tuning, combat spacing.

### D24. Authority model
Server-authoritative / client-authoritative / deterministic-verified. Follows D23. Deterministic sims give strong anti-cheat cheaply. Dictates anti-cheat, backend, trust model.

### D27. Rendering approach & fidelity target
Flat/toon shading vs stylized-PBR; Unity URP for mobile; baked vs real-time lighting. *Recommendation:* URP + flat/gradient + a stylized shader + baked lighting. Dictates perf, art pipeline, look.

### D28. Concrete art-direction spec ("enhanced Minecraft, not rectangles")
Turn the vibe into rules: chunky readable silhouettes, beveled/organic edges, saturated palette, hand-painted hero accents. Pick 2–3 reference games and specify what you take from each. Dictates every art task and the game's identity.

### D30. Progression model
Cosmetic-only unlocks vs gameplay-affecting unlocks vs both. For competitive integrity, keep gameplay earnable-not-buyable. Follows D32. Dictates economy, store, balance integrity.

### D31. Audience & content rating
Target age + IARC/ESRB/PEGI implications of a *fighting* game; if under-13s can play, COPPA/GDPR-K reshape data collection and ads. *This is legally mandatory, not optional.* Dictates compliance, monetization limits, content, marketing.

### D33. Tutorial & onboarding model
Interactive tutorial / bot-ladder / guided first match. Melee timing *and* the build system both need teaching; underinvesting here kills retention. Dictates UX scope, first-session design.

### D34. Content-generation / UGC strategy
How central is player-created content? Obstacle layouts are the seed of UGC — shareable layouts, community arenas, etc. Decide whether that's a core pillar or a side feature, because it changes moderation, storage, and social systems. Dictates backend, social features, moderation.

---

# Recommended default configuration (a coherent starting stance to accept or attack)

Not "the answer" — a self-consistent set you can adopt wholesale or pick apart. Every one is reversible on paper now and expensive to reverse in code later, so react to these before building:

- **Loop (D1):** round-based, build/adjust between rounds, no building mid-fight.
- **Camera (D2):** prototype both; bias toward third-person if FP fails a spacing playtest.
- **Format (D3):** 1v1.
- **Combat (D7/D8/D9/D10/D11):** hitbox resolution; thrust + block + timed-parry + lunge; soft lock-on; health + stamina, non-locational.
- **Input (D13/D14):** soft lock-on + timing buttons + directional dodge; generous assists, advanced mode later.
- **Determinism/netcode (D19/D22/D23):** SP + bots MVP; keep the simulation pure and input-driven so a **deterministic-rollback** PvP path stays open without forcing it now.
- **Obstacles (D16/D17/D18):** snap-grid, between-rounds, damageable→pre-fractured destruction.
- **Movement (D20/D21):** grounded move + jump + contextual vault; jump is traversal-first.
- **Engine/stack (D6/D25/D26):** Unity 6.x LTS, URP; BaaS + analytics + remote-config from day one; one lead platform + a named low-end device as the floor.
- **Art (D27/D28/D29):** modular snap kit + hero assets; flat/toon + stylized shader + baked lighting; write the concrete "enhanced Minecraft" spec against 2–3 references.
- **Business (D30/D31/D32/D34):** F2P + cosmetics-only, gameplay earnable-not-buyable; lock the age/rating target early for compliance; treat shareable layouts as a *maybe-later* UGC pillar.

**The two I'd flag as least settled and worth your active reasoning:** D1 (does building-mid-combat *have* to be the magic?) and D2 (can first-person survive a spacing playtest, or is your immersion goal fighting your readability goal?). Those two disagree with parts of your stated vision, so they're where the real conversation is.

---

*Reference decisions by ID. Tell me which to reason through first — I'd start with D1, then D2, then the D19/D22/D23 cluster, since those three unlock the most downstream clarity.*
