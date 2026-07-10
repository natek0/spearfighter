using System;
using System.Collections.Generic;
using UnityEngine;
using Spearfighter.Simulation;

namespace Spearfighter.Game
{
    /// <summary>
    /// Global access point for the analytics + remote-config services (WS11). Starts
    /// as the NullBackend so everything works with zero setup; <see cref="Init"/>
    /// upgrades to Firebase when the SDK is present AND the SPEARFIGHTER_FIREBASE
    /// scripting define is set (see firebase_setup.md). Firebase init is asynchronous —
    /// onReady fires once it's live (immediately for the Null backend).
    /// </summary>
    public static class Backend
    {
        public static IAnalyticsService Analytics { get; private set; } = new NullAnalytics();
        public static IRemoteConfigService RemoteConfig { get; private set; } = new NullRemoteConfig();
        public static bool Initialized { get; private set; }

        public static void Init(Action onReady = null)
        {
            if (Initialized) { onReady?.Invoke(); return; }
            Initialized = true;

#if SPEARFIGHTER_FIREBASE
            try
            {
                // in-app defaults for remote config = the game's current SimConfig values
                var def = SimConfigRemote.Defaults(SimConfig.Default());
                var fbDefaults = new Dictionary<string, object>(def.Count);
                foreach (var kv in def) fbDefaults[kv.Key] = kv.Value;

                var fb = new FirebaseBackend();
                fb.Initialize(fbDefaults, () =>
                {
                    Analytics = fb.Analytics;
                    RemoteConfig = fb.RemoteConfig;
                    Debug.Log("[Backend] Firebase ready.");
                    onReady?.Invoke();
                });
                return;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Backend] Firebase init failed; staying on NullBackend. " + e);
            }
#endif
            onReady?.Invoke();
        }
    }
}
