# Iteration 2 — Polish, Viewmodel, Ramp Fix (Technical Plan)

> Written after the first on-device (iPhone) playtest of the Phase 0/1 build.
> This is the plan to follow in subsequent prompts. Nothing here is coded yet.
> Build/test loop: Unity → **File → Build Profiles → Build** (same `iOSBuild`
> folder, choose **Append**) → Xcode **▶ Run**.

Legend: 🐞 = bug fix · ✨ = feature · 🧱 = foundation/refactor · 🕓 = deferred.

---

## Priority order (recommended)
1. 🐞 **Ramp traversal** — blocks the core build-for-verticality loop. Highest priority.
2. 🐞 **HUD safe-area + button placement** — quick, high daily-annoyance payoff.
3. ✨ **Build placement preview UX** — remove the always-on ghost; show intent-driven preview. *(needs your decision — see Decisions)*
4. ✨ **First-person viewmodel (arms + held spear) + off-center arc origin** — fixes the "orange circle," makes the aim arc usable, foundation for skins. *(needs your decision)*
5. 🕓 **Terrain / world generator** — architecture seam only; implement later.

---

## 1. 🐞 Ramp traversal (walk up the ramp)

**Symptom:** the ramp behaves like a wall; you can't walk up it.

**Root cause:** `CollisionWorld.ResolveBody` (in `unity/Assets/Spearfighter/Simulation/CollisionWorld.cs`).
For a ramp it only skips the horizontal push-out when the player's **center** is inside
the footprint *and* at/above the slope surface. When the player approaches the low
edge, their center is still just **outside** the footprint but their capsule radius
(0.45 m) overlaps it → the code runs the circle-vs-AABB push and shoves them back by
up to a full radius. They can never get their center onto the low edge, so the whole
ramp reads as a wall.

**Fix (specific):** in `ResolveBody`, for a ramp compute the slope surface height at
the **closest footprint point to the player** (not the player's center), and only push
out when that surface is **above `feet.Y + StepTolerance`** (i.e., a genuine wall from
the player's current height). Where the ramp is low (near the base), do **not** push —
let the player walk in; `SupportHeight` then lifts them up the slope each tick.

Sketch:
```
// inside ResolveBody, per ramp collider:
float cx = Clamp(feet.X, Min.X, Max.X);
float cz = Clamp(feet.Z, Min.Z, Max.Z);
float surfHere = SurfaceHeight(cx, cz);          // slope height at nearest point
if (surfHere <= feet.Y + StepTolerance) continue; // low/walkable → no wall push
// else: treat as wall → existing circle-vs-AABB push-out
```
Keep the existing box (wall/pillar) path unchanged.

**Also:** raise `StepTolerance` slightly if needed (currently 0.35 m) and verify
`SupportHeight` snap distance so climbing is smooth, not steppy.

**New test (must add):** `PlayerWalksUpRampFromGroundViaMovement` in
`sim/.../BuildMovementTests.cs` — place the player on flat ground a couple metres in
front of a ramp's low edge, feed `Move = (0,1)` for ~120 ticks, assert `feet.Y` climbs
to near the ramp top and the player is not stuck at the base. (The current test only
drops the player *onto* the ramp; it never exercised walking *up*, which is why this
slipped through.)

**Definition of done:** on device, you build a ramp in front of you and walk straight
up it; jumping off the top gains height.

---

## 2. 🐞 HUD safe-area + button placement

**Symptoms:** (a) "Hits"/HP/BUILD text clipped on the left edge; (b) touch buttons are
top-right — you want them bottom-right.

**Cause (a):** iPhone landscape has a safe-area inset (notch/camera + rounded corners)
we ignore; we draw at raw `x = pad`.
**Cause (b):** in `PlayerInput.RecomputeRects`, button Y uses `h - d*…`. Screen space is
y-**up**, so `h - …` is the **top**. It should be a small Y for the bottom.

**Fix:** use `Screen.safeArea` (a Rect in pixels) everywhere:
- In `PlayerInput.RecomputeRects`: anchor buttons to `safeArea.xMax` (right) and
  `safeArea.yMin` (bottom). Attack (biggest) lowest-right; Jump left of it; Build above
  Attack; Rotate near Build. All in "u" = `safeArea.height/900` units.
- In `HudGui.OnGUI`: start the top-left cluster at `safeArea.xMin + margin` and
  `Screen.height - safeArea.yMax + margin` (GUI is y-down), so text clears the notch.
- Cache `safeArea` and recompute when it changes (already recompute rects each frame).

**Files:** `PlayerInput.cs` (RecomputeRects), `HudGui.cs` (OnGUI anchoring).

**DoD:** all text fully on-screen with margin; Throw/Jab, Jump, Build, Rot buttons sit
comfortably at the bottom-right, reachable by the right thumb.

---

## 3. ✨ Build placement preview UX

**Symptom:** the ghost (now cyan, opaque) shows **any** time you look below the
horizon; on flat ground that's always, and it blocks the view.

**Goal:** you see exactly where the build will land **only when you intend to build**,
and the preview never obscures normal play.

**DECISION (locked): Option A — hold-to-preview, release to place.**

**Design options (A chosen):**
- **A. Hold-to-preview (CHOSEN).** Press-and-hold BUILD → translucent ghost appears
  at the aimed spot and tracks your aim; **release places it**. Quick tap = place
  immediately at current aim (ghost flashes for a frame). Ghost only ever visible while
  the thumb is on BUILD. Matches "know where it goes in the moment."
- **B. Toggle build mode.** Tap BUILD to enter "build mode" (ghost shows, reticle on);
  tap again / tap a confirm to place; auto-exit after placing. More clicks; clearer.
- **C. No mesh ghost — ground reticle only.** Always show a small flat outline/decal
  where the build's footprint would land (doesn't block view), place on BUILD press.

**Implementation for A (recommended):**
- Sim (`Simulation.cs`): change building from "place on rising edge" to **place on
  falling edge** of `BuildHeld`; expose `bool IsPreviewingBuild => BuildHeld && placement valid`.
  (For a quick tap, press+release within a few ticks still yields one placement.)
- Renderer (`WorldRenderer.RenderGhost`): show the ghost **only** when `IsPreviewingBuild`.
- **Ghost material:** make it truly translucent (URP transparent): on the material set
  `_Surface = 1` (Transparent), `_Blend = 0` (Alpha), `renderQueue = 3000`, and an alpha
  ~0.35 color. Add a `Mats.NewTransparent(color)` helper. (The current opaque cyan is
  the eyesore.) Color: soft cyan/white, distinct from the pink placed builds.
- Optional polish: tint the ghost **red** when placement is invalid (out of energy /
  would bury a player) using `CanPlaceBuild`.

**Files:** `Simulation.cs`, `WorldRenderer.cs`, `Mats.cs`, maybe `SimConfig` (no new tunables needed).

**DoD:** no ghost during normal movement/combat; holding BUILD shows a clean
translucent preview you can aim; releasing places a pink ramp there.

---

## 4. ✨ First-person viewmodel (arms + held spear) + usable aim arc

This bundles four of your notes: the "orange circle," the near-invisible arc, visible
arms, and a visible held spear (skins foundation).

**Why they're one problem:** the arc + real spear currently spawn from
`eye + forward*0.6` — dead screen-center — so the preview dots pile into a blob ("orange
circle") and the arc barely reads. Moving the origin to a **held spear in the lower-right**
(where a real thrower's hand is) both removes the center blob and gives the arc a clear
starting point to curve away from.

### 4a. Muzzle offset in the simulation
- `SimConfig`: replace `SpawnForwardOffset` / `SpawnVerticalOffset` with
  `MuzzleForward`, `MuzzleRight`, `MuzzleUp` (e.g. 0.55 / 0.30 / -0.20 m).
- `Simulation`: add a helper `MuzzleAndDir(PlayerState p, out origin, out dir)` used by
  **both** `DoThrow` and `TryGetChargePreview`, so the real spear and the preview share
  one origin. Compute:
  ```
  fwd = Forward(yaw,pitch); right = Normalize(Cross(fwd, up)); up' = Cross(right, fwd);
  origin = eye + fwd*MuzzleForward + right*MuzzleRight + up'*MuzzleUp;
  ```
  **Handedness note:** our Unity camera intentionally mirrors X (that's why we negate
  look/move at the input layer). So to make the muzzle appear on the **screen's right**,
  use `-right` for the lateral term in the sim (or equivalently negate `MuzzleRight`).
  I'll verify the sign on-device and lock it. (Longer term we can clean up the sim↔camera
  handedness so no negations are needed — logged as 🧱 tech-debt, not now.)

### 4b. Viewmodel rig (arms + spear), rendered without clipping
- **Rendering approach — DECISION (locked): dedicated overlay camera.**
  - **Overlay camera (CHOSEN, standard FPS).** A second URP **Overlay** camera
    stacked on the main camera, rendering only a `Viewmodel` layer, clearing depth, with
    a narrow FOV. The arms/spear live on that layer as children of the main camera, so
    they always draw on top and never poke through walls.
  - **Single camera (simpler).** Parent the viewmodel to the camera at close range; risk
    of clipping into nearby geometry. Fine for a greybox but looks wrong near walls.
- **Geometry (greybox now, art later):** a `ViewmodelRig` prefab/among code-built
  primitives — a right upper-arm + forearm + hand (stretched capsules/boxes) and a spear
  (thin cylinder) held in the hand, angled forward, positioned lower-right so the spear
  tip sits near the arc origin. Static pose (no/minimal animation for now).
- **Alignment:** place the viewmodel spear tip at camera-local `(MuzzleRight, MuzzleUp,
  MuzzleForward)` so it visually coincides with the sim muzzle → the arc emanates from
  the visible spear tip.
- **Throw feedback (minimal):** on `SpearThrown` event, briefly hide/kick the held spear
  and show it again after a short cooldown (represents throwing & drawing a new one). Keep
  it dead simple; real animation is Phase 2 art.

### 4c. Aim arc visibility
- Bigger dots that scale with distance already (sizeAttenuation-like) — increase base dot
  size; skip the first ~1–2 dots at the muzzle so they don't blob.
- Add a **landing marker** (a flat ring/disc) at the arc's end point for clarity.
- Keep the arc on only while charging a throw (unchanged), now originating off-center.

**Files:** `SimConfig.cs`, `Simulation.cs` (MuzzleAndDir, DoThrow, TryGetChargePreview),
`WorldRenderer.cs` or new `ViewmodelRig.cs`, `TrajectoryRenderer.cs`, `Bootstrap.cs`
(create overlay camera + layer), `Mats.cs`.

**DoD:** you see a greybox right arm holding a spear in the lower-right; the dotted arc
starts at the spear tip and clearly curves to where it'll land; no blob in screen center.

---

## 5. Spear projectile appearance (minor, folds into #4)
The flying projectile is already a thin tan cylinder oriented to velocity; once the
origin is off-center (#4a) it reads as a spear leaving your hand rather than a circle in
your face. Optional: add a small motion trail on `SpearThrown`. No separate work item.

---

## 6. 🕓 Terrain / world generator (deferred — seam only)
You want non-flat terrain (hills/valleys) later. We don't build it now, but keep the
sim ready:
- Today the ground is a flat plane at `CollisionWorld.GroundHeight`. Generalize to a
  **heightfield sampler**: `float GroundHeightAt(x,z)` backed later by a generated
  heightmap. `SupportHeight` and the spear/trajectory ground checks already call into the
  world, so they'd switch from a constant to `GroundHeightAt` with minimal churn.
- Matching Unity side: a mesh generator that builds the visible terrain from the same
  heightfield (so collision == visual, like the ramp).
- World-gen algorithm (Perlin/simplex hills, seeded for determinism/netcode) is a future
  workstream. **No code now** — just don't hard-assume flatness in new code.

---

## 7. Test & verification plan
- **Sim unit tests (run `cd sim && dotnet test`):** add the ramp walk-up test (#1);
  keep determinism green after the muzzle refactor (muzzle is deterministic).
- **Unity batch compile** after each change set (catches Unity-only errors the dotnet
  build can't see): headless `-batchmode -quit` compile, grep for `error CS`.
- **On-device:** one Build (Append) + Xcode Run per change set; check ramp walk, HUD
  placement, build preview, viewmodel/arc.

## 8. Decisions — RESOLVED
1. **Build preview interaction** — ✅ **Option A: hold-to-preview, release to place.**
2. **Viewmodel rendering** — ✅ **Dedicated overlay camera.**

Both locked; the plan above reflects them. Ready to implement in the priority order in
section "Priority order."
