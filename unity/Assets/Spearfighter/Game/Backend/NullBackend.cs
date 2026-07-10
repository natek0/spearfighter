using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Spearfighter.Game
{
    /// <summary>
    /// Default backend when no vendor is configured. Analytics prints to the Unity
    /// console (genuinely useful for solo playtesting — you see match_over etc.),
    /// and remote-config has no overrides (SimConfig defaults / SimConfigAsset win).
    /// </summary>
    public sealed class NullAnalytics : IAnalyticsService
    {
        public void Log(string eventName) => Debug.Log($"[analytics] {eventName}");

        public void Log(string eventName, IReadOnlyDictionary<string, object> parameters)
        {
            var sb = new StringBuilder("[analytics] ").Append(eventName);
            if (parameters != null)
                foreach (var kv in parameters) sb.Append(' ').Append(kv.Key).Append('=').Append(kv.Value);
            Debug.Log(sb.ToString());
        }
    }

    public sealed class NullRemoteConfig : IRemoteConfigService
    {
        public bool IsReady => true;
        public bool TryGetDouble(string key, out double value) { value = 0; return false; }
    }
}
