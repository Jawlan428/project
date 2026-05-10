using System.Collections.Generic;
using SmartFarm.Irrigation.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SmartFarm.Irrigation.Editor
{
    /// <summary>
    /// One-click editor that builds the full Smart Irrigation Tablet system in the
    /// active scene:
    ///
    ///   1. Adds the SmartIrrigationTabletManager + every subsystem to the
    ///      FarmSimulationHub (or a dedicated SmartIrrigationHub).
    ///   2. Creates default Corn Field + Wheat Field zones.
    ///   3. Spawns world-space irrigation pipes + sprinkler particles per zone.
    ///   4. Spawns a brand-new VR tablet GameObject "SmartIrrigationTablet"
    ///      with the agricultural green/dark theme, 4 pages, animated
    ///      indicators, and full XR Interaction Toolkit raycaster compatibility.
    ///   5. Wires every reference via reflection so the system runs immediately
    ///      after a single click.
    ///
    /// Menu: Tools &gt; Smart Farm &gt; Setup Smart Irrigation Tablet
    /// </summary>
    public static class SmartIrrigationTabletSetupEditor
    {
        // ── Theme palette ────────────────────────────────────────────────────

        private static readonly Color BgDeep        = new Color(0.04f, 0.10f, 0.10f, 0.99f);
        private static readonly Color BgPanel       = new Color(0.06f, 0.14f, 0.16f, 0.97f);
        private static readonly Color BgCard        = new Color(0.09f, 0.18f, 0.20f, 0.96f);
        private static readonly Color BgBarTrack    = new Color(0.10f, 0.22f, 0.24f, 1.00f);
        private static readonly Color HeaderTint    = new Color(0.06f, 0.20f, 0.16f, 1.00f);
        private static readonly Color TabBg         = new Color(0.08f, 0.16f, 0.20f, 1.00f);
        private static readonly Color AccentGreen   = new Color(0.30f, 0.85f, 0.55f, 1.00f);
        private static readonly Color AccentBlue    = new Color(0.40f, 0.75f, 1.00f, 1.00f);
        private static readonly Color TextPrimary   = new Color(0.94f, 1.00f, 0.96f, 1.00f);
        private static readonly Color TextSecondary = new Color(0.65f, 0.85f, 0.78f, 1.00f);
        private static readonly Color BorderColor   = new Color(0.10f, 0.85f, 0.55f, 0.55f);

        // ─────────────────────────────────────────────────────────────────────
        //  Menu items
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("Tools/Smart Farm/Setup Smart Irrigation Tablet", priority = 0)]
        public static void SetupSmartIrrigationTablet()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[SmartIrrigationTablet] Stop Play mode before running setup.");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[SmartIrrigationTablet] Open a scene first.");
                return;
            }

            GameObject hub = null;
            try
            {
                // 1. Create or find the irrigation hub (subsystems live here).
                hub = EnsureHub();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SmartIrrigationTablet] Hub setup failed: {ex.Message}\n{ex.StackTrace}");
            }

            try
            {
                // 2. Build default zones + scene visuals (pipes/sprinklers).
                if (hub != null) BuildSceneVisuals(hub);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SmartIrrigationTablet] Scene visuals setup failed (continuing): {ex.Message}\n{ex.StackTrace}");
            }

            try
            {
                // 3. Build the tablet GameObject with all UI pages.
                if (hub != null) BuildTablet(hub);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SmartIrrigationTablet] Tablet build failed: {ex.Message}\n{ex.StackTrace}");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[SmartIrrigationTablet] Setup complete!\n" +
                      "• Subsystems live on SmartIrrigationHub.\n" +
                      "• Two zones created: Corn Field + Wheat Field.\n" +
                      "• Tablet GameObject \"SmartIrrigationTablet\" added.\n" +
                      "Press Play to test.");
        }

        [MenuItem("Tools/Smart Farm/Rebuild Smart Irrigation Tablet", priority = 1)]
        public static void RebuildSmartIrrigationTablet()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[SmartIrrigationTablet] Stop Play mode first.");
                return;
            }

            var existing = GameObject.Find("SmartIrrigationTablet");
            if (existing != null) Undo.DestroyObjectImmediate(existing);

            var sceneVisuals = GameObject.Find("SmartIrrigationSceneVisuals");
            if (sceneVisuals != null) Undo.DestroyObjectImmediate(sceneVisuals);

            // Re-show any legacy tablet that a previous run hid, so BuildTablet
            // can re-anchor to it cleanly.
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go == null) continue;
                if (go.name == "SmartFarmTablet" && !go.activeSelf
                    && !UnityEditor.EditorUtility.IsPersistent(go)
                    && (go.hideFlags & HideFlags.HideAndDontSave) == 0)
                {
                    go.SetActive(true);
                }
            }

            SetupSmartIrrigationTablet();
        }

        [MenuItem("Tools/Smart Farm/Restore Legacy Smart Farm Tablet", priority = 20)]
        public static void RestoreLegacyTablet()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[SmartIrrigationTablet] Stop Play mode first.");
                return;
            }

            int restored = 0;
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go == null) continue;
                if (go.name == "SmartFarmTablet" && !go.activeSelf
                    && !UnityEditor.EditorUtility.IsPersistent(go)
                    && (go.hideFlags & HideFlags.HideAndDontSave) == 0)
                {
                    go.SetActive(true);
                    restored++;
                }
            }

            var newTablet = GameObject.Find("SmartIrrigationTablet");
            if (newTablet != null) Undo.DestroyObjectImmediate(newTablet);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log(restored > 0
                ? $"[SmartIrrigationTablet] Restored {restored} legacy SmartFarmTablet GameObject(s) and removed the new tablet."
                : "[SmartIrrigationTablet] No hidden legacy SmartFarmTablet found.");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Hub + subsystems
        // ─────────────────────────────────────────────────────────────────────

        private static GameObject EnsureHub()
        {
            // Prefer adding to FarmSimulationHub if it exists
            var hub = GameObject.Find("FarmSimulationHub");
            if (hub == null) hub = GameObject.Find("SmartIrrigationHub");
            if (hub == null)
            {
                hub = new GameObject("SmartIrrigationHub");
                Undo.RegisterCreatedObjectUndo(hub, "SmartIrrigationHub");
            }

            // Ensure every subsystem component exists
            var manager = GetOrAdd<SmartIrrigationTabletManager>(hub);
            var zones   = GetOrAdd<IrrigationZoneManager>(hub);
            var weather = GetOrAdd<WeatherIntegrationSystem>(hub);
            var analytics = GetOrAdd<WaterAnalyticsSystem>(hub);
            var bridge  = GetOrAdd<CropGrowthBridge>(hub);
            var alerts  = GetOrAdd<IrrigationAlertManager>(hub);
            var visuals = GetOrAdd<IrrigationVisualFeedback>(hub);
            var monitorBridge = GetOrAdd<IrrigationCropMonitorBridge>(hub);

            // Wire references via reflection (private fields only)
            SetField(manager, "zoneManager",   zones);
            SetField(manager, "weatherSystem", weather);
            SetField(manager, "analytics",     analytics);
            SetField(manager, "cropBridge",    bridge);
            SetField(manager, "alertManager",  alerts);
            SetField(manager, "visuals",       visuals);

            var farmData = Object.FindFirstObjectByType<FarmDataManager>();
            if (farmData != null) SetField(manager, "farmDataManager", farmData);

            // Auto-find external systems
            var weatherMgr   = Object.FindFirstObjectByType<WeatherManager>();
            var growthMgr    = GrowthManager.Instance ?? Object.FindFirstObjectByType<GrowthManager>();
            SetField(weather, "weatherManager", weatherMgr);
            SetField(weather, "zoneManager",    zones);
            SetField(zones,   "growthManager",  growthMgr);
            SetField(zones,   "analytics",      analytics);
            SetField(bridge,  "zoneManager",    zones);
            SetField(bridge,  "growthManager",  growthMgr);
            SetField(alerts,  "zoneManager",    zones);
            SetField(alerts,  "weatherSystem",  weather);
            SetField(analytics, "zoneManager",  zones);
            SetField(visuals, "zoneManager",    zones);

            // Connect the bridge to the existing Crop Growth Monitor popup (if present)
            var cropMonitorPopup = Object.FindFirstObjectByType<CropMonitorAlertPopupUI>();
            SetField(monitorBridge, "alertManager",     alerts);
            SetField(monitorBridge, "cropMonitorPopup", cropMonitorPopup);

            // Default zones: Corn + Wheat. Only seed when the manager has no zones yet.
            var existingZones = (List<IrrigationZone>)GetFieldValue(zones, "zones");
            if (existingZones == null || existingZones.Count == 0)
            {
                var newZones = new List<IrrigationZone>
                {
                    new IrrigationZone
                    {
                        id = "zone_corn",
                        displayName = "Corn Field",
                        cropType = CropType.Corn,
                        waterPerTick = 6f
                    },
                    new IrrigationZone
                    {
                        id = "zone_wheat",
                        displayName = "Wheat Field",
                        cropType = CropType.Wheat,
                        waterPerTick = 5f
                    }
                };
                SetField(zones, "zones", newZones);
            }

            return hub;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Scene visuals (pipes, sprinklers)
        // ─────────────────────────────────────────────────────────────────────

        private static void BuildSceneVisuals(GameObject hub)
        {
            var root = GameObject.Find("SmartIrrigationSceneVisuals");
            if (root == null)
            {
                root = new GameObject("SmartIrrigationSceneVisuals");
                Undo.RegisterCreatedObjectUndo(root, "Smart Irrigation Visuals");
            }

            var zoneMgr = hub.GetComponent<IrrigationZoneManager>();
            if (zoneMgr == null) return;

            var zones = (List<IrrigationZone>)GetFieldValue(zoneMgr, "zones");
            if (zones == null) return;

            // 1) Find candidate field planes (named "Plane", "Plane (1)" etc.)
            var fieldPlanes = FindFieldPlanes();

            // 2) Find all crops, grouped by type, so we can also use crop positions
            //    when a plane isn't present.
            var allCrops = Object.FindObjectsByType<CropGrowthController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < zones.Count; i++)
            {
                var zone = zones[i];
                if (zone == null) continue;

                // Pick the best field surface for this zone.
                Transform planeTarget = PickPlaneForZone(fieldPlanes, allCrops, zone, i);
                Bounds fieldBounds;
                Vector3 fieldUp = Vector3.up;

                if (planeTarget != null && planeTarget.TryGetComponent<Renderer>(out var planeRenderer))
                {
                    fieldBounds = planeRenderer.bounds;
                    fieldUp     = planeTarget.up;
                    // Mark the plane as "claimed" so the next zone picks a different one.
                    fieldPlanes.Remove(planeTarget);
                }
                else if (TryComputeCropBounds(allCrops, zone.cropType, out var cropB))
                {
                    fieldBounds = cropB;
                }
                else
                {
                    // Last-resort default — a 4×4 patch in front of the origin.
                    Vector3 fallback = i == 0
                        ? new Vector3(0f, 0f, 4f)
                        : new Vector3(4f, 0f, 4f);
                    fieldBounds = new Bounds(fallback, new Vector3(4f, 0.1f, 4f));
                }

                var zoneRoot = FindChild(root.transform, zone.id);
                if (zoneRoot == null)
                {
                    var go = new GameObject(zone.id);
                    go.transform.SetParent(root.transform, true);
                    zoneRoot = go.transform;
                }

                zoneRoot.position = new Vector3(fieldBounds.center.x, fieldBounds.max.y, fieldBounds.center.z);
                zoneRoot.rotation = Quaternion.LookRotation(Vector3.forward, fieldUp);

                for (int c = zoneRoot.childCount - 1; c >= 0; c--)
                    Undo.DestroyObjectImmediate(zoneRoot.GetChild(c).gameObject);

                var pipeRoot      = CreatePipeForField(zoneRoot, zone, fieldBounds);
                var sprinklerRoot = CreateSprinklersForField(zoneRoot, zone, fieldBounds);
                CreateZoneSignForField(zoneRoot, zone, fieldBounds);

                zone.pipeRoot      = pipeRoot;
                zone.sprinklerRoot = sprinklerRoot;
            }

            var visuals = hub.GetComponent<IrrigationVisualFeedback>();
            if (visuals != null) visuals.RefreshCache();
        }

        /// <summary>
        /// Returns every flat surface in the scene that looks like a "field plane":
        /// any GameObject whose name starts with "Plane" and that has a Renderer.
        /// Sorted left-to-right (smallest X first) so zone 0 picks the leftmost.
        /// </summary>
        private static List<Transform> FindFieldPlanes()
        {
            var list = new List<Transform>();
            var allRenderers = Object.FindObjectsByType<MeshRenderer>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < allRenderers.Length; i++)
            {
                var t = allRenderers[i].transform;
                if (t == null) continue;
                string n = t.name;
                if (string.IsNullOrEmpty(n)) continue;
                // Heuristic: anything that begins with "Plane", "Field", or "GroundField"
                if (n.StartsWith("Plane", System.StringComparison.OrdinalIgnoreCase) ||
                    n.StartsWith("Field", System.StringComparison.OrdinalIgnoreCase) ||
                    n.StartsWith("GroundField", System.StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(t);
                }
            }
            list.Sort((a, b) => a.position.x.CompareTo(b.position.x));
            return list;
        }

        /// <summary>
        /// Pick the field plane whose centre is closest to the average crop
        /// position of this zone, so Wheat plants align with their plane and
        /// Corn plants align with theirs. Falls back to deterministic order.
        /// </summary>
        private static Transform PickPlaneForZone(List<Transform> planes,
            CropGrowthController[] allCrops, IrrigationZone zone, int zoneIndex)
        {
            if (planes == null || planes.Count == 0) return null;

            if (TryComputeCropBounds(allCrops, zone.cropType, out var cropB))
            {
                Transform best = null;
                float bestDist = float.MaxValue;
                for (int i = 0; i < planes.Count; i++)
                {
                    var t = planes[i];
                    if (t == null) continue;
                    float d = Vector3.Distance(t.position, cropB.center);
                    if (d < bestDist) { bestDist = d; best = t; }
                }
                if (best != null) return best;
            }

            // No crops of this type yet — just hand out planes in order.
            return planes[Mathf.Min(zoneIndex, planes.Count - 1)];
        }

        private static bool TryComputeCropBounds(CropGrowthController[] all, CropType type, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            for (int i = 0; i < all.Length; i++)
            {
                var c = all[i];
                if (c == null || c.Data == null) continue;
                if (c.Data.cropType != type) continue;
                Vector3 p = c.transform.position;
                if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                else      { bounds.Encapsulate(p); }
            }
            return any;
        }

        // ── Material helpers ─────────────────────────────────────────────────

        private static Material BuildPipeMaterial(string id)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) return null;

            var mat = new Material(shader) { name = $"PipeMat_{id}" };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.78f, 0.84f, 0.92f, 1f));
            else mat.color = new Color(0.78f, 0.84f, 0.92f, 1f);
            if (mat.HasProperty("_Metallic"))   mat.SetFloat("_Metallic", 0.85f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.7f);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.black);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            return mat;
        }

        private static void AssignPipeMaterial(GameObject go, Material mat)
        {
            if (mat == null) return;
            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;
        }

        private static void StripCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
        }

        // ── Pipe geometry per field ──────────────────────────────────────────

        /// <summary>
        /// Builds one center-pivot-style irrigation rig for the given field plane.
        /// The main manifold is sized to match the longer of the field's X/Z axes,
        /// hovers ~1m above the surface, and sprouts evenly-spaced spray heads
        /// pointing downward.
        /// </summary>
        private static Transform CreatePipeForField(Transform zoneRoot, IrrigationZone zone, Bounds fieldBounds)
        {
            var pipes = new GameObject("Pipes");
            pipes.transform.SetParent(zoneRoot, false);
            pipes.transform.localPosition = Vector3.zero;

            var pipeMat = BuildPipeMaterial(zone.id);

            // Match the field's actual dimensions. We run the manifold along the
            // longer axis so the rig fits any aspect ratio (square or rectangular).
            bool runAlongX = fieldBounds.size.x >= fieldBounds.size.z;
            float fieldLength = Mathf.Max(1.0f, runAlongX ? fieldBounds.size.x : fieldBounds.size.z);
            float fieldWidth  = Mathf.Max(0.5f, runAlongX ? fieldBounds.size.z : fieldBounds.size.x);
            float halfLength  = fieldLength * 0.5f - 0.15f; // small inset so it sits on the plane
            float halfLength2 = Mathf.Max(0.5f, halfLength);

            // Rotate the local frame so X always means "along manifold".
            float manifoldYaw = runAlongX ? 0f : 90f;
            pipes.transform.localRotation = Quaternion.Euler(0f, manifoldYaw, 0f);

            float pipeY = 0.9f; // 90 cm above the plane top — lower so it sits ON the field
            float radiusScale = Mathf.Clamp(fieldLength * 0.025f, 0.06f, 0.12f);

            // ─── Main horizontal manifold along the local X axis ───
            var main = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            main.name = "Manifold";
            main.transform.SetParent(pipes.transform, false);
            main.transform.localScale    = new Vector3(radiusScale, halfLength2, radiusScale);
            main.transform.localPosition = new Vector3(0f, pipeY, 0f);
            main.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            AssignPipeMaterial(main, pipeMat);
            StripCollider(main);

            // ─── End caps ───
            float capDia = radiusScale * 1.6f;
            CreateSphere(pipes.transform, "CapA", new Vector3(-halfLength2, pipeY, 0f), capDia, pipeMat);
            CreateSphere(pipes.transform, "CapB", new Vector3( halfLength2, pipeY, 0f), capDia, pipeMat);

            // ─── Vertical risers at each end down to ground ───
            CreateVerticalPipe(pipes.transform, "RiserA",
                new Vector3(-halfLength2, pipeY * 0.5f, 0f), pipeY, radiusScale, pipeMat);
            CreateVerticalPipe(pipes.transform, "RiserB",
                new Vector3( halfLength2, pipeY * 0.5f, 0f), pipeY, radiusScale, pipeMat);

            // ─── Spray heads — count scales with field length so big fields get more ───
            int headCount = Mathf.Clamp(Mathf.RoundToInt(fieldLength / 1.2f), 3, 7);
            float stubLen = 0.30f;
            for (int i = 0; i < headCount; i++)
            {
                float t = (i + 0.5f) / headCount;
                float x = Mathf.Lerp(-halfLength2 * 0.92f, halfLength2 * 0.92f, t);
                CreateVerticalPipe(pipes.transform, $"Stub_{i + 1}",
                    new Vector3(x, pipeY - stubLen * 0.5f, 0f), stubLen, radiusScale * 0.55f, pipeMat);
                CreateSphere(pipes.transform, $"SprayHead_{i + 1}",
                    new Vector3(x, pipeY - stubLen, 0f), radiusScale * 1.4f, pipeMat);
            }

            // Stash field metadata so the sprinkler/sign builders can match it.
            var meta = pipes.AddComponent<FieldRigMeta>();
            meta.runAlongX  = runAlongX;
            meta.halfLength = halfLength2;
            meta.fieldWidth = fieldWidth;
            meta.pipeY      = pipeY;
            meta.headCount  = headCount;
            meta.stubLen    = stubLen;

            return pipes.transform;
        }

        /// <summary>Internal marker so the sprinkler builder can reuse pipe layout numbers.</summary>
        public class FieldRigMeta : MonoBehaviour
        {
            public bool  runAlongX;
            public float halfLength;
            public float fieldWidth;
            public float pipeY;
            public int   headCount;
            public float stubLen;
        }

        private static void CreateSphere(Transform parent, string name, Vector3 localPos, float diameter, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localScale    = new Vector3(diameter, diameter, diameter);
            go.transform.localPosition = localPos;
            AssignPipeMaterial(go, mat);
            StripCollider(go);
        }

        /// <summary>Creates a vertical cylinder of the given world height (Y).</summary>
        private static void CreateVerticalPipe(Transform parent, string name, Vector3 localCenter,
            float worldHeight, float radiusScale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            // Cylinder primitive is 2 units tall, so half-height scale = worldHeight / 2.
            go.transform.localScale    = new Vector3(radiusScale, worldHeight * 0.5f, radiusScale);
            go.transform.localPosition = localCenter;
            go.transform.localRotation = Quaternion.identity;
            AssignPipeMaterial(go, mat);
            StripCollider(go);
        }

        // ── Sign per field ───────────────────────────────────────────────────

        private static void CreateZoneSignForField(Transform zoneRoot, IrrigationZone zone, Bounds fieldBounds)
        {
            var sign = new GameObject("ZoneSign");
            sign.transform.SetParent(zoneRoot, false);

            // Reuse the pipe rig metadata so the sign sits next to the manifold's
            // entry side (the "A" riser), facing inward across the field.
            var pipes = zoneRoot.Find("Pipes");
            var meta  = pipes != null ? pipes.GetComponent<FieldRigMeta>() : null;
            float halfLen = meta != null ? meta.halfLength : Mathf.Max(0.5f, fieldBounds.size.x * 0.5f);

            sign.transform.localRotation = pipes != null
                ? pipes.localRotation * Quaternion.Euler(0f, 90f, 0f)
                : Quaternion.Euler(0f, 90f, 0f);
            sign.transform.localPosition = pipes != null
                ? pipes.localRotation * new Vector3(-halfLen - 0.4f, 0f, 0f)
                : new Vector3(-halfLen - 0.4f, 0f, 0f);

            var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = "Post";
            post.transform.SetParent(sign.transform, false);
            post.transform.localScale    = new Vector3(0.06f, 1.6f, 0.06f);
            post.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            StripCollider(post);
            var postRenderer = post.GetComponent<Renderer>();
            if (postRenderer != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader != null)
                {
                    var pm = new Material(shader) { name = $"PostMat_{zone.id}" };
                    if (pm.HasProperty("_BaseColor")) pm.SetColor("_BaseColor", new Color(0.18f, 0.22f, 0.24f, 1f));
                    else pm.color = new Color(0.18f, 0.22f, 0.24f, 1f);
                    postRenderer.sharedMaterial = pm;
                }
            }

            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "Panel";
            panel.transform.SetParent(sign.transform, false);
            panel.transform.localScale    = new Vector3(1.6f, 0.55f, 0.04f);
            panel.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            StripCollider(panel);
            var panelRenderer = panel.GetComponent<Renderer>();
            if (panelRenderer != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader != null)
                {
                    var pm = new Material(shader) { name = $"SignMat_{zone.id}" };
                    Color baseCol = zone.cropType == CropType.Corn
                        ? new Color(0.95f, 0.78f, 0.20f, 1f)
                        : new Color(0.30f, 0.85f, 0.42f, 1f);
                    if (pm.HasProperty("_BaseColor")) pm.SetColor("_BaseColor", baseCol);
                    else pm.color = baseCol;
                    if (pm.HasProperty("_EmissionColor"))
                    {
                        pm.EnableKeyword("_EMISSION");
                        pm.SetColor("_EmissionColor", baseCol * 0.5f);
                    }
                    panelRenderer.sharedMaterial = pm;
                }
            }

            // 3D label — duplicated front + back so it's visible from both sides.
            CreateSignLabel(sign.transform, zone.displayName ?? zone.id, new Vector3(0f, 1.6f, -0.025f), 0f);
            CreateSignLabel(sign.transform, zone.displayName ?? zone.id, new Vector3(0f, 1.6f,  0.025f), 180f);
        }

        private static void CreateSignLabel(Transform parent, string text, Vector3 localPos, float yRot)
        {
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(parent, false);
            labelGO.transform.localPosition = localPos;
            labelGO.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);

            var tmp = labelGO.AddComponent<TextMeshPro>();
            tmp.text = (text ?? string.Empty).ToUpperInvariant();
            tmp.fontSize = 3.5f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.black;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.fontStyle = FontStyles.Bold;
            tmp.rectTransform.sizeDelta = new Vector2(1.5f, 0.5f);
        }

        private static Transform CreateSprinklersForField(Transform zoneRoot, IrrigationZone zone, Bounds fieldBounds)
        {
            var sprinklers = new GameObject("Sprinklers");
            sprinklers.transform.SetParent(zoneRoot, false);
            sprinklers.transform.localPosition = Vector3.zero;

            // Pull layout from the pipe rig so sprinklers line up with spray heads.
            var pipes = zoneRoot.Find("Pipes");
            var meta  = pipes != null ? pipes.GetComponent<FieldRigMeta>() : null;
            float halfLen   = meta != null ? meta.halfLength : Mathf.Max(0.5f, fieldBounds.size.x * 0.5f - 0.15f);
            float pipeY     = meta != null ? meta.pipeY      : 0.9f;
            float stubLen   = meta != null ? meta.stubLen    : 0.30f;
            int   headCount = meta != null ? meta.headCount  : 3;
            float headY     = pipeY - stubLen;

            // Match the manifold orientation (along X or Z).
            sprinklers.transform.localRotation = pipes != null ? pipes.localRotation : Quaternion.identity;

            var particleMat = FindParticleMaterial();

            for (int i = 0; i < headCount; i++)
            {
                float t = (i + 0.5f) / headCount;
                float x = Mathf.Lerp(-halfLen * 0.92f, halfLen * 0.92f, t);

                var ps = new GameObject($"Sprinkler_{i + 1}");
                ps.transform.SetParent(sprinklers.transform, false);
                ps.transform.localPosition = new Vector3(x, headY, 0f);

                var system = ps.AddComponent<ParticleSystem>();

                var main = system.main;
                main.startLifetime   = 1.6f;
                main.startSpeed      = new ParticleSystem.MinMaxCurve(1.8f, 3.2f);
                main.startSize       = new ParticleSystem.MinMaxCurve(0.14f, 0.28f);
                main.startColor      = new ParticleSystem.MinMaxGradient(
                    new Color(0.55f, 0.85f, 1.00f, 0.95f),
                    new Color(0.30f, 0.65f, 0.95f, 0.95f));
                main.gravityModifier = 1.6f;
                main.maxParticles    = 1500;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.playOnAwake     = false;

                var emission = system.emission;
                emission.enabled = false;
                emission.rateOverTime = 220f;

                var shape = system.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle     = 28f;
                shape.radius    = 0.08f;
                shape.rotation  = new Vector3(180f, 0f, 0f); // emit downward

                var vel = system.velocityOverLifetime;
                vel.enabled = true;
                vel.space   = ParticleSystemSimulationSpace.Local;
                vel.y       = new ParticleSystem.MinMaxCurve(-1.2f, -2.4f);

                var color = system.colorOverLifetime;
                color.enabled = true;
                var grad = new Gradient();
                grad.SetKeys(
                    new[]
                    {
                        new GradientColorKey(new Color(0.65f, 0.90f, 1.00f), 0f),
                        new GradientColorKey(new Color(0.30f, 0.65f, 0.95f), 1f)
                    },
                    new[]
                    {
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(0f, 1f)
                    });
                color.color = new ParticleSystem.MinMaxGradient(grad);

                var size = system.sizeOverLifetime;
                size.enabled = true;
                var sizeCurve = new AnimationCurve(
                    new Keyframe(0f, 1f),
                    new Keyframe(1f, 0.4f));
                size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                {
                    // Billboard is reliable in URP + VR; Stretch often disappears.
                    renderer.renderMode           = ParticleSystemRenderMode.Billboard;
                    renderer.alignment            = ParticleSystemRenderSpace.View;
                    renderer.minParticleSize      = 0f;
                    renderer.maxParticleSize      = 2f;
                    renderer.sortingFudge         = 0f;
                    renderer.material             = null;
                    renderer.sharedMaterial       = particleMat;
                }

                // Editor-only: pre-warm so the spray is already visible the first
                // tick a zone turns on. Doesn't affect runtime.
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            return sprinklers.transform;
        }

        private static Material FindParticleMaterial()
        {
            // Prefer URP particle shaders when those shaders exist (no GraphicsSettings
            // API — names differ across Unity versions).
            Shader shader = null;
            if (Shader.Find("Universal Render Pipeline/Particles/Unlit") != null
                || Shader.Find("Universal Render Pipeline/Particles/Simple Lit") != null)
            {
                shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                    ?? Shader.Find("Universal Render Pipeline/Particles/Simple Lit");
            }

            shader ??= Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Particles/Alpha Blended")
                ?? Shader.Find("Sprites/Default");

            if (shader != null)
            {
                var mat = new Material(shader) { name = "WaterParticleMat" };
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", new Color(0.55f, 0.85f, 1f, 0.9f));
                else
                    mat.color = new Color(0.55f, 0.85f, 1f, 0.9f);
                mat.renderQueue = 3000;
                return mat;
            }

            return AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Tablet
        // ─────────────────────────────────────────────────────────────────────

        private const float TabletScale = 0.005f;

        private static void BuildTablet(GameObject hub)
        {
            var existing = GameObject.Find("SmartIrrigationTablet");
            if (existing != null) Undo.DestroyObjectImmediate(existing);

            // If the legacy SmartFarmTablet is in the scene, hide it so the new
            // Smart Irrigation Tablet visually replaces it at the same spot.
            Vector3   spawnPos = new Vector3(-0.65f, 1.35f, 1.4f);
            Quaternion spawnRot = Quaternion.Euler(12f, 200f, 0f);
            var legacy = GameObject.Find("SmartFarmTablet");
            if (legacy != null)
            {
                spawnPos = legacy.transform.position;
                spawnRot = legacy.transform.rotation;
                Undo.RecordObject(legacy, "Hide legacy Smart Farm tablet");
                legacy.SetActive(false);
                Debug.Log("[SmartIrrigationTablet] Legacy SmartFarmTablet hidden — new tablet placed at the same anchor.");
            }

            var cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();

            var tablet = new GameObject("SmartIrrigationTablet", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(tablet, "Smart Irrigation Tablet");
            tablet.transform.position   = spawnPos;
            tablet.transform.rotation   = spawnRot;
            tablet.transform.localScale = new Vector3(TabletScale, TabletScale, TabletScale);

            var canvas = tablet.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            canvas.sortingOrder = 70;

            var scaler = tablet.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            tablet.AddComponent<GraphicRaycaster>();
            var trackedType = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
            if (trackedType != null) tablet.AddComponent(trackedType);

            var rt = tablet.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1100f, 760f);

            // Background + border
            var bg = MakePanel(tablet.transform, "AppBackground", Vector2.zero, Vector2.one, BgDeep);
            bg.GetComponent<Image>().raycastTarget = true;
            BuildBorderStrips(bg.transform, BorderColor, 0.005f);

            // Header
            var header = MakePanel(tablet.transform, "Header",
                new Vector2(0.0f, 0.92f), new Vector2(1f, 1f), HeaderTint);
            var titleText = MakeText(header.transform, "TitleText", "Smart Irrigation Tablet",
                28, TextAlignmentOptions.Left, new Vector2(0.025f, 0.15f), new Vector2(0.55f, 0.85f),
                AccentGreen, true);
            var statusLed = MakeColoredCircle(header.transform, "StatusLed",
                new Vector2(0.95f, 0.40f), new Vector2(0.985f, 0.65f), AccentGreen);
            var headerStatus = MakeText(header.transform, "HeaderStatusText",
                "Sunny  ·  0/2 zones active  ·  0 alerts",
                15, TextAlignmentOptions.Right,
                new Vector2(0.55f, 0.15f), new Vector2(0.93f, 0.85f),
                TextSecondary);

            // Tab bar
            var tabBar = MakePanel(tablet.transform, "TabBar",
                new Vector2(0.0f, 0.83f), new Vector2(1f, 0.92f), TabBg);
            var overviewTab  = BuildTabButton(tabBar.transform, "OverviewTab",  "OVERVIEW",  new Vector2(0.020f, 0.18f), new Vector2(0.245f, 0.82f));
            var zonesTab     = BuildTabButton(tabBar.transform, "ZonesTab",     "ZONES",     new Vector2(0.255f, 0.18f), new Vector2(0.480f, 0.82f));
            var analyticsTab = BuildTabButton(tabBar.transform, "AnalyticsTab", "ANALYTICS", new Vector2(0.490f, 0.18f), new Vector2(0.745f, 0.82f));
            var alertsTab    = BuildTabButton(tabBar.transform, "AlertsTab",    "ALERTS",    new Vector2(0.755f, 0.18f), new Vector2(0.980f, 0.82f));

            // Content area
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(tablet.transform, false);
            var contentRect = (RectTransform)content.transform;
            contentRect.anchorMin = new Vector2(0.02f, 0.02f);
            contentRect.anchorMax = new Vector2(0.98f, 0.82f);
            contentRect.offsetMin = contentRect.offsetMax = Vector2.zero;

            // 4 pages
            var overviewPage  = CreatePage(contentRect, "OverviewPage",  isActive: true);
            var zonesPage     = CreatePage(contentRect, "ZonesPage",     isActive: false);
            var analyticsPage = CreatePage(contentRect, "AnalyticsPage", isActive: false);
            var alertsPage    = CreatePage(contentRect, "AlertsPage",    isActive: false);

            BuildOverviewPage(overviewPage.transform, hub.GetComponent<SmartIrrigationTabletManager>(),
                out var overviewUI);
            BuildZonesPage(zonesPage.transform, hub.GetComponent<SmartIrrigationTabletManager>(),
                out var zonesUI);
            BuildAnalyticsPage(analyticsPage.transform, hub.GetComponent<SmartIrrigationTabletManager>(),
                out var analyticsUI);
            BuildAlertsPage(alertsPage.transform, hub.GetComponent<SmartIrrigationTabletManager>(),
                out var alertsUI);

            // Top-level controller
            var ctrl = tablet.AddComponent<SmartIrrigationTabletAppController>();
            SetField(ctrl, "manager",            hub.GetComponent<SmartIrrigationTabletManager>());
            SetField(ctrl, "titleText",          titleText);
            SetField(ctrl, "headerStatusText",   headerStatus);
            SetField(ctrl, "statusLed",          statusLed);
            SetField(ctrl, "overviewTabButton",  overviewTab);
            SetField(ctrl, "zonesTabButton",     zonesTab);
            SetField(ctrl, "analyticsTabButton", analyticsTab);
            SetField(ctrl, "alertsTabButton",    alertsTab);
            SetField(ctrl, "overviewPage",       overviewPage);
            SetField(ctrl, "zonesPage",          zonesPage);
            SetField(ctrl, "analyticsPage",      analyticsPage);
            SetField(ctrl, "alertsPage",         alertsPage);

            // Reuse the existing TabletDeskAnchor (created by SmartFarmSetupEditor) so
            // the irrigation tablet sits where the original tablet did.
            var deskAnchor = GameObject.Find("TabletDeskAnchor");
            if (deskAnchor != null)
                SetField(ctrl, "deskAnchor", deskAnchor.transform);

            // Don't auto-snap to the desk anchor on Play — respect the user's
            // hand-placed transform. Users can still flip this in the Inspector.
            SetField(ctrl, "snapToDeskOnStart", false);

            // Tag everything with the UI layer so XR ray/poke interactors can press buttons
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) SetLayerRecursive(tablet, uiLayer);

            // Make sure the existing XR raycasters can hit our new tablet
            EnableXRUIControllers();

            Selection.activeGameObject = tablet;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Page builders
        // ─────────────────────────────────────────────────────────────────────

        private static void BuildOverviewPage(Transform page, SmartIrrigationTabletManager manager,
            out IrrigationOverviewPageUI ui)
        {
            // Top row: 3 circular indicators
            var moisture   = BuildCircularIndicator(page, "MoistureIndicator",   new Vector2(0.02f, 0.50f), new Vector2(0.32f, 0.97f), "Soil Moisture", AccentBlue);
            var health     = BuildCircularIndicator(page, "HealthIndicator",     new Vector2(0.34f, 0.50f), new Vector2(0.66f, 0.97f), "Crop Health",   AccentGreen);
            var efficiency = BuildCircularIndicator(page, "EfficiencyIndicator", new Vector2(0.68f, 0.50f), new Vector2(0.98f, 0.97f), "Efficiency",    new Color(0.95f, 0.78f, 0.25f, 1f));

            // Middle row: stats cards
            var waterCard = BuildStatCard(page, "WaterUsageCard",   new Vector2(0.02f, 0.30f), new Vector2(0.245f, 0.48f), "Total Water",  "0 units");
            var zonesCard = BuildStatCard(page, "ActiveZonesCard",  new Vector2(0.255f, 0.30f), new Vector2(0.480f, 0.48f), "Active Zones", "0 / 2");
            var weatherCard = BuildStatCard(page, "WeatherCard",     new Vector2(0.490f, 0.30f), new Vector2(0.745f, 0.48f), "Weather",      "Sunny");
            var stateCard = BuildStatCard(page, "MoistureStateCard",new Vector2(0.755f, 0.30f), new Vector2(0.98f, 0.48f), "Status",       "Healthy");

            // Animated flow bar
            var flow = BuildAnimatedFlowBar(page, "OverallFlowBar", new Vector2(0.02f, 0.16f), new Vector2(0.98f, 0.27f));

            // Action buttons
            var enableAll  = BuildButton(page, "EnableAllButton",  "ENABLE ALL",   new Vector2(0.02f, 0.02f), new Vector2(0.32f, 0.14f), AccentGreen);
            var autoAll    = BuildButton(page, "AutoAllButton",    "AUTO MODE",   new Vector2(0.34f, 0.02f), new Vector2(0.66f, 0.14f), AccentBlue);
            var disableAll = BuildButton(page, "DisableAllButton", "DISABLE ALL", new Vector2(0.68f, 0.02f), new Vector2(0.98f, 0.14f), new Color(0.92f, 0.30f, 0.25f, 1f));

            ui = page.gameObject.AddComponent<IrrigationOverviewPageUI>();
            SetField(ui, "manager",             manager);
            SetField(ui, "moistureIndicator",   moisture);
            SetField(ui, "healthIndicator",     health);
            SetField(ui, "efficiencyIndicator", efficiency);
            SetField(ui, "waterUsageText",      waterCard);
            SetField(ui, "activeZonesText",     zonesCard);
            SetField(ui, "weatherText",         weatherCard);
            SetField(ui, "moistureStateText",   stateCard);
            SetField(ui, "overallFlowBar",      flow);
            SetField(ui, "enableAllButton",     enableAll);
            SetField(ui, "disableAllButton",    disableAll);
            SetField(ui, "autoAllButton",       autoAll);
        }

        private static void BuildZonesPage(Transform page, SmartIrrigationTabletManager manager,
            out IrrigationZonesPageUI ui)
        {
            // Header label
            MakeText(page, "ZonesTitle", "IRRIGATION ZONES",
                22, TextAlignmentOptions.Left,
                new Vector2(0.02f, 0.92f), new Vector2(0.98f, 0.99f),
                AccentGreen, true);

            // ScrollRect with vertical layout
            var scrollGO = new GameObject("ZoneScroll", typeof(RectTransform));
            scrollGO.transform.SetParent(page, false);
            var scrollRect = (RectTransform)scrollGO.transform;
            scrollRect.anchorMin = new Vector2(0.01f, 0.02f);
            scrollRect.anchorMax = new Vector2(0.99f, 0.90f);
            scrollRect.offsetMin = scrollRect.offsetMax = Vector2.zero;

            var sr = scrollGO.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.vertical   = true;
            scrollGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

            var viewportGO = new GameObject("Viewport", typeof(RectTransform));
            viewportGO.transform.SetParent(scrollGO.transform, false);
            var vpRect = (RectTransform)viewportGO.transform;
            vpRect.anchorMin = Vector2.zero; vpRect.anchorMax = Vector2.one;
            vpRect.offsetMin = vpRect.offsetMax = Vector2.zero;
            viewportGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
            viewportGO.AddComponent<Mask>().showMaskGraphic = false;

            var listGO = new GameObject("ListRoot", typeof(RectTransform));
            listGO.transform.SetParent(viewportGO.transform, false);
            var listRect = (RectTransform)listGO.transform;
            listRect.anchorMin = new Vector2(0f, 1f);
            listRect.anchorMax = new Vector2(1f, 1f);
            listRect.pivot     = new Vector2(0.5f, 1f);
            listRect.offsetMin = listRect.offsetMax = Vector2.zero;
            listRect.sizeDelta = new Vector2(0f, 8f);

            var vlg = listGO.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 14;
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var csf = listGO.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            sr.viewport = vpRect;
            sr.content  = listRect;

            // Empty-state label shown when there are no zones to render.
            var emptyLabelGO = MakeText(page, "EmptyState",
                "No irrigation zones configured.\nAdd a zone in IrrigationZoneManager.",
                18, TextAlignmentOptions.Center,
                new Vector2(0.10f, 0.35f), new Vector2(0.90f, 0.65f),
                TextSecondary).gameObject;
            emptyLabelGO.SetActive(false);

            // Card template
            var cardTemplate = BuildZoneCardTemplate(page, manager);

            ui = page.gameObject.AddComponent<IrrigationZonesPageUI>();
            SetField(ui, "manager",         manager);
            SetField(ui, "listRoot",        listRect);
            SetField(ui, "cardTemplate",    cardTemplate);
            SetField(ui, "emptyStateLabel", emptyLabelGO);
        }

        private static IrrigationZoneCardUI BuildZoneCardTemplate(Transform pageRoot,
            SmartIrrigationTabletManager manager)
        {
            // Template lives off-page, hidden — IrrigationZonesPageUI clones it.
            var card = new GameObject("ZoneCardTemplate", typeof(RectTransform));
            card.transform.SetParent(pageRoot, false);
            var rt = (RectTransform)card.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, 230f);

            var bg = card.AddComponent<Image>();
            bg.color = BgCard;
            var le = card.AddComponent<LayoutElement>();
            le.preferredHeight = 230f;
            le.minHeight = 220f;

            // Header strip with title + status
            var zoneName = MakeText(card.transform, "ZoneName", "Zone",
                22, TextAlignmentOptions.Left,
                new Vector2(0.03f, 0.78f), new Vector2(0.55f, 0.96f),
                TextPrimary, true);
            var cropType = MakeText(card.transform, "CropType", "Crop",
                14, TextAlignmentOptions.Left,
                new Vector2(0.03f, 0.66f), new Vector2(0.55f, 0.78f),
                TextSecondary);
            var statusLed = MakeColoredCircle(card.transform, "StatusLed",
                new Vector2(0.78f, 0.84f), new Vector2(0.81f, 0.94f),
                AccentGreen);
            var statusText = MakeText(card.transform, "StatusText", "STANDBY",
                14, TextAlignmentOptions.Right,
                new Vector2(0.6f, 0.81f), new Vector2(0.97f, 0.97f),
                AccentGreen, true);

            // Stat row: Moisture, Health, Water Used
            var moisture = MakeText(card.transform, "MoistureText", "Moisture\n50%",
                15, TextAlignmentOptions.Left,
                new Vector2(0.03f, 0.36f), new Vector2(0.30f, 0.62f),
                TextPrimary);
            var health = MakeText(card.transform, "HealthText", "Health\n100%",
                15, TextAlignmentOptions.Left,
                new Vector2(0.32f, 0.36f), new Vector2(0.59f, 0.62f),
                TextPrimary);
            var waterUsed = MakeText(card.transform, "WaterUsedText", "Water Used\n0 u",
                15, TextAlignmentOptions.Left,
                new Vector2(0.61f, 0.36f), new Vector2(0.88f, 0.62f),
                TextPrimary);

            // Soil state pill
            var pill = MakePanel(card.transform, "SoilStatePill",
                new Vector2(0.78f, 0.66f), new Vector2(0.97f, 0.78f),
                AccentGreen);
            pill.GetComponent<Image>().raycastTarget = false;
            var pillLabel = MakeText(pill.transform, "PillLabel", "Healthy",
                12, TextAlignmentOptions.Center,
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                Color.white, true);

            // Bars
            var moistureFill = BuildSimpleBar(card.transform, "MoistureBar",
                new Vector2(0.03f, 0.28f), new Vector2(0.59f, 0.34f),
                AccentBlue);
            var healthFill = BuildSimpleBar(card.transform, "HealthBar",
                new Vector2(0.61f, 0.28f), new Vector2(0.97f, 0.34f),
                AccentGreen);

            // Reason text
            var reason = MakeText(card.transform, "ReasonText", "Auto: standby",
                12, TextAlignmentOptions.Left,
                new Vector2(0.03f, 0.20f), new Vector2(0.97f, 0.27f),
                new Color(0.65f, 0.85f, 0.78f, 0.85f));

            // Flow bar (full width)
            var flowBar = BuildAnimatedFlowBar(card.transform, "FlowBar",
                new Vector2(0.03f, 0.12f), new Vector2(0.97f, 0.18f));

            // Mode buttons
            var onBtn   = BuildButton(card.transform, "OnButton",   "ON",   new Vector2(0.03f, 0.02f), new Vector2(0.33f, 0.10f), AccentGreen);
            var offBtn  = BuildButton(card.transform, "OffButton",  "OFF",  new Vector2(0.34f, 0.02f), new Vector2(0.66f, 0.10f), new Color(0.92f, 0.30f, 0.25f, 1f));
            var autoBtn = BuildButton(card.transform, "AutoButton", "AUTO", new Vector2(0.67f, 0.02f), new Vector2(0.97f, 0.10f), AccentBlue);

            var ui = card.AddComponent<IrrigationZoneCardUI>();
            SetField(ui, "manager",        manager);
            SetField(ui, "zoneNameText",   zoneName);
            SetField(ui, "cropTypeText",   cropType);
            SetField(ui, "statusLed",      statusLed);
            SetField(ui, "statusText",     statusText);
            SetField(ui, "moistureText",   moisture);
            SetField(ui, "healthText",     health);
            SetField(ui, "waterUsedText",  waterUsed);
            SetField(ui, "reasonText",     reason);
            SetField(ui, "moistureFill",   moistureFill);
            SetField(ui, "healthFill",     healthFill);
            SetField(ui, "flowBar",        flowBar);
            SetField(ui, "soilStatePill",  pill.GetComponent<Image>());
            SetField(ui, "soilStateLabel", pillLabel);
            SetField(ui, "onButton",       onBtn);
            SetField(ui, "offButton",      offBtn);
            SetField(ui, "autoButton",     autoBtn);

            card.SetActive(false);
            return ui;
        }

        private static void BuildAnalyticsPage(Transform page, SmartIrrigationTabletManager manager,
            out IrrigationAnalyticsPageUI ui)
        {
            // Title
            MakeText(page, "AnalyticsTitle", "WATER ANALYTICS",
                22, TextAlignmentOptions.Left,
                new Vector2(0.02f, 0.92f), new Vector2(0.98f, 0.99f),
                AccentGreen, true);

            // 4 KPI cards
            var totalWater = BuildStatCard(page, "TotalWaterCard",   new Vector2(0.02f, 0.74f), new Vector2(0.245f, 0.90f), "Total Water Used", "0 units");
            var efficiency = BuildStatCard(page, "EfficiencyCard",   new Vector2(0.255f, 0.74f), new Vector2(0.480f, 0.90f), "Efficiency",       "85%");
            var hydration  = BuildStatCard(page, "HydrationCard",    new Vector2(0.490f, 0.74f), new Vector2(0.745f, 0.90f), "Hydration",        "Healthy");
            var perf       = BuildStatCard(page, "CropPerfCard",     new Vector2(0.755f, 0.74f), new Vector2(0.98f, 0.90f), "Crop Performance", "Healthy");

            // Efficiency bar
            MakeText(page, "EfficiencyBarLabel", "Live Efficiency",
                14, TextAlignmentOptions.Left,
                new Vector2(0.02f, 0.65f), new Vector2(0.98f, 0.71f),
                TextSecondary, true);
            var efficiencyBarTrack = MakePanel(page, "EfficiencyBarTrack",
                new Vector2(0.02f, 0.58f), new Vector2(0.98f, 0.64f), BgBarTrack);
            var efficiencyBarFill = MakePanel(efficiencyBarTrack.transform, "EfficiencyBarFill",
                Vector2.zero, Vector2.one, AccentGreen);
            var efficiencyBarImg = efficiencyBarFill.GetComponent<Image>();
            efficiencyBarImg.type       = Image.Type.Filled;
            efficiencyBarImg.fillMethod = Image.FillMethod.Horizontal;
            efficiencyBarImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            efficiencyBarImg.fillAmount = 0.85f;
            efficiencyBarImg.raycastTarget = false;

            // History graph (10 bars)
            MakeText(page, "GraphLabel", "Water Usage History",
                14, TextAlignmentOptions.Left,
                new Vector2(0.02f, 0.50f), new Vector2(0.98f, 0.56f),
                TextSecondary, true);

            var graphRoot = new GameObject("GraphRoot", typeof(RectTransform));
            graphRoot.transform.SetParent(page, false);
            var graphRect = (RectTransform)graphRoot.transform;
            graphRect.anchorMin = new Vector2(0.02f, 0.10f);
            graphRect.anchorMax = new Vector2(0.98f, 0.49f);
            graphRect.offsetMin = graphRect.offsetMax = Vector2.zero;
            graphRoot.AddComponent<Image>().color = new Color(0.05f, 0.10f, 0.13f, 0.95f);

            const int bucketCount = 10;
            const float padding = 0.012f;
            float barW = (1f - padding * (bucketCount + 1)) / bucketCount;
            var bars = new Image[bucketCount];
            for (int i = 0; i < bucketCount; i++)
            {
                float x0 = padding + i * (barW + padding);
                float x1 = x0 + barW;

                var trackBar = MakePanel(graphRoot.transform, $"Bar{i}_Track",
                    new Vector2(x0, 0.05f), new Vector2(x1, 0.95f), BgBarTrack);
                trackBar.GetComponent<Image>().raycastTarget = false;
                var fill = MakePanel(trackBar.transform, $"Bar{i}_Fill", Vector2.zero, Vector2.one,
                    AccentGreen);
                var img = fill.GetComponent<Image>();
                img.type        = Image.Type.Filled;
                img.fillMethod  = Image.FillMethod.Vertical;
                img.fillOrigin  = (int)Image.OriginVertical.Bottom;
                img.fillAmount  = 0f;
                img.raycastTarget = false;
                bars[i] = img;
            }

            ui = page.gameObject.AddComponent<IrrigationAnalyticsPageUI>();
            SetField(ui, "manager",             manager);
            SetField(ui, "totalWaterText",      totalWater);
            SetField(ui, "efficiencyText",      efficiency);
            SetField(ui, "hydrationStatusText", hydration);
            SetField(ui, "performanceText",     perf);
            SetField(ui, "efficiencyBar",       efficiencyBarImg);
            SetField(ui, "graphRoot",           graphRect);
            SetField(ui, "graphBars",           bars);
        }

        private static void BuildAlertsPage(Transform page, SmartIrrigationTabletManager manager,
            out IrrigationAlertsPageUI ui)
        {
            MakeText(page, "AlertsTitle", "IRRIGATION ALERTS",
                22, TextAlignmentOptions.Left,
                new Vector2(0.02f, 0.92f), new Vector2(0.98f, 0.99f),
                AccentGreen, true);

            // ScrollRect
            var scrollGO = new GameObject("AlertScroll", typeof(RectTransform));
            scrollGO.transform.SetParent(page, false);
            var scrollRect = (RectTransform)scrollGO.transform;
            scrollRect.anchorMin = new Vector2(0.01f, 0.02f);
            scrollRect.anchorMax = new Vector2(0.99f, 0.90f);
            scrollRect.offsetMin = scrollRect.offsetMax = Vector2.zero;

            var sr = scrollGO.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.vertical   = true;
            scrollGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

            var viewportGO = new GameObject("Viewport", typeof(RectTransform));
            viewportGO.transform.SetParent(scrollGO.transform, false);
            var vpRect = (RectTransform)viewportGO.transform;
            vpRect.anchorMin = Vector2.zero; vpRect.anchorMax = Vector2.one;
            vpRect.offsetMin = vpRect.offsetMax = Vector2.zero;
            viewportGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
            viewportGO.AddComponent<Mask>().showMaskGraphic = false;

            var listGO = new GameObject("ListRoot", typeof(RectTransform));
            listGO.transform.SetParent(viewportGO.transform, false);
            var listRect = (RectTransform)listGO.transform;
            listRect.anchorMin = new Vector2(0f, 1f);
            listRect.anchorMax = new Vector2(1f, 1f);
            listRect.pivot     = new Vector2(0.5f, 1f);
            listRect.offsetMin = listRect.offsetMax = Vector2.zero;

            var vlg = listGO.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8;
            vlg.padding = new RectOffset(6, 6, 6, 6);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var csf = listGO.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            sr.viewport = vpRect;
            sr.content  = listRect;

            // Empty state
            var empty = MakeText(page, "EmptyState", "No active alerts",
                20, TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.4f), new Vector2(0.95f, 0.6f),
                TextSecondary);

            // Alert item template
            var template = BuildAlertItemTemplate(page);

            ui = page.gameObject.AddComponent<IrrigationAlertsPageUI>();
            SetField(ui, "manager",        manager);
            SetField(ui, "listRoot",       listRect);
            SetField(ui, "itemTemplate",   template);
            SetField(ui, "emptyStateText", empty);
        }

        private static IrrigationTabletAlertItemUI BuildAlertItemTemplate(Transform pageRoot)
        {
            var item = new GameObject("AlertItemTemplate", typeof(RectTransform));
            item.transform.SetParent(pageRoot, false);
            var rt = (RectTransform)item.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, 78f);

            var bg = item.AddComponent<Image>();
            bg.color = new Color(0.07f, 0.12f, 0.17f, 0.95f);
            var le = item.AddComponent<LayoutElement>();
            le.preferredHeight = 78f;
            le.minHeight       = 70f;

            var accent = MakePanel(item.transform, "Accent",
                new Vector2(0f, 0f), new Vector2(0.012f, 1f),
                AccentGreen);
            accent.GetComponent<Image>().raycastTarget = false;

            var title = MakeText(item.transform, "Title", "Alert title",
                17, TextAlignmentOptions.Left,
                new Vector2(0.025f, 0.55f), new Vector2(0.85f, 0.95f),
                TextPrimary, true);
            var message = MakeText(item.transform, "Message", "Alert message",
                14, TextAlignmentOptions.Left,
                new Vector2(0.025f, 0.08f), new Vector2(0.85f, 0.55f),
                TextSecondary);
            var timestamp = MakeText(item.transform, "Timestamp", "00:00",
                12, TextAlignmentOptions.Right,
                new Vector2(0.85f, 0.55f), new Vector2(0.99f, 0.95f),
                TextSecondary);

            var ui = item.AddComponent<IrrigationTabletAlertItemUI>();
            SetField(ui, "titleText",     title);
            SetField(ui, "messageText",   message);
            SetField(ui, "timestampText", timestamp);
            SetField(ui, "accent",        accent.GetComponent<Image>());
            SetField(ui, "background",    bg);

            item.SetActive(false);
            return ui;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Widget builders
        // ─────────────────────────────────────────────────────────────────────

        private static CircularWaterIndicator BuildCircularIndicator(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, string label, Color tint)
        {
            var card = MakePanel(parent, name, anchorMin, anchorMax, BgCard);

            // Track ring (larger image behind)
            var track = MakePanel(card.transform, "Track",
                new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f),
                BgBarTrack);
            var trackImg = track.GetComponent<Image>();
            trackImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            trackImg.color  = new Color(0.12f, 0.22f, 0.26f, 1f);
            trackImg.raycastTarget = false;

            // Fill ring (radial360)
            var fill = MakePanel(card.transform, "Fill",
                new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f),
                tint);
            var fillImg = fill.GetComponent<Image>();
            fillImg.sprite     = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            fillImg.type       = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Radial360;
            fillImg.fillOrigin = (int)Image.Origin360.Top;
            fillImg.fillClockwise = true;
            fillImg.fillAmount = 0.75f;
            fillImg.raycastTarget = false;

            // Center value
            var value = MakeText(card.transform, "ValueText", "0%",
                32, TextAlignmentOptions.Center,
                new Vector2(0.18f, 0.36f), new Vector2(0.82f, 0.62f),
                TextPrimary, true);

            // Label below
            var labelText = MakeText(card.transform, "LabelText", label,
                14, TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.18f),
                TextSecondary, true);

            var indicator = card.AddComponent<CircularWaterIndicator>();
            SetField(indicator, "trackImage", trackImg);
            SetField(indicator, "fillImage",  fillImg);
            SetField(indicator, "valueText",  value);
            SetField(indicator, "labelText",  labelText);
            return indicator;
        }

        private static AnimatedFlowBar BuildAnimatedFlowBar(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var bar = MakePanel(parent, name, anchorMin, anchorMax, BgBarTrack);
            var trackImg = bar.GetComponent<Image>();
            trackImg.raycastTarget = false;

            var fill = MakePanel(bar.transform, "Fill", Vector2.zero, Vector2.one, AccentBlue);
            var fillImg = fill.GetComponent<Image>();
            fillImg.type        = Image.Type.Filled;
            fillImg.fillMethod  = Image.FillMethod.Horizontal;
            fillImg.fillOrigin  = (int)Image.OriginHorizontal.Left;
            fillImg.fillAmount  = 0f;
            fillImg.raycastTarget = false;

            var shineGO = new GameObject("Shine", typeof(RectTransform));
            shineGO.transform.SetParent(bar.transform, false);
            var shineRect = (RectTransform)shineGO.transform;
            shineRect.anchorMin = new Vector2(0f, 0f);
            shineRect.anchorMax = new Vector2(0.18f, 1f);
            shineRect.offsetMin = shineRect.offsetMax = Vector2.zero;
            var shineImg = shineGO.AddComponent<Image>();
            shineImg.color = new Color(1f, 1f, 1f, 0.18f);
            shineImg.raycastTarget = false;

            var animated = bar.AddComponent<AnimatedFlowBar>();
            SetField(animated, "fillImage",  fillImg);
            SetField(animated, "trackImage", trackImg);
            SetField(animated, "shineRect",  shineRect);
            SetField(animated, "shineImage", shineImg);
            return animated;
        }

        private static Image BuildSimpleBar(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var bg = MakePanel(parent, name, anchorMin, anchorMax, BgBarTrack);
            var bgImg = bg.GetComponent<Image>();
            bgImg.raycastTarget = false;
            var fill = MakePanel(bg.transform, "Fill", Vector2.zero, Vector2.one, color);
            var fillImg = fill.GetComponent<Image>();
            fillImg.type        = Image.Type.Filled;
            fillImg.fillMethod  = Image.FillMethod.Horizontal;
            fillImg.fillOrigin  = (int)Image.OriginHorizontal.Left;
            fillImg.fillAmount  = 0.5f;
            fillImg.raycastTarget = false;
            return fillImg;
        }

        private static TMP_Text BuildStatCard(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, string label, string value)
        {
            var card = MakePanel(parent, name, anchorMin, anchorMax, BgCard);
            var t = MakeText(card.transform, "ValueText",
                $"<size=70%><color=#9FE2C7>{label}</color></size>\n{value}",
                20, TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.10f), new Vector2(0.95f, 0.90f),
                TextPrimary);
            return t;
        }

        private static Button BuildButton(Transform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color = color;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var textGO = new GameObject("Text", typeof(RectTransform));
            textGO.transform.SetParent(go.transform, false);
            var tr = (RectTransform)textGO.transform;
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = tr.offsetMax = Vector2.zero;
            var t = textGO.AddComponent<TextMeshProUGUI>();
            t.text          = label;
            t.fontSize      = 16;
            t.fontStyle     = FontStyles.Bold;
            t.color         = Color.white;
            t.alignment     = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            return btn;
        }

        private static Button BuildTabButton(Transform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var btn = BuildButton(parent, name, label, anchorMin, anchorMax, TabBg);
            return btn;
        }

        private static GameObject CreatePage(RectTransform parent, string name, bool isActive)
        {
            var page = new GameObject(name, typeof(RectTransform));
            page.transform.SetParent(parent, false);
            var rt = (RectTransform)page.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            page.SetActive(isActive);
            return page;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Border + primitive builders
        // ─────────────────────────────────────────────────────────────────────

        private static void BuildBorderStrips(Transform parent, Color color, float thickness)
        {
            var top    = MakePanel(parent, "BorderTop",    new Vector2(0f, 1f - thickness), new Vector2(1f, 1f), color);
            var bottom = MakePanel(parent, "BorderBottom", new Vector2(0f, 0f), new Vector2(1f, thickness), color);
            var left   = MakePanel(parent, "BorderLeft",   new Vector2(0f, 0f), new Vector2(thickness, 1f), color);
            var right  = MakePanel(parent, "BorderRight",  new Vector2(1f - thickness, 0f), new Vector2(1f, 1f), color);
            top.GetComponent<Image>().raycastTarget    = false;
            bottom.GetComponent<Image>().raycastTarget = false;
            left.GetComponent<Image>().raycastTarget   = false;
            right.GetComponent<Image>().raycastTarget  = false;
        }

        private static GameObject MakePanel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = color;
            return go;
        }

        private static TMP_Text MakeText(Transform parent, string name, string value, float size,
            TextAlignmentOptions align, Vector2 anchorMin, Vector2 anchorMax, Color color, bool bold = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text          = value;
            t.fontSize      = size;
            t.alignment     = align;
            t.color         = color;
            t.raycastTarget = false;
            if (bold) t.fontStyle = FontStyles.Bold;
            return t;
        }

        private static Image MakeColoredCircle(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color         = color;
            img.raycastTarget = false;
            img.sprite        = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            return img;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Misc helpers
        // ─────────────────────────────────────────────────────────────────────

        private static Transform FindChild(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (string.Equals(c.name, name, System.StringComparison.OrdinalIgnoreCase))
                    return c;
            }
            return null;
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null) c = go.AddComponent<T>();
            return c;
        }

        private static void SetField(object obj, string field, object value)
        {
            if (obj == null) return;
            var f = obj.GetType().GetField(field,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            f?.SetValue(obj, value);
        }

        private static object GetFieldValue(object obj, string field)
        {
            if (obj == null) return null;
            var f = obj.GetType().GetField(field,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return f?.GetValue(obj);
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            for (int i = 0; i < go.transform.childCount; i++)
                SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
        }

        private static void EnableXRUIControllers()
        {
            int enabled = 0;
            foreach (var comp in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (comp == null) continue;
                var so = new SerializedObject(comp);
                var uiProp = so.FindProperty("m_EnableUIInteraction");
                if (uiProp != null && !uiProp.boolValue)
                {
                    uiProp.boolValue = true;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    enabled++;
                }
                var rayProp = so.FindProperty("m_RaycastMask");
                if (rayProp != null)
                {
                    var bits = rayProp.FindPropertyRelative("m_Bits");
                    if (bits != null)
                    {
                        int val = bits.intValue;
                        int uiLayer = LayerMask.NameToLayer("UI");
                        int uiBit = uiLayer >= 0 ? (1 << uiLayer) : 0;
                        if (uiBit > 0 && (val & uiBit) == 0)
                        {
                            bits.intValue = val | uiBit | 1;
                            so.ApplyModifiedPropertiesWithoutUndo();
                        }
                    }
                }
            }
            if (enabled > 0)
                Debug.Log($"[SmartIrrigationTablet] Enabled UI Interaction on {enabled} XR interactor(s).");
        }
    }
}
