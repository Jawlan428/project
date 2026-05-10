using System.Collections.Generic;
using UnityEngine;

namespace SmartFarm.DayNight
{
    /// <summary>
    /// Centralised controller for every <see cref="StreetLamp"/> in the scene.
    /// Listens to the manager's <c>NightWeight</c> and pushes the same on/off
    /// weight to all lamps so the rig fades in/out together.
    ///
    /// Auto-discovers lamps:
    ///   • via the inspector list (preferred), OR
    ///   • by scanning the scene for <see cref="StreetLamp"/> components on Awake.
    /// </summary>
    [AddComponentMenu("SmartFarm/Day Night/Street Lamp Manager")]
    public class StreetLampManager : MonoBehaviour
    {
        [Header("Manager")]
        [SerializeField] private DayNightModeManager manager;

        [Header("Lamps")]
        [Tooltip("Manually-assigned lamps. Leave empty to auto-discover all StreetLamp components in the scene on Awake.")]
        [SerializeField] private List<StreetLamp> lamps = new List<StreetLamp>();
        [SerializeField] private bool autoDiscoverOnAwake = true;
        [Tooltip("Include inactive lamps when auto-discovering (recommended for prefabs hidden until lit).")]
        [SerializeField] private bool includeInactive = true;

        [Header("Curve")]
        [Tooltip("Maps the manager's nightWeight (0..1) to the lamp on-weight (0..1). Default = identity = lamps follow night exactly.")]
        [SerializeField] private AnimationCurve nightToOnCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("Optional: only switch the lamps on once nightWeight crosses this threshold (creates a snappier 'lights coming on at dusk' moment).")]
        [SerializeField, Range(0f, 1f)] private float onsetThreshold = 0f;

        public IReadOnlyList<StreetLamp> Lamps => lamps;

        private void Awake()
        {
            if (manager == null) manager = DayNightModeManager.Instance ?? FindFirstObjectByType<DayNightModeManager>();
            if (autoDiscoverOnAwake && (lamps == null || lamps.Count == 0))
                DiscoverLampsInScene();
        }

        private void OnEnable()
        {
            if (manager != null)
                manager.OnNightWeightChanged += HandleWeight;
            // Apply current state immediately so lamps don't pop on the first tick.
            if (manager != null) HandleWeight(manager.NightWeight);
        }

        private void OnDisable()
        {
            if (manager != null)
                manager.OnNightWeightChanged -= HandleWeight;
        }

        private void HandleWeight(float nightWeight)
        {
            float w = Mathf.Clamp01(nightWeight);
            // Apply onset threshold: nothing happens until we cross it.
            if (onsetThreshold > 0.001f)
            {
                if (w <= onsetThreshold) w = 0f;
                else w = (w - onsetThreshold) / (1f - onsetThreshold);
            }
            float on = Mathf.Clamp01(nightToOnCurve.Evaluate(w));
            for (int i = 0; i < lamps.Count; i++)
            {
                var lamp = lamps[i];
                if (lamp == null) continue;
                lamp.SetWeight(on);
            }
        }

        // ── Discovery / API ──────────────────────────────────────────────────

        /// <summary>Scan the active scene for any <see cref="StreetLamp"/> and overwrite the list.</summary>
        public int DiscoverLampsInScene()
        {
            var found = FindObjectsByType<StreetLamp>(
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            lamps.Clear();
            for (int i = 0; i < found.Length; i++)
                if (found[i] != null) lamps.Add(found[i]);
            return lamps.Count;
        }

        /// <summary>Adds a new lamp (de-duplicated). Returns true if added.</summary>
        public bool RegisterLamp(StreetLamp lamp)
        {
            if (lamp == null) return false;
            if (lamps.Contains(lamp)) return false;
            lamps.Add(lamp);
            // Sync immediately so the new lamp matches the rest.
            if (manager != null) lamp.SetWeight(Mathf.Clamp01(nightToOnCurve.Evaluate(manager.NightWeight)));
            return true;
        }

        /// <summary>Removes a lamp from the rig.</summary>
        public bool UnregisterLamp(StreetLamp lamp)
        {
            return lamp != null && lamps.Remove(lamp);
        }

        /// <summary>Toggle storm flicker across the whole rig. Called by <see cref="WeatherNightBridge"/>.</summary>
        public void SetStormFlicker(bool enabled)
        {
            for (int i = 0; i < lamps.Count; i++)
                if (lamps[i] != null) lamps[i].SetStormFlicker(enabled);
        }
    }
}
