using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SmartFarm.DayNight.UI
{
    /// <summary>
    /// World-space UI panel that lets the user switch the smart farm between
    /// Day and Night. Subscribes to <see cref="DayNightModeManager"/>:
    ///   • Buttons drive <c>SetMode</c> on the manager.
    ///   • Status text and LED reflect the current mode + transition progress.
    ///   • Progress bar visualises the manager's <c>NightWeight</c> live.
    ///
    /// Designed to be VR-friendly:
    ///   • All graphics live on a world-space canvas (set up by the editor tool).
    ///   • <see cref="UnityEngine.UI.Button"/>s are Pressable / Pokeable through
    ///     XR Interaction Toolkit's TrackedDeviceGraphicRaycaster.
    /// </summary>
    [AddComponentMenu("SmartFarm/Day Night/UI/Environment Control Panel")]
    public class EnvironmentControlPanelUI : MonoBehaviour
    {
        [Header("Manager")]
        [SerializeField] private DayNightModeManager manager;

        [Header("Buttons")]
        [SerializeField] private EnvironmentControlButton dayButton;
        [SerializeField] private EnvironmentControlButton nightButton;

        [Header("Status")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Image statusLed;
        [SerializeField] private Image progressFill;

        [Header("Status Colors")]
        [SerializeField] private Color dayLedColor   = new Color(1.0f, 0.85f, 0.30f, 1f);
        [SerializeField] private Color nightLedColor = new Color(0.40f, 0.75f, 1.40f, 1f);

        private void Awake()
        {
            if (manager == null) manager = DayNightModeManager.Instance ?? FindFirstObjectByType<DayNightModeManager>();
        }

        private void OnEnable()
        {
            if (dayButton   != null && dayButton.Button   != null) dayButton.Button.onClick.AddListener(OnDayClicked);
            if (nightButton != null && nightButton.Button != null) nightButton.Button.onClick.AddListener(OnNightClicked);

            if (manager != null)
            {
                manager.OnModeChanged        += HandleModeChanged;
                manager.OnNightWeightChanged += HandleNightWeight;
                manager.OnTransitionStart    += HandleTransitionStart;
                manager.OnTransitionComplete += HandleTransitionComplete;

                HandleModeChanged(manager.CurrentMode);
                HandleNightWeight(manager.NightWeight);
            }
            else
            {
                if (statusText != null) statusText.text = "No DayNightModeManager found.";
            }
        }

        private void OnDisable()
        {
            if (dayButton   != null && dayButton.Button   != null) dayButton.Button.onClick.RemoveListener(OnDayClicked);
            if (nightButton != null && nightButton.Button != null) nightButton.Button.onClick.RemoveListener(OnNightClicked);

            if (manager != null)
            {
                manager.OnModeChanged        -= HandleModeChanged;
                manager.OnNightWeightChanged -= HandleNightWeight;
                manager.OnTransitionStart    -= HandleTransitionStart;
                manager.OnTransitionComplete -= HandleTransitionComplete;
            }
        }

        // ── Click forwarders ─────────────────────────────────────────────────

        private void OnDayClicked()
        {
            if (manager != null) manager.SetMode(DayNightMode.Day);
        }

        private void OnNightClicked()
        {
            if (manager != null) manager.SetMode(DayNightMode.Night);
        }

        // ── Manager callbacks ────────────────────────────────────────────────

        private void HandleModeChanged(DayNightMode mode)
        {
            if (dayButton   != null) dayButton.SetActiveVisual(mode == DayNightMode.Day);
            if (nightButton != null) nightButton.SetActiveVisual(mode == DayNightMode.Night);
            UpdateStatusText(mode, transitioning: manager != null && manager.IsTransitioning);
        }

        private void HandleTransitionStart(DayNightMode mode)
        {
            UpdateStatusText(mode, transitioning: true);
        }

        private void HandleTransitionComplete(DayNightMode mode)
        {
            UpdateStatusText(mode, transitioning: false);
        }

        private void HandleNightWeight(float nightWeight)
        {
            if (progressFill != null) progressFill.fillAmount = Mathf.Clamp01(nightWeight);
            if (statusLed != null)
                statusLed.color = Color.Lerp(dayLedColor, nightLedColor, nightWeight);
        }

        private void UpdateStatusText(DayNightMode mode, bool transitioning)
        {
            if (statusText == null) return;
            string modeName = mode == DayNightMode.Day ? "DAY" : "NIGHT";
            statusText.text = transitioning
                ? $"Transitioning to {modeName}…"
                : $"Mode: {modeName}";
        }
    }
}
