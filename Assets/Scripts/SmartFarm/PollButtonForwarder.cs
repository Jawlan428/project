using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace SmartFarm
{
    /// <summary>
    /// Forwards pointer clicks to a UnityEvent. Use when Button.onClick doesn't fire (e.g. XR).
    /// Add to the same GameObject as the Button.
    /// </summary>
    [RequireComponent(typeof(UnityEngine.UI.Button))]
    public class PollButtonForwarder : MonoBehaviour, IPointerClickHandler, IPointerDownHandler
    {
        public UnityEvent onPointerClick;
        private float _lastInvokeTime;

        private void InvokeSafe()
        {
            // XR can fire multiple pointer events rapidly; debounce to avoid double vote.
            if (Time.unscaledTime - _lastInvokeTime < 0.1f) return;
            _lastInvokeTime = Time.unscaledTime;
            if (onPointerClick != null)
                onPointerClick.Invoke();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            InvokeSafe();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // Some XR input paths trigger pointer down but not click.
            InvokeSafe();
        }
    }
}
