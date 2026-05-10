using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SmartFarm.Irrigation.UI
{
    /// <summary>
    /// Overview page on the Smart Irrigation Tablet.
    ///
    /// Layout:
    ///   • Top row: 3 circular indicators — Soil Moisture / Crop Health / Efficiency.
    ///   • Bottom row: Active zones, total water used, current weather, animated flow bar.
    /// </summary>
    [AddComponentMenu("SmartFarm/Irrigation/UI/Overview Page")]
    public class IrrigationOverviewPageUI : MonoBehaviour
    {
        [Header("Manager")]
        [SerializeField] private SmartIrrigationTabletManager manager;

        [Header("Circular Indicators")]
        [SerializeField] private CircularWaterIndicator moistureIndicator;
        [SerializeField] private CircularWaterIndicator healthIndicator;
        [SerializeField] private CircularWaterIndicator efficiencyIndicator;

        [Header("Stats")]
        [SerializeField] private TMP_Text waterUsageText;
        [SerializeField] private TMP_Text activeZonesText;
        [SerializeField] private TMP_Text weatherText;
        [SerializeField] private TMP_Text moistureStateText;

        [Header("Animated Bars")]
        [SerializeField] private AnimatedFlowBar overallFlowBar;

        [Header("Mode Buttons")]
        [SerializeField] private Button enableAllButton;
        [SerializeField] private Button disableAllButton;
        [SerializeField] private Button autoAllButton;

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (manager == null)
                manager = SmartIrrigationTabletManager.Instance ?? FindFirstObjectByType<SmartIrrigationTabletManager>();
        }

        private void Start()
        {
            if (enableAllButton  != null) enableAllButton.onClick.AddListener(()  => manager?.EnableAllZones());
            if (disableAllButton != null) disableAllButton.onClick.AddListener(() => manager?.DisableAllZones());
            if (autoAllButton    != null) autoAllButton.onClick.AddListener(()    => manager?.SetAllZonesAuto());

            if (moistureIndicator   != null) moistureIndicator.SetLabel("Soil Moisture");
            if (healthIndicator     != null) healthIndicator.SetLabel("Crop Health");
            if (efficiencyIndicator != null) efficiencyIndicator.SetLabel("Efficiency");
        }

        private void OnEnable()
        {
            if (manager == null)
                manager = SmartIrrigationTabletManager.Instance ?? FindFirstObjectByType<SmartIrrigationTabletManager>();
            if (manager != null)
            {
                manager.OnDashboardChanged += HandleDashboardChanged;
                HandleDashboardChanged(manager.LatestSnapshot);
            }
        }

        private void OnDisable()
        {
            if (manager != null) manager.OnDashboardChanged -= HandleDashboardChanged;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Apply
        // ─────────────────────────────────────────────────────────────────────

        private void HandleDashboardChanged(IrrigationDashboardSnapshot snap)
        {
            if (moistureIndicator != null)
            {
                Color c = SoilMoistureSystem.Color(snap.moistureState);
                bool critical = snap.moistureState == SoilMoistureState.Dry;
                moistureIndicator.SetValue(snap.averageMoisture, c, critical);
            }
            if (healthIndicator != null)
            {
                Color c = HealthColor(snap.averageHealth);
                bool critical = snap.averageHealth < 30f;
                healthIndicator.SetValue(snap.averageHealth, c, critical);
            }
            if (efficiencyIndicator != null)
            {
                float pct = snap.efficiency * 100f;
                Color c = HealthColor(pct);
                efficiencyIndicator.SetValue(pct, c, false);
            }

            if (waterUsageText != null)
                waterUsageText.text = $"<size=68%><color=#9FE2C7>Total Water</color></size>\n{snap.totalWaterUsed:F0} <size=70%>units</size>";
            if (activeZonesText != null)
                activeZonesText.text = $"<size=68%><color=#9FE2C7>Active Zones</color></size>\n{snap.activeZoneCount} / {snap.totalZones}";
            if (weatherText != null)
                weatherText.text = $"<size=68%><color=#9FE2C7>Weather</color></size>\n{WeatherLabel(snap.weather)}";
            if (moistureStateText != null)
            {
                Color c = SoilMoistureSystem.Color(snap.moistureState);
                string hex = ColorUtility.ToHtmlStringRGB(c);
                moistureStateText.text = $"<size=68%><color=#9FE2C7>Status</color></size>\n<color=#{hex}>{SoilMoistureSystem.Label(snap.moistureState)}</color>";
            }

            if (overallFlowBar != null)
            {
                float flow = snap.totalZones > 0
                    ? (float)snap.activeZoneCount / snap.totalZones
                    : 0f;
                overallFlowBar.SetFlow(flow);
                overallFlowBar.SetActiveColor(SoilMoistureSystem.HealthyColor);
            }
        }

        private static Color HealthColor(float pct)
        {
            if (pct < 30f) return new Color(0.92f, 0.30f, 0.25f, 1f);
            if (pct < 55f) return new Color(0.95f, 0.78f, 0.25f, 1f);
            return new Color(0.30f, 0.85f, 0.55f, 1f);
        }

        private static string WeatherLabel(WeatherManager.WeatherType w) => w switch
        {
            WeatherManager.WeatherType.Sunny => "Sunny",
            WeatherManager.WeatherType.Rainy => "Rainy",
            WeatherManager.WeatherType.Storm => "Storm",
            _ => w.ToString()
        };

        // ─────────────────────────────────────────────────────────────────────
        //  Wiring helpers
        // ─────────────────────────────────────────────────────────────────────

        public void SetManager(SmartIrrigationTabletManager mgr) => manager = mgr;

        public void SetReferences(
            CircularWaterIndicator moisture,
            CircularWaterIndicator health,
            CircularWaterIndicator efficiency,
            TMP_Text waterUsage,
            TMP_Text activeZones,
            TMP_Text weather,
            TMP_Text moistureState,
            AnimatedFlowBar flowBar,
            Button enableAll,
            Button disableAll,
            Button autoAll)
        {
            moistureIndicator   = moisture;
            healthIndicator     = health;
            efficiencyIndicator = efficiency;
            waterUsageText      = waterUsage;
            activeZonesText     = activeZones;
            weatherText         = weather;
            moistureStateText   = moistureState;
            overallFlowBar      = flowBar;
            enableAllButton     = enableAll;
            disableAllButton    = disableAll;
            autoAllButton       = autoAll;
        }
    }
}
