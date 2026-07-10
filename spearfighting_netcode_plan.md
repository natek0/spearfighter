# Spearfighter — WS10 Netcode Plan (server-authoritative, mutable-world)

> The sprint-level breakdown of the single hardest workstream. Read alongside
> `spearfighting_context_and_plan.md` (§4 architecture, §8 risks) and the WS10 entry in
> `spearfighting_task_catalog.md`. Locked inputs: **Photon Fusion**, server-authoritative,
> managed hosting, no self-managed servers, MVP plays vs bots on the real netcode path.

---

## 0. Decisions locked (2026-07-10)

Doing a **focused netcode risk-spike NOW** (WS10.0→10.2), *before* the art/perf pass — retire the
biggest unknown first; art is aesthetics, this is functionality.

- **D1 = Host mode** (a player is the authority; free via Photon Cloud). Pay for a dedicated server
  only near real final testing. *(Swappable later — not a rewrite.)*
- **D2 = Fusion-native** — authoritative state lives in Fusion `[Networked]` objects; our pure sim
  *logic* (voxel collision, ballistics, charge math, `SimConfig`) is reused as helpers, and its unit
  tests stay valid. Fusion does prediction/reconciliation for us.
- **D3 = keep the offline path** during the transition (see §6 for what this means).
- **D4 = 30 Hz** simulation tick to start. Cheap to raise later (§6/D4) because all timing is
  already `dt`-based.
- **D5 (lag-comp) — deferred to WS10.3.** **D6 (revisit Fusion) — Fusion kept**, but the free
  alternative (FishNet) is on record if cost/open-source ever outweighs Fusion's built-ins.

**First executable step: `fusion_setup.md`** (create Photon app + import the Fusion SDK — owner-only,
like Firebase). Then code + headless-compile + device-test, phase by phase.

## 1. Why this is *by far* the hardest part

Not hyperbole — seven compounding reasons:

1. **We network a MUTABLE WORLD, not just avatars.** Almost every FPS replicates players +
   projectiles across a **static** map. Here players *build collision geometry mid-fight*, so
   the physical world itself changes at runtime and must stay identical and authoritative across
   the server + every client, in real time, on mobile. This is the "Fortnite problem," and it's
   where the real money and time go.
2. **Server-authority + client-prediction + reconciliation is intrinsically hard.** To feel
   responsive you must predict locally (act before the server replies), then *reconcile* when the
   server disagrees (rewind local state to the server's snapshot and replay your unacknowledged
   inputs). That's a distributed-systems problem with subtle failure modes (rubber-banding,
   mispredicts, jitter).
3. **Lag compensation.** A hit must be judged against what the *shooter* saw, which means the
   server rewinds targets to the shooter's point in time. Getting this fair without making it
   exploitable is delicate.
4. **Mobile reality.** Cellular jitter, packet loss, NAT traversal, tight bandwidth/CPU/battery
   budgets. Replicating a changing voxel world inside a mobile bandwidth budget is a real
   optimization problem, not a formality.
5. **New cheat surface.** Building geometry lets a cheater try to spawn illegal builds, desync the
   world, or wall-hack. Server authority is the defense, but every build/movement/hit must be
   *validated* server-side.
6. **Testing is genuinely hard.** You need ≥2 physical devices, injected latency/jitter/loss,
   desync detection, and cross-region runs. The bugs (desync, hit-reg disputes) are
   hard to reproduce and diagnose.
7. **It's the least reversible + touches everything.** Feel can be tuned; a netcode architecture
   mistake is a rewrite, and every existing system (movement, combat, builds, match flow, bots)
   has to become network-aware at once.

## 2. Why our architecture already de-risks it (the good news)

Every one of these was chosen *specifically* to make WS10 addable without a rewrite:

- **Sim/render split + one `InputCommand`** → the network just becomes another producer of the
  same input struct; the simulation logic doesn't care where input comes from.
- **Fixed-tick, deterministic simulation** → snapshots + replay (the heart of prediction) are
  well-defined; two peers given the same inputs reach the same state.
- **Sim-owned voxel collision** → the server can reproduce collision exactly (not Unity PhysX,
  which isn't deterministic across machines).
- **Voxel builds = bitmask + origin + rotation** → a build is a tiny, deterministic, *rebuildable*
  command, not a blob of geometry. **This is the single most important thing that makes
  mutable-world sync tractable** (see §4).

So the difficulty is real, but the foundation is laid. The work is integration + the mutable-world
core + hardening — not inventing determinism from scratch.

## 3. What Photon Fusion gives us (the model), and why Fusion

**Fusion 2** is a tick-based state-sync netcode library for Unity built for exactly this class of
game. Core pieces we'll use:

- **`NetworkRunner`** — drives the networked simulation; hosts/joins sessions (rooms).
- **Topology = Host/Server (client-server)** — one authority. Either a **Host** (an authoritative
  peer that also plays) or a **Dedicated Server** (headless authority, no local player). *(The
  other topology, Shared Mode, has no single authority → unfit for competitive integrity.)*
- **`FixedUpdateNetwork()`** — Fusion's fixed-tick simulation callback. This is where our
  authoritative gameplay tick runs — the network analogue of today's `SimulationRunner` loop.
- **`[Networked]` properties on `NetworkBehaviour`** — replicated state. Fusion delta-compresses
  it, and **predicts + reconciles it for us** (rolls back to the last server snapshot and replays
  local input on mispredict). This is the plumbing we do *not* want to hand-roll.
- **`INetworkInput`** — per-tick input collection → **maps 1:1 onto our `InputCommand`.**
- **Built-in interpolation** for remote/proxy objects, and **lag compensation** (`Runner.Lag-
  Compensation`) via network hitboxes.
- **Managed hosting** — Photon Cloud rooms/relay (Host mode) or Fusion/Unity dedicated hosting
  (Server mode). Satisfies "no self-managed servers."

**Why Fusion over the alternatives** (locked, but for the record):
- **Photon Quantum** (deterministic rollback) — rejected: imposes strict determinism everywhere
  and is heavier/costlier; our server-authoritative model doesn't need lockstep determinism.
- **Unity Netcode for GameObjects (NGO)** — first-party + free, but weaker built-in
  prediction/reconciliation + lag-comp for competitive server-auth; more to build ourselves.
- **Mirror** — popular + free, but leans client-authoritative and lacks Fusion's prediction/lag-
  comp maturity.
- **Custom** — we'd be reinventing Fusion's hardest 20%.
- **Fusion** — purpose-built for authoritative, predicted, lag-compensated action games with
  managed hosting. Best fit; keep it. *(If you want to reopen this, it's the one truly foundational
  choice — flag it now, not after WS10.1.)*

## 4. The core problem: mutable-world sync — how we actually do it

**Do NOT replicate the voxel grid as state.** Replicate the *causes*, deterministically rebuild the
*effects*:

- The authoritative world = an **ordered log of build operations**: `place(buildId, ownerId,
  templateBitmask, origin, rotation)` and `evict(buildId)`.
- Each op is a handful of bytes. Every peer (server + clients) applies the same ops in the same
  tick order → **identical `VoxelWorld` on every machine**, because reconstruction is deterministic
  (the same code we already have: `World.AddBuild(cells)` from a decoded `VoxelTemplate`).
- **Server validates** each placement authoritatively (energy, simultaneous-cap, no-bury-a-player,
  reach, and future anti-turtle rules). Clients **predict** their own placement instantly for feel;
  on the rare server rejection, Fusion reconciliation removes the mispredicted build.
- **Cap eviction is already deterministic** ("oldest despawns"), so it replicates as an `evict` op
  with no ambiguity.
- **Late-join / reconnect** = send the current op-log (or a compacted snapshot of live builds) so
  the joiner rebuilds the exact world.
- **Bandwidth** is tiny and *event-shaped* (only on place/evict), not per-frame — which is the whole
  reason the voxel representation was chosen.

The genuinely hard sub-problems that remain (and where the time goes):
- **Prediction/collision divergence:** a client predicts a build and *stands on it* before the
  server confirms. If the server rejects or reorders, the client's feet may be inside/above nothing
  after reconciliation. Need careful ordering + tolerant reconciliation of the local body on top of
  predicted geometry.
- **Ordering & determinism under loss:** ops must apply in identical tick order on all peers even
  with packet loss/retransmit; Fusion's tick model helps, but we must keep build application inside
  the networked tick, never in render.
- **Interaction with movement reconciliation:** replaying inputs after a rollback must replay them
  against the *reconstructed* world at each replayed tick, not the latest world.

This sub-workstream (WS10.2) is the crux. Prototype it EARLY (see §7), before any polish.

## 5. Server-authority, prediction, reconciliation, lag-comp — what each is, why, what it does

| Concept | What it is | Why we need it | What it does concretely |
|---|---|---|---|
| **Server authority** | One machine's simulation is the truth | Anti-cheat backbone (pillar: competitive integrity) | Clients send *inputs*; the authority computes state and streams it back. Clients can't fabricate outcomes. |
| **Client prediction** | The local client simulates its own player immediately from local input | Mobile RTT is 40–150 ms; waiting for the server would feel laggy | You move/throw/build the instant you press, before the server replies. |
| **Reconciliation** | On each server snapshot, roll local state back to it and replay unacknowledged inputs | Prediction will sometimes be wrong; must converge to truth without a visible snap | Fusion rewinds `[Networked]` state to the server tick and re-runs `FixedUpdateNetwork` for your buffered inputs → smooth correction. |
| **Interpolation (proxies)** | Remote players/spears are drawn a little in the past, smoothly between snapshots | Snapshots arrive at the send-rate with jitter; raw would stutter | The enemy you see is a smoothed, slightly-delayed render of authoritative state. |
| **Lag compensation** | Server rewinds targets to the shooter's view-time when validating a hit | Otherwise you must "lead" by your ping to hit a moving target — unfair | On a throw/hit, the server checks the hurtbox where the target *was* at the shooter's tick. |

Note our combat is **arced + relatively slow projectiles**, not instant hitscan, so lag-comp is less
brutal than for a hitscan sniper (the spear's flight time dominates), but still required for
fairness on the impact resolution.

## 6. ⚠ Load-bearing decisions (your calls — with recommendations)

**D1 — Topology now vs at launch.**
*Options:* Host mode (a player is the authority; free, uses Photon Cloud rooms; but host has 0-ms
advantage + authority, and host-migration on disconnect is complex) vs Dedicated Server (headless
authority; fair + cheat-resistant; costs hosting $ + ops).
**Recommendation:** **Host mode for the prototype + early PvP** (cheapest, fastest path to validate
the mutable-world core), **switch to Dedicated Server for competitive launch.** Our architecture
makes the authority location swappable, so this is a *timing* decision, not a rewrite. **You decide:
is early PvP casual (host mode fine) or must it be ranked-fair from day one (pay for dedicated
sooner)?**

**D2 — Integration model (the deep one).**
*Options:* **(A)** Hold authoritative state in Fusion `[Networked]` properties on `NetworkBehaviour`s
and run the tick in `FixedUpdateNetwork`, **reusing our pure sim logic** (`VoxelWorld.MoveBody`,
`Ballistics`, charge math, `SimConfig`) as engine-agnostic helpers → we get Fusion's
prediction/reconciliation *for free*. **(B)** Keep `SimCore` as an opaque monolithic authority and
use Fusion as dumb transport, hand-rolling prediction/reconciliation ourselves.
**Recommendation:** **(A).** It preserves the genuinely valuable IP — the deterministic, unit-tested
*logic* and the voxel model — while moving the *state holder* from a POCO into Fusion, which is what
lets Fusion do the hard prediction plumbing. **Consequence to accept:** `SimCore`/`PlayerState` get
refactored so state lives in networked objects; the pure-math files and their tests stay. This is
the biggest structural decision in WS10.

**D3 — Keep an offline path during transition?** Run *everything* (even vs-bots) through Fusion host
mode (single path, exactly matches the locked "bots on the real netcode architecture") **vs** keep
today's standalone `SimCore`/`SimulationRunner` loop for fast iteration while the Fusion path is
built, converging later. **Recommendation:** build the Fusion path but **keep the standalone loop
temporarily** for quick feel-tests + to keep the xUnit suite meaningful; retire it once the Fusion
path is trustworthy. Low-stakes, reversible.

**D4 — Netcode tick rate.** Our sim is 60 Hz. Networked, 60 Hz doubles bandwidth/CPU vs 30 Hz.
**Recommendation:** try **30 Hz simulation tick with interpolation** first (common for mobile);
**validate against charge/throw feel** (the timing-sensitive part) and bump to 60 only if feel
demands. Tunable, but flag it because it touches feel.

**D5 — Lag-comp mechanism.** Fusion's built-in collider-based lag comp (add network hitboxes) vs a
custom rewind of networked positions (our hits are sim sphere-overlaps). **Recommendation:** decide
at the WS10.3 projectile milestone once we see how much the arced/slow projectile forgives; default
to Fusion's built-in if it fits.

**D6 — Revisit Fusion at all?** Locked, and I recommend keeping it (§3). But it's the one
foundational choice — **if you want to reconsider, now is the only cheap time.**

## 7. Phased task list (do in this order; note the EARLY spike)

**WS10.0 — Foundations & decisions**
Resolve D1–D4. Add the Fusion SDK + a Photon app id. Stand up `NetworkRunner`, room create/join,
host mode. Map `InputCommand` → `INetworkInput`. *Why:* nothing works until the session + input
pipe exist. *Output:* two devices join a room and exchange input.

**WS10.1 — Player replication + predicted movement (no combat yet)**
Player as a `NetworkBehaviour`; movement via the existing `MoveBody` in `FixedUpdateNetwork`;
`[Networked]` feet/yaw/pitch/velocity; local prediction + reconciliation; remote interpolation.
*Why:* proves the prediction/reconciliation loop on the simplest system before adding the hard
stuff. *Gate:* two players walk/jump around a **static** arena and it feels smooth on each screen.

**WS10.2 — ⚠⚠ Mutable-world sync (THE core — do this next, before ANY polish)**
Implement §4: build/evict as networked ops, deterministic `VoxelWorld` reconstruction, server
validation, local build prediction + reconciliation, late-join world transfer. *Why:* this is the
project's defining risk; if it can't be made to feel good and stay in sync, we need to know *now*,
not after months of polish. *Gate:* **two-device build-and-fight — both players build, walk on each
other's builds, and the worlds stay identical.** This is the milestone the whole plan is organized
around.

**WS10.3 — Projectiles, combat, lag compensation**
Predicted spear spawn; authoritative flight + hit on the server; damage/health/energy authoritative;
lag-comp per D5. *Why:* combat is the point; hits must be fair + server-judged. *Gate:* throws land
fairly across latency; no double-damage / ghost hits.

**WS10.4 — Match flow authoritative**
Stocks/lives, win/lose, respawn, auto-rematch → server-authoritative; **bots run on the authority**
inside the netcode path. *Why:* the match result must be un-fakeable; bots must exercise the real
stack. *Gate:* a full 3-life match resolves identically on both screens.

**WS10.5 — Matchmaking, lobbies, reconnection, host-migration**
Region-locked matchmaking, session management, reconnect-to-in-progress, and host migration (or
sidestep it by moving to dedicated server). *Why:* real players drop, rejoin, and must be matched by
latency. *Gate:* a dropped client reconnects into the live match; a new match is found in <Xs.

**WS10.6 — Anti-cheat & validation**
Server validates every input/build/movement (rate limits, movement/build sanity, illegal-placement
rejection). Add **deterministic state-hash comparison each tick for desync detection** — free
insurance given our determinism. *Why:* competitive integrity pillar + the new build cheat surface.
*Gate:* a tampered client can't place illegal builds / teleport; desync is detected + logged.

**WS10.7 — ⚠ Netcode test harness (built ALONGSIDE 10.1–10.6, not after)**
Injected latency/jitter/packet-loss (Fusion supports this), two-device + cross-region runs, desync
detection, soak tests. *Why:* you cannot tune or trust netcode you can't perturb and measure; bugs
only appear under bad networks. *Output:* a repeatable "bad network" test rig.

**WS10.8 — Perf + mobile hardening**
Bandwidth budget + delta compression, tick/send-rate tuning, battery/thermal, (interest management
is trivial at 1v1 but keep the seam for FFA later). *Why:* the min-spec mobile floor + sustained
thermal perf are launch gates. *Gate:* meets the fps/bandwidth/battery budgets on the target device
under a degraded network.

## 8. Definition of done (the WS10 gate)

Two players on two mobile devices across regions (with injected latency/jitter/loss) play a full
3-life 1v1: move, jump, throw arced spears, and **build geometry they both stand on**, with builds
and projectiles in sync, hits judged fairly and authoritatively, no visible desync, meeting the
mobile perf budget — and a tampered client cannot cheat. (Mirrors §8 of the context doc.)

## 9. Top risks & mitigations

- **Mutable-world sync proves un-fun under latency** → *spike WS10.2 first* (this plan's core
  ordering); if it can't feel good in host mode at realistic ping, reconsider build mechanics before
  investing further.
- **Integration model (D2) is wrong** → prototype WS10.1 on a throwaway branch; commit to the state
  model only after the prediction loop feels right.
- **Host-mode advantage/migration pain** → treat host mode as a prototype crutch; budget for
  dedicated server before ranked.
- **Scope explosion** → the phase gates are hard stops; do not polish 10.3+ until 10.2's two-device
  gate passes.

---

*This is the WS10 expansion the task catalog anticipated ("expand WS10 into a sprint-level breakdown
first — it carries the most hidden work"). It does not change any locked decision; it sequences and
de-risks their execution.*
