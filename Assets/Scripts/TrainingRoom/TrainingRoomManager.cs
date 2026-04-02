using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
using VRRecordings;
using SmartFarm;

namespace TrainingRoom
{
    /// <summary>
    /// Central manager for the VR Agricultural Training Room.
    ///
    /// Responsibilities:
    ///   - Owns the video library (list of TrainingVideoEntry assets)
    ///   - Drives the existing VRVideoScreenPlayer for playback
    ///   - Handles 360-video mode by switching skybox material
    ///   - Logs all player events via SmartFarm.EventLogger
    ///   - Provides a clean API used by TrainingRoomTabletPage and VideoPlaybackNetworkSync
    ///
    /// Setup: Add to a persistent GameObject in the Training Room scene/area.
    /// </summary>
    public class TrainingRoomManager : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Video Library")]
        [Tooltip("Ordered list of TrainingVideoEntry ScriptableObjects to show in the playlist")]
        [SerializeField] private List<TrainingVideoEntry> videoLibrary = new();

        [Header("Screen References")]
        [Tooltip("The VRVideoScreenPlayer attached to the flat training screen")]
        [SerializeField] private VRVideoScreenPlayer flatScreenPlayer;

        [Tooltip("MeshRenderer of the flat screen quad/plane (used to show/hide the screen)")]
        [SerializeField] private MeshRenderer flatScreenMesh;

        [Header("360 Video")]
        [Tooltip("Sphere used as the 360-video skybox (inverted normals sphere)")]
        [SerializeField] private MeshRenderer sphere360Renderer;

        [Tooltip("Material applied to the 360 sphere — must use an Unlit/Texture or equirectangular shader")]
        [SerializeField] private Material sphere360Material;

        [Header("Room Lighting")]
        [Tooltip("Room lights dimmed during playback")]
        [SerializeField] private Light[] roomLights;

        [Tooltip("Intensity multiplier when video is playing (0 = fully dark)")]
        [Range(0f, 1f)]
        [SerializeField] private float dimmedIntensity = 0.15f;

        [Header("Subtitles (optional)")]
        [Tooltip("WorldSpace TextMeshPro for simple video subtitles below the screen")]
        [SerializeField] private TMPro.TextMeshPro subtitleText;

        [Tooltip("How long each subtitle line stays visible (seconds)")]
        [SerializeField] private float subtitleDuration = 5f;

        // ── Public events ─────────────────────────────────────────────────────

        /// <summary>Raised when a video starts. Arg = video index in library.</summary>
        public event Action<int> OnVideoStarted;

        /// <summary>Raised when playback is paused.</summary>
        public event Action OnVideoPaused;

        /// <summary>Raised when playback is resumed.</summary>
        public event Action OnVideoResumed;

        /// <summary>Raised when playback is stopped.</summary>
        public event Action OnVideoStopped;

        /// <summary>Raised when selected video index changes (before playback starts).</summary>
        public event Action<int> OnVideoSelected;

        // ── State ─────────────────────────────────────────────────────────────

        private int _currentIndex = -1;
        private bool _is360Mode;
        private float[] _originalLightIntensities;
        private Coroutine _subtitleHideCoroutine;

        // ── Properties ────────────────────────────────────────────────────────

        public IReadOnlyList<TrainingVideoEntry> VideoLibrary => videoLibrary;
        public int CurrentIndex => _currentIndex;
        public bool IsPlaying => flatScreenPlayer != null && flatScreenPlayer.IsPlaying;
        public bool Is360Mode => _is360Mode;

        public TrainingVideoEntry CurrentEntry =>
            _currentIndex >= 0 && _currentIndex < videoLibrary.Count
                ? videoLibrary[_currentIndex]
                : null;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            CacheOriginalLightIntensities();
        }

        private void OnEnable()
        {
            if (flatScreenPlayer != null)
            {
                flatScreenPlayer.OnVideoStarted  += HandleVideoStarted;
                flatScreenPlayer.OnVideoStopped  += HandleVideoStopped;
                flatScreenPlayer.OnVideoPaused   += HandleVideoPaused;
                flatScreenPlayer.OnVideoResumed  += HandleVideoResumed;
                flatScreenPlayer.OnVideoError    += HandleVideoError;
            }
        }

        private void OnDisable()
        {
            if (flatScreenPlayer != null)
            {
                flatScreenPlayer.OnVideoStarted  -= HandleVideoStarted;
                flatScreenPlayer.OnVideoStopped  -= HandleVideoStopped;
                flatScreenPlayer.OnVideoPaused   -= HandleVideoPaused;
                flatScreenPlayer.OnVideoResumed  -= HandleVideoResumed;
                flatScreenPlayer.OnVideoError    -= HandleVideoError;
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Selects a video by index (0-based) without starting playback.
        /// Updates the tablet UI selection state.
        /// </summary>
        public void SelectVideo(int index)
        {
            if (index < 0 || index >= videoLibrary.Count) return;
            _currentIndex = index;
            OnVideoSelected?.Invoke(index);
            EventLogger.LogEvent($"[TrainingRoom] Selected: {videoLibrary[index].title}");
        }

        /// <summary>
        /// Selects and immediately plays the video at the given library index.
        /// </summary>
        public void PlayVideoAtIndex(int index)
        {
            if (index < 0 || index >= videoLibrary.Count)
            {
                Debug.LogWarning($"[TrainingRoomManager] Index {index} is out of range.");
                return;
            }

            SelectVideo(index);
            var entry = videoLibrary[index];
            string path = entry.GetRuntimePath();

            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError($"[TrainingRoomManager] '{entry.title}' has no StreamingAssets path configured.");
                return;
            }

            _is360Mode = entry.is360Video;

            if (_is360Mode)
                Play360Video(path);
            else
                PlayFlatVideo(path);
        }

        /// <summary>Toggle play/pause for the current video.</summary>
        public void TogglePlayPause()
        {
            if (flatScreenPlayer == null) return;

            if (_is360Mode)
            {
                // 360 player also uses the same VRVideoScreenPlayer
            }

            flatScreenPlayer.TogglePlayPause();
        }

        /// <summary>Pause playback.</summary>
        public void Pause()
        {
            flatScreenPlayer?.Pause();
        }

        /// <summary>Resume playback.</summary>
        public void Resume()
        {
            flatScreenPlayer?.Resume();
        }

        /// <summary>Stop playback and reset the screen.</summary>
        public void Stop()
        {
            flatScreenPlayer?.Stop();
            Set360ModeActive(false);
        }

        /// <summary>
        /// Seek to a normalized progress value (0–1).
        /// Called by VideoPlaybackNetworkSync to apply a remote seek command.
        /// </summary>
        public void SeekToNormalized(float progress)
        {
            flatScreenPlayer?.SeekToNormalized(progress);
        }

        /// <summary>Gets current normalized progress (0–1) for network sync.</summary>
        public float GetNormalizedProgress()
        {
            return flatScreenPlayer != null ? flatScreenPlayer.GetProgress() : 0f;
        }

        /// <summary>
        /// Shows a subtitle line below the screen.
        /// Called externally (e.g., a future subtitle file reader).
        /// </summary>
        public void ShowSubtitle(string text)
        {
            if (subtitleText == null) return;

            subtitleText.text = text;
            subtitleText.gameObject.SetActive(true);

            if (_subtitleHideCoroutine != null)
                StopCoroutine(_subtitleHideCoroutine);

            _subtitleHideCoroutine = StartCoroutine(HideSubtitleAfterDelay(subtitleDuration));
        }

        // ── Flat video ────────────────────────────────────────────────────────

        private void PlayFlatVideo(string path)
        {
            if (flatScreenPlayer == null)
            {
                Debug.LogError("[TrainingRoomManager] No VRVideoScreenPlayer assigned!");
                return;
            }

            Set360ModeActive(false);
            if (flatScreenMesh != null) flatScreenMesh.enabled = true;

            flatScreenPlayer.PlayVideo(path);
        }

        // ── 360 video ─────────────────────────────────────────────────────────

        private void Play360Video(string path)
        {
            if (flatScreenPlayer == null) return;

            // Reuse the same VRVideoScreenPlayer but route output to the 360 sphere
            if (sphere360Renderer != null && sphere360Material != null)
            {
                // Assign render texture from the video player to the 360 sphere material
                // The VRVideoScreenPlayer will output to its render texture;
                // we mirror that to the sphere material after prepare.
                flatScreenPlayer.PlayVideo(path);
                Set360ModeActive(true);
            }
            else
            {
                Debug.LogWarning("[TrainingRoomManager] 360 sphere references not set. Playing as flat video.");
                PlayFlatVideo(path);
            }
        }

        private void Set360ModeActive(bool active)
        {
            _is360Mode = active;
            if (sphere360Renderer != null) sphere360Renderer.enabled = active;
            if (flatScreenMesh    != null) flatScreenMesh.enabled    = !active;
        }

        // ── Light dimming ─────────────────────────────────────────────────────

        private void CacheOriginalLightIntensities()
        {
            if (roomLights == null || roomLights.Length == 0) return;
            _originalLightIntensities = new float[roomLights.Length];
            for (int i = 0; i < roomLights.Length; i++)
            {
                if (roomLights[i] != null)
                    _originalLightIntensities[i] = roomLights[i].intensity;
            }
        }

        private void DimLights(bool dim)
        {
            if (roomLights == null) return;
            for (int i = 0; i < roomLights.Length; i++)
            {
                if (roomLights[i] == null) continue;
                roomLights[i].intensity = dim
                    ? _originalLightIntensities[i] * dimmedIntensity
                    : _originalLightIntensities[i];
            }
        }

        // ── VRVideoScreenPlayer callbacks ─────────────────────────────────────

        private void HandleVideoStarted(string path)
        {
            DimLights(true);

            string title = CurrentEntry?.title ?? System.IO.Path.GetFileNameWithoutExtension(path);
            EventLogger.LogEvent($"[TrainingRoom] Video started: {title}");
            OnVideoStarted?.Invoke(_currentIndex);
        }

        private void HandleVideoStopped()
        {
            DimLights(false);
            HideSubtitleImmediate();
            EventLogger.LogEvent("[TrainingRoom] Video stopped");
            OnVideoStopped?.Invoke();
        }

        private void HandleVideoPaused()
        {
            EventLogger.LogEvent("[TrainingRoom] Video paused");
            OnVideoPaused?.Invoke();
        }

        private void HandleVideoResumed()
        {
            EventLogger.LogEvent("[TrainingRoom] Video resumed");
            OnVideoResumed?.Invoke();
        }

        private void HandleVideoError(string error)
        {
            DimLights(false);
            Debug.LogError($"[TrainingRoomManager] Playback error: {error}");
            EventLogger.LogEvent($"[TrainingRoom] Playback error: {error}");
        }

        // ── Subtitle helpers ──────────────────────────────────────────────────

        private void HideSubtitleImmediate()
        {
            if (_subtitleHideCoroutine != null)
            {
                StopCoroutine(_subtitleHideCoroutine);
                _subtitleHideCoroutine = null;
            }
            if (subtitleText != null)
                subtitleText.gameObject.SetActive(false);
        }

        private System.Collections.IEnumerator HideSubtitleAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (subtitleText != null)
                subtitleText.gameObject.SetActive(false);
            _subtitleHideCoroutine = null;
        }
    }
}
