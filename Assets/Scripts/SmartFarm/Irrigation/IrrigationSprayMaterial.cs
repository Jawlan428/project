using UnityEngine;

namespace SmartFarm.Irrigation
{
    /// <summary>
    /// Assigns a render-pipeline-safe material to sprinkler particle systems.
    /// Built-in Default-Particle materials are invisible under URP if picked first.
    /// Uses shader availability instead of GraphicsSettings API (varies by Unity version).
    /// </summary>
    public static class IrrigationSprayMaterial
    {
        private static Material _cached;
        private static bool? _urpParticlesAvailable;

        public static void ApplyTo(ParticleSystem ps)
        {
            if (ps == null) return;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            if (r == null) return;

            if (!NeedsReplace(r.sharedMaterial))
                return;

            r.sharedMaterial = GetOrCreate();
            r.renderMode     = ParticleSystemRenderMode.Billboard;
            r.sortingOrder     = 100;
        }

        private static bool UrpParticleShadersAvailable()
        {
            if (_urpParticlesAvailable.HasValue)
                return _urpParticlesAvailable.Value;

            _urpParticlesAvailable =
                Shader.Find("Universal Render Pipeline/Particles/Unlit") != null
                || Shader.Find("Universal Render Pipeline/Particles/Simple Lit") != null;
            return _urpParticlesAvailable.Value;
        }

        private static bool NeedsReplace(Material m)
        {
            if (m == null || m.shader == null) return true;

            string n = m.shader.name;

            if (UrpParticleShadersAvailable())
            {
                if (n.StartsWith("Universal Render Pipeline/Particles/", System.StringComparison.Ordinal))
                    return false;
                if (n.StartsWith("HDRP/", System.StringComparison.Ordinal) && n.Contains("Particle"))
                    return false;
                return true;
            }

            return !m.shader.isSupported;
        }

        private static Material GetOrCreate()
        {
            if (_cached != null) return _cached;

            Shader sh = null;
            if (UrpParticleShadersAvailable())
            {
                sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                  ?? Shader.Find("Universal Render Pipeline/Particles/Simple Lit");
            }

            sh ??= Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Particles/Alpha Blended")
                ?? Shader.Find("Sprites/Default");

            if (sh == null) return null;

            _cached = new Material(sh) { name = "SmartFarm_WaterSpray_Runtime" };
            if (_cached.HasProperty("_BaseColor"))
                _cached.SetColor("_BaseColor", new Color(0.55f, 0.85f, 1f, 0.85f));
            else
                _cached.color = new Color(0.55f, 0.85f, 1f, 0.85f);

            _cached.renderQueue = 3000;
            return _cached;
        }
    }
}
