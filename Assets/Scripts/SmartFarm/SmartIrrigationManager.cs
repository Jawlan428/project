using System;
using System.Collections;
using UnityEngine;

namespace SmartFarm
{
    public enum IrrigationMode { Manual, Scheduled, AI }

    /// <summary>
    /// Central Smart Irrigation Controller.
    /// Manages Manual / Scheduled / AI irrigation modes.
    /// Integrates with FarmSimulationManager and WeatherManager.
    /// Tick-based (1 s), Quest VR friendly — no per-frame Update().
    /// </summary>
    public class SmartIrrigationManager : MonoBehaviour
    {
        public static SmartIrrigationManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private FarmSimulationManager simulationManager;
        [SerializeField] private WeatherManager        weatherManager;
        [SerializeField] private IrrigationScheduler   scheduler;

        [Header("AI Thresholds")]
        [Tooltip("Soil moisture % below which AI triggers irrigation")]
        [SerializeField] [Range(0f, 100f)] private float lowMoistureThreshold  = 35f;
        [Tooltip("Crop health % below which AI triggers irrigation")]
        [SerializeField] [Range(0f, 100f)] private float lowCropHealthThreshold = 50f;

        [Header("Tick Settings")]
        [Tooltip("Seconds between irrigation evaluation ticks")]
        [SerializeField] [Range(0.5f, 5f)] private float tickInterval = 1f;

        // ── Public state ──────────────────────────────────────────────────────

        public IrrigationMode CurrentMode        { get; private set; } = IrrigationMode.Manual;
        public bool           IsIrrigationActive { get; private set; }
        public string         LastDecisionReason { get; private set; } = "";

        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>Fired when the user switches irrigation mode.</summary>
        public event Action<IrrigationMode> OnModeChanged;

        /// <summary>Fired every tick when irrigation state or reason changes. (isActive, reason)</summary>
        public event Action<bool, string> OnIrrigationStateChanged;

        // ── Private ───────────────────────────────────────────────────────────

        private bool       _manualIrrigationOn;
        private Coroutine  _tickCoroutine;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (simulationManager == null)
                simulationManager = FindFirstObjectByType<FarmSimulationManager>();
            if (weatherManager == null)
                weatherManager = FindFirstObjectByType<WeatherManager>();
            if (scheduler == null)
                scheduler = GetComponent<IrrigationScheduler>();
            if (scheduler == null)
                scheduler = FindFirstObjectByType<IrrigationScheduler>();
        }

        private void Start()
        {
            _tickCoroutine = StartCoroutine(TickLoop());
            EventLogger.LogEvent("Smart Irrigation System initialized");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_tickCoroutine != null) StopCoroutine(_tickCoroutine);
        }

        // ── Tick loop ─────────────────────────────────────────────────────────

        private IEnumerator TickLoop()
        {
            var wait = new WaitForSeconds(tickInterval);
            while (true)
            {
                yield return wait;
                EvaluateTick();
            }
        }

        private void EvaluateTick()
        {
            var weather = weatherManager != null
                ? weatherManager.CurrentWeather
                : WeatherManager.WeatherType.Sunny;

            // Storm always disables all modes — highest priority safety override
            if (weather == WeatherManager.WeatherType.Storm)
            {
                ApplyIrrigationState(false, "Storm detected — irrigation disabled");
                return;
            }

            switch (CurrentMode)
            {
                case IrrigationMode.Manual:
                    ApplyIrrigationState(
                        _manualIrrigationOn,
                        _manualIrrigationOn ? "Manual Irrigation: ON" : "Manual Irrigation: OFF");
                    break;

                case IrrigationMode.Scheduled:
                    EvaluateScheduled(weather);
                    break;

                case IrrigationMode.AI:
                    EvaluateAI(weather);
                    break;
            }
        }

        private void EvaluateScheduled(WeatherManager.WeatherType weather)
        {
            if (scheduler == null)
            {
                ApplyIrrigationState(false, "No scheduler configured");
                return;
            }

            bool scheduled = scheduler.IsScheduledTimeActive();

            // Rain provides natural moisture — skip scheduled run but don't disable the schedule
            if (weather == WeatherManager.WeatherType.Rainy && scheduled)
            {
                ApplyIrrigationState(false, "Rain detected — skipping scheduled irrigation");
                return;
            }

            string preset = scheduler.ActivePreset;
            string reason = scheduled
                ? $"Scheduled irrigation active ({preset})"
                : scheduler.GetNextActivationInfo();

            ApplyIrrigationState(scheduled, reason);
        }

        private void EvaluateAI(WeatherManager.WeatherType weather)
        {
            var state = simulationManager != null
                ? simulationManager.GetState()
                : FarmSimulationState.Default;

            var (shouldIrrigate, reason) = AIIrrigationDecision.Evaluate(
                state, weather, lowMoistureThreshold, lowCropHealthThreshold);

            ApplyIrrigationState(shouldIrrigate, reason);
        }

        /// <summary>
        /// Applies the resolved irrigation state to the simulation.
        /// Only calls SetIrrigationEnabled when the boolean state actually changes —
        /// avoids spamming EventLogger every tick.
        /// </summary>
        private void ApplyIrrigationState(bool active, string reason)
        {
            bool stateChanged  = IsIrrigationActive != active;
            bool reasonChanged = LastDecisionReason  != reason;

            if (!stateChanged && !reasonChanged) return;

            IsIrrigationActive = active;
            LastDecisionReason = reason;

            // Only push to simulation when irrigation boolean flips
            if (stateChanged && simulationManager != null)
                simulationManager.SetIrrigationEnabled(active);

            // Log meaningful mode-tagged events
            if (stateChanged)
            {
                string tag = CurrentMode.ToString();
                EventLogger.LogEvent(active
                    ? $"Irrigation turned ON [{tag}]: {reason}"
                    : $"Irrigation turned OFF [{tag}]: {reason}");
            }

            OnIrrigationStateChanged?.Invoke(active, reason);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Switch the active irrigation mode. Immediately evaluates a tick for the new mode.</summary>
        public void SetMode(IrrigationMode mode)
        {
            if (CurrentMode == mode) return;

            CurrentMode = mode;
            LastDecisionReason = "";

            EventLogger.LogEvent($"Irrigation mode changed to {mode}");
            OnModeChanged?.Invoke(mode);

            // Force immediate evaluation so UI reflects new mode without waiting for next tick
            EvaluateTick();
        }

        /// <summary>
        /// Toggle manual irrigation ON or OFF.
        /// Has no effect when not in Manual mode, but the state is remembered for when
        /// the user switches back to Manual.
        /// </summary>
        public void SetManualIrrigation(bool on)
        {
            _manualIrrigationOn = on;
            if (CurrentMode == IrrigationMode.Manual)
                EvaluateTick();
        }

        /// <summary>
        /// Activate a named schedule preset (Morning / Noon / Evening).
        /// Automatically switches to Scheduled mode if not already active.
        /// </summary>
        public void SetSchedulePreset(string presetName)
        {
            if (scheduler != null)
                scheduler.SetPreset(presetName);

            if (CurrentMode != IrrigationMode.Scheduled)
                SetMode(IrrigationMode.Scheduled);
            else
                EvaluateTick();

            EventLogger.LogEvent($"Irrigation schedule set: {presetName}");
        }

        /// <summary>Force an immediate AI evaluation tick (called from UI for instant feedback).</summary>
        public void ForceAIEvaluate()
        {
            if (CurrentMode == IrrigationMode.AI)
                EvaluateTick();
        }
    }
}
