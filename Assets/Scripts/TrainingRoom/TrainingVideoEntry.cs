using UnityEngine;

namespace TrainingRoom
{
    public enum VideoCategoryType
    {
        HarvestTechniques,
        IrrigationMethods,
        PestDetection,
        FarmEquipment,
        General
    }

    /// <summary>
    /// ScriptableObject that describes a single agricultural training video.
    /// Create via: Assets → Create → TrainingRoom → Training Video Entry
    /// </summary>
    [CreateAssetMenu(menuName = "TrainingRoom/Training Video Entry", fileName = "NewTrainingVideo")]
    public class TrainingVideoEntry : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Display title shown in the tablet UI and on the screen")]
        public string title = "Untitled Video";

        [Tooltip("Short description shown below the title in the playlist")]
        [TextArea(2, 4)]
        public string description = "";

        [Tooltip("Category for filtering / icon display")]
        public VideoCategoryType category = VideoCategoryType.General;

        [Tooltip("Duration label shown in the UI (e.g. '4:32'). Leave blank to auto-detect.")]
        public string durationLabel = "";

        [Header("Source")]
        [Tooltip("File name inside Assets/StreamingAssets/TrainingVideos/  (e.g. 'harvest_technique.mp4')")]
        public string streamingAssetsFileName = "";

        [Tooltip("If true, this video uses a 360-degree equirectangular projection")]
        public bool is360Video = false;

        [Header("Optional")]
        [Tooltip("Thumbnail sprite shown in the playlist (optional)")]
        public Sprite thumbnail;

        [Tooltip("Language / locale tag for future subtitle integration (e.g. 'en', 'ar', 'hi')")]
        public string language = "en";

        /// <summary>Resolves the absolute streaming-assets path at runtime.</summary>
        public string GetRuntimePath()
        {
            if (string.IsNullOrWhiteSpace(streamingAssetsFileName)) return string.Empty;
            return System.IO.Path.Combine(
                Application.streamingAssetsPath,
                "TrainingVideos",
                streamingAssetsFileName);
        }

        /// <summary>Returns a category icon emoji for quick display (no image assets needed).</summary>
        public string GetCategoryLabel()
        {
            return category switch
            {
                VideoCategoryType.HarvestTechniques => "Harvest",
                VideoCategoryType.IrrigationMethods => "Irrigation",
                VideoCategoryType.PestDetection     => "Pest",
                VideoCategoryType.FarmEquipment     => "Equipment",
                _                                   => "General"
            };
        }
    }
}
