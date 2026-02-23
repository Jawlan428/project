using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PlantGrowth
{
    /// <summary>
    /// Optional debug UI to show plant health, stage, and progress.
    /// Assign a PlantController to display its stats.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class PlantDebugUI : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private PlantController targetPlant;

        [Header("UI References (optional)")]
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private TextMeshProUGUI stageText;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private Slider healthBar;
        [SerializeField] private Slider waterBar;

        [Header("World Space")]
        [SerializeField] private bool worldSpace = true;
        [SerializeField] private float heightOffset = 1.5f;

        private Canvas _canvas;
        private RectTransform _rect;

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            _rect = GetComponent<RectTransform>();
            if (worldSpace)
            {
                _canvas.renderMode = RenderMode.WorldSpace;
                _canvas.worldCamera = Camera.main;
            }
        }

        private void LateUpdate()
        {
            if (targetPlant == null) return;

            if (worldSpace && targetPlant.transform != null && Camera.main != null)
            {
                transform.position = targetPlant.transform.position + Vector3.up * heightOffset;
                transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
            }

            RefreshUI();
        }

        private void RefreshUI()
        {
            if (healthText != null)
                healthText.text = $"Health: {targetPlant.Health:F0}";
            if (stageText != null)
                stageText.text = $"Stage: {targetPlant.StageIndex}";
            if (progressText != null)
                progressText.text = $"Progress: {targetPlant.StageProgress:P0}";
            if (healthBar != null)
                healthBar.value = targetPlant.Health / 100f;
            if (waterBar != null)
                waterBar.value = targetPlant.WaterLevel / 100f;
        }

        /// <summary>
        /// Set the plant to display. Call from VR raycast or other interaction.
        /// </summary>
        public void SetTarget(PlantController plant)
        {
            targetPlant = plant;
        }
    }
}
