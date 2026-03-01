using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace SmartFarm
{
    /// <summary>
    /// Fetches real-world temperature from OpenWeather and applies it to FarmSimulationManager.
    /// Uses periodic polling (no per-frame network requests).
    /// </summary>
    public class RealTemperatureService : MonoBehaviour
    {
        public enum LocationMode
        {
            CityName,
            Coordinates
        }

        [Header("References")]
        [SerializeField] private FarmSimulationManager simulationManager;

        [Header("OpenWeather Settings")]
        [SerializeField] private bool enableRealTemperature = false;
        [SerializeField] private string apiKey = "";
        [SerializeField] private LocationMode locationMode = LocationMode.CityName;
        [SerializeField] private string city = "Cairo";
        [SerializeField] private string countryCode = "";
        [SerializeField] private float latitude = 30.0444f;
        [SerializeField] private float longitude = 31.2357f;
        [SerializeField] [Min(60f)] private float refreshIntervalSeconds = 300f;
        [SerializeField] [Range(0f, 10f)] private float smoothingFactor = 0.25f;

        [Header("Fallback")]
        [SerializeField] private bool useFallbackIfRequestFails = true;
        [SerializeField] [Range(0f, 50f)] private float fallbackTemperatureC = 24f;

        private Coroutine _pollRoutine;
        private bool _hasLastApiTemp;
        private float _lastAppliedTemp;

        private void Start()
        {
            if (simulationManager == null)
                simulationManager = FindFirstObjectByType<FarmSimulationManager>();

            if (!enableRealTemperature)
            {
                Debug.Log("[SmartFarm] RealTemperatureService disabled (enable in Inspector to use live weather).");
                return;
            }

            if (simulationManager == null)
            {
                Debug.LogWarning("[SmartFarm] RealTemperatureService: FarmSimulationManager not found.");
                return;
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Debug.LogWarning("[SmartFarm] RealTemperatureService: API key missing. Using fallback temperature.");
                if (useFallbackIfRequestFails)
                    simulationManager.SetGlobalTemperature(fallbackTemperatureC);
                return;
            }

            _pollRoutine = StartCoroutine(PollLoop());
        }

        private void OnDisable()
        {
            if (_pollRoutine != null)
                StopCoroutine(_pollRoutine);
        }

        private IEnumerator PollLoop()
        {
            // Initial fetch immediately
            yield return FetchAndApply();

            var wait = new WaitForSeconds(refreshIntervalSeconds);
            while (true)
            {
                yield return wait;
                yield return FetchAndApply();
            }
        }

        private IEnumerator FetchAndApply()
        {
            string url = BuildOpenWeatherUrl();
            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = 12;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[SmartFarm] Temperature API failed: {request.error}");
                    ApplyFallbackIfNeeded();
                    yield break;
                }

                string json = request.downloadHandler.text;
                if (string.IsNullOrWhiteSpace(json))
                {
                    Debug.LogWarning("[SmartFarm] Temperature API returned empty response.");
                    ApplyFallbackIfNeeded();
                    yield break;
                }

                float tempC;
                if (!TryParseOpenWeatherTemp(json, out tempC))
                {
                    Debug.LogWarning("[SmartFarm] Temperature API parse failed.");
                    ApplyFallbackIfNeeded();
                    yield break;
                }

                ApplyTemperature(tempC);
            }
        }

        private void ApplyTemperature(float temperatureC)
        {
            if (simulationManager == null) return;

            if (!_hasLastApiTemp)
            {
                _lastAppliedTemp = temperatureC;
                _hasLastApiTemp = true;
            }
            else
            {
                _lastAppliedTemp = Mathf.Lerp(_lastAppliedTemp, temperatureC, smoothingFactor);
            }

            simulationManager.SetGlobalTemperature(_lastAppliedTemp);
            EventLogger.LogEvent($"Real temperature updated: {_lastAppliedTemp:F1}°C");
        }

        private void ApplyFallbackIfNeeded()
        {
            if (!useFallbackIfRequestFails || simulationManager == null) return;
            simulationManager.SetGlobalTemperature(fallbackTemperatureC);
        }

        private string BuildOpenWeatherUrl()
        {
            // units=metric => Celsius
            if (locationMode == LocationMode.Coordinates)
            {
                return $"https://api.openweathermap.org/data/2.5/weather?lat={latitude}&lon={longitude}&units=metric&appid={apiKey}";
            }

            string q = string.IsNullOrWhiteSpace(countryCode) ? city : $"{city},{countryCode}";
            string escapedQ = UnityWebRequest.EscapeURL(q);
            return $"https://api.openweathermap.org/data/2.5/weather?q={escapedQ}&units=metric&appid={apiKey}";
        }

        private static bool TryParseOpenWeatherTemp(string json, out float tempC)
        {
            tempC = 0f;
            try
            {
                var wrapper = JsonUtility.FromJson<OpenWeatherResponse>(json);
                if (wrapper == null || wrapper.main == null) return false;
                tempC = wrapper.main.temp;
                return true;
            }
            catch
            {
                return false;
            }
        }

        [System.Serializable]
        private class OpenWeatherResponse
        {
            public MainInfo main;
        }

        [System.Serializable]
        private class MainInfo
        {
            public float temp;
        }
    }
}
