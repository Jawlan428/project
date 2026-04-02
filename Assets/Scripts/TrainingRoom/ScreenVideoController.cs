using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VRRecordings;

namespace TrainingRoom
{
    /// <summary>
    /// Self-contained video controller for the Training Room screen.
    /// Attach to any GameObject in the Training Room. Works independently
    /// without needing TrainingRoomManager's video library to be populated.
    ///
    /// Inspector setup:
    ///   1. Assign ScreenPlayer (the VRVideoScreenPlayer on VideoPlayer_Root)
    ///   2. Fill VideoFileNames OR enable AutoScanFolder to detect all MP4s at runtime
    ///   3. Assign the buttons and label from ControlsCanvas
    /// </summary>
    public class ScreenVideoController : MonoBehaviour
    {
        [Header("Video Player")]
        [Tooltip("The VRVideoScreenPlayer component on the VideoPlayer_Root")]
        [SerializeField] private VRVideoScreenPlayer screenPlayer;

        [Header("Video Playlist")]
        [Tooltip("When true, automatically scans StreamingAssets/TrainingVideos/ at startup for all MP4 files")]
        [SerializeField] private bool autoScanFolder = true;

        [Tooltip("MP4 filenames inside Assets/StreamingAssets/TrainingVideos/ (e.g. 'harvest.mp4'). " +
                 "Auto-populated when AutoScanFolder is true.")]
        [SerializeField] private string[] videoFileNames;

        [Tooltip("Friendly display titles matching the videoFileNames array")]
        [SerializeField] private string[] videoTitles;

        [Header("Controls UI")]
        [SerializeField] private Button playPauseButton;
        [SerializeField] private Button stopButton;
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;

        [Header("Labels")]
        [Tooltip("The 'No video selected' label in the ControlsCanvas")]
        [SerializeField] private TextMeshProUGUI nowPlayingLabel;

        [Tooltip("Shows current / total (e.g. '1 / 4')")]
        [SerializeField] private TextMeshProUGUI indexLabel;

        [Header("Play/Pause Icons (optional)")]
        [SerializeField] private Sprite playSprite;
        [SerializeField] private Sprite pauseSprite;

        // ── State ─────────────────────────────────────────────────────────────
        private int   _currentIndex = -1;
        private bool  _isPlaying;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Start()
        {
            // Auto-scan folder for all MP4s if enabled or playlist is empty
            if (autoScanFolder || videoFileNames == null || videoFileNames.Length == 0)
                ScanVideoFolder();

            WireButtons();

            if (screenPlayer != null)
            {
                screenPlayer.OnVideoStarted  += OnVideoStarted;
                screenPlayer.OnVideoStopped  += OnVideoStopped;
                screenPlayer.OnVideoPaused   += OnVideoPaused;
                screenPlayer.OnVideoResumed  += OnVideoResumed;
            }

            RefreshUI();

            // Auto-select first video so Play works immediately
            if (videoFileNames != null && videoFileNames.Length > 0)
                SelectVideo(0);
        }

        /// <summary>
        /// Scans StreamingAssets/TrainingVideos/ and populates the playlist at runtime.
        /// Called automatically on Start when autoScanFolder is true.
        /// </summary>
        public void ScanVideoFolder()
        {
            string folder = Path.Combine(Application.streamingAssetsPath, "TrainingVideos");
            if (!Directory.Exists(folder))
            {
                Debug.LogWarning("[ScreenVideoController] TrainingVideos folder not found: " + folder);
                return;
            }

            string[] found = Directory.GetFiles(folder, "*.mp4")
                                      .Select(Path.GetFileName)
                                      .OrderBy(f => f)
                                      .ToArray();

            if (found.Length == 0)
            {
                Debug.LogWarning("[ScreenVideoController] No MP4 files found in " + folder);
                return;
            }

            // Merge: keep existing inspector-assigned titles if lengths match,
            // otherwise just use filenames as fallback titles
            bool titlesValid = videoTitles != null && videoTitles.Length == found.Length;

            videoFileNames = found;

            if (!titlesValid)
            {
                videoTitles = found.Select(f =>
                {
                    // Try to make a clean title from the filename
                    string name = Path.GetFileNameWithoutExtension(f);
                    // Replace underscores/hyphens and title-case
                    name = System.Text.RegularExpressions.Regex.Replace(name, @"[-_]", " ");
                    // Remove resolution suffixes like "uhd_3840_2160_25fps"
                    name = System.Text.RegularExpressions.Regex.Replace(name, @"\s+\d{3,4}\s+\d{3,4}\s+\d{2,3}fps.*", "",
                               System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
                    return name;
                }).ToArray();
            }

            Debug.Log($"[ScreenVideoController] Playlist scanned: {found.Length} video(s) found.");
            foreach (var f in found)
                Debug.Log($"  • {f}");
        }

        private void OnDestroy()
        {
            if (screenPlayer == null) return;
            screenPlayer.OnVideoStarted -= OnVideoStarted;
            screenPlayer.OnVideoStopped -= OnVideoStopped;
            screenPlayer.OnVideoPaused  -= OnVideoPaused;
            screenPlayer.OnVideoResumed -= OnVideoResumed;
        }

        // ── Button wiring ─────────────────────────────────────────────────────

        private void WireButtons()
        {
            if (playPauseButton != null) playPauseButton.onClick.AddListener(OnPlayPauseClicked);
            if (stopButton      != null) stopButton.onClick.AddListener(OnStopClicked);
            if (prevButton      != null) prevButton.onClick.AddListener(OnPrevClicked);
            if (nextButton      != null) nextButton.onClick.AddListener(OnNextClicked);
        }

        // ── Button callbacks ──────────────────────────────────────────────────

        private void OnPlayPauseClicked()
        {
            if (screenPlayer == null) return;

            if (screenPlayer.IsPlaying)
            {
                screenPlayer.Pause();
            }
            else if (screenPlayer.IsPreparing)
            {
                // still loading — ignore
            }
            else if (_currentIndex >= 0)
            {
                if (screenPlayer.CurrentVideoPath != null)
                    screenPlayer.Resume();
                else
                    PlayCurrentVideo();
            }
            else if (videoFileNames != null && videoFileNames.Length > 0)
            {
                SelectVideo(0);
                PlayCurrentVideo();
            }
        }

        private void OnStopClicked()
        {
            screenPlayer?.Stop();
        }

        private void OnPrevClicked()
        {
            if (videoFileNames == null || videoFileNames.Length == 0) return;
            // Stop current playback cleanly before switching
            screenPlayer?.Stop();
            int prev = _currentIndex <= 0 ? videoFileNames.Length - 1 : _currentIndex - 1;
            SelectVideo(prev);
            PlayCurrentVideo();
        }

        private void OnNextClicked()
        {
            if (videoFileNames == null || videoFileNames.Length == 0) return;
            // Stop current playback cleanly before switching
            screenPlayer?.Stop();
            int next = (_currentIndex + 1) % videoFileNames.Length;
            SelectVideo(next);
            PlayCurrentVideo();
        }

        // ── Core playback ─────────────────────────────────────────────────────

        public void SelectVideo(int index)
        {
            if (videoFileNames == null || index < 0 || index >= videoFileNames.Length) return;
            _currentIndex = index;
            RefreshUI();
        }

        public void PlayCurrentVideo()
        {
            if (screenPlayer == null || videoFileNames == null || _currentIndex < 0) return;
            if (_currentIndex >= videoFileNames.Length) return;

            string fileName = videoFileNames[_currentIndex];
            if (string.IsNullOrWhiteSpace(fileName))
            {
                Debug.LogWarning("[ScreenVideoController] Video filename is empty for index " + _currentIndex);
                return;
            }

            string fullPath = Path.Combine(Application.streamingAssetsPath, "TrainingVideos", fileName);
            Debug.Log($"[ScreenVideoController] Playing: {fullPath}");
            screenPlayer.PlayVideo(fullPath);
        }

        // ── VRVideoScreenPlayer callbacks ─────────────────────────────────────

        private void OnVideoStarted(string path)
        {
            _isPlaying = true;
            RefreshPlayPauseIcon();
        }

        private void OnVideoStopped()
        {
            _isPlaying = false;
            RefreshUI();
        }

        private void OnVideoPaused()
        {
            _isPlaying = false;
            RefreshPlayPauseIcon();
        }

        private void OnVideoResumed()
        {
            _isPlaying = true;
            RefreshPlayPauseIcon();
        }

        // ── UI refresh ────────────────────────────────────────────────────────

        private void RefreshUI()
        {
            // Now playing label
            if (nowPlayingLabel != null)
            {
                if (_currentIndex >= 0 && videoTitles != null && _currentIndex < videoTitles.Length
                    && !string.IsNullOrWhiteSpace(videoTitles[_currentIndex]))
                {
                    nowPlayingLabel.text = videoTitles[_currentIndex];
                }
                else if (_currentIndex >= 0 && videoFileNames != null && _currentIndex < videoFileNames.Length)
                {
                    nowPlayingLabel.text = Path.GetFileNameWithoutExtension(videoFileNames[_currentIndex]);
                }
                else
                {
                    nowPlayingLabel.text = "No video selected";
                }
            }

            // Index label (e.g. "1 / 4")
            if (indexLabel != null && videoFileNames != null && videoFileNames.Length > 0)
            {
                indexLabel.text = $"{_currentIndex + 1} / {videoFileNames.Length}";
            }

            RefreshPlayPauseIcon();
        }

        private void RefreshPlayPauseIcon()
        {
            if (playPauseButton == null) return;

            // Plain ASCII labels so any TMP font renders them on one line
            var label = playPauseButton.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
                label.text = _isPlaying ? "Pause" : "Play";

            // Update icon sprite if assigned
            var img = playPauseButton.GetComponentInChildren<Image>();
            if (img != null)
            {
                if (_isPlaying && pauseSprite != null) img.sprite = pauseSprite;
                else if (!_isPlaying && playSprite != null) img.sprite = playSprite;
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Test: Play First Video")]
        private void EditorTestPlayFirst()
        {
            SelectVideo(0);
            PlayCurrentVideo();
        }
#endif
    }
}
