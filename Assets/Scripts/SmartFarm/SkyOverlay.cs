using UnityEngine;
using UnityEngine.UI;

namespace SmartFarm
{
    /// <summary>
    /// Full-screen overlay that darkens your sky and scene for Rainy/Storm.
    /// Keeps your real sky visible underneath - adds a gray tint for rain, darker for storm.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class SkyOverlay : MonoBehaviour
    {
        [Header("Rainy Overlay")]
        [SerializeField] private Color rainyTint = new Color(0.35f, 0.4f, 0.5f);
        [SerializeField] [Range(0f, 1f)] private float rainyStrength = 0.5f;

        [Header("Storm Overlay")]
        [SerializeField] private Color stormTint = new Color(0.15f, 0.18f, 0.25f);
        [SerializeField] [Range(0f, 1f)] private float stormStrength = 0.65f;

        [SerializeField] private Image overlayImage;

        private void Awake()
        {
            if (overlayImage == null)
                overlayImage = GetComponentInChildren<Image>();
        }

        /// <summary>
        /// Set overlay for weather. Sunny = invisible. Rainy/Storm = dark tint over your sky.
        /// </summary>
        public void SetWeather(WeatherManager.WeatherType type)
        {
            if (overlayImage == null) return;

            Color c;
            switch (type)
            {
                case WeatherManager.WeatherType.Sunny:
                    c = new Color(1f, 1f, 1f, 0f);
                    overlayImage.gameObject.SetActive(false);
                    return;
                case WeatherManager.WeatherType.Rainy:
                    c = new Color(rainyTint.r, rainyTint.g, rainyTint.b, rainyStrength);
                    break;
                case WeatherManager.WeatherType.Storm:
                    c = new Color(stormTint.r, stormTint.g, stormTint.b, stormStrength);
                    break;
                default:
                    c = new Color(1f, 1f, 1f, 0f);
                    overlayImage.gameObject.SetActive(false);
                    return;
            }

            overlayImage.gameObject.SetActive(true);
            overlayImage.color = c;
        }
    }
}
