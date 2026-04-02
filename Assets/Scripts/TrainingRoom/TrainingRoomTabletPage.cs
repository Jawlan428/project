using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TrainingRoom
{
    /// <summary>
    /// Tablet page UI for the VR Training Room.
    ///
    /// Add this script to the "Training" page GameObject inside the Smart Tablet,
    /// then add a "Training" tab button in TabletAppController like the other pages.
    ///
    /// Layout expected (create in Unity):
    ///   TrainingPage (this script)
    ///   ├── Header / TitleText            ← section label
    ///   ├── VideoListScrollView
    ///   │   └── Content                  ← _playlistContent (assign in inspector)
    ///   ├── NowPlayingPanel
    ///   │   ├── NowPlayingTitle           ← _nowPlayingTitle
    ///   │   ├── CategoryLabel             ← _categoryLabel
    ///   │   └── ProgressText              ← _progressText
    ///   └── ControlsBar
    ///       ├── PlayPauseButton           ← _playPauseButton
    ///       ├── StopButton                ← _stopButton
    ///       ├── PrevButton                ← _prevButton
    ///       └── NextButton                ← _nextButton
    /// </summary>
    public class TrainingRoomTabletPage : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Manager")]
        [SerializeField] private TrainingRoomManager trainingRoomManager;

        [Header("Playlist")]
        [Tooltip("Content transform of the scroll view — row prefabs are instantiated here")]
        [SerializeField] private Transform playlistContent;

        [Tooltip("Prefab for a single playlist row (needs PlaylistRowUI component)")]
        [SerializeField] private GameObject playlistRowPrefab;

        [Header("Now Playing Panel")]
        [SerializeField] private TextMeshProUGUI nowPlayingTitle;
        [SerializeField] private TextMeshProUGUI categoryLabel;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private Image thumbnailImage;

        [Header("Playback Controls")]
        [SerializeField] private Button playPauseButton;
        [SerializeField] private Button stopButton;
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;

        [Header("Play/Pause Icons")]
        [SerializeField] private Sprite playSprite;
        [SerializeField] private Sprite pauseSprite;

        [Header("Network Sync (optional)")]
        [Tooltip("If assigned, all playback commands will go through the network sync layer")]
        [SerializeField] private VideoPlaybackNetworkSync networkSync;

        // ── State ─────────────────────────────────────────────────────────────

        private readonly List<PlaylistRowUI> _rows = new();
        private bool _isPlaying;
        private Coroutine _progressUpdateCoroutine;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Start()
        {
            if (trainingRoomManager == null)
                trainingRoomManager = FindFirstObjectByType<TrainingRoomManager>();

            WireButtons();
            SubscribeToManager();
            BuildPlaylist();
            RefreshNowPlaying();
        }

        private void OnEnable()
        {
            SubscribeToManager();

            if (_progressUpdateCoroutine == null && _isPlaying)
                _progressUpdateCoroutine = StartCoroutine(ProgressUpdateLoop());
        }

        private void OnDisable()
        {
            UnsubscribeFromManager();
            StopProgressUpdate();
        }

        // ── Playlist construction ─────────────────────────────────────────────

        private void BuildPlaylist()
        {
            if (playlistContent == null || playlistRowPrefab == null || trainingRoomManager == null)
                return;

            // Clear existing rows
            foreach (var row in _rows)
                if (row != null) Destroy(row.gameObject);
            _rows.Clear();

            var library = trainingRoomManager.VideoLibrary;
            for (int i = 0; i < library.Count; i++)
            {
                int capturedIndex = i;
                var entry = library[i];

                GameObject rowGO = Instantiate(playlistRowPrefab, playlistContent);
                var rowUI = rowGO.GetComponent<PlaylistRowUI>();

                if (rowUI != null)
                {
                    rowUI.Setup(entry, capturedIndex, OnRowSelected);
                    _rows.Add(rowUI);
                }
            }
        }

        // ── Button wiring ─────────────────────────────────────────────────────

        private void WireButtons()
        {
            if (playPauseButton != null) playPauseButton.onClick.AddListener(OnPlayPauseClicked);
            if (stopButton      != null) stopButton.onClick.AddListener(OnStopClicked);
            if (prevButton      != null) prevButton.onClick.AddListener(OnPrevClicked);
            if (nextButton      != null) nextButton.onClick.AddListener(OnNextClicked);
        }

        private void OnPlayPauseClicked()
        {
            if (trainingRoomManager == null) return;

            if (trainingRoomManager.CurrentIndex < 0)
            {
                // Nothing selected — auto-select and play first video
                CommandPlay(0);
                return;
            }

            if (_isPlaying)
                CommandPause();
            else
                CommandResume();
        }

        private void OnStopClicked()   => CommandStop();
        private void OnPrevClicked()   => CommandPlay(Mathf.Max(0, trainingRoomManager.CurrentIndex - 1));
        private void OnNextClicked()
        {
            if (trainingRoomManager == null) return;
            int next = trainingRoomManager.CurrentIndex + 1;
            if (next < trainingRoomManager.VideoLibrary.Count)
                CommandPlay(next);
        }

        private void OnRowSelected(int index) => CommandPlay(index);

        // ── Commands (routed through network sync when available) ─────────────

        private void CommandPlay(int index)
        {
            if (networkSync != null && networkSync.IsNetworkReady)
                networkSync.RequestPlay(index);
            else
                trainingRoomManager?.PlayVideoAtIndex(index);
        }

        private void CommandPause()
        {
            if (networkSync != null && networkSync.IsNetworkReady)
                networkSync.RequestPause();
            else
                trainingRoomManager?.Pause();
        }

        private void CommandResume()
        {
            if (networkSync != null && networkSync.IsNetworkReady)
                networkSync.RequestResume();
            else
                trainingRoomManager?.Resume();
        }

        private void CommandStop()
        {
            if (networkSync != null && networkSync.IsNetworkReady)
                networkSync.RequestStop();
            else
                trainingRoomManager?.Stop();
        }

        // ── Manager event handlers ────────────────────────────────────────────

        private void SubscribeToManager()
        {
            if (trainingRoomManager == null) return;
            trainingRoomManager.OnVideoStarted  += OnManagerVideoStarted;
            trainingRoomManager.OnVideoStopped  += OnManagerVideoStopped;
            trainingRoomManager.OnVideoPaused   += OnManagerVideoPaused;
            trainingRoomManager.OnVideoResumed  += OnManagerVideoResumed;
            trainingRoomManager.OnVideoSelected += OnManagerVideoSelected;
        }

        private void UnsubscribeFromManager()
        {
            if (trainingRoomManager == null) return;
            trainingRoomManager.OnVideoStarted  -= OnManagerVideoStarted;
            trainingRoomManager.OnVideoStopped  -= OnManagerVideoStopped;
            trainingRoomManager.OnVideoPaused   -= OnManagerVideoPaused;
            trainingRoomManager.OnVideoResumed  -= OnManagerVideoResumed;
            trainingRoomManager.OnVideoSelected -= OnManagerVideoSelected;
        }

        private void OnManagerVideoStarted(int index)
        {
            _isPlaying = true;
            RefreshNowPlaying();
            RefreshPlayPauseIcon();
            HighlightRow(index);
            StartProgressUpdate();
        }

        private void OnManagerVideoStopped()
        {
            _isPlaying = false;
            RefreshPlayPauseIcon();
            StopProgressUpdate();
            if (progressText != null) progressText.text = "0:00 / 0:00";
        }

        private void OnManagerVideoPaused()
        {
            _isPlaying = false;
            RefreshPlayPauseIcon();
            StopProgressUpdate();
        }

        private void OnManagerVideoResumed()
        {
            _isPlaying = true;
            RefreshPlayPauseIcon();
            StartProgressUpdate();
        }

        private void OnManagerVideoSelected(int index)
        {
            HighlightRow(index);
            RefreshNowPlaying();
        }

        // ── UI refresh helpers ────────────────────────────────────────────────

        private void RefreshNowPlaying()
        {
            var entry = trainingRoomManager?.CurrentEntry;

            if (nowPlayingTitle != null)
                nowPlayingTitle.text = entry != null ? entry.title : "No video selected";

            if (categoryLabel != null)
                categoryLabel.text = entry != null ? entry.GetCategoryLabel() : "";

            if (thumbnailImage != null)
            {
                bool hasThumbnail = entry != null && entry.thumbnail != null;
                thumbnailImage.sprite  = hasThumbnail ? entry.thumbnail : null;
                thumbnailImage.enabled = hasThumbnail;
            }
        }

        private void RefreshPlayPauseIcon()
        {
            if (playPauseButton == null) return;
            var imgComp = playPauseButton.GetComponentInChildren<Image>();
            if (imgComp == null) return;

            Sprite target = _isPlaying ? pauseSprite : playSprite;
            if (target != null) imgComp.sprite = target;
        }

        private void HighlightRow(int selectedIndex)
        {
            for (int i = 0; i < _rows.Count; i++)
                _rows[i]?.SetHighlighted(i == selectedIndex);
        }

        // ── Progress update coroutine ─────────────────────────────────────────

        private void StartProgressUpdate()
        {
            StopProgressUpdate();
            _progressUpdateCoroutine = StartCoroutine(ProgressUpdateLoop());
        }

        private void StopProgressUpdate()
        {
            if (_progressUpdateCoroutine != null)
            {
                StopCoroutine(_progressUpdateCoroutine);
                _progressUpdateCoroutine = null;
            }
        }

        private System.Collections.IEnumerator ProgressUpdateLoop()
        {
            var wait = new WaitForSeconds(0.25f);
            while (_isPlaying && trainingRoomManager != null)
            {
                UpdateProgressText();
                yield return wait;
            }
            _progressUpdateCoroutine = null;
        }

        private void UpdateProgressText()
        {
            if (progressText == null || trainingRoomManager == null) return;
            float progress = trainingRoomManager.GetNormalizedProgress();
            // We only have normalized 0-1; the VRVideoScreenPlayer handles formatted time.
            int pct = Mathf.RoundToInt(progress * 100f);
            progressText.text = $"{pct}%";
        }
    }
}
