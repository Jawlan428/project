using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

namespace PlantGrowth.Editor
{
    /// <summary>
    /// Editor wizard to create Plant Growth System assets and prefabs.
    /// Menu: Tools > Plant Growth > Setup Wizard
    /// </summary>
    public static class PlantGrowthSetupWizard
    {
        private const string BasePath = "Assets/PlantGrowth";
        private const string PrefabsPath = "Assets/PlantGrowth/Prefabs";
        private const string StagePrefabsPath = "Assets/PlantGrowth/Prefabs/Stages";
        private const string DataPath = "Assets/PlantGrowth/Data";
        private const string MaterialsPath = "Assets/PlantGrowth/Materials";

        [MenuItem("Tools/Plant Growth/Add Watermelon Plants to SampleScene")]
        public static void AddWatermelonToSampleScene()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[PlantGrowth] Stop Play mode first!");
                return;
            }
            const string sampleScenePath = "Assets/Scenes/SampleScene.unity";
            if (!File.Exists(sampleScenePath))
            {
                Debug.LogError($"[PlantGrowth] SampleScene not found at {sampleScenePath}");
                return;
            }
            var managerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PlantGrowth/Prefabs/PlantGrowthManager.prefab");
            var plantPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PlantGrowth/Prefabs/PlantInstance.prefab");
            if (managerPrefab == null || plantPrefab == null)
            {
                Debug.LogError("[PlantGrowth] Prefabs not found. Run 'FULL WATERMELON SETUP' first.");
                return;
            }
            EditorSceneManager.OpenScene(sampleScenePath);
            RemoveExistingPlantSetup();
            var managerGo = (GameObject)PrefabUtility.InstantiatePrefab(managerPrefab);
            managerGo.name = "PlantGrowthManager";
            var positions = new[] { new Vector3(0, 0, 0), new Vector3(1.5f, 0, 0), new Vector3(3f, 0, 0) };
            for (int i = 0; i < 3; i++)
            {
                var plantGo = (GameObject)PrefabUtility.InstantiatePrefab(plantPrefab);
                plantGo.name = "WatermelonPlant_" + (i + 1);
                plantGo.transform.position = positions[i];
            }
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[PlantGrowth] Added 3 watermelon plants to SampleScene. Save the scene (Ctrl+S) if needed.");
        }

        [MenuItem("Tools/Plant Growth/FULL WATERMELON SETUP (one click, no manual steps)")]
        public static void FullWatermelonSetup()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[PlantGrowth] Stop Play mode first!");
                return;
            }
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[PlantGrowth] Open a scene first.");
                return;
            }
            const string packPath = "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs";
            var pandazolePaths = new[] {
                packPath + "/Env_GrassPlant_01.prefab",
                packPath + "/Env_GrassPlant_03.prefab",
                packPath + "/Env_GrassPlant_06.prefab",
                packPath + "/food_Watermelon.prefab"
            };
            for (int i = 0; i < 4; i++)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(pandazolePaths[i]) == null)
                {
                    Debug.LogError($"[PlantGrowth] Pandazole pack not found: {pandazolePaths[i]}. Import Pandazole Farm Ranch Pack first.");
                    return;
                }
            }
            EnsureDirectories();
            PlantGrowth.PlantSaveLoadService.DeleteSave();
            CreateStagePrefabs();
            CreatePlantStageAsset();
            UseWatermelonPlantInternal(pandazolePaths);
            CreatePlantInstancePrefab();
            CreateManagerPrefab();
            FixPlantGrowth();
            AssetDatabase.Refresh();
            RemoveExistingPlantSetup();
            AddFullSetupToScene();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[PlantGrowth] FULL WATERMELON SETUP complete! Press Play - 3 watermelon plants will grow from sprout to watermelon.");
        }

        private static void RemoveExistingPlantSetup()
        {
            var manager = Object.FindObjectOfType<PlantGrowth.PlantGrowthManager>();
            if (manager != null) Object.DestroyImmediate(manager.gameObject);
            var plants = Object.FindObjectsOfType<PlantGrowth.PlantController>();
            foreach (var p in plants) if (p != null) Object.DestroyImmediate(p.gameObject);
        }

        [MenuItem("Tools/Plant Growth/FULL WATERMELON SETUP - New Scene")]
        public static void FullWatermelonSetupNewScene()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[PlantGrowth] Stop Play mode first!");
                return;
            }
            const string packPath = "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs";
            var pandazolePaths = new[] {
                packPath + "/Env_GrassPlant_01.prefab",
                packPath + "/Env_GrassPlant_03.prefab",
                packPath + "/Env_GrassPlant_06.prefab",
                packPath + "/food_Watermelon.prefab"
            };
            for (int i = 0; i < 4; i++)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(pandazolePaths[i]) == null)
                {
                    Debug.LogError($"[PlantGrowth] Pandazole pack not found. Import Pandazole Farm Ranch Pack first.");
                    return;
                }
            }
            EnsureDirectories();
            PlantGrowth.PlantSaveLoadService.DeleteSave();
            CreateStagePrefabs();
            CreatePlantStageAsset();
            UseWatermelonPlantInternal(pandazolePaths);
            CreatePlantInstancePrefab();
            CreateManagerPrefab();
            FixPlantGrowth();
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(1.5f, 1.5f, -4f);
                cam.transform.LookAt(new Vector3(1.5f, 0, 0));
            }
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = new Vector3(1.5f, -0.5f, 0);
            ground.transform.localScale = new Vector3(2, 1, 2);

            var managerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PlantGrowth/Prefabs/PlantGrowthManager.prefab");
            var plantPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PlantGrowth/Prefabs/PlantInstance.prefab");
            var positions = new[] { new Vector3(0.5f, 0, 0), new Vector3(2f, 0, 0), new Vector3(3.5f, 0, 0) };
            var managerGo = (GameObject)PrefabUtility.InstantiatePrefab(managerPrefab);
            managerGo.name = "PlantGrowthManager";
            for (int i = 0; i < 3; i++)
            {
                var plantGo = (GameObject)PrefabUtility.InstantiatePrefab(plantPrefab);
                plantGo.name = "WatermelonPlant_" + (i + 1);
                plantGo.transform.position = positions[i];
            }

            string scenePath = "Assets/PlantGrowth/WatermelonFarmScene.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.OpenScene(scenePath);
            Debug.Log("[PlantGrowth] New Watermelon Farm scene created and opened. Press Play!");
        }

        private static void UseWatermelonPlantInternal(string[] stagePaths)
        {
            var stagePrefabs = new GameObject[4];
            for (int i = 0; i < 4; i++)
                stagePrefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(stagePaths[i]);
            var stageAsset = AssetDatabase.LoadAssetAtPath<PlantGrowth.PlantStageAsset>(Path.Combine(DataPath, "DefaultPlantStage.asset"));
            if (stageAsset == null) return;
            var so = new SerializedObject(stageAsset);
            so.FindProperty("stagePrefabs").arraySize = 4;
            for (int i = 0; i < 4; i++)
                so.FindProperty("stagePrefabs").GetArrayElementAtIndex(i).objectReferenceValue = stagePrefabs[i];
            so.FindProperty("stageDurations").arraySize = 4;
            so.FindProperty("stageDurations").GetArrayElementAtIndex(0).floatValue = 5f;
            so.FindProperty("stageDurations").GetArrayElementAtIndex(1).floatValue = 10f;
            so.FindProperty("stageDurations").GetArrayElementAtIndex(2).floatValue = 15f;
            so.FindProperty("stageDurations").GetArrayElementAtIndex(3).floatValue = 20f;
            so.FindProperty("waterDecayPerSecond").floatValue = 0.5f;
            so.FindProperty("fertilizerDecayPerSecond").floatValue = 0.2f;
            var scaleProp = so.FindProperty("stageVisualScale");
            if (scaleProp != null) scaleProp.floatValue = 2f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(stageAsset);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Plant Growth/COMPLETE SETUP - Do This First!")]
        public static void CompleteSetup()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[PlantGrowth] Open a scene first, then run this.");
                return;
            }
            EnsureDirectories();
            PlantGrowth.PlantSaveLoadService.DeleteSave();
            CreateStagePrefabs();
            CreatePlantStageAsset();
            CreatePlantInstancePrefab();
            CreateManagerPrefab();
            FixPlantMaterials();
            FixPlantGrowth();
            AssetDatabase.Refresh();
            AddFullSetupToScene();
            Debug.Log("[PlantGrowth] COMPLETE SETUP done! Press Play - you should see 3 colored plants that grow. Use 'Use Watermelon Plant' for watermelon instead.");
        }

        [MenuItem("Tools/Plant Growth/Add Full Setup to Current Scene")]
        public static void AddFullSetupToScene()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[PlantGrowth] No active scene. Open a scene first.");
                return;
            }

            // Load prefabs
            var managerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PlantGrowth/Prefabs/PlantGrowthManager.prefab");
            var plantPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PlantGrowth/Prefabs/PlantInstance.prefab");

            if (managerPrefab == null || plantPrefab == null)
            {
                Debug.LogError("[PlantGrowth] Prefabs not found. Run 'Setup Wizard - Create All Assets' first.");
                return;
            }

            // Check if Manager already exists
            var existingManager = Object.FindObjectOfType<PlantGrowth.PlantGrowthManager>();
            if (existingManager == null)
            {
                var managerGo = (GameObject)PrefabUtility.InstantiatePrefab(managerPrefab);
                managerGo.name = "PlantGrowthManager";
                Undo.RegisterCreatedObjectUndo(managerGo, "Add Plant Growth Manager");
                Debug.Log("[PlantGrowth] Added PlantGrowthManager to scene.");
            }
            else
            {
                Debug.Log("[PlantGrowth] PlantGrowthManager already exists in scene.");
            }

            // Add 3 plants
            var positions = new[] { new Vector3(0, 0, 0), new Vector3(1.5f, 0, 0), new Vector3(3f, 0, 0) };
            int added = 0;
            foreach (var pos in positions)
            {
                var plantGo = (GameObject)PrefabUtility.InstantiatePrefab(plantPrefab);
                plantGo.name = "Plant_" + (added + 1);
                plantGo.transform.position = pos;
                Undo.RegisterCreatedObjectUndo(plantGo, "Add Plant");
                added++;
            }
            Debug.Log($"[PlantGrowth] Added {added} plants to scene at positions (0,0,0), (1.5,0,0), (3,0,0).");

            EditorSceneManager.MarkSceneDirty(scene);
        }

        [MenuItem("Tools/Plant Growth/Create Minimal Test Scene (guaranteed to work)")]
        public static void CreateMinimalTestScene()
        {
            EnsureDirectories();
            PlantGrowth.PlantSaveLoadService.DeleteSave();
            CreateStagePrefabs();
            CreatePlantStageAsset();
            var pandazolePaths = new[] {
                "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Env_GrassPlant_01.prefab",
                "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Env_GrassPlant_03.prefab",
                "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Env_GrassPlant_06.prefab",
                "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/food_Watermelon.prefab"
            };
            bool hasPandazole = true;
            for (int i = 0; i < 4; i++)
                if (AssetDatabase.LoadAssetAtPath<GameObject>(pandazolePaths[i]) == null) { hasPandazole = false; break; }
            if (hasPandazole)
                UseWatermelonPlantInternal(pandazolePaths);
            CreatePlantInstancePrefab();
            CreateManagerPrefab();
            FixPlantGrowth();
            FixPlantMaterials();
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(1.5f, 1.5f, -4f);
                cam.transform.LookAt(new Vector3(1.5f, 0, 0));
            }

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = new Vector3(1.5f, -0.5f, 0);
            ground.transform.localScale = new Vector3(2, 1, 2);

            var managerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PlantGrowth/Prefabs/PlantGrowthManager.prefab");
            var plantPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PlantGrowth/Prefabs/PlantInstance.prefab");
            if (managerPrefab == null || plantPrefab == null)
            {
                Debug.LogError("[PlantGrowth] Prefabs missing. Run COMPLETE SETUP first.");
                return;
            }

            var managerGo = (GameObject)PrefabUtility.InstantiatePrefab(managerPrefab);
            managerGo.name = "PlantGrowthManager";

            var positions = new[] { new Vector3(0.5f, 0, 0), new Vector3(2f, 0, 0), new Vector3(3.5f, 0, 0) };
            for (int i = 0; i < 3; i++)
            {
                var plantGo = (GameObject)PrefabUtility.InstantiatePrefab(plantPrefab);
                plantGo.name = "Plant_" + (i + 1);
                plantGo.transform.position = positions[i];
            }

            string scenePath = "Assets/PlantGrowth/PlantGrowthTestScene.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.OpenScene(scenePath);
            Debug.Log(hasPandazole
                ? "[PlantGrowth] Test scene created with WATERMELON plants. Press Play!"
                : "[PlantGrowth] Test scene created with capsule plants. Import Pandazole pack for watermelon.");
        }

        [MenuItem("Tools/Plant Growth/Create New Demo Scene")]
        public static void CreateDemoScene()
        {
            var managerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PlantGrowth/Prefabs/PlantGrowthManager.prefab");
            var plantPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PlantGrowth/Prefabs/PlantInstance.prefab");
            if (managerPrefab == null || plantPrefab == null)
            {
                Debug.LogError("[PlantGrowth] Prefabs not found. Run 'Setup Wizard - Create All Assets' first.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var cam = Camera.main;
            if (cam != null) cam.transform.position = new Vector3(1.5f, 2f, -5f);

            var managerGo = (GameObject)PrefabUtility.InstantiatePrefab(managerPrefab);
            managerGo.name = "PlantGrowthManager";

            var positions = new[] { new Vector3(0, 0, 0), new Vector3(1.5f, 0, 0), new Vector3(3f, 0, 0) };
            for (int i = 0; i < positions.Length; i++)
            {
                var plantGo = (GameObject)PrefabUtility.InstantiatePrefab(plantPrefab);
                plantGo.name = "Plant_" + (i + 1);
                plantGo.transform.position = positions[i];
            }

            EnsureDirectories();
            string scenePath = "Assets/PlantGrowth/PlantGrowthDemo.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"[PlantGrowth] Created demo scene at {scenePath}. Open it and press Play.");
        }

        [MenuItem("Tools/Plant Growth/Clear Save Data (fix freeze)")]
        public static void ClearSaveData()
        {
            PlantGrowth.PlantSaveLoadService.DeleteSave();
            Debug.Log("[PlantGrowth] Save data cleared. This can fix freeze/corruption issues.");
        }

        [MenuItem("Tools/Plant Growth/Setup Wizard - Create All Assets")]
        public static void CreateAll()
        {
            EnsureDirectories();
            CreateStagePrefabs();
            CreatePlantStageAsset();
            CreatePlantInstancePrefab();
            CreateManagerPrefab();
            AssetDatabase.Refresh();
            Debug.Log("[PlantGrowth] Setup complete. See Assets/PlantGrowth folder.");
        }

        [MenuItem("Tools/Plant Growth/Fix Plant Growth (faster growth, visible progress)")]
        public static void FixPlantGrowth()
        {
            var stageAsset = AssetDatabase.LoadAssetAtPath<PlantGrowth.PlantStageAsset>(Path.Combine(DataPath, "DefaultPlantStage.asset"));
            if (stageAsset == null)
            {
                Debug.LogError("[PlantGrowth] DefaultPlantStage.asset not found.");
                return;
            }
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
            Debug.Log("[PlantGrowth] Growth fixed: 5/10/15/20s per stage, slower water decay. Plants should now grow visibly!");
        }

        [MenuItem("Tools/Plant Growth/Use Watermelon Plant")]
        public static void UseWatermelonPlant()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[PlantGrowth] Stop Play mode first! Run 'Use Watermelon Plant' when the editor is NOT playing, then press Play.");
                return;
            }
            const string packPath = "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs";
            var stagePaths = new[]
            {
                packPath + "/Env_GrassPlant_01.prefab",  // Seed - tiny sprout
                packPath + "/Env_GrassPlant_03.prefab",  // Sprout - small leaves
                packPath + "/Env_GrassPlant_06.prefab",  // Young - vine with leaves
                packPath + "/food_Watermelon.prefab"     // Mature - watermelon!
            };

            var stagePrefabs = new GameObject[4];
            for (int i = 0; i < 4; i++)
            {
                stagePrefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(stagePaths[i]);
                if (stagePrefabs[i] == null)
                {
                    Debug.LogError($"[PlantGrowth] Prefab not found: {stagePaths[i]}. Ensure Pandazole Farm Ranch Pack is imported.");
                    return;
                }
            }

            var stageAsset = AssetDatabase.LoadAssetAtPath<PlantGrowth.PlantStageAsset>(Path.Combine(DataPath, "DefaultPlantStage.asset"));
            if (stageAsset == null)
            {
                Debug.LogError("[PlantGrowth] DefaultPlantStage.asset not found. Run 'Setup Wizard - Create All Assets' first.");
                return;
            }

            var so = new SerializedObject(stageAsset);
            so.FindProperty("stagePrefabs").arraySize = 4;
            for (int i = 0; i < 4; i++)
                so.FindProperty("stagePrefabs").GetArrayElementAtIndex(i).objectReferenceValue = stagePrefabs[i];
            so.FindProperty("stageDurations").arraySize = 4;
            so.FindProperty("stageDurations").GetArrayElementAtIndex(0).floatValue = 5f;
            so.FindProperty("stageDurations").GetArrayElementAtIndex(1).floatValue = 10f;
            so.FindProperty("stageDurations").GetArrayElementAtIndex(2).floatValue = 15f;
            so.FindProperty("stageDurations").GetArrayElementAtIndex(3).floatValue = 20f;
            so.FindProperty("waterDecayPerSecond").floatValue = 0.5f;
            so.FindProperty("fertilizerDecayPerSecond").floatValue = 0.2f;
            var scaleProp = so.FindProperty("stageVisualScale");
            if (scaleProp != null) scaleProp.floatValue = 2f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(stageAsset);
            AssetDatabase.SaveAssets();

            AssetDatabase.Refresh();
            Debug.Log("[PlantGrowth] Watermelon plant ready! STOP Play if running, then press Play again. Scale set to 2x for Pandazole models.");
        }

        [MenuItem("Tools/Plant Growth/Use Real Plants (Pandazole Pack)")]
        public static void UseRealPlantsFromPandazole()
        {
            const string packPath = "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs";
            var stagePaths = new[]
            {
                packPath + "/Env_GrassPlant_01.prefab",  // Seed - smallest grass
                packPath + "/Env_GrassPlant_03.prefab",  // Sprout
                packPath + "/Env_GrassPlant_05.prefab",  // Young
                packPath + "/Env_Wheat.prefab"           // Mature - full crop
            };

            var stagePrefabs = new GameObject[4];
            for (int i = 0; i < 4; i++)
            {
                stagePrefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(stagePaths[i]);
                if (stagePrefabs[i] == null)
                {
                    Debug.LogError($"[PlantGrowth] Prefab not found: {stagePaths[i]}. Ensure Pandazole Farm Ranch Pack is imported.");
                    return;
                }
            }

            var stageAsset = AssetDatabase.LoadAssetAtPath<PlantGrowth.PlantStageAsset>(Path.Combine(DataPath, "DefaultPlantStage.asset"));
            if (stageAsset == null)
            {
                Debug.LogError("[PlantGrowth] DefaultPlantStage.asset not found. Run 'Setup Wizard - Create All Assets' first.");
                return;
            }

            var so = new SerializedObject(stageAsset);
            so.FindProperty("stagePrefabs").arraySize = 4;
            for (int i = 0; i < 4; i++)
                so.FindProperty("stagePrefabs").GetArrayElementAtIndex(i).objectReferenceValue = stagePrefabs[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(stageAsset);
            AssetDatabase.SaveAssets();

            Debug.Log("[PlantGrowth] Switched to real plants from Pandazole Pack. Plants will now show grass and wheat.");
        }

        [MenuItem("Tools/Plant Growth/Fix Plant Materials (fix magenta)")]
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
            Debug.Log("[PlantGrowth] Plant stage materials fixed. Plants should now show correct colors.");
        }

        [MenuItem("Tools/Plant Growth/Create Stage Placeholder Prefabs")]
        public static void CreateStagePrefabs()
        {
            EnsureDirectories();
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
            AssetDatabase.Refresh();
            Debug.Log($"[PlantGrowth] Created stage prefabs in {StagePrefabsPath}");
        }

        private static Material[] CreateOrLoadStageMaterials()
        {
            EnsureFolder("Assets/PlantGrowth", "Materials");
            var colors = new[] {
                new Color(0.4f, 0.25f, 0.1f),   // Seed - brown
                new Color(0.2f, 0.6f, 0.2f),    // Sprout - green
                new Color(0.1f, 0.7f, 0.15f),   // Young - bright green
                new Color(0.15f, 0.5f, 0.1f),   // Mature - dark green
                new Color(0.3f, 0.2f, 0.1f)     // Dead - brown/gray
            };
            var names = new[] { "PlantStage_Seed", "PlantStage_Sprout", "PlantStage_Young", "PlantStage_Mature", "PlantStage_Dead" };
            var result = new Material[5];

            var refMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Samples/XR Interaction Toolkit/3.2.2/Starter Assets/DemoSceneAssets/Materials/Lit White.mat");
            if (refMat == null) refMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/VRMPAssets/Materials/Black.mat");
            if (refMat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("Sprites/Default");
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
                    if (mat.HasProperty("_BaseColor"))
                        mat.SetColor("_BaseColor", colors[i]);
                    else if (mat.HasProperty("_Color"))
                        mat.SetColor("_Color", colors[i]);
                    else
                        mat.color = colors[i];
                    AssetDatabase.CreateAsset(mat, matPath);
                }
                else
                {
                    if (mat.HasProperty("_BaseColor"))
                        mat.SetColor("_BaseColor", colors[i]);
                    else if (mat.HasProperty("_Color"))
                        mat.SetColor("_Color", colors[i]);
                    else
                        mat.color = colors[i];
                    EditorUtility.SetDirty(mat);
                }
                result[i] = mat;
            }
            AssetDatabase.SaveAssets();
            return result;
        }

        [MenuItem("Tools/Plant Growth/Create Plant Stage Asset")]
        public static void CreatePlantStageAsset()
        {
            EnsureDirectories();
            var asset = ScriptableObject.CreateInstance<PlantGrowth.PlantStageAsset>();
            asset.stageDurations = new[] { 10f, 30f, 60f, 120f };
            asset.hasDeadStage = false;

            var stagePaths = new[]
            {
                "Assets/PlantGrowth/Prefabs/Stages/Stage0_Seed.prefab",
                "Assets/PlantGrowth/Prefabs/Stages/Stage1_Sprout.prefab",
                "Assets/PlantGrowth/Prefabs/Stages/Stage2_Young.prefab",
                "Assets/PlantGrowth/Prefabs/Stages/Stage3_Mature.prefab"
            };
            asset.stagePrefabs = new GameObject[4];
            for (int i = 0; i < 4; i++)
            {
                asset.stagePrefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(stagePaths[i]);
                if (asset.stagePrefabs[i] == null)
                    Debug.LogWarning($"[PlantGrowth] Stage prefab not found: {stagePaths[i]}. Run 'Create Stage Placeholder Prefabs' first.");
            }

            string assetPath = Path.Combine(DataPath, "DefaultPlantStage.asset");
            AssetDatabase.CreateAsset(asset, assetPath);
            Debug.Log($"[PlantGrowth] Created PlantStageAsset at {assetPath}");
        }

        [MenuItem("Tools/Plant Growth/Create Plant Instance Prefab")]
        public static void CreatePlantInstancePrefab()
        {
            EnsureDirectories();
            var root = new GameObject("PlantInstance");
            var holder = new GameObject("StageHolder");
            holder.transform.SetParent(root.transform, false);
            holder.transform.localPosition = Vector3.zero;

            var controller = root.AddComponent<PlantGrowth.PlantController>();
            var stageAsset = AssetDatabase.LoadAssetAtPath<PlantGrowth.PlantStageAsset>(Path.Combine(DataPath, "DefaultPlantStage.asset"));
            var so = new SerializedObject(controller);
            so.FindProperty("stageHolder").objectReferenceValue = holder.transform;
            so.FindProperty("stageAsset").objectReferenceValue = stageAsset;
            so.ApplyModifiedPropertiesWithoutUndo();

            var collider = root.AddComponent<SphereCollider>();
            collider.radius = 0.5f;
            collider.isTrigger = true;

            string path = Path.Combine(PrefabsPath, "PlantInstance.prefab");
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Debug.Log($"[PlantGrowth] Created PlantInstance prefab at {path}");
        }

        [MenuItem("Tools/Plant Growth/Create Manager Prefab")]
        public static void CreateManagerPrefab()
        {
            EnsureDirectories();
            var go = new GameObject("PlantGrowthManager");
            go.AddComponent<PlantGrowth.PlantGrowthManager>();
            string path = Path.Combine(PrefabsPath, "PlantGrowthManager.prefab");
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            Debug.Log($"[PlantGrowth] Created Manager prefab at {path}");
        }

        private static void EnsureDirectories()
        {
            EnsureFolder("Assets", "PlantGrowth");
            EnsureFolder("Assets/PlantGrowth", "Prefabs");
            EnsureFolder("Assets/PlantGrowth/Prefabs", "Stages");
            EnsureFolder("Assets/PlantGrowth", "Data");
            EnsureFolder("Assets/PlantGrowth", "Materials");
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
