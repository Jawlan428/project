using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace TrainingRoom
{
    /// <summary>
    /// Teleports the local XR player to a target transform when invoked.
    /// Can be triggered from a UI Button onClick or called manually.
    /// </summary>
    public class TrainingRoomTeleportTrigger : MonoBehaviour
    {
        [Header("Teleport Target")]
        [SerializeField] private Transform destination;

        [Header("Optional References")]
        [SerializeField] private TeleportationProvider teleportationProvider;
        [SerializeField] private XROrigin xrOrigin;
        [SerializeField] private Button triggerButton;

        [Header("Options")]
        [SerializeField] private bool autoWireButtonOnAwake = true;

        private void Awake()
        {
            if (teleportationProvider == null)
                teleportationProvider = FindFirstObjectByType<TeleportationProvider>();

            if (xrOrigin == null)
                xrOrigin = FindFirstObjectByType<XROrigin>();

            if (autoWireButtonOnAwake)
            {
                if (triggerButton == null)
                    triggerButton = GetComponent<Button>();

                if (triggerButton != null)
                    triggerButton.onClick.AddListener(TeleportToDestination);
            }
        }

        private void OnDestroy()
        {
            if (triggerButton != null)
                triggerButton.onClick.RemoveListener(TeleportToDestination);
        }

        public void TeleportToDestination()
        {
            if (destination == null)
            {
                Debug.LogWarning("[TrainingRoomTeleport] No destination assigned.");
                return;
            }

            if (teleportationProvider == null)
                teleportationProvider = FindFirstObjectByType<TeleportationProvider>();

            var request = new TeleportRequest
            {
                destinationPosition = destination.position,
                destinationRotation = destination.rotation
            };

            if (teleportationProvider != null && teleportationProvider.QueueTeleportRequest(request))
                return;

            // Fallback for scenes that do not have a TeleportationProvider configured.
            if (xrOrigin == null)
                xrOrigin = FindFirstObjectByType<XROrigin>();

            if (xrOrigin != null)
            {
                xrOrigin.transform.SetPositionAndRotation(destination.position, destination.rotation);
                return;
            }

            Debug.LogWarning("[TrainingRoomTeleport] Could not teleport: no TeleportationProvider or XROrigin found.");
        }
    }
}
