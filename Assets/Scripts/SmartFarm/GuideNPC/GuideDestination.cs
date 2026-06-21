using UnityEngine;

namespace SmartFarm.GuideNPC
{
    /// <summary>
    /// Marks a transform in the scene as a guide destination (CropFieldTarget,
    /// MeetingAreaTarget, SmartScreensTarget, TrainingRoomTarget...).
    ///
    /// Drop this on an empty GameObject and place it where you want the guide to
    /// stop. The <see cref="SmartFarmGuideNPC"/> picks these up automatically when
    /// its own destination list is empty, so you can author destinations purely in
    /// the scene if you prefer.
    /// </summary>
    [DisallowMultipleComponent]
    public class GuideDestination : MonoBehaviour
    {
        [Tooltip("Which farm area this point represents.")]
        [SerializeField] private GuideArea area = GuideArea.CropField;

        [Tooltip("Optional label override for the menu button. Leave empty to use the default area name.")]
        [SerializeField] private string label = "";

        [Tooltip("Optional: a point the guide should look toward / point at after arriving (e.g. the centre of the field). " +
                 "If empty the guide simply faces the player.")]
        [SerializeField] private Transform lookAtOnArrival;

        public GuideArea Area => area;
        public string Label => string.IsNullOrWhiteSpace(label) ? GuideAreaLabels.For(area) : label;
        public Transform LookAtOnArrival => lookAtOnArrival;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.30f, 1f, 0.66f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 0.35f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.8f);
        }

        private void OnDrawGizmosSelected()
        {
            UnityEditor.Handles.color = new Color(0.30f, 1f, 0.66f, 1f);
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.4f, Label + " (" + area + ")");
        }
#endif
    }
}
