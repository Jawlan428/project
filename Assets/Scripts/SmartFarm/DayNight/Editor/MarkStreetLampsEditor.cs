using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SmartFarm.DayNight.Editor
{
    /// <summary>
    /// Selection-based helper for wiring arbitrary lamps into the Day &amp; Night
    /// system, regardless of how they are named. Select the lamp GameObjects you
    /// added to the scene (each lamp's root) in the Hierarchy, then run
    /// <c>Tools &gt; Smart Farm &gt; Mark Selected As Night Lamps</c>.
    ///
    /// For every selected object this:
    ///   1. Ensures a <see cref="DayNightModeManager"/> + <see cref="StreetLampManager"/>
    ///      exist (creates the <c>DayNightHub</c> if missing).
    ///   2. Adds a <see cref="StreetLamp"/> component if missing.
    ///   3. Reuses a child <see cref="Light"/> or creates a warm Point Light at the
    ///      lamp head (starts at 0 intensity — the manager lights it at night).
    ///   4. Finds a likely emissive "bulb" renderer to glow at night.
    ///   5. Rebuilds the manager's lamp list so the new lamps follow night mode.
    /// </summary>
    public static class MarkStreetLampsEditor
    {
        [MenuItem("Tools/Smart Farm/Mark Selected As Night Lamps", priority = 32)]
        public static void MarkSelected()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[DayNight] Stop Play mode before marking lamps.");
                return;
            }

            var selection = Selection.gameObjects;
            if (selection == null || selection.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Mark Selected As Night Lamps",
                    "Select one or more lamp GameObjects in the Hierarchy first, then run this again.",
                    "OK");
                return;
            }

            var lampMgr = EnsureLampManager(out var hub);
            int wired = 0;

            foreach (var go in selection)
            {
                if (go == null) continue;

                var lampComp = go.GetComponent<StreetLamp>();
                if (lampComp == null) lampComp = Undo.AddComponent<StreetLamp>(go);

                var light = go.GetComponentInChildren<Light>(true);
                if (light == null) light = CreateLampLight(go);
                else               TuneLampLight(light);

                var bulb = FindBulbRenderer(go);

                SetField(lampComp, "lampLight",    light);
                SetField(lampComp, "bulbRenderer", bulb);

                wired++;
            }

            // Rebuild the manager's lamp list to include every StreetLamp in the scene
            // (so previously-wired lamps are not dropped).
            var all = Object.FindObjectsByType<StreetLamp>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            SetField(lampMgr, "lamps", new List<StreetLamp>(all));

            EditorUtility.SetDirty(lampMgr);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(lampMgr.gameObject.scene);

            Debug.Log($"[DayNight] Marked {wired} lamp(s) as night lamps. " +
                      $"Manager now controls {all.Length} lamp(s). " +
                      $"Press Play and switch to Night mode to see them light up.");

            Selection.activeGameObject = hub;
        }

        // ─────────────────────────────────────────────────────────────────────

        private static StreetLampManager EnsureLampManager(out GameObject hub)
        {
            hub = GameObject.Find("DayNightHub");
            if (hub == null)
            {
                hub = new GameObject("DayNightHub");
                Undo.RegisterCreatedObjectUndo(hub, "DayNightHub");
            }

            var manager = hub.GetComponent<DayNightModeManager>();
            if (manager == null) manager = Undo.AddComponent<DayNightModeManager>(hub);

            var lampMgr = hub.GetComponent<StreetLampManager>();
            if (lampMgr == null) lampMgr = Undo.AddComponent<StreetLampManager>(hub);

            SetField(lampMgr, "manager", manager);
            return lampMgr;
        }

        private static Light CreateLampLight(GameObject lampRoot)
        {
            Vector3 placement = lampRoot.transform.position + Vector3.up * 3f;
            var renderers = lampRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers != null && renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                placement = new Vector3(b.center.x, b.max.y - 0.25f, b.center.z);
            }

            var lightGO = new GameObject("LampLight");
            Undo.RegisterCreatedObjectUndo(lightGO, "Lamp Light");
            lightGO.transform.SetParent(lampRoot.transform, true);
            lightGO.transform.position = placement;

            var light = lightGO.AddComponent<Light>();
            light.type             = LightType.Point;
            light.color            = new Color(1.00f, 0.78f, 0.42f);
            light.intensity        = 0f; // manager turns this on at night
            light.range            = 9f;
            light.shadows          = LightShadows.Soft;
            light.shadowStrength   = 0.5f;
            light.shadowResolution = UnityEngine.Rendering.LightShadowResolution.Low;
            light.renderMode       = LightRenderMode.Auto;
            light.bounceIntensity  = 0f; // realtime only — Quest VR friendly
            light.lightmapBakeType = LightmapBakeType.Realtime;
            return light;
        }

        private static void TuneLampLight(Light light)
        {
            if (light == null) return;
            if (light.range < 1f) light.range = 9f;
            if (light.shadows == LightShadows.Hard) light.shadows = LightShadows.Soft;
            light.shadowResolution = UnityEngine.Rendering.LightShadowResolution.Low;
            light.bounceIntensity  = 0f;
            light.lightmapBakeType = LightmapBakeType.Realtime;
        }

        private static Renderer FindBulbRenderer(GameObject lampRoot)
        {
            var renderers = lampRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                string n = renderers[i].gameObject.name.ToLowerInvariant();
                if (n.Contains("bulb") || n.Contains("glass") || n.Contains("light") || n.Contains("head") || n.Contains("emit"))
                    return renderers[i];
            }
            Renderer best = null;
            float bestY = float.NegativeInfinity;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                float y = renderers[i].bounds.max.y;
                if (y > bestY) { bestY = y; best = renderers[i]; }
            }
            return best;
        }

        private static void SetField(object obj, string field, object value)
        {
            if (obj == null) return;
            var f = obj.GetType().GetField(field,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            f?.SetValue(obj, value);
        }
    }
}
