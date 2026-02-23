using UnityEngine;
using System.Collections.Generic;
using System.IO;

namespace PlantGrowth
{
    /// <summary>
    /// Handles save/load of plant state to JSON in Application.persistentDataPath.
    /// </summary>
    public static class PlantSaveLoadService
    {
        private const string FileName = "plant_growth_save.json";

        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>
        /// Save all plants to disk.
        /// </summary>
        public static void SavePlants(IReadOnlyList<PlantController> plants)
        {
            var dataList = new List<PlantData>();
            foreach (var p in plants)
            {
                if (p == null) continue;
                dataList.Add(p.ExportData());
            }
            var wrapper = new PlantSaveData(dataList.ToArray(), System.DateTime.UtcNow);
            string json = JsonUtility.ToJson(wrapper, true);
            try
            {
                File.WriteAllText(SavePath, json);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PlantSaveLoad] Save failed: {e.Message}");
            }
        }

        /// <summary>
        /// Load saved data and restore plants. Matches by closest position.
        /// If no save exists, initializes all plants with defaults.
        /// </summary>
        public static void LoadAndRestorePlants(List<PlantController> plants)
        {
            if (!File.Exists(SavePath))
            {
                foreach (var plant in plants)
                    if (plant != null) plant.Initialize(null);
                return;
            }

            try
            {
                string json = File.ReadAllText(SavePath);
                var wrapper = JsonUtility.FromJson<PlantSaveData>(json);
                if (wrapper?.plants == null || wrapper.plants.Length == 0) return;

                const float maxMatchDistance = 2f; // meters
                var used = new HashSet<int>();

                foreach (var plant in plants)
                {
                    if (plant == null) continue;
                    PlantData best = null;
                    int bestIdx = -1;
                    float bestDistSq = maxMatchDistance * maxMatchDistance;

                    for (int i = 0; i < wrapper.plants.Length; i++)
                    {
                        if (used.Contains(i)) continue;
                        var d = wrapper.plants[i];
                        var savedPos = new Vector3(d.posX, d.posY, d.posZ);
                        float distSq = (plant.transform.position - savedPos).sqrMagnitude;
                        if (distSq < bestDistSq)
                        {
                            bestDistSq = distSq;
                            best = d;
                            bestIdx = i;
                        }
                    }
                    if (best != null && bestIdx >= 0)
                    {
                        used.Add(bestIdx);
                        plant.Initialize(best);
                    }
                    else
                        plant.Initialize(null);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PlantSaveLoad] Load failed: {e.Message}");
                foreach (var plant in plants)
                    if (plant != null) plant.Initialize(null);
            }
        }

        /// <summary>
        /// Load raw save data. Used when instantiating plants from save.
        /// </summary>
        public static PlantSaveData LoadRaw()
        {
            if (!File.Exists(SavePath)) return null;
            try
            {
                string json = File.ReadAllText(SavePath);
                return JsonUtility.FromJson<PlantSaveData>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Delete save file. Useful for testing.
        /// </summary>
        public static void DeleteSave()
        {
            if (File.Exists(SavePath))
                File.Delete(SavePath);
        }
    }
}
