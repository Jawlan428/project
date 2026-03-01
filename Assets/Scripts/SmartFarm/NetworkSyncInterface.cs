using System;
using UnityEngine;

namespace SmartFarm
{
    /// <summary>
    /// Abstract interface for host-authoritative farm state synchronization.
    /// Implementations use Unity Netcode for GameObjects to sync from host to clients.
    /// Clients read state; only host writes.
    /// </summary>
    public interface INetworkSyncInterface
    {
        /// <summary>
        /// Whether this instance is the host (authoritative).
        /// </summary>
        bool IsHost { get; }

        /// <summary>
        /// Whether we are connected to a network session.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Get the latest synchronized farm state.
        /// </summary>
        FarmSimulationState GetState();

        /// <summary>
        /// Called when state is updated (for UI refresh).
        /// </summary>
        event Action<FarmSimulationState> OnStateUpdated;

        /// <summary>
        /// Request an action from the host (e.g., vote, irrigation toggle).
        /// Clients call this; host validates and applies.
        /// </summary>
        void RequestAction(string actionType, object payload);
    }

    /// <summary>
    /// Fallback implementation when not using networking (single-player / editor).
    /// State is set directly by FarmSimulationManager.
    /// </summary>
    public class LocalSyncInterface : INetworkSyncInterface
    {
        private FarmSimulationState _state;
        public bool IsHost => true;
        public bool IsConnected => false;
        public event Action<FarmSimulationState> OnStateUpdated;

        public FarmSimulationState GetState() => _state;

        public void SetState(FarmSimulationState state)
        {
            _state = state;
            OnStateUpdated?.Invoke(_state);
        }

        public void RequestAction(string actionType, object payload)
        {
            // In local mode, actions are applied directly by FarmSimulationManager
            // This is a no-op; the manager handles it
        }
    }
}
