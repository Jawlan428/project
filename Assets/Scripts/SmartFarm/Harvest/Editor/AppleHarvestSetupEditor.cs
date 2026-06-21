using System.Collections.Generic;
using SmartFarm.Harvest;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using XRGrabInteractable = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;

namespace SmartFarm.HarvestEditor
{
    /// <summary>
    /// One-click setup for the Apple Harvest Status labels.
    ///
    /// Builds the experience in the active scene:
    ///   1. Finds apples (current selection ▸ existing <c>AppleGrabHandler</c> ▸
    ///      objects named "apple"). If none exist it spawns a small demo orchard
    ///      row from the <c>food_Apple</c> prefab.
    ///   2. Ensures every apple has the required components: Rigidbody, Collider,
    ///      XRGrabInteractable, AppleGrabHandler and <see cref="AppleHarvest"/>.
    ///   3. Distributes ripeness (a number marked "Ready", the rest "Not Ready").
    ///
    /// Each apple then shows a floating ready / not-ready label when the player
    /// gets close. There is no objective HUD / counter.
    ///
    /// Menu: Tools ▸ Smart Farm ▸ Harvest ▸ Setup Apple Harvest System
    /// (also surfaced under Tools ▸ Farm).
    /// </summary>
    public static class AppleHarvestSetupEditor
    {
        private const string ManagerObjectName = "AppleHarvestManager";
        private const string HudObjectName = "AppleHarvestHUD";
        private const string ApplePrefabPath =
            "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/food_Apple.prefab";

        // How many apples are marked ripe by default when running setup.
        private const int DefaultReadyCount = 5;

        [MenuItem("Tools/Smart Farm/Harvest/Setup Apple Harvest System")]
        public static void SetupAppleHarvestSystem()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[AppleHarvest] Stop Play mode before running setup.");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[AppleHarvest] Open a scene first.");
                return;
            }

            RunSetup();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[AppleHarvest] Apple Harvest System setup complete!");
        }

        [MenuItem("Tools/Smart Farm/Harvest/Mark Selected Apples Ready")]
        public static void MarkSelectedReady() => SetSelectionState(AppleHarvestState.ReadyToHarvest);

        [MenuItem("Tools/Smart Farm/Harvest/Mark Selected Apples Not Ready")]
        public static void MarkSelectedNotReady() => SetSelectionState(AppleHarvestState.NotReadyYet);

        [MenuItem("Tools/Smart Farm/Harvest/Remove Harvest HUD / Counter")]
        public static void RemoveHarvestHud()
        {
            int removed = 0;

            // Destroy the old HUD panel and standalone manager GameObjects by name.
            foreach (var name in new[] { HudObjectName, ManagerObjectName })
            {
                var go = GameObject.Find(name);
                if (go != null)
                {
                    Undo.DestroyObjectImmediate(go);
                    removed++;
                }
            }

            // Clean up any leftover "missing script" components (e.g. the old
            // AppleHarvestManager that may have lived on FarmSimulationHub).
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[AppleHarvest] Removed {removed} HUD/counter object(s) and cleaned missing scripts.");
        }

        // ─────────────────────────────────────────────────────────────────────

        public static void RunSetup()
        {
            var apples = FindOrSpawnApples();
            if (apples.Count == 0)
            {
                Debug.LogWarning("[AppleHarvest] No apples found and the food_Apple prefab is missing - nothing to set up.");
                return;
            }

            int readyTarget = Mathf.Clamp(DefaultReadyCount, 1, apples.Count);
            int readyAssigned = 0;
            foreach (var apple in apples)
            {
                var harvest = EnsureAppleComponents(apple);
                // First N become ripe, the rest stay unripe so the player must choose.
                var state = readyAssigned < readyTarget
                    ? AppleHarvestState.ReadyToHarvest
                    : AppleHarvestState.NotReadyYet;
                harvest.SetState(state);
                if (state == AppleHarvestState.ReadyToHarvest) readyAssigned++;
                EditorUtility.SetDirty(harvest);
            }

            // Remove any HUD / counter left over from an earlier setup.
            RemoveHarvestHud();

            Debug.Log($"[AppleHarvest] Configured {apples.Count} apples ({readyAssigned} ready, {apples.Count - readyAssigned} not ready). Each shows a ready / not-ready label up close.");
        }

        // ── apple discovery / spawning ───────────────────────────────────────
        private static List<GameObject> FindOrSpawnApples()
        {
            var result = new List<GameObject>();

            // 1) Honour an explicit selection of objects in the scene.
            foreach (var go in Selection.gameObjects)
                if (go != null && go.scene.IsValid() && LooksLikeApple(go))
                    result.Add(go);
            if (result.Count > 0) return Dedupe(result);

            // 2) Existing apples already carrying the grab handler.
            foreach (var handler in Object.FindObjectsByType<AppleGrabHandler>(FindObjectsSortMode.None))
                result.Add(handler.gameObject);
            if (result.Count > 0) return Dedupe(result);

            // 3) Objects named like an apple.
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (LooksLikeApple(t.gameObject))
                    result.Add(t.gameObject);
            if (result.Count > 0) return Dedupe(result);

            // 4) Nothing in the scene – spawn a small demo orchard row.
            return SpawnDemoApples();
        }

        private static bool LooksLikeApple(GameObject go)
        {
            string n = go.name.ToLowerInvariant();
            return n.Contains("apple") && go.GetComponentInChildren<MeshRenderer>() != null;
        }

        private static List<GameObject> Dedupe(List<GameObject> list)
        {
            var seen = new HashSet<GameObject>();
            var outList = new List<GameObject>();
            foreach (var g in list)
                if (g != null && seen.Add(g)) outList.Add(g);
            return outList;
        }

        private static List<GameObject> SpawnDemoApples()
        {
            var result = new List<GameObject>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ApplePrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[AppleHarvest] Apple prefab not found at {ApplePrefabPath}. Place some apples in the scene and run setup again.");
                return result;
            }

            var root = new GameObject("DemoOrchardApples");
            Undo.RegisterCreatedObjectUndo(root, "Demo Orchard");

            Vector3 origin = new Vector3(0f, 1.3f, 2.5f);
            if (Camera.main != null)
                origin = Camera.main.transform.position + Camera.main.transform.forward * 2.5f + Vector3.up * 0.1f;

            const int count = 8; // 5 ripe + 3 unripe via the ready-target logic.
            for (int i = 0; i < count; i++)
            {
                var apple = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                apple.name = $"Apple_{i + 1:00}";
                apple.transform.SetParent(root.transform, true);
                float col = i % 4;
                float rowZ = i / 4;
                apple.transform.position = origin + new Vector3((col - 1.5f) * 0.4f, 0f, rowZ * 0.45f);
                Undo.RegisterCreatedObjectUndo(apple, "Spawn Apple");
                result.Add(apple);
            }

            Debug.Log("[AppleHarvest] No apples found - spawned a demo orchard row of 8 apples. Reposition them onto your trees as needed.");
            return result;
        }

        // ── component wiring ─────────────────────────────────────────────────
        private static AppleHarvest EnsureAppleComponents(GameObject apple)
        {
            // Collider (needed for grabbing). The food_Apple prefab ships with a
            // convex MeshCollider; add a sphere collider as a fallback.
            if (apple.GetComponentInChildren<Collider>() == null)
            {
                var sc = apple.AddComponent<SphereCollider>();
                sc.radius = 0.06f;
            }

            var rb = apple.GetComponent<Rigidbody>();
            if (rb == null) rb = apple.AddComponent<Rigidbody>();
            rb.mass = 0.2f;
            rb.useGravity = true;
            // Start held in place; AppleGrabHandler frees physics on a valid grab.
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            if (apple.GetComponent<XRGrabInteractable>() == null)
            {
                var grab = apple.AddComponent<XRGrabInteractable>();
                grab.useDynamicAttach = true;
                grab.throwOnDetach = true;
            }

            if (apple.GetComponent<AppleGrabHandler>() == null)
                apple.AddComponent<AppleGrabHandler>();

            var harvest = apple.GetComponent<AppleHarvest>();
            if (harvest == null) harvest = apple.AddComponent<AppleHarvest>();

            // Keep the trees clean (no glow), and make sure the label triggers at a
            // comfortable distance. Status is shown by the floating label only.
            var so = new SerializedObject(harvest);
            var glowProp = so.FindProperty("useGlow");
            if (glowProp != null) glowProp.boolValue = false;
            var distProp = so.FindProperty("showDistance");
            if (distProp != null && distProp.floatValue < 3f) distProp.floatValue = 3f;
            so.ApplyModifiedProperties();

            return harvest;
        }

        private static void SetSelectionState(AppleHarvestState state)
        {
            int changed = 0;
            foreach (var go in Selection.gameObjects)
            {
                var harvest = go != null ? go.GetComponent<AppleHarvest>() : null;
                if (harvest == null && go != null) harvest = EnsureAppleComponents(go);
                if (harvest != null)
                {
                    harvest.SetState(state);
                    EditorUtility.SetDirty(harvest);
                    changed++;
                }
            }
            if (changed > 0)
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[AppleHarvest] Marked {changed} apple(s) as {state}.");
        }
    }
}
