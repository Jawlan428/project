using Unity.Netcode;
using UnityEngine;

namespace SmartFarm
{
    /// <summary>
    /// Syncs the active WeatherType from the simulation authority to all clients.
    ///
    /// Uses NetworkVariableWritePermission.Owner:
    ///   LocalOnly → host owns scene-placed NetworkObjects (IsOwner = true)
    ///   DA mode   → session creator is assigned ownership of scene-placed NetworkObjects
    ///
    /// Attach to FarmSimulationHub (same GameObject as the NetworkObject).
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class WeatherNetworkSync : NetworkBehaviour
    {
        private readonly NetworkVariable<byte> _weatherType = new NetworkVariable<byte>(
            (byte)WeatherManager.WeatherType.Sunny,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private WeatherManager _weatherManager;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _weatherManager = FindFirstObjectByType<WeatherManager>();
            _weatherType.OnValueChanged += OnNetworkWeatherChanged;

            if (IsOwner) // true for LocalOnly host + DA session creator
            {
                if (_weatherManager != null)
                {
                    _weatherManager.OnWeatherChanged += OnHostWeatherChanged;
                    _weatherType.Value = (byte)_weatherManager.CurrentWeather;
                }
            }
            else
            {
                // Client: apply current synced weather immediately on join
                ApplyWeatherLocally((WeatherManager.WeatherType)_weatherType.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            _weatherType.OnValueChanged -= OnNetworkWeatherChanged;

            if (_weatherManager != null)
                _weatherManager.OnWeatherChanged -= OnHostWeatherChanged;

            base.OnNetworkDespawn();
        }

        private void OnHostWeatherChanged(WeatherManager.WeatherType newType)
        {
            if (IsOwner)
                _weatherType.Value = (byte)newType;
        }

        private void OnNetworkWeatherChanged(byte _, byte newValue)
        {
            if (IsOwner) return; // owner already applied it locally
            ApplyWeatherLocally((WeatherManager.WeatherType)newValue);
        }

        private void ApplyWeatherLocally(WeatherManager.WeatherType type)
        {
            if (_weatherManager == null)
                _weatherManager = FindFirstObjectByType<WeatherManager>();

            if (_weatherManager == null) return;
            if (_weatherManager.CurrentWeather == type) return;

            _weatherManager.SetWeather(type);
        }
    }
}
