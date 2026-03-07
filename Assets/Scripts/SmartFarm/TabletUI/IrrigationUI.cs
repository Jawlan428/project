using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SmartFarm
{
    /// <summary>
    /// Smart Irrigation Control — Irrigation tab on the Smart Farm Tablet.
    ///
    /// Layout (wire up in the Inspector):
    ///
    ///   [Title]  "Smart Irrigation Control"
    ///
    ///   ── Mode Selection ──────────────────────────────────
    ///   [Manual Mode]   [Scheduled Mode]   [AI Mode]
    ///
    ///   ── Manual Panel (visible in Manual mode) ───────────
    ///   Status:  "Manual Irrigation: ON / OFF"
    ///   [ON]  [OFF]
    ///
    ///   ── Scheduled Panel (visible in Scheduled mode) ─────
    ///   [Morning]  [Noon]  [Evening]
    ///   Status:  e.g. "Next: Morning in 2h 14m"
    ///
    ///   ── AI Panel (visible in AI mode) ───────────────────
    ///   Status:  "AI Irrigation Active / Standby"
    ///   Reason:  e.g. "Low soil moisture detected (28%)"
    ///
    /// Integrates with SmartIrrigationManager (mode logic) and
    /// FarmDataManager (live state refreshes from simulation ticks).
    /// </summary>
    public class IrrigationUI : MonoBehaviour
    {
        // ── Managers ──────────────────────────────────────────────────────────

        [Header("Managers")]
        [SerializeField] private FarmDataManager       dataManager;
        [SerializeField] private SmartIrrigationManager irrigationManager;

        // ── Mode Selection ────────────────────────────────────────────────────

        [Header("Mode Selection Buttons")]
        [SerializeField] private Button manualModeButton;
        [SerializeField] private Button scheduledModeButton;
        [SerializeField] private Button aiModeButton;

        // ── Mode Panels ───────────────────────────────────────────────────────

        [Header("Mode Panels")]
        [Tooltip("Root GameObject shown only in Manual mode")]
        [SerializeField] private GameObject manualPanel;
        [Tooltip("Root GameObject shown only in Scheduled mode")]
        [SerializeField] private GameObject scheduledPanel;
        [Tooltip("Root GameObject shown only in AI mode")]
        [SerializeField] private GameObject aiPanel;

        // ── Manual Mode ───────────────────────────────────────────────────────

        [Header("Manual Mode UI")]
        [Tooltip("Text showing 'Manual Irrigation: ON/OFF'")]
        [SerializeField] private TMP_Text manualStatusText;
        [SerializeField] private Button   manualOnButton;
        [SerializeField] private Button   manualOffButton;

        // ── Scheduled Mode ────────────────────────────────────────────────────

        [Header("Scheduled Mode UI")]
        [Tooltip("Text showing schedule status / next activation info")]
        [SerializeField] private TMP_Text scheduleStatusText;
        [SerializeField] private Button   morningButton;
        [SerializeField] private Button   noonButton;
        [SerializeField] private Button   eveningButton;

        // ── AI Mode ───────────────────────────────────────────────────────────

        [Header("AI Mode UI")]
        [Tooltip("Text showing 'AI Irrigation Active' or 'AI Irrigation Standby'")]
        [SerializeField] private TMP_Text aiStatusText;
        [Tooltip("Text showing the AI decision reason")]
        [SerializeField] private TMP_Text aiReasonText;

        // ── Button colours ────────────────────────────────────────────────────

        private static readonly Color ActiveGreen  = new Color(0.10f, 0.72f, 0.20f, 1.00f);
        private static readonly Color DimGreen     = new Color(0.20f, 0.70f, 0.30f, 0.45f);
        private static readonly Color ActiveRed    = new Color(0.82f, 0.18f, 0.10f, 1.00f);
        private static readonly Color DimRed       = new Color(0.70f, 0.25f, 0.20f, 0.45f);
        private static readonly Color ActiveBlue   = new Color(0.10f, 0.45f, 0.90f, 1.00f);
        private static readonly Color ActiveYellow = new Color(0.90f, 0.72f, 0.05f, 1.00f);
        private static readonly Color DimWhite     = new Color(1.00f, 1.00f, 1.00f, 0.35f);

        private string _activeSchedulePreset = "";

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (dataManager == null)
                dataManager = FindFirstObjectByType<FarmDataManager>();
            if (irrigationManager == null)
                irrigationManager = SmartIrrigationManager.Instance
                                    ?? FindFirstObjectByType<SmartIrrigationManager>();
        }

        private void Start()
        {
            // Re-check in case Awake ran before managers were initialised
            if (irrigationManager == null)
                irrigationManager = SmartIrrigationManager.Instance
                                    ?? FindFirstObjectByType<SmartIrrigationManager>();

            // Mode selector
            if (manualModeButton    != null) manualModeButton.onClick.AddListener(()    => OnSelectMode(IrrigationMode.Manual));
            if (scheduledModeButton != null) scheduledModeButton.onClick.AddListener(() => OnSelectMode(IrrigationMode.Scheduled));
            if (aiModeButton        != null) aiModeButton.onClick.AddListener(()        => OnSelectMode(IrrigationMode.AI));

            // Manual panel
            if (manualOnButton  != null) manualOnButton.onClick.AddListener(()  => OnManualToggle(true));
            if (manualOffButton != null) manualOffButton.onClick.AddListener(() => OnManualToggle(false));

            // Scheduled presets
            if (morningButton != null) morningButton.onClick.AddListener(() => OnSchedulePreset("Morning"));
            if (noonButton    != null) noonButton.onClick.AddListener(()    => OnSchedulePreset("Noon"));
            if (eveningButton != null) eveningButton.onClick.AddListener(() => OnSchedulePreset("Evening"));

            // Draw initial state
            var startMode = irrigationManager != null ? irrigationManager.CurrentMode : IrrigationMode.Manual;
            RefreshModeUI(startMode);
        }

        private void OnEnable()
        {
            if (dataManager != null)
                dataManager.OnDataChanged += OnDataChanged;

            if (irrigationManager != null)
            {
                irrigationManager.OnModeChanged            += OnModeChanged;
                irrigationManager.OnIrrigationStateChanged += OnIrrigationStateChanged;
            }
        }

        private void OnDisable()
        {
            if (dataManager != null)
                dataManager.OnDataChanged -= OnDataChanged;

            if (irrigationManager != null)
            {
                irrigationManager.OnModeChanged            -= OnModeChanged;
                irrigationManager.OnIrrigationStateChanged -= OnIrrigationStateChanged;
            }
        }

        // ── Button handlers ───────────────────────────────────────────────────

        private void OnSelectMode(IrrigationMode mode)
        {
            irrigationManager?.SetMode(mode);
            RefreshModeUI(mode);
        }

        private void OnManualToggle(bool on)
        {
            irrigationManager?.SetManualIrrigation(on);
            // Optimistic UI update — confirmed via OnIrrigationStateChanged event
            UpdateManualStatus(on);
        }

        private void OnSchedulePreset(string preset)
        {
            _activeSchedulePreset = preset;
            irrigationManager?.SetSchedulePreset(preset);
            HighlightScheduleButtons();
        }

        // ── Manager event handlers ────────────────────────────────────────────

        private void OnModeChanged(IrrigationMode mode) => RefreshModeUI(mode);

        private void OnIrrigationStateChanged(bool active, string reason)
        {
            var mode = irrigationManager != null ? irrigationManager.CurrentMode : IrrigationMode.Manual;
            switch (mode)
            {
                case IrrigationMode.Manual:
                    UpdateManualStatus(active);
                    break;
                case IrrigationMode.Scheduled:
                    if (scheduleStatusText != null) scheduleStatusText.text = reason;
                    break;
                case IrrigationMode.AI:
                    UpdateAIStatus(active, reason);
                    break;
            }
        }

        private void OnDataChanged(FarmSimulationState state)
        {
            // Keep AI panel reason in sync on every simulation tick
            if (irrigationManager != null && irrigationManager.CurrentMode == IrrigationMode.AI)
                UpdateAIStatus(irrigationManager.IsIrrigationActive, irrigationManager.LastDecisionReason);
        }

        // ── Panel management ──────────────────────────────────────────────────

        private void RefreshModeUI(IrrigationMode mode)
        {
            // Show / hide mode-specific panels
            if (manualPanel    != null) manualPanel.SetActive(mode    == IrrigationMode.Manual);
            if (scheduledPanel != null) scheduledPanel.SetActive(mode == IrrigationMode.Scheduled);
            if (aiPanel        != null) aiPanel.SetActive(mode        == IrrigationMode.AI);

            // Highlight active mode tab button
            SetButtonColor(manualModeButton,    mode == IrrigationMode.Manual    ? ActiveBlue : DimWhite);
            SetButtonColor(scheduledModeButton, mode == IrrigationMode.Scheduled ? ActiveBlue : DimWhite);
            SetButtonColor(aiModeButton,        mode == IrrigationMode.AI        ? ActiveBlue : DimWhite);

            // Refresh the content of the newly visible panel
            if (irrigationManager != null)
                OnIrrigationStateChanged(irrigationManager.IsIrrigationActive, irrigationManager.LastDecisionReason);
        }

        // ── Manual panel helpers ──────────────────────────────────────────────

        private void UpdateManualStatus(bool isOn)
        {
            if (manualStatusText != null)
            {
                manualStatusText.text = isOn
                    ? "Manual Irrigation: <color=#18B834>ON</color>"
                    : "Manual Irrigation: <color=#D12D1A>OFF</color>";
            }

            SetButtonColor(manualOnButton,  isOn ? ActiveGreen : DimGreen);
            SetButtonColor(manualOffButton, isOn ? DimRed      : ActiveRed);
        }

        // ── Scheduled panel helpers ───────────────────────────────────────────

        private void HighlightScheduleButtons()
        {
            SetButtonColor(morningButton, _activeSchedulePreset == "Morning" ? ActiveYellow : DimWhite);
            SetButtonColor(noonButton,    _activeSchedulePreset == "Noon"    ? ActiveYellow : DimWhite);
            SetButtonColor(eveningButton, _activeSchedulePreset == "Evening" ? ActiveYellow : DimWhite);
        }

        // ── AI panel helpers ──────────────────────────────────────────────────

        private void UpdateAIStatus(bool active, string reason)
        {
            if (aiStatusText != null)
            {
                aiStatusText.text = active
                    ? "<color=#18B834>AI Irrigation Active</color>"
                    : "<color=#D12D1A>AI Irrigation Standby</color>";
            }

            if (aiReasonText != null)
                aiReasonText.text = string.IsNullOrEmpty(reason) ? "Evaluating conditions..." : reason;
        }

        // ── Utility ───────────────────────────────────────────────────────────

        private static void SetButtonColor(Button btn, Color color)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null) img.color = color;
        }
    }
}
