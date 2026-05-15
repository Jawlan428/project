using UnityEngine;

namespace SmartFarm.Irrigation
{
    /// <summary>
    /// Marker placed on each ParticleSystem inside a multi-layer sprinkler
    /// (droplets / mist / splash) so <see cref="IrrigationVisualFeedback"/>
    /// can drive every layer with its own base rate, instead of stomping the
    /// same number onto every system.
    ///
    /// <list type="bullet">
    /// <item><b>baseRatePerSecond</b> — the emission rate that maps to flow = 1.0.
    /// The visual feedback multiplies this by the zone's smoothed flow.</item>
    /// <item><b>driveEmissionRate</b> — when false, only <c>emission.enabled</c>
    /// is toggled (used for splash sub-emitters that emit via parent events).</item>
    /// <item><b>minRateWhenActive</b> — clamp so droplets stay visible at low flow.</item>
    /// </list>
    /// </summary>
    [AddComponentMenu("SmartFarm/Irrigation/Irrigation Spray Layer")]
    [RequireComponent(typeof(ParticleSystem))]
    [DisallowMultipleComponent]
    public class IrrigationSprayLayer : MonoBehaviour
    {
        [Tooltip("Emission rate (particles/sec) when zone flow is 1.0.")]
        public float baseRatePerSecond = 400f;

        [Tooltip("If false the visual feedback only toggles emission.enabled on this system " +
                 "(used for sub-emitters that get triggered by parent particle events).")]
        public bool driveEmissionRate = true;

        [Tooltip("Minimum emission rate while active so the spray stays visible at low flow.")]
        public float minRateWhenActive = 80f;

        [Tooltip("Friendly tag so editor menus and debug logs can identify the layer purpose.")]
        public SprayLayerKind kind = SprayLayerKind.Stream;

        public enum SprayLayerKind
        {
            Stream  = 0,
            Mist    = 1,
            Splash  = 2,
        }
    }
}
