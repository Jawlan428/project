using System.Reflection;
using UnityEngine;

namespace SmartFarm
{
    /// <summary>
    /// Optional helper: sends haptic pulse by reflection so it works across XRI variants.
    /// Assign your controller interactor/component references in Inspector.
    /// </summary>
    public class VRHapticsHelper : MonoBehaviour
    {
        [SerializeField] private Component leftControllerRef;
        [SerializeField] private Component rightControllerRef;

        public void PulseLeft(float amplitude = 0.4f, float duration = 0.05f)
        {
            SendPulse(leftControllerRef, amplitude, duration);
        }

        public void PulseRight(float amplitude = 0.4f, float duration = 0.05f)
        {
            SendPulse(rightControllerRef, amplitude, duration);
        }

        public void PulseBoth(float amplitude = 0.3f, float duration = 0.04f)
        {
            SendPulse(leftControllerRef, amplitude, duration);
            SendPulse(rightControllerRef, amplitude, duration);
        }

        private static void SendPulse(Component componentRef, float amplitude, float duration)
        {
            if (componentRef == null) return;

            // Path 1: component has SendHapticImpulse(float, float)
            MethodInfo direct = componentRef.GetType().GetMethod("SendHapticImpulse", new[] { typeof(float), typeof(float) });
            if (direct != null)
            {
                direct.Invoke(componentRef, new object[] { amplitude, duration });
                return;
            }

            // Path 2: component has "xrController" property that has SendHapticImpulse.
            PropertyInfo xrControllerProp = componentRef.GetType().GetProperty("xrController", BindingFlags.Public | BindingFlags.Instance);
            object xrController = xrControllerProp?.GetValue(componentRef);
            if (xrController == null) return;

            MethodInfo send = xrController.GetType().GetMethod("SendHapticImpulse", new[] { typeof(float), typeof(float) });
            if (send != null)
                send.Invoke(xrController, new object[] { amplitude, duration });
        }
    }
}
