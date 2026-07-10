using System.Collections.Generic;

namespace Spearfighter.Game
{
    /// <summary>
    /// Provider-agnostic backend seam (WS11). Gameplay code talks to these interfaces,
    /// never to a vendor SDK, so Firebase (or anything else) can be swapped without
    /// touching gameplay — and the game runs fully with the no-op NullBackend when no
    /// vendor is configured.
    /// </summary>
    public interface IAnalyticsService
    {
        void Log(string eventName);
        void Log(string eventName, IReadOnlyDictionary<string, object> parameters);
    }

    /// <summary>Live-tunable values fetched from a backend. TryGetDouble returns true
    /// ONLY when a real remote value exists (not an in-app default), so gameplay only
    /// overrides SimConfig with values a human actually set in the console.</summary>
    public interface IRemoteConfigService
    {
        bool IsReady { get; }
        bool TryGetDouble(string key, out double value);
    }
}
