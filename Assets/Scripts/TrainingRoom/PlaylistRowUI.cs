using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TrainingRoom
{
    /// <summary>
    /// Represents one row in the training room playlist scroll view.
    ///
    /// Attach to the PlaylistRow prefab. Expected child layout:
    ///   [Image] Thumbnail  (optional)
    ///   [TMP_Text] TitleText
    ///   [TMP_Text] CategoryText
    ///   [TMP_Text] DurationText
    ///   [Button]   SelectButton  (covers the whole row, or a dedicated button)
    ///   [Image]    HighlightBorder  (shown when this row is selected)
    /// </summary>
    public class PlaylistRowUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI categoryText;
        [SerializeField] private TextMeshProUGUI durationText;
        [SerializeField] private Image           thumbnailImage;
        [SerializeField] private Button          selectButton;
        [SerializeField] private GameObject      highlightBorder;

        [Header("Colors")]
        [SerializeField] private Color normalColor      = new Color(0.15f, 0.15f, 0.15f, 1f);
        [SerializeField] private Color highlightedColor = new Color(0.2f, 0.55f, 0.25f, 1f);

        private Image _backgroundImage;
        private int   _index;
        private Action<int> _onSelected;

        private void Awake()
        {
            _backgroundImage = GetComponent<Image>();
        }

        /// <summary>Populate the row with video entry data and wire the click callback.</summary>
        public void Setup(TrainingVideoEntry entry, int index, Action<int> onSelected)
        {
            _index      = index;
            _onSelected = onSelected;

            if (titleText    != null) titleText.text    = entry.title;
            if (categoryText != null) categoryText.text = entry.GetCategoryLabel();
            if (durationText != null) durationText.text = string.IsNullOrEmpty(entry.durationLabel)
                ? ""
                : entry.durationLabel;

            if (thumbnailImage != null)
            {
                bool hasThumbnail   = entry.thumbnail != null;
                thumbnailImage.sprite  = hasThumbnail ? entry.thumbnail : null;
                thumbnailImage.enabled = hasThumbnail;
            }

            if (selectButton != null)
                selectButton.onClick.AddListener(() => _onSelected?.Invoke(_index));

            SetHighlighted(false);
        }

        /// <summary>Toggles the highlighted (selected) visual state of this row.</summary>
        public void SetHighlighted(bool highlighted)
        {
            if (highlightBorder != null)
                highlightBorder.SetActive(highlighted);

            if (_backgroundImage != null)
                _backgroundImage.color = highlighted ? highlightedColor : normalColor;
        }
    }
}
