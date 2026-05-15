#if UNITY_EDITOR
using SmartFarm;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SmartFarm.Editor
{
    /// <summary>
    /// Swaps the <i>Mature</i> stage prefab on the Wheat and Corn
    /// <see cref="CropData"/> assets to the Wild Harvest "12" variants:
    /// <list type="bullet">
    ///   <item><b>Corn</b>  → <c>Assets/NV3D/Wild Harvest/Grains/Prefabs/Plants/SM_CornPlant_12.prefab</c></item>
    ///   <item><b>Wheat</b> → <c>Assets/NV3D/Wild Harvest/Grains/Prefabs/Plants/SM_WheatPlants_12.prefab</c></item>
    /// </list>
    ///
    /// Index <c>3</c> in <c>stagePrefabs</c> is the Mature stage (matches the
    /// <see cref="SmartFarm.CropStage.Mature"/> enum value).
    ///
    /// Also offers to refresh every already-mature crop in the open scene so
    /// the new look appears immediately without waiting for the next stage
    /// transition.
    ///
    /// Menu: <i>Tools › Smart Farm › Crops › Use SM_*_12 Prefabs For Mature Stage</i>
    /// </summary>
    public static class MaturePrefabSwapEditor
    {
        // Indices in CropData.stagePrefabs
        private const int MatureIndex = 3;

        // Asset paths
        private const string CornCropDataPath  = "Assets/SmartFarm/CropData/Corn_CropData.asset";
        private const string WheatCropDataPath = "Assets/SmartFarm/CropData/Wheat_CropData.asset";
        private const string CornMaturePrefab  = "Assets/NV3D/Wild Harvest/Grains/Prefabs/Plants/SM_CornPlant_12.prefab";
        private const string WheatMaturePrefab = "Assets/NV3D/Wild Harvest/Grains/Prefabs/Plants/SM_WheatPlants_12.prefab";

        [MenuItem("Tools/Smart Farm/Crops/Use SM_*_12 Prefabs For Mature Stage", priority = 50)]
        public static void UseTwelveAsMature()
        {
            int swapped = 0;

            if (TrySwap(CornCropDataPath, CornMaturePrefab, "Corn"))   swapped++;
            if (TrySwap(WheatCropDataPath, WheatMaturePrefab, "Wheat")) swapped++;

            if (swapped == 0)
            {
                EditorUtility.DisplayDialog("Mature Prefab Swap",
                    "Could not load CropData assets or the SM_*_12 prefabs.\n\n" +
                    "Expected paths:\n" +
                    $"  • {CornCropDataPath}\n" +
                    $"  • {WheatCropDataPath}\n" +
                    $"  • {CornMaturePrefab}\n" +
                    $"  • {WheatMaturePrefab}",
                    "OK");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            int refreshed = RefreshInSceneMatureCrops();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            EditorUtility.DisplayDialog(
                "Mature Prefab Swap — Done",
                $"Updated {swapped} CropData asset(s):\n" +
                "  • Corn  → SM_CornPlant_12 (Mature)\n" +
                "  • Wheat → SM_WheatPlants_12 (Mature)\n\n" +
                $"Refreshed {refreshed} already-mature crop(s) in the open scene.\n\n" +
                "Crops that are still growing will use the new Mature prefab\n" +
                "automatically the next time they reach the Mature stage.",
                "OK");

            Debug.Log($"[MaturePrefabSwap] Updated {swapped} CropData asset(s); refreshed {refreshed} live mature crop(s).");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Internals
        // ─────────────────────────────────────────────────────────────────────

        private static bool TrySwap(string cropDataPath, string prefabPath, string label)
        {
            var cropData = AssetDatabase.LoadAssetAtPath<CropData>(cropDataPath);
            if (cropData == null)
            {
                Debug.LogWarning($"[MaturePrefabSwap] Could not find {label} CropData at '{cropDataPath}'.");
                return false;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[MaturePrefabSwap] Could not find {label} Mature prefab at '{prefabPath}'.");
                return false;
            }

            var so   = new SerializedObject(cropData);
            var list = so.FindProperty("stagePrefabs");
            if (list == null)
            {
                Debug.LogWarning($"[MaturePrefabSwap] {label} CropData has no 'stagePrefabs' field.");
                return false;
            }

            // Grow the array if needed (Dead at index 4 is enforced elsewhere; just safeguard).
            while (list.arraySize <= MatureIndex)
                list.InsertArrayElementAtIndex(list.arraySize);

            list.GetArrayElementAtIndex(MatureIndex).objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(cropData);

            Debug.Log($"[MaturePrefabSwap] {label}: stagePrefabs[{MatureIndex}] → {prefab.name}");
            return true;
        }

        private static int RefreshInSceneMatureCrops()
        {
            var all = Object.FindObjectsByType<CropGrowthController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            int refreshed = 0;
            for (int i = 0; i < all.Length; i++)
            {
                var c = all[i];
                if (c == null) continue;
                if (c.CurrentStage != CropStage.Mature) continue;

                c.RefreshStageVisual();
                refreshed++;
            }
            return refreshed;
        }
    }
}
#endif
