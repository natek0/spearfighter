# Photon Fusion setup — WS10.0 (owner step)

This is the first task of the netcode spike, and it's the part only you can do (a Photon account +
an SDK download/import — just like the Firebase steps). Once it's done, tell me and I'll write the
netcode against the real SDK, headless-compile-check it, and hand you builds to device-test.

**Why this must come first:** the Fusion-native design (D2) holds our authoritative game state in
Fusion's own types (`NetworkBehaviour`, `[Networked]`). Those don't exist in the project until the
SDK is imported, so no netcode compiles until this is done.

## Steps

### 1. Create a Photon account + a Fusion app
- Go to <https://dashboard.photonengine.com> → sign up / log in.
- **Create a new app** → **Photon Type: Fusion** → give it a name (e.g. `spearfighter`).
- Copy the **App ID** it gives you (a long GUID). You'll paste it into Unity in step 3.
- The **free tier** (limited concurrent users) is plenty for development + 2-device testing.

### 2. Import the Fusion 2 SDK into Unity
- Download **Fusion 2** from the Photon site (dashboard → SDKs, or
  <https://doc.photonengine.com/fusion/current/getting-started/sdk-download>). It's a
  `.unitypackage`.
- In Unity: **Assets ▸ Import Package ▸ Custom Package** → select the Fusion `.unitypackage` →
  Import all.
- A **Fusion Hub** window usually opens automatically (or **Tools ▸ Fusion ▸ Fusion Hub**).

### 3. Paste the App ID
- In the Fusion Hub (or **Tools ▸ Fusion ▸ Realtime Settings** → the `PhotonAppSettings` asset),
  paste your **Fusion App ID** into the App Id field.
- That's what connects the game to Photon's cloud.

### 4. Sanity check
- Unity recompiles with the Fusion assemblies present (a **Fusion** menu appears).
- No need to build or run anything yet — just confirm it imported without errors.

### 5. Ping me
- Tell me it's imported + the App ID is set. I'll then:
  1. Add a `SPEARFIGHTER_FUSION` scripting define (so the netcode code compiles only when the SDK
     is present — the offline path keeps working meanwhile, D3).
  2. Write WS10.0→10.2 (session/room + input pipe → predicted movement → **mutable-world sync**),
     headless-compile-checking each step.
  3. Hand you two builds to test the two-device build-and-fight.

## Notes
- **Cost:** Fusion is a paid product with a free dev tier; you won't pay anything for the spike.
  We stay on **Host mode** (D1) so there's **no dedicated-server cost** — players connect through
  Photon's cloud relay/rooms, and one player's device is the authority.
- **You'll need two devices** (or one device + the Unity editor as the second peer) to test the
  actual multiplayer once the code lands.
- This doesn't touch the working single-player game — that keeps running on the offline path (D3)
  the whole time.
