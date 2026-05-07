using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SmartFarm
{
    /// <summary>
    /// World-space UI for the Crop Growth Monitor.
    ///
    /// Renders the live <see cref="CropMonitorReading"/> from
    /// <see cref="CropGrowthMonitorManager"/> as a smart-farm dashboard:
    ///   • Animated growth, health, water bars
    ///   • Weather panel that re-tints the entire monitor (orange/blue/red)
    ///   • Stage badge + crop name + sample count
    ///   • Harvest countdown (mm:ss / READY / —)
    ///   • Critical-state border flash (storm or low health)
    ///   • Buttons: ◀ Prev / Next ▶ / Harvest / Reset View
    ///
    /// Designed to be Quest-friendly: lerps in OnReadingChanged + a single
    /// Update for smooth bar interpolation. No allocations in the hot path.
    /// </summary>
    [AddComponentMenu("SmartFarm/Crops/Crop Growth Monitor UI")]
    public class CropGrowthMonitorUI : MonoBehaviour
    {
        [Header("Manager")]
        [SerializeField] private CropGrowthMonitorManager monitor;

        [Header("Header")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private Image    statusLed;
        [SerializeField] private Image    borderImage;

        [Header("Crop Card")]
        [SerializeField] private TMP_Text cropNameText;
        [SerializeField] private TMP_Text stageText;
        [SerializeField] private Image    stageBadgeBg;
        [SerializeField] private Image    stageIconImage;
        [SerializeField] private TMP_Text sampleText;

        [Header("Growth Bar")]
        [SerializeField] private Image    growthBarFill;
        [SerializeField] private TMP_Text growthValueText;

        [Header("Health Bar")]
        [SerializeField] private Image    healthBarFill;
        [SerializeField] private TMP_Text healthValueText;

        [Header("Water Bar")]
        [SerializeField] private Image    waterBarFill;
        [SerializeField] private TMP_Text waterValueText;

        [Header("Weather Panel")]
        [SerializeField] private Image    weatherCardBg;
        [SerializeField] private Image    weatherIconImage;
        [SerializeField] private TMP_Text weatherTitleText;
        [SerializeField] private TMP_Text weatherDescriptionText;

        [Header("Harvest Countdown")]
        [SerializeField] private TMP_Text harvestTimerText;
        [SerializeField] private TMP_Text harvestLabelText;

        [Header("Buttons")]
        [SerializeField] private Button   previousButton;
        [SerializeField] private Button   nextButton;
        [SerializeField] private Button   harvestButton;
        [SerializeField] private Button   resetViewButton;

        [Header("Animation")]
        [SerializeField, Tooltip("How quickly bars lerp toward target values.")]
        [Range(1f, 30f)] private float barLerpSpeed = 8f;
        [SerializeField, Tooltip("Speed of the critical border flash.")]
        [Range(0.5f, 6f)] private float borderFlashSpeed = 2.5f;

        // ── Theme colours ─────────────────────────────────────────────────────

        private static readonly Color NeonGreen   = new Color(0.30f, 1.00f, 0.66f, 1f);
        private static readonly Color WarmAmber   = new Color(1.00f, 0.78f, 0.20f, 1f);
        private static readonly Color CriticalRed = new Color(1.00f, 0.25f, 0.30f, 1f);
        private static readonly Color RainBlue    = new Color(0.35f, 0.70f, 1.00f, 1f);
        private static readonly Color StormPurple = new Color(0.78f, 0.40f, 1.00f, 1f);
        private static readonly Color BorderIdle  = new Color(0.10f, 0.85f, 0.55f, 0.65f);
        private static readonly Color BorderAlert = new Color(1.00f, 0.20f, 0.30f, 0.95f);

        // ── State ─────────────────────────────────────────────────────────────

        private CropMonitorReading _target;
        private float _displayedGrowth;
        private float _displayedHealth;
        private float _displayedWater;
        private bool  _hasReading;
        private bool  _isCriticalState;

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (monitor == null)
                monitor = FindFirstObjectByType<CropGrowthMonitorManager>();
        }

        private void OnEnable()
        {
            WireButtons();
            if (monitor == null)
                monitor = FindFirstObjectByType<CropGrowthMonitorManager>();
            if (monitor != null)
            {
                monitor.OnReadingChanged += OnReadingChanged;
                monitor.OnFocusChanged   += OnFocusChanged;
                _target = monitor.CurrentReading;
                _hasReading = _target.sampleCount >= 0;
                ApplyImmediate(_target);
            }
        }

        private void OnDisable()
        {
            if (monitor != null)
            {
                monitor.OnReadingChanged -= OnReadingChanged;
                monitor.OnFocusChanged   -= OnFocusChanged;
            }
        }

        private void WireButtons()
        {
            if (previousButton != null)
            {
                previousButton.onClick.RemoveAllListeners();
                previousButton.onClick.AddListener(OnPreviousClicked);
            }
            if (nextButton != null)
            {
                nextButton.onClick.RemoveAllListeners();
                nextButton.onClick.AddListener(OnNextClicked);
            }
            if (harvestButton != null)
            {
                harvestButton.onClick.RemoveAllListeners();
                harvestButton.onClick.AddListener(OnHarvestClicked);
            }
            if (resetViewButton != null)
            {
                resetViewButton.onClick.RemoveAllListeners();
                resetViewButton.onClick.AddListener(OnResetViewClicked);
            }
        }

        private void Update()
        {
            if (!_hasReading) return;

            float t = 1f - Mathf.Exp(-barLerpSpeed * Time.deltaTime);

            _displayedGrowth = Mathf.Lerp(_displayedGrowth, _target.overallProgress,        t);
            _displayedHealth = Mathf.Lerp(_displayedHealth, _target.healthPercent / 100f,   t);
            _displayedWater  = Mathf.Lerp(_displayedWater,  _target.waterPercent  / 100f,   t);

            if (growthBarFill != null) growthBarFill.fillAmount = _displayedGrowth;
            if (healthBarFill != null) healthBarFill.fillAmount = _displayedHealth;
            if (waterBarFill  != null) waterBarFill.fillAmount  = _displayedWater;

            if (healthBarFill != null) healthBarFill.color = HealthColor(_displayedHealth * 100f);
            if (waterBarFill  != null) waterBarFill.color  = WaterColor (_displayedWater  * 100f);

            if (borderImage != null)
            {
                Color target = _isCriticalState ? BorderAlert : BorderIdle;
                if (_isCriticalState)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * borderFlashSpeed * Mathf.PI);
                    target.a = Mathf.Lerp(0.4f, 1f, pulse);
                }
                borderImage.color = Color.Lerp(borderImage.color, target, t);
            }

            // Live-update timer text every frame so the countdown ticks visibly.
            if (harvestTimerText != null && _hasReading && !_target.isDead)
            {
                if (_target.isHarvestReady) harvestTimerText.text = "READY";
                else harvestTimerText.text = _target.FormatHarvestTime();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Manager callbacks
        // ─────────────────────────────────────────────────────────────────────

        private void OnReadingChanged(CropMonitorReading reading)
        {
            _target     = reading;
            _hasReading = true;
            ApplyDiscrete(reading);
            UpdateCriticalState(reading);
        }

        private void OnFocusChanged()
        {
            // Force-refresh the discrete labels next frame; the bar lerp continues smoothly.
            if (monitor != null) ApplyDiscrete(monitor.CurrentReading);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Apply
        // ─────────────────────────────────────────────────────────────────────

        private void ApplyImmediate(CropMonitorReading reading)
        {
            _displayedGrowth = reading.overallProgress;
            _displayedHealth = reading.healthPercent / 100f;
            _displayedWater  = reading.waterPercent  / 100f;
            ApplyDiscrete(reading);
            UpdateCriticalState(reading);
        }

        private void ApplyDiscrete(CropMonitorReading reading)
        {
            if (titleText      != null) titleText.text      = "CROP GROWTH MONITOR";
            if (subtitleText   != null) subtitleText.text   = $"<color=#9FE2C7>Smart Agriculture · </color>{System.DateTime.Now:HH:mm}";
            if (cropNameText   != null) cropNameText.text   = reading.displayName ?? "Crop";
            if (sampleText     != null) sampleText.text     = reading.sampleCount > 0
                ? $"{reading.sampleCount} crop{(reading.sampleCount == 1 ? "" : "s")} sampled"
                : "No crops in field";

            if (stageText      != null) stageText.text      = StageLabel(reading.stage);
            if (stageBadgeBg   != null) stageBadgeBg.color  = StageColor(reading.stage);
            if (stageIconImage != null) stageIconImage.color = StageColor(reading.stage);

            if (growthValueText != null) growthValueText.text = $"{Mathf.RoundToInt(reading.overallProgress * 100f)}%";
            if (healthValueText != null) healthValueText.text = $"{Mathf.RoundToInt(reading.healthPercent)}%";
            if (waterValueText  != null) waterValueText.text  = $"{Mathf.RoundToInt(reading.waterPercent)}%";

            if (statusLed != null)
                statusLed.color = reading.isDead ? CriticalRed
                                : reading.isHarvestReady ? NeonGreen
                                : NeonGreen;

            ApplyWeather(reading.weather);

            if (harvestLabelText != null)
                harvestLabelText.text = reading.isHarvestReady ? "Harvest Status" : "Harvest Ready In";

            if (harvestTimerText != null)
            {
                if (reading.isDead) harvestTimerText.text = "—";
                else if (reading.isHarvestReady) harvestTimerText.text = "READY";
                else harvestTimerText.text = reading.FormatHarvestTime();
            }

            if (harvestButton != null)
                harvestButton.interactable = reading.isHarvestReady;
        }

        private void UpdateCriticalState(CropMonitorReading reading)
        {
            _isCriticalState =
                (reading.weather == WeatherManager.WeatherType.Storm) ||
                (reading.healthPercent < 25f && !reading.isDead) ||
                (reading.waterPercent  < 15f && !reading.isDead);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Weather
        // ─────────────────────────────────────────────────────────────────────

        private void ApplyWeather(WeatherManager.WeatherType weather)
        {
            (string title, string description, Color tint, string icon) info = weather switch
            {
                WeatherManager.WeatherType.Sunny =>
                    ("SUNNY",  "Faster growth · soil moisture decreases.", WarmAmber,   "\u2600"),  // ☀
                WeatherManager.WeatherType.Rainy =>
                    ("RAINY",  "Soil moisture rising · health recovering.", RainBlue,    "\u2614"), // ☔
                WeatherManager.WeatherType.Storm =>
                    ("STORM",  "Damage risk · growth slowed.",              StormPurple, "\u26A1"), // ⚡
                _ => ("SUNNY", string.Empty, NeonGreen, "*")
            };

            if (weatherTitleText       != null) weatherTitleText.text       = info.title;
            if (weatherDescriptionText != null) weatherDescriptionText.text = info.description;
            if (weatherCardBg          != null) weatherCardBg.color         = WithAlpha(info.tint, 0.18f);
            if (weatherIconImage       != null) weatherIconImage.color      = info.tint;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Buttons
        // ─────────────────────────────────────────────────────────────────────

        private void OnPreviousClicked() { if (monitor != null) monitor.CycleFocusBackward(); }
        private void OnNextClicked()     { if (monitor != null) monitor.CycleFocusForward();  }
        private void OnHarvestClicked()  { if (monitor != null) monitor.HarvestFocused();     }
        private void OnResetViewClicked(){ if (monitor != null) monitor.SetFocusCropType(CropType.Wheat); }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static string StageLabel(CropStage stage) => stage switch
        {
            CropStage.Seed   => "SEED",
            CropStage.Sprout => "SPROUT",
            CropStage.Young  => "GROWING",
            CropStage.Mature => "READY TO HARVEST",
            CropStage.Dead   => "DAMAGED",
            _                => stage.ToString().ToUpperInvariant()
        };

        private static Color StageColor(CropStage stage) => stage switch
        {
            CropStage.Seed   => new Color(0.55f, 0.40f, 0.20f, 1f), // earth brown
            CropStage.Sprout => new Color(0.40f, 0.85f, 0.45f, 1f), // young green
            CropStage.Young  => new Color(0.30f, 1.00f, 0.55f, 1f), // vibrant green
            CropStage.Mature => new Color(1.00f, 0.78f, 0.20f, 1f), // golden
            CropStage.Dead   => CriticalRed,
            _                => NeonGreen
        };

        private static Color HealthColor(float pct)
        {
            if (pct < 25f) return CriticalRed;
            if (pct < 55f) return WarmAmber;
            return NeonGreen;
        }

        private static Color WaterColor(float pct)
        {
            if (pct < 20f) return CriticalRed;
            if (pct < 40f) return WarmAmber;
            return RainBlue;
        }

        private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
