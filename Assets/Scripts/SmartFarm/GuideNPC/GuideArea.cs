using System;
using UnityEngine;

namespace SmartFarm.GuideNPC
{
    /// <summary>
    /// The four farm areas the Smart Farm Guide NPC can walk the player to.
    /// </summary>
    public enum GuideArea
    {
        CropField = 0,
        MeetingArea = 1,
        SmartScreens = 2,
        TrainingRoom = 3
    }

    /// <summary>
    /// One walkable destination: the area it represents, the label shown on the
    /// menu button, and the transform the NavMeshAgent should walk to.
    /// </summary>
    [Serializable]
    public class GuideDestinationEntry
    {
        [Tooltip("Which farm area this destination represents.")]
        public GuideArea area = GuideArea.CropField;

        [Tooltip("Label shown on the floating menu button.")]
        public string label = "Crop Field";

        [Tooltip("World transform the guide walks to. Place this where you want the guide to stop.")]
        public Transform target;

        public GuideDestinationEntry() { }

        public GuideDestinationEntry(GuideArea area, string label, Transform target)
        {
            this.area = area;
            this.label = label;
            this.target = target;
        }
    }

    /// <summary>Default human-readable labels for each area.</summary>
    public static class GuideAreaLabels
    {
        public static string For(GuideArea area)
        {
            switch (area)
            {
                case GuideArea.CropField:    return "Crop Field";
                case GuideArea.MeetingArea:  return "Meeting Area";
                case GuideArea.SmartScreens: return "Smart Screens";
                case GuideArea.TrainingRoom: return "Training Room";
                default:                     return area.ToString();
            }
        }

        /// <summary>The conventional scene object name for an area's destination transform.</summary>
        public static string TargetName(GuideArea area)
        {
            switch (area)
            {
                case GuideArea.CropField:    return "CropFieldTarget";
                case GuideArea.MeetingArea:  return "MeetingAreaTarget";
                case GuideArea.SmartScreens: return "SmartScreensTarget";
                case GuideArea.TrainingRoom: return "TrainingRoomTarget";
                default:                     return area + "Target";
            }
        }
    }
}
