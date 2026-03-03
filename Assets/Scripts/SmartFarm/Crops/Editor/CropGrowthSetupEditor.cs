using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace SmartFarm.Editor
{
    /// <summary>
    /// One-click setup for the CropGrowthController (Wheat + Corn) system.
    ///
    /// What it creates / wires:
    ///   1. Dead-stage placeholder prefabs  (wilted flat sphere)
    ///   2. Stage prefabs sourced from Wild Harvest NV3D models (with capsule fallback)
    ///   3. Wheat_CropData.asset + Corn_CropData.asset  (ScriptableObjects, all values pre-set)
    ///   4. CropPlot_Wheat.prefab + CropPlot_Corn.prefab  (CropGrowthController + SphereCollider)
    ///   5. GrowthManager component in scene  (added to FarmSimulationHub if present)
    ///   6. CropGrowthField root with 3 Wheat + 3 Corn plots placed at (10, 0, z)
    ///
    /// Also called automatically from SmartFarmSetupEditor.FullSetup().
    /// Menu: Tools > Smart Farm > Crops > Setup Crop Growth System
    /// </summary>
    public static class CropGrowthSetupEditor
    {
        // ── Paths ─────────────────────────────────────────────────────────────

        private const string CropDataPath    = "Assets/SmartFarm/CropData";
        private const string CropPrefabsPath = "Assets/SmartFarm/Prefabs/Crops";
        private const string WildHarvestPath = "Assets/NV3D/Wild Harvest/Grains/Prefabs/Plants";

        // ── Stage durations (seconds at 1× growth speed) ──────────────────────

        // Stage durations (seconds at 1× growth speed)
        private static readonly float[] WheatDurations = { 30f,  90f, 180f, 300f };
        private static readonly float[] CornDurations  = { 45f, 120f, 240f, 360f };

        // Wild Harvest stage indices: 15 stages available (1–15).
        // Start at stage 3 (visible small sprout) rather than stage 1 (flat disc seed)
        // so the Seed visual is immediately recognisable as a crop.
        private static readonly int[] WheatStageNums = { 3,  6, 10, 15 };
        private static readonly int[] CornStageNums  = { 3,  7, 11, 15 };

        // ─────────────────────────────────────────────────────────────────────
        //  Menu Items
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("Tools/Smart Farm/Crops/Setup Crop Growth System")]
        public static void SetupCropGrowthSystem()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[SmartFarm Crops] Stop Play mode before running setup.");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[SmartFarm Crops] Open a scene first.");
                return;
            }

            RunSetup();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log(
                "[SmartFarm Crops] Setup complete!\n" +
                "  • Wheat_CropData + Corn_CropData assets created in Assets/SmartFarm/CropData/\n" +
                "  • CropPlot prefabs in Assets/SmartFarm/Prefabs/Crops/\n" +
                "  • GrowthManager added to scene\n" +
                "  • CropGrowthField spawned at (10,0,0) — 3 Wheat + 3 Corn plots\n" +
                "Press Play to test!");
        }

        [MenuItem("Tools/Smart Farm/Crops/Respawn Crop Field")]
        private static void RespawnCropField()
        {
            if (Application.isPlaying) { Debug.LogWarning("[SmartFarm Crops] Stop Play mode first."); return; }

            var field = GameObject.Find("CropGrowthField");
            if (field != null) Undo.DestroyObjectImmediate(field);

            var wheatPlot = AssetDatabase.LoadAssetAtPath<GameObject>($"{CropPrefabsPath}/CropPlot_Wheat.prefab");
            var cornPlot  = AssetDatabase.LoadAssetAtPath<GameObject>($"{CropPrefabsPath}/CropPlot_Corn.prefab");

            if (wheatPlot == null || cornPlot == null)
            {
                Debug.LogWarning("[SmartFarm Crops] Prefabs not found. Run 'Setup Crop Growth System' first.");
                return;
            }

            SpawnCropField(wheatPlot, cornPlot);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[SmartFarm Crops] CropGrowthField respawned.");
        }

        [MenuItem("Tools/Smart Farm/Crops/Add GrowthManager to Scene")]
        private static void AddGrowthManagerMenuItem()
        {
            if (Application.isPlaying) { Debug.LogWarning("[SmartFarm Crops] Stop Play mode first."); return; }
            CreateOrFindGrowthManager();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        [MenuItem("Tools/Smart Farm/Crops/Repair Crop Wiring")]
        private static void RepairCropWiring()
        {
            if (Application.isPlaying) { Debug.LogWarning("[SmartFarm Crops] Stop Play mode first."); return; }

            var mgr    = Object.FindFirstObjectByType<GrowthManager>();
            var simMgr = Object.FindFirstObjectByType<FarmSimulationManager>();

            if (mgr == null) { Debug.LogWarning("[SmartFarm Crops] GrowthManager not found. Run Setup first."); return; }
            if (simMgr == null) { Debug.LogWarning("[SmartFarm Crops] FarmSimulationManager not found."); return; }

            var so = new SerializedObject(mgr);
            so.FindProperty("farmSimulationManager").objectReferenceValue = simMgr;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(mgr);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[SmartFarm Crops] GrowthManager → FarmSimulationManager wiring repaired.");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Core Setup (also called from SmartFarmSetupEditor.FullSetup)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Called by SmartFarmSetupEditor.FullSetup() at step 5c.
        /// Does not save assets or refresh database — caller is responsible.
        /// </summary>
        public static void RunSetup()
        {
            EnsureDirectories();

            // 1. Dead-stage placeholder prefabs
            var deadWheat = CreateOrFindDeadStagePrefab("CropDead_Wheat", new Color(0.45f, 0.35f, 0.15f, 1f));
            var deadCorn  = CreateOrFindDeadStagePrefab("CropDead_Corn",  new Color(0.50f, 0.38f, 0.12f, 1f));

            // 2. Stage prefabs — Wild Harvest first, capsule fallback if missing
            //    Uses stages 3,6,10,15 (wheat) and 3,7,11,15 (corn) so the Seed stage
            //    already looks like a recognisable small sprout, not a flat disc.
            var wheatStages = LoadOrFallbackStagePrefabs(
                "WheatPlants", WheatStageNums, deadWheat, new Color(0.85f, 0.82f, 0.30f));
            var cornStages = LoadOrFallbackStagePrefabs(
                "CornPlant",   CornStageNums,  deadCorn,  new Color(0.20f, 0.65f, 0.15f));

            // 3. CropData assets
            var wheatData = CreateOrUpdateCropData(
                "Wheat_CropData", CropType.Wheat, "Wheat", wheatStages, WheatDurations);
            var cornData = CreateOrUpdateCropData(
                "Corn_CropData", CropType.Corn, "Corn", cornStages, CornDurations);

            // 4. CropPlot prefabs
            var wheatPlot = CreateOrUpdateCropPlotPrefab("CropPlot_Wheat", wheatData);
            var cornPlot  = CreateOrUpdateCropPlotPrefab("CropPlot_Corn",  cornData);

            // 5. GrowthManager in scene
            CreateOrFindGrowthManager();

            // 6. Crop field
            SpawnCropField(wheatPlot, cornPlot);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Directory Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static void EnsureDirectories()
        {
            EnsureFolder("Assets",                  "SmartFarm");
            EnsureFolder("Assets/SmartFarm",        "CropData");
            EnsureFolder("Assets/SmartFarm",        "Prefabs");
            EnsureFolder("Assets/SmartFarm/Prefabs","Crops");
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
                AssetDatabase.CreateFolder(parent, name);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Dead Stage Placeholder
        // ─────────────────────────────────────────────────────────────────────

        private static GameObject CreateOrFindDeadStagePrefab(string prefabName, Color color)
        {
            string path = $"{CropPrefabsPath}/{prefabName}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            // Flat squashed sphere = wilted/dead plant on the ground
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = prefabName;
            go.transform.localScale = new Vector3(0.4f, 0.06f, 0.4f);

            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            go.GetComponent<Renderer>().sharedMaterial = CreateOrLoadMaterial(
                $"{CropPrefabsPath}/{prefabName}_Mat.mat", color);

            var saved = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return saved;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Stage Prefabs (Wild Harvest → capsule fallback)
        // ─────────────────────────────────────────────────────────────────────

        private static GameObject[] LoadOrFallbackStagePrefabs(
            string prefix, int[] stageNumbers, GameObject deadPrefab, Color fallbackColor)
        {
            var prefabs = new GameObject[5]; // [0]=Seed [1]=Sprout [2]=Young [3]=Mature [4]=Dead
            bool anyFound = false;

            for (int i = 0; i < stageNumbers.Length && i < 4; i++)
            {
                string p = $"{WildHarvestPath}/SM_{prefix}_{stageNumbers[i]}.prefab";
                prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (prefabs[i] != null) anyFound = true;
            }

            if (!anyFound)
                Debug.LogWarning(
                    $"[SmartFarm Crops] Wild Harvest models not found at '{WildHarvestPath}/SM_{prefix}_*.prefab'. " +
                    "Using placeholder capsules. Assign real models in the CropData Inspector later.");

            // Fill any missing slots with capsule placeholders
            float[] scaleXZ = { 0.08f, 0.12f, 0.20f, 0.26f };
            float[] scaleY  = { 0.12f, 0.30f, 0.58f, 0.92f };
            string[] labels = { "Seed", "Sprout", "Young", "Mature" };

            for (int i = 0; i < 4; i++)
            {
                if (prefabs[i] != null) continue;
                prefabs[i] = CreateFallbackStagePrefab(
                    $"CropFallback_{prefix}_{labels[i]}", fallbackColor, i, scaleXZ[i], scaleY[i]);
            }

            prefabs[4] = deadPrefab;
            return prefabs;
        }

        private static GameObject CreateFallbackStagePrefab(
            string name, Color baseColor, int stageIndex, float scaleXZ, float scaleY)
        {
            string path = $"{CropPrefabsPath}/{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.localScale = new Vector3(scaleXZ, scaleY, scaleXZ);

            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            // Darken colour at early stages, brighten at mature
            Color tint = Color.Lerp(
                new Color(baseColor.r * 0.45f, baseColor.g * 0.45f, baseColor.b * 0.25f),
                baseColor,
                stageIndex / 3f);

            go.GetComponent<Renderer>().sharedMaterial = CreateOrLoadMaterial(
                $"{CropPrefabsPath}/{name}_Mat.mat", tint);

            var saved = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return saved;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  CropData ScriptableObject
        // ─────────────────────────────────────────────────────────────────────

        private static CropData CreateOrUpdateCropData(
            string assetName, CropType cropType, string cropName,
            GameObject[] stagePrefabs, float[] stageDurations)
        {
            string path  = $"{CropDataPath}/{assetName}.asset";
            var asset    = AssetDatabase.LoadAssetAtPath<CropData>(path);
            bool isNew   = asset == null;

            if (isNew)
            {
                asset = ScriptableObject.CreateInstance<CropData>();
                AssetDatabase.CreateAsset(asset, path);
            }

            var so = new SerializedObject(asset);

            // Identity
            so.FindProperty("cropName").stringValue      = cropName;
            so.FindProperty("cropType").enumValueIndex   = (int)cropType;

            // Stage durations (always update so re-running fixes values)
            var durProp = so.FindProperty("stageDurations");
            durProp.arraySize = stageDurations.Length;
            for (int i = 0; i < stageDurations.Length; i++)
                durProp.GetArrayElementAtIndex(i).floatValue = stageDurations[i];

            // Stage prefabs (always update)
            var pfProp = so.FindProperty("stagePrefabs");
            int pfLen  = stagePrefabs != null ? stagePrefabs.Length : 5;
            pfProp.arraySize = pfLen;
            if (stagePrefabs != null)
                for (int i = 0; i < pfLen; i++)
                    pfProp.GetArrayElementAtIndex(i).objectReferenceValue = stagePrefabs[i];

            // Defaults — only written on first creation so the user can tweak later
            if (isNew)
            {
                so.FindProperty("waterIdealMin").floatValue                 = 40f;
                so.FindProperty("waterIdealMax").floatValue                 = 80f;
                so.FindProperty("tempIdealMin").floatValue                  = 15f;
                so.FindProperty("tempIdealMax").floatValue                  = 32f;
                so.FindProperty("soilMoistureDecayPerSecond").floatValue    = 1.5f;

                so.FindProperty("sunnyGrowthMultiplier").floatValue         = 1.5f;
                so.FindProperty("sunnyMoistureDecayBonus").floatValue       = 0.5f;

                so.FindProperty("rainyGrowthMultiplier").floatValue         = 1.0f;
                so.FindProperty("rainyMoistureGainPerSecond").floatValue    = 5f;
                so.FindProperty("rainyHealthRecoveryPerSecond").floatValue  = 2f;

                so.FindProperty("stormGrowthMultiplier").floatValue         = 0.4f;
                so.FindProperty("stormHealthDamagePerSecond").floatValue    = 3f;
                so.FindProperty("stormRandomDamageChance").floatValue       = 0.15f;
                so.FindProperty("stormRandomDamageAmount").floatValue       = 8f;
                so.FindProperty("stormMoistureGainPerSecond").floatValue    = 8f;

                so.FindProperty("healthDecayLowMoisture").floatValue        = 4f;
                so.FindProperty("moistureCriticalThreshold").floatValue     = 20f;
                so.FindProperty("healthDecayBadTemp").floatValue            = 2f;

                so.FindProperty("baseYield").floatValue        = cropType == CropType.Corn ? 150f : 100f;
                so.FindProperty("minYieldHealthRatio").floatValue = 0.3f;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  CropPlot Prefab
        // ─────────────────────────────────────────────────────────────────────

        private static GameObject CreateOrUpdateCropPlotPrefab(string prefabName, CropData cropData)
        {
            string path = $"{CropPrefabsPath}/{prefabName}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var root = new GameObject(prefabName);

            // StageHolder — stage models are instantiated here at runtime
            var holder = new GameObject("StageHolder");
            holder.transform.SetParent(root.transform, false);

            // CropGrowthController
            var controller = root.AddComponent<CropGrowthController>();
            var so = new SerializedObject(controller);
            so.FindProperty("cropData").objectReferenceValue         = cropData;
            so.FindProperty("stageHolder").objectReferenceValue      = holder.transform;
            so.FindProperty("initialHealth").floatValue              = 100f;
            so.FindProperty("initialSoilMoisture").floatValue        = 60f;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Trigger collider for VR grab / interaction
            var col    = root.AddComponent<SphereCollider>();
            col.radius  = 0.5f;
            col.isTrigger = true;

            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  GrowthManager in Scene
        // ─────────────────────────────────────────────────────────────────────

        private static GrowthManager CreateOrFindGrowthManager()
        {
            var existing = Object.FindFirstObjectByType<GrowthManager>();
            if (existing != null)
            {
                WireGrowthManager(existing);
                EnsureNetworkSyncComponents(existing.gameObject);
                return existing;
            }

            // Prefer to live on FarmSimulationHub (already has NetworkObject); fallback to dedicated GO
            var hub    = GameObject.Find("FarmSimulationHub");
            var target = hub ?? new GameObject("GrowthManager");

            if (hub == null)
                Undo.RegisterCreatedObjectUndo(target, "Create GrowthManager");

            var mgr = target.AddComponent<GrowthManager>();
            WireGrowthManager(mgr);
            EnsureNetworkSyncComponents(target);

            Debug.Log($"[SmartFarm Crops] GrowthManager added to '{target.name}'.");
            return mgr;
        }

        /// <summary>
        /// Ensures WeatherNetworkSync and CropFieldNetworkSync are present on the hub.
        /// Both require a NetworkObject on the same GameObject — FarmSimulationHub already has one.
        /// </summary>
        private static void EnsureNetworkSyncComponents(GameObject target)
        {
            // WeatherNetworkSync
            if (target.GetComponent<WeatherNetworkSync>() == null)
            {
                target.AddComponent<WeatherNetworkSync>();
                Debug.Log($"[SmartFarm Crops] WeatherNetworkSync added to '{target.name}'.");
            }

            // CropFieldNetworkSync
            if (target.GetComponent<CropFieldNetworkSync>() == null)
            {
                target.AddComponent<CropFieldNetworkSync>();
                Debug.Log($"[SmartFarm Crops] CropFieldNetworkSync added to '{target.name}'.");
            }
        }

        private static void WireGrowthManager(GrowthManager mgr)
        {
            var simMgr = Object.FindFirstObjectByType<FarmSimulationManager>();
            if (simMgr == null) return;

            var so = new SerializedObject(mgr);
            so.FindProperty("farmSimulationManager").objectReferenceValue = simMgr;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(mgr);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Crop Field
        // ─────────────────────────────────────────────────────────────────────

        private static void SpawnCropField(GameObject wheatPlotPrefab, GameObject cornPlotPrefab)
        {
            if (GameObject.Find("CropGrowthField") != null) return;

            var root = new GameObject("CropGrowthField");
            Undo.RegisterCreatedObjectUndo(root, "Create CropGrowthField");
            root.transform.position = new Vector3(10f, 0f, 0f);

            const int   count   = 3;
            const float spacing = 1.4f;

            for (int i = 0; i < count; i++)
            {
                if (wheatPlotPrefab == null) continue;
                var w = (GameObject)PrefabUtility.InstantiatePrefab(wheatPlotPrefab);
                w.name = $"Wheat_CropPlot_{i + 1}";
                w.transform.SetParent(root.transform);
                w.transform.position = root.transform.position + new Vector3(i * spacing, 0f, 0f);
            }

            for (int i = 0; i < count; i++)
            {
                if (cornPlotPrefab == null) continue;
                var c = (GameObject)PrefabUtility.InstantiatePrefab(cornPlotPrefab);
                c.name = $"Corn_CropPlot_{i + 1}";
                c.transform.SetParent(root.transform);
                c.transform.position = root.transform.position + new Vector3(i * spacing, 0f, spacing);
            }

            Debug.Log("[SmartFarm Crops] CropGrowthField spawned: 3 Wheat + 3 Corn plots.");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Shared Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static Material CreateOrLoadMaterial(string matPath, Color color)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit")
                      ?? Shader.Find("Standard")
                      ?? Shader.Find("Legacy Shaders/Diffuse");

            var mat = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(matPath) };

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);

            AssetDatabase.CreateAsset(mat, matPath);
            return mat;
        }

        private static void SetPrivateField(object obj, string fieldName, object value)
        {
            if (obj == null) return;
            var field = obj.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(obj, value);
        }
    }
}
