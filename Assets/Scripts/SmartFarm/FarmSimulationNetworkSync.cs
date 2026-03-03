using Unity.Netcode;
using UnityEngine;

namespace SmartFarm
{
    /// <summary>
    /// NetworkBehaviour that syncs farm simulation state from host to all clients.
    /// Host-authoritative: only host writes; clients read.
    /// Attach to a GameObject with NetworkObject in the scene.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class FarmSimulationNetworkSync : NetworkBehaviour, INetworkSyncInterface
    {
        // Owner write permission: LocalOnly host owns scene objects (IsOwner = true for host).
        // DA mode: session creator is assigned ownership of scene-placed NetworkObjects.
        private NetworkVariable<float> _soilMoisture = new NetworkVariable<float>(50f,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<float> _cropHealth = new NetworkVariable<float>(100f,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<float> _waterUsageToday = new NetworkVariable<float>(0f,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<float> _temperature = new NetworkVariable<float>(24f,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<int> _predictedYield = new NetworkVariable<int>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<bool> _irrigationEnabled = new NetworkVariable<bool>(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<Unity.Collections.FixedString64Bytes> _activeAlerts = new NetworkVariable<Unity.Collections.FixedString64Bytes>(default,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _soilMoisture.OnValueChanged += OnAnyValueChanged;
            _cropHealth.OnValueChanged += OnAnyValueChanged;
            _waterUsageToday.OnValueChanged += OnAnyValueChanged;
            _temperature.OnValueChanged += OnAnyValueChanged;
            _predictedYield.OnValueChanged += OnAnyValueChanged;
            _irrigationEnabled.OnValueChanged += OnAnyValueChanged;
            _activeAlerts.OnValueChanged += OnAnyValueChanged;
        }

        public override void OnNetworkDespawn()
        {
            _soilMoisture.OnValueChanged -= OnAnyValueChanged;
            _cropHealth.OnValueChanged -= OnAnyValueChanged;
            _waterUsageToday.OnValueChanged -= OnAnyValueChanged;
            _temperature.OnValueChanged -= OnAnyValueChanged;
            _predictedYield.OnValueChanged -= OnAnyValueChanged;
            _irrigationEnabled.OnValueChanged -= OnAnyValueChanged;
            _activeAlerts.OnValueChanged -= OnAnyValueChanged;
            base.OnNetworkDespawn();
        }

        private void OnAnyValueChanged<T>(T _, T __) => NotifyStateUpdated();

        /// <summary>
        /// Host only: update and broadcast state.
        /// </summary>
        public void SetState(FarmSimulationState state)
        {
            // IsOwner = true for the LocalOnly host AND for the DA session creator
            if (!IsOwner) return;

            _soilMoisture.Value = state.soilMoisturePercent;
            _cropHealth.Value = state.cropHealthPercent;
            _waterUsageToday.Value = state.waterUsageToday;
            _temperature.Value = state.temperature;
            _predictedYield.Value = state.predictedYield;
            _irrigationEnabled.Value = state.irrigationEnabled;

            // Truncate alerts to 64 chars for FixedString64Bytes
            string alerts = state.activeAlertsJson ?? "[]";
            if (alerts.Length > 60) alerts = alerts.Substring(0, 60) + "...";
            _activeAlerts.Value = new Unity.Collections.FixedString64Bytes(alerts);
        }

        /// <summary>
        /// Get current state (works on host and clients).
        /// </summary>
        public FarmSimulationState GetState()
        {
            return new FarmSimulationState
            {
                soilMoisturePercent = _soilMoisture.Value,
                cropHealthPercent = _cropHealth.Value,
                waterUsageToday = _waterUsageToday.Value,
                temperature = _temperature.Value,
                predictedYield = _predictedYield.Value,
                irrigationEnabled = _irrigationEnabled.Value,
                activeAlertsJson = _activeAlerts.Value.ToString(),
                timestampTicks = System.DateTime.UtcNow.Ticks
            };
        }

        public bool IsHostAuthority => IsServer;
        public bool IsConnectedToNetwork => IsSpawned;

        // INetworkSyncInterface
        bool INetworkSyncInterface.IsHost => IsServer;
        bool INetworkSyncInterface.IsConnected => IsSpawned;

        public event System.Action<FarmSimulationState> OnStateUpdated;

        /// <summary>
        /// Clients request actions via PollVoteManager RPCs. This is a fallback for generic actions.
        /// </summary>
        public void RequestAction(string actionType, object payload)
        {
            // Actions (vote, irrigation) are handled by PollVoteManager RPCs.
            // Extend here for future action types if needed.
        }

        private void NotifyStateUpdated()
        {
            OnStateUpdated?.Invoke(GetState());
        }
    }
}
