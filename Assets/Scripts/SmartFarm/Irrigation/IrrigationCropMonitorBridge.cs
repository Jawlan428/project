using System;
using UnityEngine;

namespace SmartFarm.Irrigation
{
    /// <summary>
    /// Forwards irrigation alerts to the existing Crop Growth Monitor's popup so
    /// the player sees the same warnings ("Low Moisture", "Overwatering Risk",
    /// "Storm Disabled" etc.) on both screens.
    ///
    /// Listens to <see cref="IrrigationAlertManager.OnAlertRaised"/> and pushes
    /// a converted <see cref="CropMonitorAlert"/> into
    /// <see cref="CropMonitorAlertPopupUI.ShowAlert"/>.
    /// </summary>
    [AddComponentMenu("SmartFarm/Irrigation/Crop Monitor Bridge")]
    public class IrrigationCropMonitorBridge : MonoBehaviour
    {
        [Header("References (auto-found if empty)")]
        [SerializeField] private IrrigationAlertManager   alertManager;
        [SerializeField] private CropMonitorAlertPopupUI  cropMonitorPopup;

        private void Awake()
        {
            if (alertManager == null) alertManager = FindFirstObjectByType<IrrigationAlertManager>();
            if (cropMonitorPopup == null) cropMonitorPopup = FindFirstObjectByType<CropMonitorAlertPopupUI>();
        }

        private void OnEnable()
        {
            if (alertManager == null) alertManager = FindFirstObjectByType<IrrigationAlertManager>();
            if (cropMonitorPopup == null) cropMonitorPopup = FindFirstObjectByType<CropMonitorAlertPopupUI>();

            if (alertManager != null)
                alertManager.OnAlertRaised += HandleAlertRaised;
        }

        private void OnDisable()
        {
            if (alertManager != null)
                alertManager.OnAlertRaised -= HandleAlertRaised;
        }

        private void HandleAlertRaised(IrrigationAlert alert)
        {
            if (cropMonitorPopup == null)
            {
                cropMonitorPopup = FindFirstObjectByType<CropMonitorAlertPopupUI>();
                if (cropMonitorPopup == null) return;
            }

            var monitorAlert = new CropMonitorAlert
            {
                id           = "irrigation_" + alert.id,
                title        = alert.title,
                message      = alert.message,
                level        = ToCropLevel(alert.level),
                timestampUtc = alert.timestampUtc
            };
            cropMonitorPopup.ShowAlert(monitorAlert);
        }

        private static CropAlertLevel ToCropLevel(IrrigationAlertLevel level) => level switch
        {
            IrrigationAlertLevel.Critical => CropAlertLevel.Critical,
            IrrigationAlertLevel.Warning  => CropAlertLevel.Warning,
            IrrigationAlertLevel.Success  => CropAlertLevel.Success,
            _                             => CropAlertLevel.Info
        };

        public void SetReferences(IrrigationAlertManager mgr, CropMonitorAlertPopupUI popup)
        {
            alertManager     = mgr;
            cropMonitorPopup = popup;
        }
    }
}
