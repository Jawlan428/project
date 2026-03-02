using UnityEngine;

namespace SmartFarm
{
    /// <summary>
    /// Sky cloud layer that changes opacity/density with weather.
    /// Renders soft clouds over the skybox for more realistic atmosphere.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class CloudLayer : MonoBehaviour
    {
        [Header("Weather Opacity")]
        [SerializeField] [Range(0f, 1f)] private float sunnyOpacity = 0.12f;  // Few light clouds
        [SerializeField] [Range(0f, 1f)] private float rainyOpacity = 0.88f;  // Heavy gray clouds
        [SerializeField] [Range(0f, 1f)] private float stormOpacity = 0.98f;

        private Material _material;
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            var r = GetComponent<MeshRenderer>();
            if (r != null && r.sharedMaterial != null)
                _material = r.material;
        }

        private void OnDestroy()
        {
            if (_material != null)
                Destroy(_material);
        }

        /// <summary>
        /// Set cloud appearance for weather type. Call from WeatherManager.
        /// </summary>
        public void SetWeather(WeatherManager.WeatherType type)
        {
            if (_material == null) return;

            float opacity = type switch
            {
                WeatherManager.WeatherType.Sunny => sunnyOpacity,
                WeatherManager.WeatherType.Rainy => rainyOpacity,
                WeatherManager.WeatherType.Storm => stormOpacity,
                _ => sunnyOpacity
            };

            Color cloudTint = type switch
            {
                WeatherManager.WeatherType.Sunny => Color.white,
                WeatherManager.WeatherType.Rainy => new Color(0.7f, 0.72f, 0.78f),  // Dark gray clouds
                WeatherManager.WeatherType.Storm => new Color(0.45f, 0.47f, 0.52f),
                _ => Color.white
            };

            if (_material.HasProperty(OpacityId))
                _material.SetFloat(OpacityId, opacity);
            if (_material.HasProperty(ColorId))
                _material.SetColor(ColorId, new Color(cloudTint.r, cloudTint.g, cloudTint.b, opacity));
            if (_material.HasProperty("_TintColor"))
                _material.SetColor("_TintColor", new Color(cloudTint.r, cloudTint.g, cloudTint.b, opacity));
            if (_material.HasProperty("_BaseColor"))
                _material.SetColor("_BaseColor", new Color(cloudTint.r, cloudTint.g, cloudTint.b, opacity));
        }
    }
}
