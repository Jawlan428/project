using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SmartFarm
{
    public class OverviewUI : MonoBehaviour
    {
        [SerializeField] private FarmDataManager dataManager;

        [Header("Cards")]
        [SerializeField] private TMP_Text soilValueText;
        [SerializeField] private Image soilProgress;
        [SerializeField] private TMP_Text soilTrendText;
        [SerializeField] private TMP_Text healthValueText;
        [SerializeField] private Image healthProgress;
        [SerializeField] private TMP_Text healthTrendText;
        [SerializeField] private TMP_Text temperatureValueText;
        [SerializeField] private TMP_Text temperatureTrendText;
        [SerializeField] private TMP_Text predictedYieldText;
        [SerializeField] private TMP_Text irrigationStatusText;

        private float _lastSoil;
        private float _lastHealth;
        private float _lastTemp;
        private bool _hasLast;

        private void OnEnable()
        {
            if (dataManager == null) dataManager = FindFirstObjectByType<FarmDataManager>();
            if (dataManager != null) dataManager.OnDataChanged += OnDataChanged;
        }

        private void OnDisable()
        {
            if (dataManager != null) dataManager.OnDataChanged -= OnDataChanged;
        }

        private void OnDataChanged(FarmSimulationState state)
        {
            if (soilValueText != null)
                soilValueText.text = $"<size=65%><color=#D7E7F5>Soil Moisture</color></size>\n{state.soilMoisturePercent:F0}%";
            if (healthValueText != null)
                healthValueText.text = $"<size=65%><color=#D7E7F5>Crop Health</color></size>\n{state.cropHealthPercent:F0}%";
            if (temperatureValueText != null)
                temperatureValueText.text = $"<size=65%><color=#D7E7F5>Temperature</color></size>\n{state.temperature:F1}°C";
            if (predictedYieldText != null)
                predictedYieldText.text = $"<size=65%><color=#D7E7F5>Predicted Yield</color></size>\n{state.predictedYield}";
            if (irrigationStatusText != null)
                irrigationStatusText.text = $"<size=65%><color=#D7E7F5>Irrigation</color></size>\n{(state.irrigationEnabled ? "ON" : "OFF")}";

            if (soilProgress != null) soilProgress.fillAmount = Mathf.Clamp01(state.soilMoisturePercent / 100f);
            if (healthProgress != null) healthProgress.fillAmount = Mathf.Clamp01(state.cropHealthPercent / 100f);

            if (_hasLast)
            {
                SetTrend(soilTrendText, state.soilMoisturePercent - _lastSoil);
                SetTrend(healthTrendText, state.cropHealthPercent - _lastHealth);
                SetTrend(temperatureTrendText, state.temperature - _lastTemp);
            }
            else
            {
                SetTrend(soilTrendText, float.NaN);
                SetTrend(healthTrendText, float.NaN);
                SetTrend(temperatureTrendText, float.NaN);
                _hasLast = true;
            }

            _lastSoil = state.soilMoisturePercent;
            _lastHealth = state.cropHealthPercent;
            _lastTemp = state.temperature;
        }

        private static void SetTrend(TMP_Text label, float delta)
        {
            if (label == null) return;
            if (float.IsNaN(delta))
            {
                label.text = string.Empty;
                return;
            }
            if (Mathf.Abs(delta) < 0.01f)
            {
                label.text = string.Empty;
            }
            else
            {
                label.text = delta > 0 ? "<color=#58E07C>↑</color>" : "<color=#F08A8A>↓</color>";
            }
        }
    }
}
