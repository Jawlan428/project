using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SmartFarm
{
    public class IrrigationUI : MonoBehaviour
    {
        [SerializeField] private FarmDataManager dataManager;
        [SerializeField] private TMP_Text irrigationStatusText;
        [SerializeField] private Button toggleButton;
        [SerializeField] private TMP_Text toggleButtonText;
        [SerializeField] private Button boost30Button;
        [SerializeField] private Button morningPresetButton;
        [SerializeField] private Button noonPresetButton;
        [SerializeField] private Button eveningPresetButton;

        private void Start()
        {
            if (dataManager == null) dataManager = FindFirstObjectByType<FarmDataManager>();

            if (toggleButton != null) toggleButton.onClick.AddListener(() => dataManager?.ToggleIrrigationManual());
            if (boost30Button != null) boost30Button.onClick.AddListener(() => dataManager?.BoostIrrigation30Seconds());
            if (morningPresetButton != null) morningPresetButton.onClick.AddListener(() => dataManager?.ApplySchedulePlaceholder("Morning"));
            if (noonPresetButton != null) noonPresetButton.onClick.AddListener(() => dataManager?.ApplySchedulePlaceholder("Noon"));
            if (eveningPresetButton != null) eveningPresetButton.onClick.AddListener(() => dataManager?.ApplySchedulePlaceholder("Evening"));
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

        private void OnDataChanged(FarmSimulationState state)
        {
            if (irrigationStatusText != null)
                irrigationStatusText.text = $"Irrigation: {(state.irrigationEnabled ? "ON" : "OFF")}";
            if (toggleButtonText != null)
                toggleButtonText.text = state.irrigationEnabled ? "Turn OFF" : "Turn ON";
        }
    }
}
