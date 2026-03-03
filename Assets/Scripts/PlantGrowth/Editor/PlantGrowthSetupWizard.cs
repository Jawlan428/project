using UnityEngine;
using UnityEditor;
using System.IO;

namespace PlantGrowth.Editor
{
    /// <summary>
    /// Farm setup menu. Use Tools > Farm > Farm Setup for full setup.
    /// </summary>
    public static class PlantGrowthSetupWizard
    {
        private const string DataPath = "Assets/PlantGrowth/Data";
        private const string StagePrefabsPath = "Assets/PlantGrowth/Prefabs/Stages";
        private const string MaterialsPath = "Assets/PlantGrowth/Materials";
        private const string WildHarvestPlantsPath = "Assets/NV3D/Wild Harvest/Grains/Prefabs/Plants";
        private const string WildHarvestOutputRoot = "Assets/PlantGrowth/WildHarvest";

        public static void ClearSaveData()
        {
            PlantSaveLoadService.DeleteSave();
            Debug.Log("[Farm] Save data cleared.");
        }

        public static void FixPlantMaterials()
        {
            EnsureDirectories();
            var materials = CreateOrLoadStageMaterials();
            var prefabNames = new[] { "Stage0_Seed", "Stage1_Sprout", "Stage2_Young", "Stage3_Mature", "Stage4_Dead" };
            for (int i = 0; i < 5 && i < materials.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path.Combine(StagePrefabsPath, prefabNames[i] + ".prefab"));
                if (prefab == null) continue;
                var prefabPath = AssetDatabase.GetAssetPath(prefab);
                var instance = PrefabUtility.LoadPrefabContents(prefabPath);
                var renderer = instance.GetComponent<Renderer>();
                if (renderer != null && materials[i] != null)
                {
                    renderer.sharedMaterial = materials[i];
                    PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                }
                PrefabUtility.UnloadPrefabContents(instance);
            }
            AssetDatabase.Refresh();
            Debug.Log("[Farm] Plant materials fixed.");
        }

        public static void EnsureDirectories()
        {
            EnsureFolder("Assets", "PlantGrowth");
            EnsureFolder("Assets/PlantGrowth", "Prefabs");
            EnsureFolder("Assets/PlantGrowth/Prefabs", "Stages");
            EnsureFolder("Assets/PlantGrowth", "Data");
            EnsureFolder("Assets/PlantGrowth", "Materials");
        }

        public static void CreatePlantAssetsIfMissing()
        {
            EnsureDirectories();
            var managerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PlantGrowth/Prefabs/PlantGrowthManager.prefab");
            var plantPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PlantGrowth/Prefabs/PlantInstance.prefab");
            if (managerPrefab != null && plantPrefab != null) return;

            PlantSaveLoadService.DeleteSave();
            CreateStagePrefabs();
            CreatePlantStageAsset();
            CreatePlantInstancePrefab();
            CreateManagerPrefab();
            FixPlantMaterials();
            FixPlantGrowth();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Creates Wild Harvest crop growth setup for Wheat and Corn:
        /// - Stage assets (WheatStage.asset, CornStage.asset)
        /// - Plant prefabs (PlantInstance_Wheat.prefab, PlantInstance_Corn.prefab)
        /// This reuses the existing PlantController/PlantGrowthManager system.
        /// </summary>
        public static void SetupWildHarvestCrops()
        {
            EnsureDirectories();
            EnsureFolder("Assets/PlantGrowth", "WildHarvest");
            EnsureFolder("Assets/PlantGrowth/WildHarvest", "Data");
            EnsureFolder("Assets/PlantGrowth/WildHarvest", "Prefabs");

            var wheatAsset = CreateOrUpdateWildHarvestStageAsset(
                "Assets/PlantGrowth/WildHarvest/Data/WheatStage.asset",
                "WheatPlants",
                new[] { 1, 5, 10, 15 },
                new[] { 20f, 35f, 55f, 80f },
                stageVisualScale: 1f);

            var cornAsset = CreateOrUpdateWildHarvestStageAsset(
                "Assets/PlantGrowth/WildHarvest/Data/CornStage.asset",
                "CornPlant",
                new[] { 1, 5, 10, 15 },
                new[] { 20f, 35f, 55f, 80f },
                stageVisualScale: 1f);

            if (wheatAsset != null)
                CreateOrUpdatePlantInstancePrefabForStageAsset(
                    "Assets/PlantGrowth/WildHarvest/Prefabs/PlantInstance_Wheat.prefab",
                    wheatAsset);

            if (cornAsset != null)
                CreateOrUpdatePlantInstancePrefabForStageAsset(
                    "Assets/PlantGrowth/WildHarvest/Prefabs/PlantInstance_Corn.prefab",
                    cornAsset);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Farm] Wild Harvest setup complete. Created Wheat/Corn stage assets and plant prefabs in Assets/PlantGrowth/WildHarvest.");
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }

        private static void CreateStagePrefabs()
        {
            var materials = CreateOrLoadStageMaterials();
            var names = new[] { "Stage0_Seed", "Stage1_Sprout", "Stage2_Young", "Stage3_Mature", "Stage4_Dead" };
            var scales = new[] { 0.15f, 0.35f, 0.6f, 1f, 0.8f };
            for (int i = 0; i < 5; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = names[i];
                go.transform.localScale = new Vector3(scales[i] * 0.5f, scales[i], scales[i] * 0.5f);
                if (materials[i] != null)
                    go.GetComponent<Renderer>().sharedMaterial = materials[i];
                string path = Path.Combine(StagePrefabsPath, names[i] + ".prefab");
                PrefabUtility.SaveAsPrefabAsset(go, path);
                Object.DestroyImmediate(go);
            }
        }

        private static Material[] CreateOrLoadStageMaterials()
        {
            EnsureFolder("Assets/PlantGrowth", "Materials");
            var colors = new[] {
                new Color(0.4f, 0.25f, 0.1f),
                new Color(0.2f, 0.6f, 0.2f),
                new Color(0.1f, 0.7f, 0.15f),
                new Color(0.15f, 0.5f, 0.1f),
                new Color(0.3f, 0.2f, 0.1f)
            };
            var names = new[] { "PlantStage_Seed", "PlantStage_Sprout", "PlantStage_Young", "PlantStage_Mature", "PlantStage_Dead" };
            var result = new Material[5];
            var refMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Samples/XR Interaction Toolkit/3.2.2/Starter Assets/DemoSceneAssets/Materials/Lit White.mat")
                ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/VRMPAssets/Materials/Black.mat");
            if (refMat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
                refMat = new Material(shader ?? Shader.Find("Legacy Shaders/Diffuse"));
            }
            for (int i = 0; i < 5; i++)
            {
                string matPath = Path.Combine(MaterialsPath, names[i] + ".mat");
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    mat = new Material(refMat);
                    mat.name = names[i];
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", colors[i]);
                    else if (mat.HasProperty("_Color")) mat.SetColor("_Color", colors[i]);
                    else mat.color = colors[i];
                    AssetDatabase.CreateAsset(mat, matPath);
                }
                result[i] = mat;
            }
            AssetDatabase.SaveAssets();
            return result;
        }

        private static void CreatePlantStageAsset()
        {
            var asset = ScriptableObject.CreateInstance<PlantStageAsset>();
            asset.stageDurations = new[] { 5f, 10f, 15f, 20f };
            asset.hasDeadStage = false;
            asset.waterDecayPerSecond = 0.5f;
            asset.fertilizerDecayPerSecond = 0.2f;
            var stagePaths = new[] {
                "Assets/PlantGrowth/Prefabs/Stages/Stage0_Seed.prefab",
                "Assets/PlantGrowth/Prefabs/Stages/Stage1_Sprout.prefab",
                "Assets/PlantGrowth/Prefabs/Stages/Stage2_Young.prefab",
                "Assets/PlantGrowth/Prefabs/Stages/Stage3_Mature.prefab"
            };
            asset.stagePrefabs = new GameObject[4];
            for (int i = 0; i < 4; i++)
                asset.stagePrefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(stagePaths[i]);
            string assetPath = Path.Combine(DataPath, "DefaultPlantStage.asset");
            AssetDatabase.CreateAsset(asset, assetPath);
        }

        private static void CreatePlantInstancePrefab()
        {
            var root = new GameObject("PlantInstance");
            var holder = new GameObject("StageHolder");
            holder.transform.SetParent(root.transform, false);
            var controller = root.AddComponent<PlantController>();
            var stageAsset = AssetDatabase.LoadAssetAtPath<PlantStageAsset>(Path.Combine(DataPath, "DefaultPlantStage.asset"));
            var so = new SerializedObject(controller);
            so.FindProperty("stageHolder").objectReferenceValue = holder.transform;
            so.FindProperty("stageAsset").objectReferenceValue = stageAsset;
            so.ApplyModifiedPropertiesWithoutUndo();
            var col = root.AddComponent<SphereCollider>();
            col.radius = 0.5f;
            col.isTrigger = true;
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/PlantGrowth/Prefabs/PlantInstance.prefab");
            Object.DestroyImmediate(root);
        }

        private static void CreateManagerPrefab()
        {
            var go = new GameObject("PlantGrowthManager");
            go.AddComponent<PlantGrowthManager>();
            PrefabUtility.SaveAsPrefabAsset(go, "Assets/PlantGrowth/Prefabs/PlantGrowthManager.prefab");
            Object.DestroyImmediate(go);
        }

        private static void FixPlantGrowth()
        {
            var stageAsset = AssetDatabase.LoadAssetAtPath<PlantStageAsset>(Path.Combine(DataPath, "DefaultPlantStage.asset"));
            if (stageAsset == null) return;
            var so = new SerializedObject(stageAsset);
            so.FindProperty("stageDurations").arraySize = 4;
            so.FindProperty("stageDurations").GetArrayElementAtIndex(0).floatValue = 5f;
            so.FindProperty("stageDurations").GetArrayElementAtIndex(1).floatValue = 10f;
            so.FindProperty("stageDurations").GetArrayElementAtIndex(2).floatValue = 15f;
            so.FindProperty("stageDurations").GetArrayElementAtIndex(3).floatValue = 20f;
            so.FindProperty("waterDecayPerSecond").floatValue = 0.5f;
            so.FindProperty("fertilizerDecayPerSecond").floatValue = 0.2f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(stageAsset);
            AssetDatabase.SaveAssets();
        }

        private static PlantStageAsset CreateOrUpdateWildHarvestStageAsset(
            string assetPath,
            string wildHarvestPrefix,
            int[] stageNumbers,
            float[] stageDurations,
            float stageVisualScale)
        {
            if (stageNumbers == null || stageDurations == null || stageNumbers.Length == 0 || stageNumbers.Length != stageDurations.Length)
            {
                Debug.LogError("[Farm] Invalid Wild Harvest stage config.");
                return null;
            }

            var prefabs = new GameObject[stageNumbers.Length];
            for (int i = 0; i < stageNumbers.Length; i++)
            {
                string prefabPath = $"{WildHarvestPlantsPath}/SM_{wildHarvestPrefix}_{stageNumbers[i]}.prefab";
                prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefabs[i] == null)
                {
                    Debug.LogError($"[Farm] Missing Wild Harvest prefab: {prefabPath}");
                    return null;
                }
            }

            var asset = AssetDatabase.LoadAssetAtPath<PlantStageAsset>(assetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<PlantStageAsset>();
                AssetDatabase.CreateAsset(asset, assetPath);
            }

            var so = new SerializedObject(asset);
            so.FindProperty("hasDeadStage").boolValue = false;
            so.FindProperty("stageVisualScale").floatValue = stageVisualScale;
            so.FindProperty("waterDecayPerSecond").floatValue = 0.5f;
            so.FindProperty("fertilizerDecayPerSecond").floatValue = 0.2f;

            var durationsProp = so.FindProperty("stageDurations");
            durationsProp.arraySize = stageDurations.Length;
            for (int i = 0; i < stageDurations.Length; i++)
                durationsProp.GetArrayElementAtIndex(i).floatValue = stageDurations[i];

            var prefabsProp = so.FindProperty("stagePrefabs");
            prefabsProp.arraySize = prefabs.Length;
            for (int i = 0; i < prefabs.Length; i++)
                prefabsProp.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void CreateOrUpdatePlantInstancePrefabForStageAsset(string outputPrefabPath, PlantStageAsset stageAsset)
        {
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PlantGrowth/Prefabs/PlantInstance.prefab");
            if (basePrefab == null)
            {
                Debug.LogError("[Farm] Base PlantInstance prefab not found at Assets/PlantGrowth/Prefabs/PlantInstance.prefab");
                return;
            }

            string basePath = AssetDatabase.GetAssetPath(basePrefab);
            var root = PrefabUtility.LoadPrefabContents(basePath);
            if (root == null) return;

            try
            {
                var controller = root.GetComponent<PlantController>();
                if (controller == null)
                    controller = root.AddComponent<PlantController>();

                Transform stageHolder = root.transform.Find("StageHolder");
                if (stageHolder == null)
                {
                    var holderGO = new GameObject("StageHolder");
                    holderGO.transform.SetParent(root.transform, false);
                    stageHolder = holderGO.transform;
                }

                var so = new SerializedObject(controller);
                so.FindProperty("stageAsset").objectReferenceValue = stageAsset;
                so.FindProperty("stageHolder").objectReferenceValue = stageHolder;
                so.ApplyModifiedPropertiesWithoutUndo();

                root.name = Path.GetFileNameWithoutExtension(outputPrefabPath);
                PrefabUtility.SaveAsPrefabAsset(root, outputPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
