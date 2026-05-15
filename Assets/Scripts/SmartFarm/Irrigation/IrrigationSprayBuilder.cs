using UnityEngine;

namespace SmartFarm.Irrigation
{
    /// <summary>
    /// Builds a realistic 3-layer water-spray effect on any GameObject:
    ///
    /// <list type="number">
    /// <item><b>Stream</b> — stretched-billboard droplets that arc outward
    /// under gravity, like jets from a real sprinkler nozzle.</item>
    /// <item><b>Mist</b> — small, soft, fast-fading particles right at the
    /// nozzle so the spray has a "wet hiss" close to the head.</item>
    /// <item><b>Splash</b> — bursts spawned via a Death sub-emitter every time
    /// a droplet's lifetime ends (i.e. when it hits the ground). Gives the
    /// real-water "kicked-up droplets" look without per-frame collisions.</item>
    /// </list>
    ///
    /// Quest VR friendly — all three systems share the same URP particle
    /// material from <see cref="IrrigationSprayMaterial"/>, no allocations at
    /// runtime, and emission is gated by <see cref="IrrigationSprayLayer"/>.
    /// </summary>
    public static class IrrigationSprayBuilder
    {
        // Tuned defaults so the spray reads as "real water" from a few metres away.
        // Sized large + dense + bright so droplets are clearly visible at Quest
        // resolution. Billboard mode for maximum URP/VR compatibility.
        private const float StreamLifetime    = 1.10f;
        private const float StreamSpeedMin    = 3.5f;
        private const float StreamSpeedMax    = 5.5f;
        private const float StreamSizeMin     = 0.10f;
        private const float StreamSizeMax     = 0.20f;
        private const float StreamRate        = 380f;
        private const float StreamConeAngle   = 28f;

        private const float MistLifetime      = 0.55f;
        private const float MistSpeedMin      = 1.4f;
        private const float MistSpeedMax      = 2.4f;
        private const float MistSizeMin       = 0.12f;
        private const float MistSizeMax       = 0.22f;
        private const float MistRate          = 140f;
        private const float MistConeAngle     = 38f;

        private const int   SplashBurstMin    = 2;
        private const int   SplashBurstMax    = 4;
        private const float SplashLifetime    = 0.50f;
        private const float SplashSpeedMin    = 0.6f;
        private const float SplashSpeedMax    = 1.4f;
        private const float SplashSizeMin     = 0.05f;
        private const float SplashSizeMax     = 0.10f;
        private const float SplashConeAngle   = 75f;

        private static readonly Color WaterBright = new Color(0.75f, 0.92f, 1.00f, 0.95f);
        private static readonly Color WaterDeep   = new Color(0.30f, 0.65f, 0.95f, 0.95f);

        // ─────────────────────────────────────────────────────────────────────
        //  Public entry
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Clears any existing children on <paramref name="nozzle"/> and rebuilds
        /// it with the three-layer realistic spray. Returns the root nozzle
        /// Transform so callers can stash it on the irrigation zone.
        /// </summary>
        public static Transform Build(Transform nozzle)
        {
            if (nozzle == null) return null;

            // Wipe any prior layers
            for (int i = nozzle.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(nozzle.GetChild(i).gameObject);

            // 1) Splash first (so the stream can reference it as sub-emitter).
            var splash = BuildSplashLayer(nozzle);

            // 2) Main stream (arcs droplets), referencing splash for death sub-emitter.
            BuildStreamLayer(nozzle, splash);

            // 3) Mist (close-to-nozzle hiss).
            BuildMistLayer(nozzle);

            return nozzle;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Layer builders
        // ─────────────────────────────────────────────────────────────────────

        private static ParticleSystem BuildStreamLayer(Transform parent, ParticleSystem splashSub)
        {
            var go = new GameObject("Spray_Stream");
            go.transform.SetParent(parent, false);

            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration         = 5f;
            main.loop             = true;
            main.startLifetime    = StreamLifetime;
            main.startSpeed       = new ParticleSystem.MinMaxCurve(StreamSpeedMin, StreamSpeedMax);
            main.startSize        = new ParticleSystem.MinMaxCurve(StreamSizeMin, StreamSizeMax);
            main.startColor       = BuildWaterGradient(WaterBright, WaterDeep);
            main.gravityModifier  = 2.4f;
            main.maxParticles     = 1500;
            main.simulationSpace  = ParticleSystemSimulationSpace.World;
            main.playOnAwake      = false;
            main.scalingMode      = ParticleSystemScalingMode.Local;

            var emission = ps.emission;
            emission.enabled      = false;
            emission.rateOverTime = StreamRate;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle     = StreamConeAngle;
            shape.radius    = 0.06f;
            shape.radiusThickness = 0.7f;
            shape.rotation  = new Vector3(180f, 0f, 0f); // emit downward

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(WaterBright, 0f),
                    new GradientColorKey(WaterDeep,   1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var sizeCurve = new AnimationCurve(
                new Keyframe(0f, 1.05f, 0f, 0f),
                new Keyframe(0.55f, 1f, 0f, 0f),
                new Keyframe(1f, 0.55f, 0f, 0f));
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // Death sub-emitter — splash on impact
            if (splashSub != null)
            {
                var sub = ps.subEmitters;
                sub.enabled = true;
                sub.AddSubEmitter(splashSub, ParticleSystemSubEmitterType.Death,
                    ParticleSystemSubEmitterProperties.InheritColor);
            }

            // Billboard works reliably in URP / Quest VR. We pad sizes a bit so
            // droplets read as water from gameplay distance.
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode      = ParticleSystemRenderMode.Billboard;
                renderer.alignment       = ParticleSystemRenderSpace.View;
                renderer.minParticleSize = 0f;
                renderer.maxParticleSize = 2f;
                renderer.sortingFudge    = 0f;
                IrrigationSprayMaterial.ApplyTo(ps);
                if (renderer.sharedMaterial != null)
                    renderer.material    = renderer.sharedMaterial;
            }

            // Mark so IrrigationVisualFeedback drives this rate.
            var layer = go.AddComponent<IrrigationSprayLayer>();
            layer.baseRatePerSecond = StreamRate;
            layer.driveEmissionRate = true;
            layer.minRateWhenActive = 180f;
            layer.kind = IrrigationSprayLayer.SprayLayerKind.Stream;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }

        private static ParticleSystem BuildMistLayer(Transform parent)
        {
            var go = new GameObject("Spray_Mist");
            go.transform.SetParent(parent, false);

            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration         = 5f;
            main.loop             = true;
            main.startLifetime    = MistLifetime;
            main.startSpeed       = new ParticleSystem.MinMaxCurve(MistSpeedMin, MistSpeedMax);
            main.startSize        = new ParticleSystem.MinMaxCurve(MistSizeMin, MistSizeMax);
            main.startColor       = BuildWaterGradient(new Color(0.85f, 0.95f, 1f, 0.70f),
                                                       new Color(0.55f, 0.80f, 1f, 0.55f));
            main.gravityModifier  = 0.6f;
            main.maxParticles     = 600;
            main.simulationSpace  = ParticleSystemSimulationSpace.World;
            main.playOnAwake      = false;

            var emission = ps.emission;
            emission.enabled      = false;
            emission.rateOverTime = MistRate;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle     = MistConeAngle;
            shape.radius    = 0.04f;
            shape.rotation  = new Vector3(180f, 0f, 0f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.85f, 0.95f, 1f), 0f),
                    new GradientColorKey(new Color(0.55f, 0.80f, 1f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.85f, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0.7f),
                new Keyframe(1f, 1.6f));
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode      = ParticleSystemRenderMode.Billboard;
                renderer.alignment       = ParticleSystemRenderSpace.View;
                renderer.minParticleSize = 0f;
                renderer.maxParticleSize = 2f;
                IrrigationSprayMaterial.ApplyTo(ps);
                renderer.material        = renderer.sharedMaterial;
            }

            var layer = go.AddComponent<IrrigationSprayLayer>();
            layer.baseRatePerSecond = MistRate;
            layer.driveEmissionRate = true;
            layer.minRateWhenActive = 60f;
            layer.kind = IrrigationSprayLayer.SprayLayerKind.Mist;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }

        private static ParticleSystem BuildSplashLayer(Transform parent)
        {
            var go = new GameObject("Spray_Splash");
            go.transform.SetParent(parent, false);

            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration         = 5f;
            main.loop             = true; // keep "alive" so sub-emitter events fire reliably
            main.startLifetime    = SplashLifetime;
            main.startSpeed       = new ParticleSystem.MinMaxCurve(SplashSpeedMin, SplashSpeedMax);
            main.startSize        = new ParticleSystem.MinMaxCurve(SplashSizeMin, SplashSizeMax);
            main.startColor       = BuildWaterGradient(WaterBright, WaterDeep);
            main.gravityModifier  = 1.4f;
            main.maxParticles     = 800;
            main.simulationSpace  = ParticleSystemSimulationSpace.World;
            main.playOnAwake      = false;

            // Bursts only — sub-emitter triggers Birth via parent particle death.
            var emission = ps.emission;
            emission.enabled      = false;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)SplashBurstMin, (short)SplashBurstMax, 1, 0.01f)
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle     = SplashConeAngle;
            shape.radius    = 0.02f;
            // Splash should burst upward+outward from the impact point
            shape.rotation  = new Vector3(0f, 0f, 0f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(WaterBright, 0f),
                    new GradientColorKey(WaterDeep,   1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var sizeCurve = new AnimationCurve(
                new Keyframe(0f, 1.1f),
                new Keyframe(1f, 0.2f));
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode      = ParticleSystemRenderMode.Billboard;
                renderer.alignment       = ParticleSystemRenderSpace.View;
                renderer.minParticleSize = 0f;
                renderer.maxParticleSize = 0.8f;
                IrrigationSprayMaterial.ApplyTo(ps);
                renderer.material        = renderer.sharedMaterial;
            }

            // Don't drive rate from flow — splash is event-driven only.
            var layer = go.AddComponent<IrrigationSprayLayer>();
            layer.baseRatePerSecond = 0f;
            layer.driveEmissionRate = false;
            layer.minRateWhenActive = 0f;
            layer.kind = IrrigationSprayLayer.SprayLayerKind.Splash;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static ParticleSystem.MinMaxGradient BuildWaterGradient(Color bright, Color deep)
        {
            return new ParticleSystem.MinMaxGradient(bright, deep);
        }
    }
}
