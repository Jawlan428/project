using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SmartFarm
{
    /// <summary>
    /// UI controller for Weather Control Panel. World Space UI.
    /// Triggers WeatherManager.SetWeather() only - no farm logic here.
    /// </summary>
    public class WeatherUIController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WeatherManager weatherManager;

        [Header("UI Elements")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text currentWeatherText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Button sunnyButton;
        [SerializeField] private Button rainyButton;
        [SerializeField] private Button stormButton;

        private static readonly string[] Descriptions = new string[]
        {
            "Sunny: Increases temperature and plant growth rate. Soil moisture decreases gradually.",
            "Rainy: Increases soil moisture and improves crop health over time. Slightly reduces temperature.",
            "Storm: Heavily increases soil moisture but reduces crop health. May cause random damage to plants."
        };

        private void Start()
        {
            if (weatherManager == null)
                weatherManager = FindFirstObjectByType<WeatherManager>();

            if (titleText != null)
                titleText.text = "Weather Control";

            if (sunnyButton != null)
                sunnyButton.onClick.AddListener(() => weatherManager?.SetWeather(WeatherManager.WeatherType.Sunny));
            if (rainyButton != null)
                rainyButton.onClick.AddListener(() => weatherManager?.SetWeather(WeatherManager.WeatherType.Rainy));
            if (stormButton != null)
                stormButton.onClick.AddListener(() => weatherManager?.SetWeather(WeatherManager.WeatherType.Storm));

            if (weatherManager != null)
                weatherManager.OnWeatherChanged += OnWeatherChanged;

            RefreshDisplay();
        }

        private void OnDestroy()
        {
            if (weatherManager != null)
                weatherManager.OnWeatherChanged -= OnWeatherChanged;
        }

        private void OnWeatherChanged(WeatherManager.WeatherType _)
        {
            RefreshDisplay();
        }

        private void RefreshDisplay()
        {
            if (weatherManager == null) return;

            if (currentWeatherText != null)
                currentWeatherText.text = $"Current: {weatherManager.CurrentWeather}";

            if (descriptionText != null)
            {
                int idx = GetDescriptionIndex(weatherManager.CurrentWeather);
                descriptionText.text = Descriptions[idx];
            }
        }

        private static int GetDescriptionIndex(WeatherManager.WeatherType type)
        {
            return type switch
            {
                WeatherManager.WeatherType.Sunny => 0,
                WeatherManager.WeatherType.Rainy => 1,
                WeatherManager.WeatherType.Storm => 2,
                _ => 0
            };
        }
    }
}
