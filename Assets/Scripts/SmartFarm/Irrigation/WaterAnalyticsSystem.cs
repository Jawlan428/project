using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SmartFarm.Irrigation
{
    /// <summary>
    /// Tracks water usage history for the Smart Irrigation Tablet's analytics page.
    ///
    /// Buckets data into rolling slots (default: 10 buckets, ~6 seconds each in demo mode)
    /// so a mini bar/line graph can be rendered without doing any math at draw time.
    /// </summary>
    [AddComponentMenu("SmartFarm/Irrigation/Water Analytics System")]
    public class WaterAnalyticsSystem : MonoBehaviour
    {
        [Header("Bucket Settings")]
        [SerializeField, Range(4, 24)] private int bucketCount = 10;
        [SerializeField, Range(0.5f, 60f)] private float bucketSeconds = 6f;

        [Header("References (auto-found if empty)")]
        [SerializeField] private IrrigationZoneManager zoneManager;

        // Rolling history of water usage. Index 0 = oldest, last = current bucket.
        private float[] _history;
        private float   _currentBucketWater;
        private float   _currentBucketTime;

        // Per-zone session totals.
        private readonly Dictionary<string, float> _zoneTotals = new Dictionary<string, float>();
        private float _sessionTotal;

        // Smoothed efficiency [0..1] computed from history vs healthy moisture target.
        private float _efficiency = 0.85f;

        public IReadOnlyList<float> History => _history;
        public float SessionTotal => _sessionTotal;
        public float CurrentBucket => _currentBucketWater;
        public float MaxBucket
        {
            get
            {
                float max = 0.0001f;
                for (int i = 0; i < _history.Length; i++)
                    if (_history[i] > max) max = _history[i];
                return max;
            }
        }
        public float Efficiency => _efficiency;

        public event Action OnHistoryChanged;

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            EnsureBucketArray();
            if (zoneManager == null) zoneManager = FindFirstObjectByType<IrrigationZoneManager>();
        }

        private void OnEnable()
        {
            EnsureBucketArray();
            _currentBucketTime = 0f;
            StartCoroutine(EfficiencyLoop());
        }

        private void EnsureBucketArray()
        {
            if (_history == null || _history.Length != bucketCount)
                _history = new float[Mathf.Max(2, bucketCount)];
        }

        private void Update()
        {
            _currentBucketTime += Time.deltaTime;
            if (_currentBucketTime < bucketSeconds) return;

            // Roll buckets — drop oldest, append current.
            for (int i = 0; i < _history.Length - 1; i++)
                _history[i] = _history[i + 1];
            _history[_history.Length - 1] = _currentBucketWater;

            _currentBucketWater = 0f;
            _currentBucketTime  = 0f;
            OnHistoryChanged?.Invoke();
        }

        private IEnumerator EfficiencyLoop()
        {
            var wait = new WaitForSeconds(2f);
            while (true)
            {
                yield return wait;
                if (zoneManager == null) zoneManager = FindFirstObjectByType<IrrigationZoneManager>();
                if (zoneManager == null) continue;

                // Efficiency = 1 - normalized distance from average moisture to "ideal 65%".
                // Plus a small bonus when fewer zones are running unnecessarily.
                float moistureScore = 1f - Mathf.Clamp01(Mathf.Abs(zoneManager.AverageMoisture - 65f) / 65f);
                float healthScore   = Mathf.Clamp01(zoneManager.AverageHealth / 100f);
                float target        = (moistureScore * 0.6f + healthScore * 0.4f);
                _efficiency         = Mathf.Lerp(_efficiency, Mathf.Clamp01(target), 0.25f);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        public void RecordWaterUsage(string zoneId, float amount)
        {
            if (amount <= 0f) return;
            EnsureBucketArray();
            _currentBucketWater += amount;
            _sessionTotal       += amount;

            if (string.IsNullOrEmpty(zoneId)) return;
            if (!_zoneTotals.TryGetValue(zoneId, out var current)) current = 0f;
            _zoneTotals[zoneId] = current + amount;
        }

        public float GetZoneTotal(string zoneId)
        {
            return _zoneTotals.TryGetValue(zoneId, out var v) ? v : 0f;
        }

        public void ResetSession()
        {
            _sessionTotal = 0f;
            _currentBucketWater = 0f;
            _zoneTotals.Clear();
            for (int i = 0; i < _history.Length; i++) _history[i] = 0f;
            OnHistoryChanged?.Invoke();
        }

        public void SetZoneManager(IrrigationZoneManager mgr) => zoneManager = mgr;
    }
}
