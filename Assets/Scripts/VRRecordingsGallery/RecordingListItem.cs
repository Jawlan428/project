using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace VRRecordings
{
    /// <summary>
    /// UI component for a single recording item in the gallery list.
    /// Displays recording info and handles play/delete actions.
    /// </summary>
    public class RecordingListItem : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI subtitleText;
        [SerializeField] private TextMeshProUGUI dateText;
        [SerializeField] private TextMeshProUGUI sizeText;
        [SerializeField] private RawImage thumbnailImage;
        [SerializeField] private Button playButton;
        [SerializeField] private Button deleteButton;
        [SerializeField] private Image backgroundImage;
        
        [Header("Visual States")]
        [SerializeField] private Color normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        [SerializeField] private Color selectedColor = new Color(0.3f, 0.5f, 0.8f, 0.9f);
        [SerializeField] private Color hoverColor = new Color(0.25f, 0.25f, 0.3f, 0.85f);

        [Header("Delete Confirmation")]
        [SerializeField] private GameObject deleteConfirmPanel;
        [SerializeField] private Button confirmDeleteButton;
        [SerializeField] private Button cancelDeleteButton;

        // Internal state
        private VRRecordingsGalleryManager.RecordingInfo recordingInfo;
        private VRRecordingsGalleryManager galleryManager;
        private bool isSelected;

        private void Awake()
        {
            // Setup button listeners
            if (playButton != null)
            {
                playButton.onClick.AddListener(OnPlayClicked);
            }
            else
            {
                // If no separate play button, make the whole item clickable
                Button itemButton = GetComponent<Button>();
                if (itemButton != null)
                {
                    itemButton.onClick.AddListener(OnPlayClicked);
                }
            }
            
            if (deleteButton != null)
                deleteButton.onClick.AddListener(OnDeleteClicked);
            
            if (confirmDeleteButton != null)
                confirmDeleteButton.onClick.AddListener(OnConfirmDelete);
            
            if (cancelDeleteButton != null)
                cancelDeleteButton.onClick.AddListener(OnCancelDelete);

            // Hide delete confirmation by default
            if (deleteConfirmPanel != null)
                deleteConfirmPanel.SetActive(false);

            // Set initial background color
            UpdateVisualState();
        }

        /// <summary>
        /// Initializes the list item with recording data
        /// </summary>
        public void Setup(VRRecordingsGalleryManager.RecordingInfo info, VRRecordingsGalleryManager manager)
        {
            recordingInfo = info;
            galleryManager = manager;

            // Update UI texts
            if (titleText != null)
                titleText.text = info.DisplayName;
            
            if (subtitleText != null)
                subtitleText.text = $"{info.FormattedDate} • {info.FormattedSize}";
            
            if (dateText != null)
                dateText.text = info.FormattedDate;
            
            if (sizeText != null)
                sizeText.text = info.FormattedSize;

            // Set thumbnail if available
            if (thumbnailImage != null && info.Thumbnail != null)
            {
                thumbnailImage.texture = info.Thumbnail;
                thumbnailImage.gameObject.SetActive(true);
            }
            else if (thumbnailImage != null)
            {
                // Use default thumbnail or hide
                thumbnailImage.gameObject.SetActive(false);
            }

            UpdateVisualState();
        }

        /// <summary>
        /// Called when play button is clicked
        /// </summary>
        private void OnPlayClicked()
        {
            Debug.Log($"[RecordingListItem] Play clicked: {recordingInfo?.DisplayName}");
            
            if (galleryManager != null && recordingInfo != null)
            {
                Debug.Log($"[RecordingListItem] Playing file: {recordingInfo.FilePath}");
                galleryManager.PlayRecording(recordingInfo.FilePath);
                SetSelected(true);
            }
            else
            {
                Debug.LogError($"[RecordingListItem] Cannot play - galleryManager: {galleryManager != null}, recordingInfo: {recordingInfo != null}");
            }
        }

        /// <summary>
        /// Called when delete button is clicked - shows confirmation
        /// </summary>
        private void OnDeleteClicked()
        {
            if (deleteConfirmPanel != null)
            {
                deleteConfirmPanel.SetActive(true);
            }
            else
            {
                // No confirmation panel, delete directly (not recommended)
                OnConfirmDelete();
            }
        }

        /// <summary>
        /// Called when delete is confirmed
        /// </summary>
        private void OnConfirmDelete()
        {
            if (galleryManager != null && recordingInfo != null)
            {
                galleryManager.DeleteRecording(recordingInfo.FilePath);
            }
            
            if (deleteConfirmPanel != null)
                deleteConfirmPanel.SetActive(false);
        }

        /// <summary>
        /// Called when delete is cancelled
        /// </summary>
        private void OnCancelDelete()
        {
            if (deleteConfirmPanel != null)
                deleteConfirmPanel.SetActive(false);
        }

        /// <summary>
        /// Sets the selected state of this item
        /// </summary>
        public void SetSelected(bool selected)
        {
            isSelected = selected;
            UpdateVisualState();
        }

        /// <summary>
        /// Updates the visual appearance based on state
        /// </summary>
        private void UpdateVisualState()
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = isSelected ? selectedColor : normalColor;
            }
        }

        /// <summary>
        /// Called by EventTrigger when pointer enters
        /// </summary>
        public void OnPointerEnter()
        {
            if (!isSelected && backgroundImage != null)
            {
                backgroundImage.color = hoverColor;
            }
        }

        /// <summary>
        /// Called by EventTrigger when pointer exits
        /// </summary>
        public void OnPointerExit()
        {
            UpdateVisualState();
        }

        /// <summary>
        /// Gets the recording info for this item
        /// </summary>
        public VRRecordingsGalleryManager.RecordingInfo GetRecordingInfo() => recordingInfo;

        /// <summary>
        /// Gets the file path for this recording
        /// </summary>
        public string GetFilePath() => recordingInfo?.FilePath;
    }
}

