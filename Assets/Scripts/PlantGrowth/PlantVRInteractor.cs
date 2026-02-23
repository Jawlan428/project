using UnityEngine;

namespace PlantGrowth
{
    /// <summary>
    /// Example VR interaction: water/fertilize plants on trigger or raycast.
    /// Attach to a watering can, fertilizer tool, or controller.
    /// For XR Interaction Toolkit: use OnSelectEntered or similar to call Water/AddFertilizer.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class PlantVRInteractor : MonoBehaviour
    {
        [Header("Interaction")]
        [SerializeField] private float waterAmount = 30f;
        [SerializeField] private float fertilizerAmount = 20f;
        [SerializeField] private bool isWateringTool = true;

        [Header("Debug (Editor)")]
        [SerializeField] private bool debugLogOnInteract = false;

        /// <summary>
        /// Call from XR Interactable (e.g. OnSelectEntered) when tool touches plant.
        /// Or use OnTriggerEnter - this script will auto-detect plant in trigger.
        /// </summary>
        public void InteractWithPlant(PlantController plant)
        {
            if (plant == null || plant.IsDead) return;
            if (isWateringTool)
                plant.Water(waterAmount);
            else
                plant.AddFertilizer(fertilizerAmount);
            if (debugLogOnInteract)
                Debug.Log($"[PlantVRInteractor] {(isWateringTool ? "Watered" : "Fertilized")} plant. Health: {plant.Health:F0}");
        }

        private void OnTriggerEnter(Collider other)
        {
            var plant = other.GetComponent<PlantController>();
            if (plant != null)
                InteractWithPlant(plant);
        }

        /// <summary>
        /// Public API for external scripts (e.g. XR Interactable events).
        /// </summary>
        public void WaterPlant(PlantController plant) => InteractWithPlant(plant);
        public void FertilizePlant(PlantController plant)
        {
            if (plant == null || plant.IsDead) return;
            plant.AddFertilizer(fertilizerAmount);
        }
    }
}
