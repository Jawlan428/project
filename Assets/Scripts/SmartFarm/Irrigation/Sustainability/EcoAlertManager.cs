using System;
using System.Collections.Generic;
using UnityEngine;

namespace SmartFarm.Irrigation.Sustainability
{
    /// <summary>
    /// Eco-flavoured alert types raised by the Sustainability Monitor.
    /// Independent from the existing <see cref="IrrigationAlertManager"/> so
    /// engineers can mute "operational" alerts and still see eco messages
    /// (and vice-versa).
    /// </summary>
    public enum EcoAlertLevel
    {
        Eco      = 0, // green — celebrating savings
        Info     = 1, // blue
        Warning  = 2, // amber
        Critical = 3  // red
    }

    /// <summary>Plain data record for a single eco alert popup / row.</summary>
    [Serializable]
    public struct EcoAlert
    {
        public string         id;
        public string         title;
        public string         message;
        public EcoAlertLevel  level;
        public DateTime       timestampUtc;

        public Color GetColor() => level switch
        {
            EcoAlertLevel.Critical => new Color(0.92f, 0.30f, 0.25f, 1f),
            EcoAlertLevel.Warning  => new Color(0.95f, 0.78f, 0.25f, 1f),
            EcoAlertLevel.Info     => new Color(0.40f, 0.75f, 1.00f, 1f),
            _                      => new Color(0.30f, 0.85f, 0.55f, 1f)
        };
    }

    /// <summary>
    /// Watches the Sustainability sub-systems and surfaces popup-style alerts
    /// like "Rainwater Optimization Enabled", "High Water Consumption",
    /// "Efficient Irrigation Active", "Water Waste Detected".
    ///
    /// Mirrors the existing <see cref="IrrigationAlertManager"/> shape so any
    /// UI code that already knows how to bind to one can drop in.
    /// </summary>
    [AddComponentMenu("SmartFarm/Irrigation/Sustainability/Eco Alert Manager")]
    public class EcoAlertManager : MonoBehaviour
    {
        [Header("References (auto-found if empty)")]
        [SerializeField] private WeatherOptimizationSystem  weatherOptimization;
        [SerializeField] private WaterSavingTracker         waterSaver;
        [SerializeField] private IrrigationEfficiencySystem efficiencySystem;
        [SerializeField] private SustainabilityScoreSystem  scoreSystem;
        [SerializeField] private IrrigationZoneManager      zoneManager;

        [Header("Detection thresholds")]
        [SerializeField, Range(50f, 800f)] private float milestoneEverySavedLitres = 100f;
        [SerializeField, Range(0.30f, 0.95f)] private float efficiencyGoodCutoff   = 0.80f;
        [SerializeField, Range(0.10f, 0.60f)] private float efficiencyBadCutoff    = 0.45f;

        [Header("Cooldowns")]
        [SerializeField, Range(2f, 60f)] private float alertCooldownSeconds = 8f;

        public IReadOnlyList<EcoAlert> ActiveAlerts => _active;

        public event Action<EcoAlert>                     OnAlertRaised;
        public event Action<string>                       OnAlertResolved;
        public event Action<IReadOnlyList<EcoAlert>>      OnActiveListChanged;

        // ── Private ──────────────────────────────────────────────────────────

        private readonly List<EcoAlert>             _active     = new();
        private readonly HashSet<string>            _activeIds  = new();
        private readonly Dictionary<string, float>  _lastRaised = new();
        private int   _nextMilestone = 100;
        private float _checkTimer;

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (weatherOptimization == null) weatherOptimization = FindFirstObjectByType<WeatherOptimizationSystem>();
            if (waterSaver          == null) waterSaver          = FindFirstObjectByType<WaterSavingTracker>();
            if (efficiencySystem    == null) efficiencySystem    = FindFirstObjectByType<IrrigationEfficiencySystem>();
            if (scoreSystem         == null) scoreSystem         = FindFirstObjectByType<SustainabilityScoreSystem>();
            if (zoneManager         == null) zoneManager         = FindFirstObjectByType<IrrigationZoneManager>();
            _nextMilestone = Mathf.RoundToInt(milestoneEverySavedLitres);
        }

        private void OnEnable()
        {
            if (weatherOptimization != null) weatherOptimization.OnRecommendationChanged += HandleRecommendation;
        }

        private void OnDisable()
        {
            if (weatherOptimization != null) weatherOptimization.OnRecommendationChanged -= HandleRecommendation;
        }

        private void Update()
        {
            _checkTimer += Time.deltaTime;
            if (_checkTimer < 1f) return;
            _checkTimer = 0f;
            PeriodicCheck();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Detection
        // ─────────────────────────────────────────────────────────────────────

        private void PeriodicCheck()
        {
            // ── Savings milestones ──
            if (waterSaver != null && waterSaver.WaterSavedTodayLitres >= _nextMilestone)
            {
                Raise("eco_milestone",
                    $"Saved {_nextMilestone}L Today",
                    "Smart irrigation milestone unlocked. Eco mode is paying off!",
                    EcoAlertLevel.Eco,
                    forceNew: true);
                _nextMilestone += Mathf.RoundToInt(milestoneEverySavedLitres);
            }

            // ── Efficiency band ──
            if (efficiencySystem != null)
            {
                if (efficiencySystem.Efficiency01 >= efficiencyGoodCutoff)
                {
                    Raise("efficient_irrigation",
                        "Efficient Irrigation Active",
                        $"Efficiency at {efficiencySystem.EfficiencyPercent:F0}%. Crops thriving with minimal water.",
                        EcoAlertLevel.Eco);
                }
                else
                {
                    Resolve("efficient_irrigation");
                }

                if (efficiencySystem.Efficiency01 <= efficiencyBadCutoff)
                {
                    Raise("water_waste",
                        "Water Waste Detected",
                        "Irrigation efficiency is low. Review zone schedules to reduce waste.",
                        EcoAlertLevel.Warning);
                }
                else
                {
                    Resolve("water_waste");
                }
            }

            // ── Over-consumption (any zone over the overwater threshold) ──
            if (zoneManager != null)
            {
                bool overConsumption = false;
                var zones = zoneManager.Zones;
                for (int i = 0; i < zones.Count; i++)
                {
                    var z = zones[i];
                    if (z != null && z.cropCount > 0 && z.averageMoisture >= z.overwaterThreshold)
                    {
                        overConsumption = true; break;
                    }
                }
                if (overConsumption)
                {
                    Raise("high_consumption",
                        "High Water Consumption",
                        "One or more zones are over-saturated. Auto-reducing flow to protect crops.",
                        EcoAlertLevel.Warning);
                }
                else
                {
                    Resolve("high_consumption");
                }
            }
        }

        private void HandleRecommendation(string _)
        {
            if (weatherOptimization == null) return;

            switch (weatherOptimization.CurrentWeather)
            {
                case WeatherManager.WeatherType.Rainy:
                    Raise("rainwater_optimization",
                        "Rainwater Optimization Enabled",
                        "Natural rainfall detected. Irrigation reduced — water saved automatically.",
                        EcoAlertLevel.Info,
                        forceNew: true);
                    break;
                case WeatherManager.WeatherType.Storm:
                    Raise("storm_pause",
                        "Storm Detected — Irrigation Paused",
                        "Severe weather protection active. Water flow stopped for safety.",
                        EcoAlertLevel.Critical,
                        forceNew: true);
                    break;
                case WeatherManager.WeatherType.Sunny:
                default:
                    Resolve("rainwater_optimization");
                    Resolve("storm_pause");
                    break;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Raise / Resolve
        // ─────────────────────────────────────────────────────────────────────

        private void Raise(string id, string title, string message, EcoAlertLevel level, bool forceNew = false)
        {
            float now = Time.unscaledTime;
            bool alreadyActive = _activeIds.Contains(id);

            if (!forceNew && alreadyActive
                && _lastRaised.TryGetValue(id, out var prev)
                && now - prev < alertCooldownSeconds)
            {
                return;
            }

            _lastRaised[id] = now;

            var alert = new EcoAlert
            {
                id           = id,
                title        = title,
                message      = message,
                level        = level,
                timestampUtc = DateTime.UtcNow
            };

            if (!alreadyActive)
            {
                _activeIds.Add(id);
                _active.Insert(0, alert);
            }
            else
            {
                // Update existing entry's text + timestamp
                for (int i = 0; i < _active.Count; i++)
                {
                    if (_active[i].id != id) continue;
                    _active[i] = alert;
                    break;
                }
            }

            // Keep the list bounded so UI doesn't drift
            const int MaxAlerts = 12;
            if (_active.Count > MaxAlerts) _active.RemoveAt(_active.Count - 1);

            OnAlertRaised?.Invoke(alert);
            OnActiveListChanged?.Invoke(_active);
            EventLogger.LogEvent($"[Eco] {title}: {message}");
        }

        private void Resolve(string id)
        {
            if (!_activeIds.Remove(id)) return;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (_active[i].id == id)
                {
                    _active.RemoveAt(i);
                    break;
                }
            }
            OnAlertResolved?.Invoke(id);
            OnActiveListChanged?.Invoke(_active);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        public void ClearAll()
        {
            _active.Clear();
            _activeIds.Clear();
            _lastRaised.Clear();
            OnActiveListChanged?.Invoke(_active);
        }

        public void SetReferences(WeatherOptimizationSystem wo, WaterSavingTracker ws,
            IrrigationEfficiencySystem eff, SustainabilityScoreSystem score, IrrigationZoneManager zm)
        {
            weatherOptimization = wo;
            waterSaver          = ws;
            efficiencySystem    = eff;
            scoreSystem         = score;
            zoneManager         = zm;
        }
    }
}
