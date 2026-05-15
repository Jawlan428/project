using System.Collections.Generic;
using SmartFarm.Irrigation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SmartFarm.Irrigation.Sustainability.UI
{
    /// <summary>
    /// Binds the Sustainability sub-systems to a single tablet page —
    /// the <i>Sustainability Monitor</i>.
    ///
    /// Renders:
    ///   • Animated "Water Saved Today" counter.
    ///   • Circular Irrigation Efficiency gauge (re-uses CircularWaterIndicator).
    ///   • Smart Water Recommendation banner (driven by weather + soil state).
    ///   • Sustainability Score badge + grade.
    ///   • Weather state pill with optimisation %.
    ///   • Scrolling eco alerts list (cloned from a hidden template).
    ///
    /// Subscribes once to <see cref="SustainabilityWaterManager.OnSnapshotChanged"/>
    /// and <see cref="EcoAlertManager.OnActiveListChanged"/>, so per-frame cost is
    /// near-zero.
    /// </summary>
    [AddComponentMenu("SmartFarm/Irrigation/Sustainability/UI/Sustainability Monitor Page")]
    public class SustainabilityMonitorPageUI : MonoBehaviour
    {
        [Header("Manager")]
        [SerializeField] private SustainabilityWaterManager manager;

        [Header("Header / Score")]
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text scoreGradeText;
        [SerializeField] private Image    scoreBadge;
        [SerializeField] private TMP_Text weatherStateText;
        [SerializeField] private Image    weatherStatePill;

        [Header("Water Saved Today")]
        [SerializeField] private AnimatedNumberCounter waterSavedCounter;
        [SerializeField] private TMP_Text              waterSavedSubtitle;

        [Header("Efficiency")]
        [SerializeField] private CircularWaterIndicator efficiencyIndicator;
        [SerializeField] private AnimatedFlowBar        efficiencyBar;

        [Header("Recommendation")]
        [SerializeField] private TMP_Text recommendationText;
        [SerializeField] private Image    recommendationAccent;

        [Header("Auto Irrigation Toggle")]
        [SerializeField] private Button   autoIrrigationButton;
        [SerializeField] private TMP_Text autoIrrigationLabel;
        [SerializeField] private Image    autoIrrigationLed;

        [Header("Reset")]
        [SerializeField] private Button resetAnalyticsButton;

        [Header("Details Toggle")]
        [SerializeField] private Button     detailsButton;
        [SerializeField] private GameObject detailsPanel;

        [Header("Eco Alerts List")]
        [SerializeField] private RectTransform alertsListRoot;
        [SerializeField] private EcoAlertItemUI alertsItemTemplate;
        [SerializeField] private TMP_Text       alertsEmptyState;

        // ── Theme colours ────────────────────────────────────────────────────

        private static readonly Color EcoGreen   = new Color(0.30f, 0.85f, 0.55f, 1f);
        private static readonly Color EcoBlue    = new Color(0.40f, 0.75f, 1.00f, 1f);
        private static readonly Color EcoAmber   = new Color(0.95f, 0.78f, 0.25f, 1f);
        private static readonly Color EcoRed     = new Color(0.92f, 0.30f, 0.25f, 1f);

        // ── Internal pool ────────────────────────────────────────────────────

        private readonly List<EcoAlertItemUI> _activeItems = new();
        private readonly Stack<EcoAlertItemUI> _itemPool   = new();
        private bool _detailsVisible;

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (manager == null)
                manager = SustainabilityWaterManager.Instance ?? FindFirstObjectByType<SustainabilityWaterManager>();
        }

        private void Start()
        {
            if (autoIrrigationButton != null)
                autoIrrigationButton.onClick.AddListener(OnAutoIrrigationClicked);
            if (resetAnalyticsButton != null)
                resetAnalyticsButton.onClick.AddListener(OnResetAnalyticsClicked);
            if (detailsButton != null)
                detailsButton.onClick.AddListener(OnDetailsClicked);

            if (alertsItemTemplate != null) alertsItemTemplate.gameObject.SetActive(false);
            if (detailsPanel != null)        detailsPanel.SetActive(_detailsVisible);
            if (efficiencyIndicator != null) efficiencyIndicator.SetLabel("Efficiency");

            ApplyAutoIrrigationVisuals(manager != null && manager.AutoIrrigation);
        }

        private void OnEnable()
        {
            if (manager == null)
                manager = SustainabilityWaterManager.Instance ?? FindFirstObjectByType<SustainabilityWaterManager>();

            if (manager != null)
            {
                manager.OnSnapshotChanged       += HandleSnapshot;
                manager.OnAutoIrrigationToggled += ApplyAutoIrrigationVisuals;
                if (manager.Alerts != null)
                    manager.Alerts.OnActiveListChanged += RebuildAlertsList;
                HandleSnapshot(manager.LatestSnapshot);
                RebuildAlertsList(manager.Alerts != null ? manager.Alerts.ActiveAlerts : null);
            }
        }

        private void OnDisable()
        {
            if (manager != null)
            {
                manager.OnSnapshotChanged       -= HandleSnapshot;
                manager.OnAutoIrrigationToggled -= ApplyAutoIrrigationVisuals;
                if (manager.Alerts != null)
                    manager.Alerts.OnActiveListChanged -= RebuildAlertsList;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Snapshot binding
        // ─────────────────────────────────────────────────────────────────────

        private void HandleSnapshot(SustainabilitySnapshot snap)
        {
            // Water Saved Today
            if (waterSavedCounter != null) waterSavedCounter.SetTarget(snap.waterSavedLitres);
            if (waterSavedSubtitle != null)
                waterSavedSubtitle.text = $"<color=#9FE2C7>Water Saved Today</color>";

            // Efficiency gauge
            if (efficiencyIndicator != null)
            {
                Color c = ResolveEfficiencyColor(snap.efficiency01);
                bool critical = snap.efficiency01 < 0.45f;
                efficiencyIndicator.SetValue(snap.efficiency01 * 100f, c, critical);
            }
            if (efficiencyBar != null)
            {
                efficiencyBar.SetFlow(snap.efficiency01);
                efficiencyBar.SetActiveColor(ResolveEfficiencyColor(snap.efficiency01));
            }

            // Sustainability score badge
            if (scoreText != null)
                scoreText.text = $"{Mathf.RoundToInt(snap.sustainabilityScore01 * 100f)}%";
            if (scoreGradeText != null)
                scoreGradeText.text = snap.grade;
            if (scoreBadge != null)
                scoreBadge.color = ResolveScoreColor(snap.sustainabilityScore01);

            // Weather pill
            if (weatherStateText != null)
            {
                int weatherPct = Mathf.RoundToInt(snap.weatherOptimization01 * 100f);
                weatherStateText.text = $"{WeatherLabel(snap.weather)}  ·  {weatherPct}%";
            }
            if (weatherStatePill != null)
                weatherStatePill.color = ResolveWeatherColor(snap.weather);

            // Smart recommendation
            if (recommendationText != null)
                recommendationText.text = string.IsNullOrEmpty(snap.recommendation)
                    ? "Smart irrigation active."
                    : snap.recommendation;
            if (recommendationAccent != null)
                recommendationAccent.color = ResolveWeatherColor(snap.weather);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Alerts list
        // ─────────────────────────────────────────────────────────────────────

        private void RebuildAlertsList(IReadOnlyList<EcoAlert> alerts)
        {
            // Recycle existing rows back to the pool
            for (int i = 0; i < _activeItems.Count; i++)
            {
                var it = _activeItems[i];
                if (it == null) continue;
                it.gameObject.SetActive(false);
                _itemPool.Push(it);
            }
            _activeItems.Clear();

            int count = alerts != null ? alerts.Count : 0;
            if (alertsEmptyState != null) alertsEmptyState.gameObject.SetActive(count == 0);

            if (alertsListRoot == null || alertsItemTemplate == null || count == 0) return;

            for (int i = 0; i < count; i++)
            {
                var row = AcquireItem();
                row.gameObject.SetActive(true);
                row.transform.SetSiblingIndex(i);
                row.Bind(alerts[i]);
                _activeItems.Add(row);
            }
        }

        private EcoAlertItemUI AcquireItem()
        {
            if (_itemPool.Count > 0) return _itemPool.Pop();
            var clone = Instantiate(alertsItemTemplate, alertsListRoot);
            clone.name = "EcoAlertRow";
            return clone;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Buttons
        // ─────────────────────────────────────────────────────────────────────

        private void OnAutoIrrigationClicked()
        {
            if (manager == null) return;
            manager.ToggleAutoIrrigation();
        }

        private void OnResetAnalyticsClicked()
        {
            if (manager == null) return;
            manager.ResetAnalytics();
            if (waterSavedCounter != null) waterSavedCounter.SnapToTarget(0f);
        }

        private void OnDetailsClicked()
        {
            _detailsVisible = !_detailsVisible;
            if (detailsPanel != null) detailsPanel.SetActive(_detailsVisible);
        }

        private void ApplyAutoIrrigationVisuals(bool on)
        {
            if (autoIrrigationLabel != null)
                autoIrrigationLabel.text = on ? "AUTO IRRIGATION ON" : "AUTO IRRIGATION OFF";
            if (autoIrrigationLed != null)
                autoIrrigationLed.color = on ? EcoGreen : new Color(0.5f, 0.5f, 0.5f, 1f);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Colour resolution
        // ─────────────────────────────────────────────────────────────────────

        private static Color ResolveEfficiencyColor(float eff01)
        {
            if (eff01 >= 0.75f) return EcoGreen;
            if (eff01 >= 0.50f) return EcoAmber;
            return EcoRed;
        }

        private static Color ResolveScoreColor(float score01)
        {
            if (score01 >= 0.80f) return EcoGreen;
            if (score01 >= 0.55f) return EcoAmber;
            return EcoRed;
        }

        private static Color ResolveWeatherColor(WeatherManager.WeatherType weather) => weather switch
        {
            WeatherManager.WeatherType.Rainy => EcoBlue,
            WeatherManager.WeatherType.Storm => EcoRed,
            _                                => EcoAmber
        };

        private static string WeatherLabel(WeatherManager.WeatherType w) => w switch
        {
            WeatherManager.WeatherType.Sunny => "Sunny",
            WeatherManager.WeatherType.Rainy => "Rainy",
            WeatherManager.WeatherType.Storm => "Storm",
            _ => w.ToString()
        };

        // ─────────────────────────────────────────────────────────────────────
        //  Editor / setup hookup
        // ─────────────────────────────────────────────────────────────────────

        public void SetReferences(SustainabilityWaterManager mgr,
            TMP_Text score, TMP_Text grade, Image badge,
            TMP_Text weather, Image weatherPill,
            AnimatedNumberCounter counter, TMP_Text counterSubtitle,
            CircularWaterIndicator efficiency, AnimatedFlowBar effBar,
            TMP_Text recommendation, Image recommendationAccentImg,
            Button autoBtn, TMP_Text autoLabel, Image autoLed,
            Button resetBtn, Button detailsBtn, GameObject detailsPanelGO,
            RectTransform alertsRoot, EcoAlertItemUI alertsTemplate, TMP_Text alertsEmpty)
        {
            manager               = mgr;
            scoreText             = score;
            scoreGradeText        = grade;
            scoreBadge            = badge;
            weatherStateText      = weather;
            weatherStatePill      = weatherPill;
            waterSavedCounter     = counter;
            waterSavedSubtitle    = counterSubtitle;
            efficiencyIndicator   = efficiency;
            efficiencyBar         = effBar;
            recommendationText    = recommendation;
            recommendationAccent  = recommendationAccentImg;
            autoIrrigationButton  = autoBtn;
            autoIrrigationLabel   = autoLabel;
            autoIrrigationLed     = autoLed;
            resetAnalyticsButton  = resetBtn;
            detailsButton         = detailsBtn;
            detailsPanel          = detailsPanelGO;
            alertsListRoot        = alertsRoot;
            alertsItemTemplate    = alertsTemplate;
            alertsEmptyState      = alertsEmpty;
        }
    }
}
