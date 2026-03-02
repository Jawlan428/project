using System.Collections;
using UnityEngine;

namespace SmartFarm
{
    /// <summary>
    /// Brief light intensity spike for storm lightning effect.
    /// Add to the same GameObject as the Directional Light, or assign a separate light.
    /// </summary>
    public class LightningEffect : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Light targetLight;

        [Header("Lightning Settings")]
        [SerializeField] private float flashIntensity = 3f;
        [SerializeField] private float flashDuration = 0.08f;
        [SerializeField] private float minInterval = 2f;
        [SerializeField] private float maxInterval = 6f;

        private float _baseIntensity;
        private Coroutine _flashCoroutine;

        private void Awake()
        {
            if (targetLight == null)
                targetLight = GetComponent<Light>();
            if (targetLight != null)
                _baseIntensity = targetLight.intensity;
        }

        /// <summary>
        /// Enable lightning flashes during storm. Call from WeatherManager when Storm is set.
        /// </summary>
        public void EnableForStorm()
        {
            Disable();
            if (targetLight != null)
            {
                _baseIntensity = targetLight.intensity; // Use current storm intensity as base
                _flashCoroutine = StartCoroutine(FlashLoop());
            }
        }

        /// <summary>
        /// Stop lightning. Call when switching away from Storm.
        /// </summary>
        public void Disable()
        {
            if (_flashCoroutine != null)
            {
                StopCoroutine(_flashCoroutine);
                _flashCoroutine = null;
            }
            if (targetLight != null)
                targetLight.intensity = _baseIntensity;
        }

        private IEnumerator FlashLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(minInterval, maxInterval));
                yield return FlashOnce();
            }
        }

        private IEnumerator FlashOnce()
        {
            if (targetLight == null) yield break;

            float prev = targetLight.intensity;
            targetLight.intensity = flashIntensity;
            yield return new WaitForSeconds(flashDuration);
            targetLight.intensity = prev;
        }
    }
}
