# Firebase setup (Analytics + Remote Config) — WS11

The **code is done and dormant.** The game already runs with a no-op `NullBackend`
(analytics prints to the Unity console; remote-config does nothing). Everything below is
the part only *you* can do — a Google account, the Firebase console, the SDK, and a device
build. When you finish, set one scripting define and Firebase goes live with **zero further
code changes**.

## What the code already does
- `Backend.Init()` runs at startup ([Bootstrap.cs](unity/Assets/Spearfighter/Game/Bootstrap.cs)).
  With the define off → `NullBackend`. With it on → Firebase.
- **Remote config** overrides the live `SimConfig` a second or two after launch
  (`SimConfigRemote.Apply`), so values you change in the console tune the game **without a
  rebuild**. Only keys you actually set in the console override anything.
- **Analytics** logs `app_open`, `match_start`, `match_over{result}`, `build_placed{custom}`,
  `build_editor_opened`, `custom_template_saved{cells}`.
- All vendor code is isolated in
  [FirebaseBackend.cs](unity/Assets/Spearfighter/Game/Backend/FirebaseBackend.cs), compiled
  only under the `SPEARFIGHTER_FIREBASE` define.

---

## Your steps

### 1. Create the Firebase project
- Go to <https://console.firebase.google.com> → **Add project** → name it (e.g. `spearfighter`).
- Google Analytics: **enable** it when prompted (that's what powers the analytics dashboards).

### 2. Set your app bundle IDs (Unity)
- Unity → **Edit ▸ Project Settings ▸ Player ▸ Other Settings ▸ Identification**.
- Set **Bundle Identifier** for iOS and Android (e.g. `com.yourname.spearfighter`). Note them —
  Firebase needs an exact match. (It's currently blank.)

### 3. Register the apps + drop in the config files
- In the Firebase console, **Add app ▸ Android**: enter the Android package name → download
  **`google-services.json`** → put it in **`unity/Assets/`**.
- **Add app ▸ iOS**: enter the iOS bundle ID → download **`GoogleService-Info.plist`** → put it
  in **`unity/Assets/`**.
- *(These are client config, not secret keys, but restrict the API key in Google Cloud console
  later, and it's fine to gitignore them if you prefer.)*

### 4. Import the Firebase Unity SDK
- Download the **Firebase Unity SDK** from <https://firebase.google.com/download/unity>.
- In Unity, **Assets ▸ Import Package ▸ Custom Package**, and import **both**:
  - `FirebaseAnalytics.unitypackage`
  - `FirebaseRemoteConfig.unitypackage`
  - (These pull in `FirebaseApp` + the External Dependency Manager automatically.)
- Let the **External Dependency Manager** finish resolving (Android Gradle / iOS CocoaPods).

### 5. Turn the code on
- **Project Settings ▸ Player ▸ Other Settings ▸ Scripting Define Symbols**, for **iOS and
  Android**, add: `SPEARFIGHTER_FIREBASE`
- Unity recompiles; `FirebaseBackend` now compiles in and `Backend.Init()` uses it.
- *(If it fails to compile with "Firebase not found," the Firebase DLLs weren't auto-referenced
  by the `Spearfighter.Game` asmdef — open the asmdef and either enable "Auto Referenced" on the
  Firebase plugins or add them to the asmdef's Assembly References.)*

### 6. Enter the Remote Config parameters (console)
Firebase console → **Remote Config** → add each key below with its **default value**, then
**Publish changes**. (You only need the ones you actually want to tune — but adding them all now
means you can hot-fix any of them later.) These defaults exactly match the game's built-in values,
so publishing them changes nothing until you edit one.

| Key | Default | Key | Default |
|---|---|---|---|
| `spearDamage` | 34 | `buildMaxEnergy` | 100 |
| `jabDamage` | 18 | `buildEnergyRegenPerSec` | 20 |
| `maxHealth` | 100 | `buildCostPerPlace` | 34 |
| `throwSpeedMin` | 15 | `maxSimultaneousBuilds` | 6 |
| `throwSpeedMax` | 34 | `buildReach` | 8 |
| `spearGravity` | -15 | `matchLives` | 3 |
| `jabRange` | 3 | `respawnDelaySeconds` | 2 |
| `chargeFullSeconds` | 1.15 | `matchResetDelaySeconds` | 4 |
| `tapMaxSeconds` | 0.15 | `botPreferredRange` | 12 |
| `enemyHurtRadius` | 1.05 | `botReactionSeconds` | 0.25 |
| `moveSpeed` | 6 | `botChargeSeconds` | 0.7 |
| `jumpSpeed` | 8 | `botTurnRateRadPerSec` | 3 |
| `gravity` | -22 | `botDodgeTimeToImpact` | 0.5 |
| | | `botBuildCooldownSeconds` | 6 |
| | | `botThreatMemorySeconds` | 3 |

### 7. Build to a device and verify
- **Analytics:** Firebase console ▸ Analytics ▸ **DebugView** (enable debug mode on the device)
  shows `app_open`, `match_over`, etc. as you play. (Production events take a few hours to appear
  in the main dashboards; DebugView is real-time.)
- **Remote Config:** in the console, change e.g. `botChargeSeconds` to `1.5`, Publish, relaunch
  the app — the bot noticeably slows its throws. That's live tuning with no rebuild.

---

## Notes
- **Bot difficulty tiers** (an open WS5 item) fall out of this for free — tier presets are just
  different remote-config value sets you can A/B or ship per-cohort.
- **Fetch caching:** Firebase caches config for ~12 h by default, so during testing you may need
  to relaunch / clear app data to see a change immediately. (A dev-only low fetch interval can be
  added in `FirebaseRemoteConfigService` if this gets annoying.)
- **Crashlytics** (crash reporting) is the natural next add from the same Firebase project — a
  separate `.unitypackage`, no gameplay code. Worth doing before external testers (WS18).
- To go back to no-backend, just remove the `SPEARFIGHTER_FIREBASE` define — the game returns to
  the `NullBackend` with no other changes.
