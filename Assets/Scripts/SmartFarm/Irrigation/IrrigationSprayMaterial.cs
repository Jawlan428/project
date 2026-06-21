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

        /// <summary>
        /// Ensures the particle system's renderer uses a render-pipeline-safe
        /// material. Does NOT change the renderer's <see cref="ParticleSystemRenderMode"/>
        /// — callers are free to use Billboard, Stretch, Mesh, etc.
        /// </summary>
        public static void ApplyTo(ParticleSystem ps)
        {
            if (ps == null) return;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            if (r == null) return;

            if (!NeedsReplace(r.sharedMaterial))
                return;

            r.sharedMaterial = GetOrCreate();
            // Bump sorting order so the spray reliably draws on top of the
            // terrain/grass when alpha-blended.
            if (r.sortingOrder < 100) r.sortingOrder = 100;
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
                bool isUrpParticle =
                    n.StartsWith("Universal Render Pipeline/Particles/", System.StringComparison.Ordinal);
                bool isHdrpParticle =
                    n.StartsWith("HDRP/", System.StringComparison.Ordinal) && n.Contains("Particle");

                if (isUrpParticle || isHdrpParticle)
                {
                    // Materials that say they're URP particles but were
                    // serialised with the default Opaque surface still render
                    // invisible — replace them with our properly-configured
                    // transparent material.
                    bool isTransparent = !m.HasProperty("_Surface")
                                         || Mathf.Approximately(m.GetFloat("_Surface"), 1f);
                    bool hasTransparentQueue = m.renderQueue >= 3000;
                    if (isTransparent && hasTransparentQueue) return false;
                    return true;
                }
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
            // Whiter + more translucent than before so the spray reads as real
            // water rather than solid blue paint.
            ConfigureTransparent(_cached, new Color(0.78f, 0.90f, 1f, 0.65f));
            return _cached;
        }

        /// <summary>
        /// Forces the supplied material into a Transparent / Alpha-Blended
        /// surface so alpha-fade water particles actually render. URP shaders
        /// default to <i>Opaque</i> at runtime, so a freshly-created material
        /// would show particles as solid squares (or invisible squares against
        /// matching skies). This sets the URP "Surface = Transparent" props
        /// as well as the legacy built-in equivalents so both pipelines work.
        /// </summary>
        private static void ConfigureTransparent(Material m, Color color)
        {
            if (m == null) return;

            // ── URP-style Surface settings ─────────────────────────────────
            if (m.HasProperty("_Surface"))   m.SetFloat("_Surface",   1f); // 0 = Opaque, 1 = Transparent
            if (m.HasProperty("_Blend"))     m.SetFloat("_Blend",     0f); // 0 = Alpha
            if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 0f);
            if (m.HasProperty("_ZWrite"))    m.SetFloat("_ZWrite",    0f);
            if (m.HasProperty("_Cull"))      m.SetFloat("_Cull",      (float)UnityEngine.Rendering.CullMode.Off);

            // Source/destination blend factors for SrcAlpha + OneMinusSrcAlpha
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

            // ── Required URP keywords ──────────────────────────────────────
            m.DisableKeyword("_SURFACE_TYPE_OPAQUE");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");

            // ── Colour ─────────────────────────────────────────────────────
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color"))     m.SetColor("_Color",     color);
            m.color = color;

            // ── Soft round droplet texture ─────────────────────────────────
            // Without a texture URP/built-in particle shaders render solid
            // squares, which is why the spray looked like blocky blue blobs.
            // A soft radial-alpha sprite turns every particle into a rounded,
            // feathered water droplet.
            var droplet = GetDropletTexture();
            if (droplet != null)
            {
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", droplet);
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", droplet);
            }

            // Transparent render queue
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent; // 3000
        }

        // ── Soft droplet texture (generated once, cached) ──────────────────────
        private static Texture2D _droplet;

        private static Texture2D GetDropletTexture()
        {
            if (_droplet != null) return _droplet;

            const int size = 128;
            _droplet = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "SmartFarm_WaterDroplet",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            float r = size * 0.5f;
            var center = new Vector2(r, r);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / r; // 0..~1
                    // Smooth feathered falloff with a slightly brighter core so it
                    // looks like a lit water droplet.
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a;                              // soft edge
                    float core = Mathf.Clamp01(1f - d * 1.8f);
                    float lum = 0.85f + 0.15f * core;       // bright centre
                    _droplet.SetPixel(x, y, new Color(lum, lum, lum, a));
                }
            }
            _droplet.Apply(true);
            return _droplet;
        }
    }
}
