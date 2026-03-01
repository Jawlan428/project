using UnityEngine;
using UnityEngine.EventSystems;

namespace SmartFarm
{
    /// <summary>
    /// Move Farm Dashboard by dragging the header.
    /// Supports both Screen Space and World Space canvases.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class FarmDashboardDrag : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        [Tooltip("RectTransform to move (canvas root if null)")]
        [SerializeField] private RectTransform _targetToMove;

        [Tooltip("World-space drag sensitivity (pixels to meters). Used when canvas is World Space.")]
        [SerializeField] private float _worldSpaceSensitivity = 0.002f;

        private Canvas _canvas;

        private void Awake()
        {
            if (_targetToMove == null) _targetToMove = GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
            _canvas = GetComponentInParent<Canvas>();
        }

        public void OnPointerDown(PointerEventData eventData) { }

        public void OnDrag(PointerEventData eventData)
        {
            if (_targetToMove == null) return;

            if (_canvas != null && _canvas.renderMode == RenderMode.WorldSpace)
            {
                // World Space: move Transform in canvas plane (right/up)
                Transform t = _targetToMove.transform;
                Vector3 delta = t.right * (eventData.delta.x * _worldSpaceSensitivity)
                    + t.up * (eventData.delta.y * _worldSpaceSensitivity);
                t.position += delta;
            }
            else
            {
                // Screen Space: move anchoredPosition
                _targetToMove.anchoredPosition += eventData.delta;
            }
        }
    }
}
