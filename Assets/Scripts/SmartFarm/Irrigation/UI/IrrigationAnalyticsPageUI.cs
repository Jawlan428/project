using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SmartFarm.Irrigation.UI
{
    /// <summary>
    /// Analytics page for the Smart Irrigation Tablet.
    ///
    /// Renders:
    ///   • Total water used this session.
    ///   • Efficiency % (smoothed score from <see cref="WaterAnalyticsSystem"/>).
    ///   • Mini bar chart of recent water buckets (no per-frame allocations).
    ///   • Hydration status summary (avg moisture, avg health).
    /// </summary>
    [AddComponentMenu("SmartFarm/Irrigation/UI/Analytics Page")]
    public class IrrigationAnalyticsPageUI : MonoBehaviour
    {
        [Header("Manager")]
        [SerializeField] private SmartIrrigationTabletManager manager;

        [Header("Stats")]
        [SerializeField] private TMP_Text totalWaterText;
        [SerializeField] private TMP_Text efficiencyText;
        [SerializeField] private TMP_Text hydrationStatusText;
        [SerializeField] private TMP_Text performanceText;

        [Header("Bars")]
        [SerializeField] private Image    efficiencyBar;
        [SerializeField] private RectTransform graphRoot;
        [SerializeField] private Image[] graphBars;

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (manager == null)
                manager = SmartIrrigationTabletManager.Instance ?? FindFirstObjectByType<SmartIrrigationTabletManager>();
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
            if (totalWaterText != null)
                totalWaterText.text = $"<size=70%><color=#9FE2C7>Total Water Used</color></size>\n{snap.totalWaterUsed:F0} <size=70%>units</size>";

            if (efficiencyText != null)
                efficiencyText.text = $"<size=70%><color=#9FE2C7>Efficiency</color></size>\n{(snap.efficiency * 100f):F0}%";

            if (efficiencyBar != null)
                efficiencyBar.fillAmount = Mathf.Clamp01(snap.efficiency);

            if (hydrationStatusText != null)
            {
                Color stateColor = SoilMoistureSystem.Color(snap.moistureState);
                string hex = ColorUtility.ToHtmlStringRGB(stateColor);
                hydrationStatusText.text =
                    $"<size=70%><color=#9FE2C7>Hydration</color></size>\n" +
                    $"<color=#{hex}>{SoilMoistureSystem.Label(snap.moistureState)}</color> · {snap.averageMoisture:F0}%";
            }

            if (performanceText != null)
            {
                string verdict = snap.averageHealth >= 75f ? "Excellent"
                              : snap.averageHealth >= 55f ? "Healthy"
                              : snap.averageHealth >= 30f ? "Stressed"
                              : "Critical";
                performanceText.text =
                    $"<size=70%><color=#9FE2C7>Crop Performance</color></size>\n" +
                    $"{verdict} · {snap.averageHealth:F0}%";
            }

            UpdateGraph();
        }

        private void UpdateGraph()
        {
            if (manager == null || manager.Analytics == null || graphBars == null) return;
            var history = manager.Analytics.History;
            if (history == null || history.Count == 0) return;

            float max = manager.Analytics.MaxBucket;
            int barCount = graphBars.Length;

            for (int i = 0; i < barCount; i++)
            {
                int historyIndex = history.Count - barCount + i;
                if (historyIndex < 0) historyIndex = 0;
                if (historyIndex >= history.Count) historyIndex = history.Count - 1;

                float value = history[historyIndex];
                float pct   = max > 0.0001f ? value / max : 0f;
                if (graphBars[i] != null)
                {
                    graphBars[i].fillAmount = Mathf.Clamp01(pct);
                    graphBars[i].color      = Color.Lerp(
                        new Color(0.30f, 0.85f, 0.55f, 1f),
                        new Color(0.40f, 0.75f, 1.00f, 1f),
                        pct);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Wiring helpers
        // ─────────────────────────────────────────────────────────────────────

        public void SetReferences(
            SmartIrrigationTabletManager mgr,
            TMP_Text totalWater, TMP_Text efficiency,
            TMP_Text hydration, TMP_Text performance,
            Image efficiencyBar, RectTransform graph,
            Image[] bars)
        {
            manager             = mgr;
            totalWaterText      = totalWater;
            efficiencyText      = efficiency;
            hydrationStatusText = hydration;
            performanceText     = performance;
            this.efficiencyBar  = efficiencyBar;
            graphRoot           = graph;
            graphBars           = bars;
        }
    }
}
