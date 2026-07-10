// Firebase implementation of the backend seam (WS11). Compiled ONLY when the
// Firebase Unity SDK is imported AND the SPEARFIGHTER_FIREBASE scripting define is
// set — see firebase_setup.md. Until then this file is empty and the game uses the
// NullBackend, so the project always builds.
#if SPEARFIGHTER_FIREBASE
using System;
using System.Collections.Generic;
using UnityEngine;
using Firebase;
using Firebase.Analytics;
using Firebase.Extensions;
using Firebase.RemoteConfig;

namespace Spearfighter.Game
{
    public sealed class FirebaseBackend
    {
        public IAnalyticsService Analytics { get; private set; } = new NullAnalytics();
        public IRemoteConfigService RemoteConfig { get; private set; } = new NullRemoteConfig();

        private readonly FirebaseAnalyticsService _analytics = new FirebaseAnalyticsService();
        private readonly FirebaseRemoteConfigService _remote = new FirebaseRemoteConfigService();

        public void Initialize(Dictionary<string, object> remoteDefaults, Action onReady)
        {
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                if (task.Result != DependencyStatus.Available)
                {
                    Debug.LogWarning("[Firebase] dependencies unavailable: " + task.Result);
                    onReady?.Invoke();
                    return;
                }

                _analytics.MarkReady();
                Analytics = _analytics;

                _remote.Initialize(remoteDefaults, () =>
                {
                    RemoteConfig = _remote;
                    onReady?.Invoke();
                });
            });
        }
    }

    internal sealed class FirebaseAnalyticsService : IAnalyticsService
    {
        private bool _ready;
        public void MarkReady() => _ready = true;

        public void Log(string eventName)
        {
            if (_ready) FirebaseAnalytics.LogEvent(eventName);
        }

        public void Log(string eventName, IReadOnlyDictionary<string, object> parameters)
        {
            if (!_ready) return;
            var list = new List<Parameter>();
            foreach (var kv in parameters)
            {
                switch (kv.Value)
                {
                    case string s: list.Add(new Parameter(kv.Key, s)); break;
                    case bool b:   list.Add(new Parameter(kv.Key, b ? 1L : 0L)); break;
                    case int i:    list.Add(new Parameter(kv.Key, (long)i)); break;
                    case long l:   list.Add(new Parameter(kv.Key, l)); break;
                    case float f:  list.Add(new Parameter(kv.Key, (double)f)); break;
                    case double d: list.Add(new Parameter(kv.Key, d)); break;
                    default:       list.Add(new Parameter(kv.Key, kv.Value?.ToString() ?? "")); break;
                }
            }
            FirebaseAnalytics.LogEvent(eventName, list.ToArray());
        }
    }

    internal sealed class FirebaseRemoteConfigService : IRemoteConfigService
    {
        public bool IsReady { get; private set; }

        public void Initialize(Dictionary<string, object> defaults, Action onReady)
        {
            var rc = FirebaseRemoteConfig.DefaultInstance;
            rc.SetDefaultsAsync(defaults).ContinueWithOnMainThread(_ =>
            {
                rc.FetchAndActivateAsync().ContinueWithOnMainThread(__ =>
                {
                    IsReady = true;
                    onReady?.Invoke();
                });
            });
        }

        public bool TryGetDouble(string key, out double value)
        {
            value = 0;
            if (!IsReady) return false;
            var v = FirebaseRemoteConfig.DefaultInstance.GetValue(key);
            if (v.Source != ValueSource.RemoteValue) return false; // ignore in-app defaults
            try { value = v.DoubleValue; return true; }
            catch { return false; }
        }
    }
}
#endif
