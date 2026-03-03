using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SmartFarm
{
    /// <summary>
    /// Controls the Irrigation tab on the Smart Farm Tablet.
    /// Three buttons: Turn ON · Turn OFF · Boost 30 seconds.
    /// The active state button is highlighted; the inactive one is dimmed.
    /// </summary>
    public class IrrigationUI : MonoBehaviour
    {
        [SerializeField] private FarmDataManager dataManager;
        [SerializeField] private TMP_Text irrigationStatusText;
        [SerializeField] private Button   turnOnButton;
        [SerializeField] private Button   turnOffButton;
        [SerializeField] private Button   boost30Button;

        // Button highlight colours
        private static readonly Color ActiveGreen  = new Color(0.10f, 0.72f, 0.20f, 1f);
        private static readonly Color DimGreen     = new Color(0.20f, 0.70f, 0.30f, 0.45f);
        private static readonly Color ActiveRed    = new Color(0.82f, 0.18f, 0.10f, 1f);
        private static readonly Color DimRed       = new Color(0.70f, 0.25f, 0.20f, 0.45f);

        private void Start()
        {
            if (dataManager == null)
                dataManager = FindFirstObjectByType<FarmDataManager>();

            if (turnOnButton  != null) turnOnButton.onClick.AddListener(OnTurnOn);
            if (turnOffButton != null) turnOffButton.onClick.AddListener(OnTurnOff);
            if (boost30Button != null) boost30Button.onClick.AddListener(OnBoost);
        }

        private void OnEnable()
        {
            if (dataManager != null)
                dataManager.OnDataChanged += OnDataChanged;
        }

        private void OnDisable()
        {
            if (dataManager != null)
                dataManager.OnDataChanged -= OnDataChanged;
        }

        // ── Button handlers ───────────────────────────────────────────────────

        private void OnTurnOn()  => dataManager?.SetIrrigationManual(true);
        private void OnTurnOff() => dataManager?.SetIrrigationManual(false);
        private void OnBoost()   => dataManager?.BoostIrrigation30Seconds();

        // ── State update ──────────────────────────────────────────────────────

        private void OnDataChanged(FarmSimulationState state)
        {
            bool on = state.irrigationEnabled;

            if (irrigationStatusText != null)
                irrigationStatusText.text = $"Irrigation: {(on ? "ON" : "OFF")}";

            SetButtonColor(turnOnButton,  on ? ActiveGreen : DimGreen);
            SetButtonColor(turnOffButton, on ? DimRed      : ActiveRed);
        }

        private static void SetButtonColor(Button btn, Color color)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null) img.color = color;
        }
    }
}
