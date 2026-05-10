using UnityEngine;

namespace SmartFarm.DayNight
{
    /// <summary>
    /// Atmospheric "mood" effects that complement the lighting:
    ///   • Moonlight (a separate dim blue light that fades in at night)
    ///   • Fireflies particle system
    ///   • Crickets / wind ambient audio
    ///   • Day ambient audio (birds, wind etc.)
    ///
    /// All references are optional — the controller silently skips anything
    /// that isn't wired up.
    /// </summary>
    [AddComponentMenu("SmartFarm/Day Night/Atmosphere Controller")]
    public class AtmosphereController : MonoBehaviour
    {
        [Header("Manager")]
        [SerializeField] private DayNightModeManager manager;

        [Header("Moonlight")]
        [SerializeField] private Light moonLight;
        [SerializeField, Range(0f, 2f)] private float moonNightIntensity = 0.35f;
        [SerializeField] private Color moonColor = new Color(0.55f, 0.70f, 1.00f);

        [Header("Fireflies")]
        [SerializeField] private ParticleSystem firefliesParticles;
        [Tooltip("Emission rate at full night.")]
        [SerializeField, Min(0f)] private float firefliesNightRate = 12f;
        [Tooltip("Begin fading the particle system in only after the night weight crosses this.")]
        [SerializeField, Range(0f, 1f)] private float firefliesOnsetThreshold = 0.35f;

        [Header("Audio (Day)")]
        [SerializeField] private AudioSource dayAmbientSource;
        [SerializeField, Range(0f, 1f)] private float dayAmbientMaxVolume = 0.6f;

        [Header("Audio (Night)")]
        [Tooltip("Crickets / nocturnal ambience.")]
        [SerializeField] private AudioSource cricketsSource;
        [SerializeField, Range(0f, 1f)] private float cricketsMaxVolume = 0.55f;

        [SerializeField] private AudioSource windSource;
        [SerializeField, Range(0f, 1f)] private float windNightMaxVolume = 0.4f;
        [SerializeField, Range(0f, 1f)] private float windDayMaxVolume   = 0.15f;

        [Header("Onset")]
        [Tooltip("Curve mapping the manager's nightWeight (0..1) to the night-side audio/particle weight (0..1).")]
        [SerializeField] private AnimationCurve nightCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private void Awake()
        {
            if (manager == null) manager = DayNightModeManager.Instance ?? FindFirstObjectByType<DayNightModeManager>();

            if (moonLight != null)
            {
                moonLight.color = moonColor;
                moonLight.intensity = 0f;
                moonLight.enabled = false;
            }

            if (firefliesParticles != null)
            {
                var emission = firefliesParticles.emission;
                emission.rateOverTime = 0f;
                if (firefliesParticles.isPlaying) firefliesParticles.Stop();
            }

            PrepareAudio(dayAmbientSource);
            PrepareAudio(cricketsSource);
            PrepareAudio(windSource);
        }

        private static void PrepareAudio(AudioSource src)
        {
            if (src == null) return;
            src.loop = true;
            src.spatialBlend = src.spatialBlend; // honour current authoring
            src.volume = 0f;
            // Don't auto-play — we'll enable when the corresponding mode kicks in.
            src.playOnAwake = false;
        }

        private void OnEnable()
        {
            if (manager != null)
            {
                manager.OnNightWeightChanged += HandleWeight;
                HandleWeight(manager.NightWeight);
            }
        }

        private void OnDisable()
        {
            if (manager != null)
                manager.OnNightWeightChanged -= HandleWeight;
        }

        private void HandleWeight(float nightWeight)
        {
            float night = Mathf.Clamp01(nightCurve.Evaluate(Mathf.Clamp01(nightWeight)));
            float day   = 1f - night;

            // Moonlight
            if (moonLight != null)
            {
                moonLight.intensity = moonNightIntensity * night;
                bool enable = night > 0.02f;
                if (moonLight.enabled != enable) moonLight.enabled = enable;
            }

            // Fireflies
            if (firefliesParticles != null)
            {
                float fireflyW = Mathf.InverseLerp(firefliesOnsetThreshold, 1f, night);
                fireflyW = Mathf.Clamp01(fireflyW);
                var emission = firefliesParticles.emission;
                emission.rateOverTime = firefliesNightRate * fireflyW;

                if (fireflyW > 0.02f && !firefliesParticles.isPlaying) firefliesParticles.Play();
                else if (fireflyW <= 0.02f && firefliesParticles.isPlaying) firefliesParticles.Stop();
            }

            // Audio
            ApplyAudio(dayAmbientSource, dayAmbientMaxVolume * day);
            ApplyAudio(cricketsSource,   cricketsMaxVolume   * night);
            ApplyAudio(windSource,       Mathf.Lerp(windDayMaxVolume, windNightMaxVolume, night));
        }

        private static void ApplyAudio(AudioSource src, float volume)
        {
            if (src == null) return;
            src.volume = Mathf.Clamp01(volume);
            bool shouldPlay = src.volume > 0.005f;
            if (shouldPlay && !src.isPlaying && src.enabled) src.Play();
            else if (!shouldPlay && src.isPlaying) src.Pause();
        }
    }
}
