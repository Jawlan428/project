using System.Collections.Generic;
using UnityEngine;

namespace SmartFarm.Irrigation
{
    /// <summary>
    /// Drives the in-world visual + audio feedback for active irrigation:
    ///   • Pipe glow material emission lerp.
    ///   • Sprinkler particle emission ramp.
    ///   • Per-zone water audio source fade.
    ///
    /// Listens to <see cref="IrrigationZoneManager.OnZonesChanged"/> snapshots
    /// and applies effects accordingly. Uses MaterialPropertyBlocks to avoid
    /// instantiating new materials at runtime (Quest-friendly).
    /// </summary>
    [AddComponentMenu("SmartFarm/Irrigation/Irrigation Visual Feedback")]
    public class IrrigationVisualFeedback : MonoBehaviour
    {
        [Header("References (auto-found if empty)")]
        [SerializeField] private IrrigationZoneManager zoneManager;

        [Header("Pipe Glow Colour")]
        [SerializeField] private Color pipeGlowColor = new Color(0.40f, 0.75f, 1.00f, 1f);

        [Header("Sounds")]
        [SerializeField] private AudioClip waterLoopClip;
        [SerializeField, Range(0f, 1f)] private float waterLoopVolume = 0.45f;
        [SerializeField, Range(0.5f, 8f)] private float fadeSpeed = 4f;

        // Per-zone runtime cache so we don't allocate every tick
        private readonly Dictionary<string, ZoneRuntime> _runtime = new Dictionary<string, ZoneRuntime>();

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private class ZoneRuntime
        {
            public Renderer[]            pipeRenderers;
            public ParticleSystem[]      sprinklers;
            public IrrigationSprayLayer[] sprayLayers; // parallel to sprinklers, null entries allowed
            public AudioSource           audioSource;
            public float                 displayedFlow; // 0..1 smoothed
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (zoneManager == null) zoneManager = FindFirstObjectByType<IrrigationZoneManager>();
        }

        private void OnEnable()
        {
            if (zoneManager == null) zoneManager = FindFirstObjectByType<IrrigationZoneManager>();
            zoneManager?.TryBindSceneVisualRoots();
            if (zoneManager != null) zoneManager.OnZonesChanged += HandleZonesChanged;
            CacheZoneRuntime();
        }

        private void OnDisable()
        {
            if (zoneManager != null) zoneManager.OnZonesChanged -= HandleZonesChanged;
        }

        private void Update()
        {
            if (zoneManager == null) return;

            float dt = Time.deltaTime;
            var zones = zoneManager.Zones;
            for (int i = 0; i < zones.Count; i++)
            {
                var z = zones[i];
                if (z == null) continue;
                if (!_runtime.TryGetValue(z.id, out var rt))
                {
                    rt = CreateZoneRuntime(z);
                    _runtime[z.id] = rt;
                }

                float target = z.flowRate;
                rt.displayedFlow = Mathf.MoveTowards(rt.displayedFlow, target, dt * fadeSpeed);
                ApplyVisualState(z, rt, rt.displayedFlow);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Caching / setup
        // ─────────────────────────────────────────────────────────────────────

        private void HandleZonesChanged(IReadOnlyList<IrrigationZoneSnapshot> _)
        {
            // Snapshot list is informational; we rely on per-zone Renderer cache
            // which we (re)build lazily in Update().
        }

        private void CacheZoneRuntime()
        {
            _runtime.Clear();
            if (zoneManager == null) return;
            var zones = zoneManager.Zones;
            for (int i = 0; i < zones.Count; i++)
            {
                var z = zones[i];
                if (z == null) continue;
                _runtime[z.id] = CreateZoneRuntime(z);
            }
        }

        private ZoneRuntime CreateZoneRuntime(IrrigationZone zone)
        {
            var rt = new ZoneRuntime();

            if (zone.pipeRoot != null)
                rt.pipeRenderers = zone.pipeRoot.GetComponentsInChildren<Renderer>(true);

            if (zone.sprinklerRoot != null)
                rt.sprinklers = zone.sprinklerRoot.GetComponentsInChildren<ParticleSystem>(true);

            if (rt.sprinklers != null)
            {
                rt.sprayLayers = new IrrigationSprayLayer[rt.sprinklers.Length];
                for (int s = 0; s < rt.sprinklers.Length; s++)
                {
                    var ps = rt.sprinklers[s];
                    if (ps == null) continue;
                    IrrigationSprayMaterial.ApplyTo(ps);
                    rt.sprayLayers[s] = ps.GetComponent<IrrigationSprayLayer>();
                }
            }

            if (zone.sprinklerRoot != null)
            {
                rt.audioSource = zone.sprinklerRoot.GetComponent<AudioSource>();
                if (rt.audioSource == null)
                {
                    rt.audioSource = zone.sprinklerRoot.gameObject.AddComponent<AudioSource>();
                    rt.audioSource.loop          = true;
                    rt.audioSource.playOnAwake   = false;
                    rt.audioSource.spatialBlend  = 1f;
                    rt.audioSource.volume        = 0f;
                    if (waterLoopClip != null) rt.audioSource.clip = waterLoopClip;
                }
            }

            return rt;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Apply
        // ─────────────────────────────────────────────────────────────────────

        private static MaterialPropertyBlock _block;

        private void ApplyVisualState(IrrigationZone zone, ZoneRuntime rt, float flow)
        {
            if (rt == null) return;

            if (_block == null) _block = new MaterialPropertyBlock();

            if (rt.pipeRenderers != null)
            {
                Color emission = pipeGlowColor * Mathf.LinearToGammaSpace(flow * 1.6f);
                for (int i = 0; i < rt.pipeRenderers.Length; i++)
                {
                    var r = rt.pipeRenderers[i];
                    if (r == null) continue;
                    r.GetPropertyBlock(_block);
                    _block.SetColor(EmissionColorId, emission);
                    r.SetPropertyBlock(_block);
                }
            }

            if (rt.sprinklers != null)
            {
                bool active = flow > 0.01f;
                for (int i = 0; i < rt.sprinklers.Length; i++)
                {
                    var ps = rt.sprinklers[i];
                    if (ps == null) continue;

                    var layer = rt.sprayLayers != null ? rt.sprayLayers[i] : null;

                    // Splash sub-emitters are driven entirely by parent particle
                    // Death events — leave them alone so they don't double-emit.
                    if (layer != null && !layer.driveEmissionRate)
                        continue;

                    var emission = ps.emission;
                    emission.enabled = active;

                    if (active)
                    {
                        float baseRate = layer != null ? layer.baseRatePerSecond : 420f;
                        float minRate  = layer != null ? layer.minRateWhenActive : 120f;
                        var rate = emission.rateOverTime;
                        rate.constant         = Mathf.Max(minRate, baseRate * flow);
                        emission.rateOverTime = rate;

                        if (!ps.isPlaying) ps.Play(true);
                    }
                    else if (ps.isPlaying)
                    {
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }
                }
            }

            if (rt.audioSource != null)
            {
                if (rt.audioSource.clip == null && waterLoopClip != null)
                    rt.audioSource.clip = waterLoopClip;
                if (flow > 0.01f && !rt.audioSource.isPlaying && rt.audioSource.clip != null)
                    rt.audioSource.Play();

                rt.audioSource.volume = waterLoopVolume * flow;
                if (flow <= 0.001f && rt.audioSource.isPlaying)
                    rt.audioSource.Stop();
            }

            // No-op when both pipeRoot and sprinklerRoot are null — keeps the
            // setup script free to wire only the visual you need.
            _ = zone;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Wiring helpers
        // ─────────────────────────────────────────────────────────────────────

        public void SetZoneManager(IrrigationZoneManager mgr)
        {
            zoneManager = mgr;
            CacheZoneRuntime();
        }

        public void RefreshCache() => CacheZoneRuntime();
    }
}
