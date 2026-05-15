#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SmartFarm.Irrigation.Editor
{
    /// <summary>
    /// Menus for the realistic water spray system.
    ///
    ///   <i>Tools › Smart Farm › Rebuild Realistic Water Spray</i>
    ///       Destroys and rebuilds every Sprinkler nozzle in the scene with the
    ///       3-layer water effect (droplets + mist + splash on impact).
    ///
    ///   <i>Tools › Smart Farm › Preview Water Spray (Scene View)</i>
    ///       Live-simulates every sprinkler in the Scene view so you can SEE the
    ///       water without pressing Play. (Unity normally pauses particles in
    ///       Scene view.)
    ///
    ///   <i>Tools › Smart Farm › Stop Water Spray Preview</i>
    ///       Stops the editor-time preview.
    ///
    ///   <i>Tools › Smart Farm › Test Spray (Force All Zones ON)</i>
    ///       Runtime helper — forces every zone into the On mode so you see
    ///       water in Play mode regardless of soil moisture.
    /// </summary>
    public static class IrrigationSprayRebuilderEditor
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Rebuild
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("Tools/Smart Farm/Rebuild Realistic Water Spray", priority = 30)]
        public static void RebuildRealisticWaterSpray()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Rebuild Water Spray",
                    "Please stop Play mode before rebuilding the spray visuals.", "OK");
                return;
            }

            var visualsRoot = GameObject.Find("SmartIrrigationSceneVisuals");
            if (visualsRoot == null)
            {
                EditorUtility.DisplayDialog("Rebuild Water Spray",
                    "Could not find 'SmartIrrigationSceneVisuals' in the scene.\n\n" +
                    "Run 'Tools › Smart Farm › Setup Smart Irrigation Tablet' first.",
                    "OK");
                return;
            }

            int rebuiltNozzles = 0;
            int zonesProcessed = 0;

            for (int z = 0; z < visualsRoot.transform.childCount; z++)
            {
                var zoneRoot = visualsRoot.transform.GetChild(z);
                var sprinklerRoot = zoneRoot.Find("Sprinklers");
                if (sprinklerRoot == null) continue;
                zonesProcessed++;

                for (int n = 0; n < sprinklerRoot.childCount; n++)
                {
                    var nozzle = sprinklerRoot.GetChild(n);
                    if (nozzle == null) continue;

                    // Strip ParticleSystem on the nozzle root if present so the
                    // builder can add its own child systems instead.
                    var existingPs = nozzle.GetComponent<ParticleSystem>();
                    if (existingPs != null) Object.DestroyImmediate(existingPs);
                    var existingRenderer = nozzle.GetComponent<ParticleSystemRenderer>();
                    if (existingRenderer != null) Object.DestroyImmediate(existingRenderer);

                    Undo.RegisterFullObjectHierarchyUndo(nozzle.gameObject, "Rebuild Spray Layers");
                    IrrigationSprayBuilder.Build(nozzle);
                    rebuiltNozzles++;
                }
            }

            var feedback = Object.FindFirstObjectByType<IrrigationVisualFeedback>();
            if (feedback != null) feedback.RefreshCache();

            // Add the Play-mode helper so the user sees water immediately on Play
            // even if every zone is in Auto + standby. They can disable it later.
            EnsureSprayDebug();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            // Kick the editor preview so the user immediately sees water in
            // the Scene view (Unity otherwise pauses particles in edit mode).
            PreviewSprayInSceneView();

            EditorUtility.DisplayDialog(
                "Realistic Water Spray Built!",
                $"Rebuilt {rebuiltNozzles} sprinkler nozzle(s) across {zonesProcessed} zone(s).\n\n" +
                "Each nozzle now has three water layers:\n" +
                "  • Stream — water droplets that arc and fall.\n" +
                "  • Mist — soft hiss around the nozzle.\n" +
                "  • Splash — bursts on impact when droplets land.\n\n" +
                "WHAT TO DO NOW:\n" +
                "  1. Scene view: water is already being previewed for you.\n" +
                "     Stop it with 'Tools › Smart Farm › Stop Water Spray Preview'.\n" +
                "  2. Play mode: the new 'IrrigationSprayDebug' component on the\n" +
                "     hub will force every zone ON when you press Play so water\n" +
                "     is visible immediately. Disable it later for normal Auto\n" +
                "     behaviour.\n" +
                "  3. Stuck? Run 'Tools › Smart Farm › Diagnose Spray (Play Mode)'\n" +
                "     while Play is running — it prints the state of every zone\n" +
                "     and its particle systems to the Console.",
                "OK");
        }

        private static void EnsureSprayDebug()
        {
            var hub = GameObject.Find("SmartIrrigationHub") ?? GameObject.Find("FarmSimulationHub");
            if (hub == null) return;
            var debug = hub.GetComponent<IrrigationSprayDebug>();
            if (debug == null)
            {
                Undo.AddComponent<IrrigationSprayDebug>(hub);
                Debug.Log("[Spray] Added IrrigationSprayDebug to " + hub.name +
                          " — every zone will be forced ON on Play. Disable in inspector to return to Auto.");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Scene-view live preview
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("Tools/Smart Farm/Preview Water Spray (Scene View)", priority = 31)]
        public static void PreviewSprayInSceneView()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[Spray] Scene-view preview is for Edit mode. Use Play mode for the real thing.");
                return;
            }

            var systems = CollectSpraySystems();
            if (systems.Count == 0)
            {
                EditorUtility.DisplayDialog("Preview Water Spray",
                    "No sprinkler particle systems found.\n\n" +
                    "Run 'Tools › Smart Farm › Rebuild Realistic Water Spray' first.",
                    "OK");
                return;
            }

            foreach (var ps in systems)
            {
                if (ps == null) continue;
                var emission = ps.emission;
                emission.enabled = true;
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                // Warm the simulation a bit so we don't start from an empty frame.
                ps.Simulate(0.6f, true, true);
                ps.Play(true);
            }

            EditorPrefs.SetBool(PreviewActivePrefKey, true);
            SceneView.RepaintAll();
            Debug.Log($"[Spray] Editor preview started on {systems.Count} particle system(s). " +
                      "Look at the Scene view to see the water spraying.");
        }

        [MenuItem("Tools/Smart Farm/Stop Water Spray Preview", priority = 32)]
        public static void StopSprayPreview()
        {
            var systems = CollectSpraySystems();
            foreach (var ps in systems)
            {
                if (ps == null) continue;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            EditorPrefs.SetBool(PreviewActivePrefKey, false);
            SceneView.RepaintAll();
            Debug.Log("[Spray] Editor preview stopped.");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Runtime force-on
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("Tools/Smart Farm/Test Spray (Force All Zones ON)", priority = 33)]
        public static void TestSprayForceOn()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Test Spray",
                    "Press Play first, then run this menu to force every zone into On mode " +
                    "so you can verify the runtime water spray.\n\n" +
                    "For Scene-view preview WITHOUT Play, use:\n" +
                    "  Tools › Smart Farm › Preview Water Spray (Scene View)",
                    "OK");
                return;
            }

            var mgr = SmartIrrigationTabletManager.Instance
                      ?? Object.FindFirstObjectByType<SmartIrrigationTabletManager>();
            if (mgr == null)
            {
                Debug.LogWarning("[Spray] No SmartIrrigationTabletManager in the scene.");
                return;
            }

            mgr.EnableAllZones();
            Debug.Log("[Spray] All zones forced ON. Run 'Test Spray (Restore Auto)' when done.");
        }

        [MenuItem("Tools/Smart Farm/Diagnose Spray (Play Mode)", priority = 35)]
        public static void DiagnoseSpray()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Diagnose Spray",
                    "Press Play first. This prints the runtime state of every zone " +
                    "and its sprinkler particle systems so you can see exactly why " +
                    "(or whether) water should be visible.", "OK");
                return;
            }

            var dbg = Object.FindFirstObjectByType<IrrigationSprayDebug>();
            if (dbg != null) { dbg.LogStatusOnce(); return; }

            var zones = Object.FindFirstObjectByType<IrrigationZoneManager>();
            if (zones == null) { Debug.LogWarning("[SprayDiagnose] No zone manager."); return; }

            zones.TryBindSceneVisualRoots();
            var sb = new System.Text.StringBuilder(256);
            sb.Append("[SprayDiagnose] ");
            for (int i = 0; i < zones.Zones.Count; i++)
            {
                var z = zones.Zones[i];
                if (z == null) continue;
                int systems = z.sprinklerRoot != null
                    ? z.sprinklerRoot.GetComponentsInChildren<ParticleSystem>(true).Length : 0;
                int alive = 0;
                if (z.sprinklerRoot != null)
                {
                    var arr = z.sprinklerRoot.GetComponentsInChildren<ParticleSystem>(true);
                    for (int j = 0; j < arr.Length; j++)
                        if (arr[j] != null) alive += arr[j].particleCount;
                }
                if (i > 0) sb.Append("  ·  ");
                sb.Append($"{z.displayName}: mode={z.mode}, flow={z.flowRate:F2}, " +
                          $"moist={z.averageMoisture:F0}%, systems={systems}, particles={alive}");
            }
            Debug.Log(sb.ToString());
        }

        [MenuItem("Tools/Smart Farm/Test Spray (Restore Auto)", priority = 34)]
        public static void TestSprayRestoreAuto()
        {
            if (!Application.isPlaying) return;

            var mgr = SmartIrrigationTabletManager.Instance
                      ?? Object.FindFirstObjectByType<SmartIrrigationTabletManager>();
            if (mgr == null) return;
            mgr.SetAllZonesAuto();
            Debug.Log("[Spray] All zones restored to Auto mode.");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        private const string PreviewActivePrefKey = "SmartFarm.Irrigation.SprayPreviewActive";

        private static List<ParticleSystem> CollectSpraySystems()
        {
            var result = new List<ParticleSystem>(64);
            var visualsRoot = GameObject.Find("SmartIrrigationSceneVisuals");
            if (visualsRoot == null) return result;

            for (int z = 0; z < visualsRoot.transform.childCount; z++)
            {
                var zoneRoot = visualsRoot.transform.GetChild(z);
                var sprinklerRoot = zoneRoot.Find("Sprinklers");
                if (sprinklerRoot == null) continue;
                var found = sprinklerRoot.GetComponentsInChildren<ParticleSystem>(true);
                for (int i = 0; i < found.Length; i++)
                    if (found[i] != null) result.Add(found[i]);
            }
            return result;
        }
    }
}
#endif
