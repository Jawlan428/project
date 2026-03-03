using Unity.Netcode;
using XRMultiplayer;

namespace SmartFarm
{
    /// <summary>
    /// Shared authority check for all simulation managers.
    /// Works in both session types used by this project:
    ///
    ///   LocalOnly (LAN)         → NetworkManager.IsServer is true for the host
    ///   Distributed Authority   → NetworkManager.IsServer is ALWAYS false (cloud service = server)
    ///                             ISession.IsHost is true for the session creator
    ///   Offline / Editor        → always authoritative
    ///
    /// ISession.IsHost is already used in SessionManager.UpdateLobbyName() — it is the
    /// correct UGS way to identify the session owner in DA mode.
    /// </summary>
    public static class NetworkHelper
    {
        /// <summary>
        /// Returns true when this instance should run authoritative simulation logic
        /// (tick crops, broadcast state, drive irrigation, etc.).
        /// </summary>
        public static bool IsSimulationAuthority
        {
            get
            {
                var nm = NetworkManager.Singleton;

                // ── Offline / Editor ──────────────────────────────────────────
                if (nm == null || !nm.IsListening) return true;

                // ── LocalOnly (LAN) mode ──────────────────────────────────────
                // IsServer is true only for the traditional host in client-server topology
                if (nm.IsServer) return true;

                // ── Distributed Authority mode ────────────────────────────────
                // IsServer is false for every local client; use ISession.IsHost instead.
                // ISession.IsHost is already used in SessionManager.UpdateLobbyName()
                // for host-only room operations — it is the correct authority check for DA.
                try
                {
                    var session = XRINetworkGameManager.Instance?.sessionManager?.currentSession;
                    return session?.IsHost ?? false;
                }
                catch
                {
                    // XRINetworkGameManager not yet initialized or session not joined
                    return false;
                }
            }
        }

        /// <summary>
        /// Convenience inverse — use in guards like: if (NetworkHelper.IsNotSimulationAuthority) return;
        /// </summary>
        public static bool IsNotSimulationAuthority => !IsSimulationAuthority;
    }
}
