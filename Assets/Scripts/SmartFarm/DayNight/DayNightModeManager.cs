using System;
using System.Collections;
using UnityEngine;

namespace SmartFarm.DayNight
{
    /// <summary>
    /// Two-state mode driving the entire environment.
    /// </summary>
    public enum DayNightMode
    {
        Day,
        Night
    }

    /// <summary>
    /// Central authority for the Day &amp; Night system.
    ///
    /// Drives a single <see cref="NightWeight"/> value (0 = full day, 1 = full night)
    /// over <see cref="transitionDuration"/> seconds whenever the mode changes.
    /// Every subsystem (lighting, street lamps, screen glow, atmosphere, weather
    /// bridge) subscribes to <see cref="OnNightWeightChanged"/> and lerps its own
    /// state from that one value. This means:
    ///   • One coroutine total for the whole system (Quest VR friendly).
    ///   • Modules stay decoupled and can be added/removed freely.
    ///   • UI gets a continuous progress signal for animated transitions.
    /// </summary>
    [AddComponentMenu("SmartFarm/Day Night/Day Night Mode Manager")]
    [DisallowMultipleComponent]
    public class DayNightModeManager : MonoBehaviour
    {
        // ── Configuration ────────────────────────────────────────────────────

        [Header("Startup")]
        [Tooltip("Mode applied (instantly) on Awake.")]
        [SerializeField] private DayNightMode startMode = DayNightMode.Day;

        [Tooltip("If true, Awake immediately drives the start mode through every subscriber.")]
        [SerializeField] private bool applyStartModeOnAwake = true;

        [Header("Transition")]
        [Tooltip("Seconds it takes to morph between Day and Night.")]
        [SerializeField, Range(0.05f, 8f)] private float transitionDuration = 2.5f;

        [Tooltip("Easing curve sampled over the transition (X = time 0..1, Y = nightWeight blend 0..1).")]
        [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Logging")]
        [SerializeField] private bool logEvents = true;

        // ── Public state ─────────────────────────────────────────────────────

        public static DayNightModeManager Instance { get; private set; }

        /// <summary>The mode the system is currently transitioning toward (or already settled on).</summary>
        public DayNightMode CurrentMode { get; private set; } = DayNightMode.Day;

        /// <summary>0 = full day visuals, 1 = full night visuals. Updated continuously during a transition.</summary>
        public float NightWeight { get; private set; }

        public bool IsTransitioning { get; private set; }

        public float TransitionDuration => transitionDuration;

        // ── Events ───────────────────────────────────────────────────────────

        /// <summary>Fired the instant <see cref="SetMode"/> kicks off a transition (or instant snap).</summary>
        public event Action<DayNightMode> OnTransitionStart;

        /// <summary>Fired every frame during the transition with the new night weight (0..1).</summary>
        public event Action<float> OnNightWeightChanged;

        /// <summary>Fired when a transition finishes (or instantly after a snap).</summary>
        public event Action<DayNightMode> OnTransitionComplete;

        /// <summary>Fired only when the target mode actually changes (skipped for redundant calls).</summary>
        public event Action<DayNightMode> OnModeChanged;

        // ── Private ──────────────────────────────────────────────────────────

        private Coroutine _transition;

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[DayNight] Duplicate DayNightModeManager — destroying the new one.");
                enabled = false;
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (applyStartModeOnAwake)
                SetMode(startMode, instant: true);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Cycle between Day and Night.</summary>
        public void ToggleMode()
        {
            SetMode(CurrentMode == DayNightMode.Day ? DayNightMode.Night : DayNightMode.Day);
        }

        /// <summary>UI helper — set Day mode.</summary>
        public void SetDay()   => SetMode(DayNightMode.Day);

        /// <summary>UI helper — set Night mode.</summary>
        public void SetNight() => SetMode(DayNightMode.Night);

        /// <summary>
        /// Re-broadcast the current <see cref="NightWeight"/> to every subscriber
        /// without changing mode or interrupting a transition. Useful when a
        /// sibling system (e.g. weather) mutates shared render state and we
        /// want the day/night look to "win" on top.
        /// </summary>
        public void ReapplyCurrentWeight()
        {
            OnNightWeightChanged?.Invoke(NightWeight);
        }

        /// <summary>
        /// Switch to <paramref name="mode"/>. If <paramref name="instant"/> is true the change is
        /// applied in a single frame; otherwise the manager smoothly interpolates over
        /// <see cref="TransitionDuration"/> seconds using <see cref="transitionCurve"/>.
        /// </summary>
        public void SetMode(DayNightMode mode, bool instant = false)
        {
            bool modeChanged = mode != CurrentMode;
            CurrentMode = mode;

            if (_transition != null)
            {
                StopCoroutine(_transition);
                _transition = null;
            }

            float targetWeight = mode == DayNightMode.Night ? 1f : 0f;

            OnTransitionStart?.Invoke(mode);

            if (instant || transitionDuration <= 0.01f || !isActiveAndEnabled)
            {
                NightWeight = targetWeight;
                OnNightWeightChanged?.Invoke(NightWeight);
                IsTransitioning = false;
                OnTransitionComplete?.Invoke(mode);
            }
            else
            {
                _transition = StartCoroutine(TransitionRoutine(NightWeight, targetWeight, mode));
            }

            if (modeChanged)
            {
                OnModeChanged?.Invoke(mode);
                if (logEvents) EventLogger.LogEvent($"Environment mode set to {mode}");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Transition coroutine
        // ─────────────────────────────────────────────────────────────────────

        private IEnumerator TransitionRoutine(float fromWeight, float toWeight, DayNightMode targetMode)
        {
            IsTransitioning = true;
            float elapsed = 0f;

            while (elapsed < transitionDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / transitionDuration);
                float curved = Mathf.Clamp01(transitionCurve.Evaluate(t));
                NightWeight = Mathf.Lerp(fromWeight, toWeight, curved);
                OnNightWeightChanged?.Invoke(NightWeight);
                yield return null;
            }

            NightWeight = toWeight;
            OnNightWeightChanged?.Invoke(NightWeight);
            IsTransitioning = false;
            _transition = null;
            OnTransitionComplete?.Invoke(targetMode);
        }
    }
}
