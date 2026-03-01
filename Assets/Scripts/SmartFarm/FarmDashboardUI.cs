using UnityEngine;
using TMPro;
using System.Collections.Generic;
using Unity.Netcode;

namespace SmartFarm
{
    /// <summary>
    /// 3D world-space farm dashboard. DISPLAY ONLY – no simulation logic.
    /// Works with XR Interaction Toolkit, XR Ray Interactor, Tracked Device Graphic Raycaster.
    /// Displays: Soil Moisture, Crop Health, Water Usage, Temperature, Predicted Yield, Alerts.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class FarmDashboardUI : MonoBehaviour
    {
        [Header("Data Source")]
        [SerializeField] private FarmSimulationManager simulationManager;
        [SerializeField] private FarmSimulationNetworkSync networkSync;
        [SerializeField] private PollVoteManager pollManager;

        [Header("Display Fields")]
        [SerializeField] private TMP_Text soilMoistureText;
        [SerializeField] private TMP_Text cropHealthText;
        [SerializeField] private TMP_Text waterUsageText;
        [SerializeField] private TMP_Text temperatureText;
        [SerializeField] private TMP_Text predictedYieldText;
        [SerializeField] private TMP_Text alertsText;

        [Header("Refresh")]
        [SerializeField] [Tooltip("How often to refresh display (seconds)")]
        private float refreshInterval = 0.5f;

        private float _nextRefreshTime;

        private void Awake() { }

        private void Start()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas != null && canvas.worldCamera == null)
                canvas.worldCamera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();

            if (simulationManager == null) simulationManager = FindFirstObjectByType<FarmSimulationManager>();
            if (networkSync == null) networkSync = FindFirstObjectByType<FarmSimulationNetworkSync>();
            if (pollManager == null) pollManager = FindFirstObjectByType<PollVoteManager>();

            EnsureDisplayReferences();

            if (networkSync != null)
                networkSync.OnStateUpdated += OnStateUpdated;

            _nextRefreshTime = 0;  // refresh immediately on first frame
        }

        private void EnsureDisplayReferences()
        {
            var container = transform.Find("DashboardContainer");
            var panel = container != null ? container.Find("Panel") : transform.Find("Panel");
            if (panel == null) return;
            if (soilMoistureText == null) soilMoistureText = panel.Find("SoilMoistureText")?.GetComponent<TMP_Text>();
            if (cropHealthText == null) cropHealthText = panel.Find("CropHealthText")?.GetComponent<TMP_Text>();
            if (waterUsageText == null) waterUsageText = panel.Find("WaterUsageText")?.GetComponent<TMP_Text>();
            if (temperatureText == null) temperatureText = panel.Find("TemperatureText")?.GetComponent<TMP_Text>();
            if (predictedYieldText == null) predictedYieldText = panel.Find("PredictedYieldText")?.GetComponent<TMP_Text>();
            if (alertsText == null) alertsText = panel.Find("AlertsText")?.GetComponent<TMP_Text>();
        }

        private void OnDestroy()
        {
            if (networkSync != null)
                networkSync.OnStateUpdated -= OnStateUpdated;
        }

        private void OnStateUpdated(FarmSimulationState state)
        {
            ApplyState(state);
        }

        private void Update()
        {
            if (Time.time < _nextRefreshTime) return;
            _nextRefreshTime = Time.time + refreshInterval;

            var state = GetCurrentState();
            ApplyState(state);
        }

        private bool ShouldUseNetworkState()
        {
            var nm = NetworkManager.Singleton;
            return nm != null && nm.IsListening && nm.IsConnectedClient && networkSync != null && networkSync.IsSpawned;
        }

        private FarmSimulationState GetCurrentState()
        {
            // Prefer local manager when networking is not fully active.
            if (ShouldUseNetworkState())
                return networkSync.GetState();
            if (simulationManager != null)
                return simulationManager.GetState();
            return FarmSimulationState.Default;
        }

        private void ApplyState(FarmSimulationState state)
        {
            if (soilMoistureText != null)
                soilMoistureText.text = $"Soil Moisture: {state.soilMoisturePercent:F0}%";

            if (cropHealthText != null)
                cropHealthText.text = $"Crop Health: {state.cropHealthPercent:F0}%";

            bool irrigationEnabled = state.irrigationEnabled;
            if (pollManager != null && pollManager.HasLocalAppliedResult)
                irrigationEnabled = pollManager.LocalAppliedIrrigationEnabled;

            if (waterUsageText != null)
                waterUsageText.text = $"Water Usage: {state.waterUsageToday:F1} | Irrigation: {(irrigationEnabled ? "ON" : "OFF")}";

            if (temperatureText != null)
                temperatureText.text = $"Temperature: {state.temperature:F1}°C";

            if (predictedYieldText != null)
                predictedYieldText.text = $"Predicted Yield: {state.predictedYield}";

            if (alertsText != null)
            {
                var alerts = ParseAlerts(state.activeAlertsJson);
                if (alerts.Count == 0)
                {
                    alertsText.text = "No active alerts";
                    alertsText.color = Color.green;
                }
                else
                {
                    alertsText.text = string.Join("\n", alerts);
                    alertsText.color = Color.red;
                }
            }
        }

        private List<string> ParseAlerts(string json)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(json) || json == "[]") return list;
            try
            {
                // Simple parse: ["Alert1","Alert2"] 
                json = json.Trim();
                if (json.StartsWith("[") && json.EndsWith("]"))
                {
                    string inner = json.Substring(1, json.Length - 2);
                    foreach (var part in inner.Split(','))
                    {
                        string s = part.Trim().Trim('"');
                        if (!string.IsNullOrEmpty(s))
                            list.Add(s);
                    }
                }
            }
            catch { }
            return list;
        }
    }
}
