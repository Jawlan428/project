using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace SmartFarm
{
    public enum FarmAlertSeverity { Info, Warning, Critical }

    [Serializable]
    public class FarmAlertItem
    {
        public string id;
        public FarmAlertSeverity severity;
        public DateTime timestampUtc;
        public string message;
        public bool acknowledged;
    }

    [Serializable]
    public class FarmHistoryItem
    {
        public DateTime timestampUtc;
        public string message;
    }

    /// <summary>
    /// UI data source for tablet pages.
    /// Keeps UI event-driven while reusing FarmSimulationManager + PollVoteManager.
    /// </summary>
    public class FarmDataManager : MonoBehaviour
    {
        [Header("Data Sources")]
        [SerializeField] private FarmSimulationManager simulationManager;
        [SerializeField] private FarmSimulationNetworkSync networkSync;
        [SerializeField] private PollVoteManager pollVoteManager;

        [Header("Refresh")]
        [SerializeField] private float fallbackRefreshInterval = 0.4f;
        [SerializeField] private int maxHistoryItems = 200;

        public event Action<FarmSimulationState> OnDataChanged;
        public event Action<IReadOnlyList<FarmAlertItem>, int> OnAlertsChanged;
        public event Action<IReadOnlyList<FarmHistoryItem>> OnHistoryChanged;
        public event Action OnPollChanged;

        private readonly List<FarmAlertItem> _activeAlerts = new List<FarmAlertItem>();
        private readonly List<FarmHistoryItem> _history = new List<FarmHistoryItem>();
        private Coroutine _fallbackLoop;

        public FarmSimulationManager SimulationManager => simulationManager;
        public PollVoteManager PollVoteManager => pollVoteManager;
        public IReadOnlyList<FarmAlertItem> ActiveAlerts => _activeAlerts;
        public IReadOnlyList<FarmHistoryItem> History => _history;

        private void Awake()
        {
            if (simulationManager == null) simulationManager = FindFirstObjectByType<FarmSimulationManager>();
            if (networkSync == null) networkSync = FindFirstObjectByType<FarmSimulationNetworkSync>();
            if (pollVoteManager == null) pollVoteManager = FindFirstObjectByType<PollVoteManager>();
        }

        private void OnEnable()
        {
            if (simulationManager != null)
                simulationManager.OnStateChanged += OnStateFromSimulation;
            if (networkSync != null)
                networkSync.OnStateUpdated += OnStateFromNetwork;
            if (pollVoteManager != null)
            {
                pollVoteManager.OnVoteReceived.AddListener(OnPollEvent);
                pollVoteManager.OnPollResultApplied.AddListener(OnPollResultApplied);
            }

            EventLogger.OnEventLogged += OnEventLogged;
            _fallbackLoop = StartCoroutine(FallbackStateLoop());
            PublishCurrentState();
        }

        private void OnDisable()
        {
            if (simulationManager != null)
                simulationManager.OnStateChanged -= OnStateFromSimulation;
            if (networkSync != null)
                networkSync.OnStateUpdated -= OnStateFromNetwork;
            if (pollVoteManager != null)
            {
                pollVoteManager.OnVoteReceived.RemoveListener(OnPollEvent);
                pollVoteManager.OnPollResultApplied.RemoveListener(OnPollResultApplied);
            }

            EventLogger.OnEventLogged -= OnEventLogged;
            if (_fallbackLoop != null) StopCoroutine(_fallbackLoop);
        }

        private IEnumerator FallbackStateLoop()
        {
            var wait = new WaitForSeconds(fallbackRefreshInterval);
            while (true)
            {
                yield return wait;
                PublishCurrentState();
            }
        }

        private void OnStateFromSimulation(FarmSimulationState state) => PublishState(state);
        private void OnStateFromNetwork(FarmSimulationState state) => PublishState(state);

        private void PublishCurrentState()
        {
            var state = FarmSimulationState.Default;
            bool useNetwork = NetworkManager.Singleton != null
                && NetworkManager.Singleton.IsListening
                && NetworkManager.Singleton.IsConnectedClient
                && networkSync != null
                && networkSync.IsSpawned;

            if (useNetwork)
                state = networkSync.GetState();
            else if (simulationManager != null)
                state = simulationManager.GetState();

            PublishState(state);
        }

        private void PublishState(FarmSimulationState state)
        {
            OnDataChanged?.Invoke(state);
            UpdateAlerts(state.activeAlertsJson);
        }

        private void UpdateAlerts(string alertsJson)
        {
            var activeMessages = ParseAlerts(alertsJson);
            _activeAlerts.RemoveAll(a => !activeMessages.Contains(a.message));

            for (int i = 0; i < activeMessages.Count; i++)
            {
                string msg = activeMessages[i];
                if (_activeAlerts.Exists(a => a.message == msg)) continue;
                _activeAlerts.Add(new FarmAlertItem
                {
                    id = Guid.NewGuid().ToString("N"),
                    message = msg,
                    timestampUtc = DateTime.UtcNow,
                    severity = GetSeverity(msg),
                    acknowledged = false
                });
            }

            int unread = 0;
            for (int i = 0; i < _activeAlerts.Count; i++)
                if (!_activeAlerts[i].acknowledged) unread++;

            OnAlertsChanged?.Invoke(_activeAlerts, unread);
        }

        public void AcknowledgeAlert(string alertId)
        {
            for (int i = 0; i < _activeAlerts.Count; i++)
            {
                if (_activeAlerts[i].id != alertId) continue;
                _activeAlerts[i].acknowledged = true;
                break;
            }

            int unread = 0;
            for (int i = 0; i < _activeAlerts.Count; i++)
                if (!_activeAlerts[i].acknowledged) unread++;

            OnAlertsChanged?.Invoke(_activeAlerts, unread);
        }

        public void SetIrrigationManual(bool enabled)
        {
            if (simulationManager == null) return;
            simulationManager.SetIrrigationEnabled(enabled);
            PublishCurrentState();
        }

        public void ToggleIrrigationManual()
        {
            if (simulationManager == null) return;
            simulationManager.ToggleIrrigation();
            PublishCurrentState();
        }

        public void BoostIrrigation30Seconds(float amountPerPlant = 12f)
        {
            if (simulationManager == null) return;
            StartCoroutine(BoostRoutine(amountPerPlant, 30f));
        }

        private IEnumerator BoostRoutine(float amountPerPlant, float seconds)
        {
            bool previous = simulationManager.IrrigationEnabled;
            simulationManager.SetIrrigationEnabled(true);
            simulationManager.ApplyInstantMoistureBoost(amountPerPlant);
            PublishCurrentState();
            EventLogger.LogEvent("Boost irrigation started (30s)");

            yield return new WaitForSeconds(seconds);

            if (!previous)
                simulationManager.SetIrrigationEnabled(false);
            PublishCurrentState();
            EventLogger.LogEvent("Boost irrigation finished");
        }

        public void ApplySchedulePlaceholder(string preset)
        {
            EventLogger.LogEvent($"Irrigation schedule selected: {preset} (placeholder)");
        }

        private void OnPollEvent(string _)
        {
            OnPollChanged?.Invoke();
        }

        private void OnPollResultApplied(bool _)
        {
            OnPollChanged?.Invoke();
            PublishCurrentState();
        }

        private void OnEventLogged(DateTime timestampUtc, string message)
        {
            _history.Insert(0, new FarmHistoryItem { timestampUtc = timestampUtc, message = message });
            if (_history.Count > maxHistoryItems)
                _history.RemoveRange(maxHistoryItems, _history.Count - maxHistoryItems);
            OnHistoryChanged?.Invoke(_history);
        }

        private static FarmAlertSeverity GetSeverity(string message)
        {
            if (string.IsNullOrEmpty(message)) return FarmAlertSeverity.Info;
            if (message.Contains("Critical", StringComparison.OrdinalIgnoreCase)) return FarmAlertSeverity.Critical;
            if (message.Contains("Risk", StringComparison.OrdinalIgnoreCase) || message.Contains("Low", StringComparison.OrdinalIgnoreCase))
                return FarmAlertSeverity.Warning;
            return FarmAlertSeverity.Info;
        }

        private static List<string> ParseAlerts(string json)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(json) || json == "[]") return list;
            try
            {
                string inner = json.Trim().TrimStart('[').TrimEnd(']');
                string[] parts = inner.Split(',');
                for (int i = 0; i < parts.Length; i++)
                {
                    string s = parts[i].Trim().Trim('"');
                    if (!string.IsNullOrEmpty(s))
                        list.Add(s);
                }
            }
            catch
            {
                // Ignore malformed alert payload and keep UI stable.
            }
            return list;
        }
    }
}
