using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SmartFarm
{
    /// <summary>
    /// Micro-interactions for tablet buttons: hover highlight + click sound + optional haptics.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class TabletUIButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] private Image targetGraphic;
        [SerializeField] private Color normalColor = new Color(0.2f, 0.7f, 0.3f, 1f);
        [SerializeField] private Color hoverColor = new Color(0.3f, 0.85f, 0.45f, 1f);
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip clickClip;
        [SerializeField] private VRHapticsHelper haptics;

        private void Awake()
        {
            if (targetGraphic == null)
                targetGraphic = GetComponent<Image>();
            if (targetGraphic != null)
                targetGraphic.color = normalColor;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (targetGraphic != null)
                targetGraphic.color = hoverColor;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (targetGraphic != null)
                targetGraphic.color = normalColor;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (audioSource != null && clickClip != null)
                audioSource.PlayOneShot(clickClip);
            if (haptics != null)
                haptics.PulseRight();
        }
    }
}
