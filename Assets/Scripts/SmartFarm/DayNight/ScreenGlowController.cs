using System.Collections.Generic;
using UnityEngine;

namespace SmartFarm.DayNight
{
    /// <summary>
    /// Centralised driver for every <see cref="SmartScreenGlowTarget"/> in the
    /// scene. Applies the manager's nightWeight to each target so the Crop
    /// Growth Monitor, Smart Irrigation Tablet, weather screens, dashboards
    /// and analytics panels all glow in unison at night.
    /// </summary>
    [AddComponentMenu("SmartFarm/Day Night/Screen Glow Controller")]
    public class ScreenGlowController : MonoBehaviour
    {
        [Header("Manager")]
        [SerializeField] private DayNightModeManager manager;

        [Header("Targets")]
        [Tooltip("Manually-assigned screens. Leave empty to auto-discover all SmartScreenGlowTarget components in the scene on Awake.")]
        [SerializeField] private List<SmartScreenGlowTarget> targets = new List<SmartScreenGlowTarget>();
        [SerializeField] private bool autoDiscoverOnAwake = true;
        [SerializeField] private bool includeInactive = true;

        [Header("Curve")]
        [Tooltip("Maps nightWeight (0..1) to the per-screen weight (0..1). Default = identity.")]
        [SerializeField] private AnimationCurve nightCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [Tooltip("Multiplier applied to every screen's weight (1 = follow manager exactly, 1.5 = brighter night, 0.5 = dimmer).")]
        [SerializeField, Range(0f, 2f)] private float globalIntensity = 1f;

        public IReadOnlyList<SmartScreenGlowTarget> Targets => targets;

        private void Awake()
        {
            if (manager == null) manager = DayNightModeManager.Instance ?? FindFirstObjectByType<DayNightModeManager>();
            if (autoDiscoverOnAwake && (targets == null || targets.Count == 0))
                DiscoverTargetsInScene();
        }

        private void OnEnable()
        {
            if (manager != null)
            {
                manager.OnNightWeightChanged += HandleWeight;
                HandleWeight(manager.NightWeight);
            }
        }

        private void OnDisable()
        {
            if (manager != null)
                manager.OnNightWeightChanged -= HandleWeight;
        }

        private void HandleWeight(float nightWeight)
        {
            float w = Mathf.Clamp01(nightCurve.Evaluate(Mathf.Clamp01(nightWeight)) * globalIntensity);
            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                if (t == null) continue;
                t.ApplyWeight(w);
            }
        }

        /// <summary>Scan the scene and overwrite the target list.</summary>
        public int DiscoverTargetsInScene()
        {
            var found = FindObjectsByType<SmartScreenGlowTarget>(
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            targets.Clear();
            for (int i = 0; i < found.Length; i++)
                if (found[i] != null) targets.Add(found[i]);
            return targets.Count;
        }

        public bool RegisterTarget(SmartScreenGlowTarget target)
        {
            if (target == null || targets.Contains(target)) return false;
            targets.Add(target);
            if (manager != null) target.ApplyWeight(Mathf.Clamp01(nightCurve.Evaluate(manager.NightWeight)));
            return true;
        }

        public bool UnregisterTarget(SmartScreenGlowTarget target)
        {
            return target != null && targets.Remove(target);
        }
    }
}
