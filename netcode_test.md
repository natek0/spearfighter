# WS10.0/10.1 netcode test — two-peer movement (Mac host + iPhone client)

This is the first networking milestone: two players join one Photon room and see each other
**move around, synced**, using our real voxel-collision movement. No second person needed — your
**Mac (editor) is player 1, your iPhone (build) is player 2**. No combat/builds yet (that's WS10.2,
built once this syncs).

## What got built (all compile-verified)
- `Spearfighter.Net` assembly (isolated — the offline game is untouched): `NetInput`, `NetPlayer`
  (Fusion `NetworkBehaviour` running our `VoxelWorld.MoveBody`), `NetLauncher` (host/join + input),
  `NetArena` (flat ground for now).
- `Assets/Spearfighter/Net/NetPlayer.prefab` (NetworkObject + NetPlayer + pink body).
- `Assets/Spearfighter/Net/NetTest.unity` scene (a `NetLauncher` wired to the prefab), added to
  Build Settings.

## Test steps

### On the Mac (host)
1. Open the scene **`Assets/Spearfighter/Net/NetTest.unity`** (double-click it in the Project window).
2. Press **Play**.
3. Click **HOST (Mac)**. You're now the authority, first-person, standing on a dark ground plane.
4. Move with **WASD**, look by **dragging the mouse**.

### On the iPhone (client)
5. In **File ▸ Build Settings**, drag **NetTest** to the **top** of the scene list (so the phone
   launches into it), make sure it's checked, and **Build & Run** to your iPhone. *(Drag `gamev1`
   back to the top afterwards for normal builds.)*
6. On the iPhone, tap **JOIN (iPhone)**.
7. Move by **dragging on the LEFT half** of the screen, look by **dragging on the RIGHT half**.
   (The visual joystick/buttons aren't in this bare test scene — just drag.)

### What success looks like
- Both connect (the status line at top-left says "Connected").
- On the Mac you see a **pink capsule** (the iPhone's player) that **moves when you move the phone**,
  and vice-versa. Movement should be smooth (Fusion is predicting + interpolating).

## If something doesn't work
- **They don't find each other / "Failed":** almost always a **Photon region mismatch**. Fix: open
  **Tools ▸ Fusion ▸ Realtime Settings** and set a **Fixed Region** (e.g. `us`) so both peers use the
  same one, rebuild, retry.
- **Runtime error "prefab not registered" on spawn:** click **Tools ▸ Fusion ▸ Rebuild Prefab
  Table** once, then Play again. (Fusion usually bakes it automatically on import; this forces it.)
- **Can't move / no camera:** make sure you clicked HOST/JOIN (players only spawn after you join a
  session).

Tell me what you see — especially whether the two capsules sync — and I'll build **WS10.2 (the real
arena + mutable-world build-and-fight)** on top of this foundation.
