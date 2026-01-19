using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

namespace VRRecordings
{
    /// <summary>
    /// Handles video playback on a virtual screen in VR.
    /// Works with Unity's VideoPlayer and supports Quest 3 (Android) playback.
    /// </summary>
    [RequireComponent(typeof(VideoPlayer))]
    public class VRVideoScreenPlayer : MonoBehaviour
    {
        [Header("Video Display")]
        [Tooltip("The RawImage or Renderer that displays the video. If null, creates a RenderTexture.")]
        [SerializeField] private RawImage videoDisplayImage;
        
        [Tooltip("Alternative: MeshRenderer for 3D screen (uses material's main texture)")]
        [SerializeField] private MeshRenderer videoDisplayRenderer;
        
        [Tooltip("RenderTexture resolution width")]
        [SerializeField] private int renderTextureWidth = 1920;
        
        [Tooltip("RenderTexture resolution height")]
        [SerializeField] private int renderTextureHeight = 1080;

        [Header("Audio")]
        [Tooltip("AudioSource for video audio. If null, uses Direct output.")]
        [SerializeField] private AudioSource audioSource;
        
        [Tooltip("Enable spatial audio (3D positioned sound)")]
        [SerializeField] private bool useSpatialAudio = true;
        
        [Tooltip("Default volume (0-1)")]
        [Range(0f, 1f)]
        [SerializeField] private float defaultVolume = 0.8f;

        [Header("UI Controls")]
        [SerializeField] private Button playPauseButton;
        [SerializeField] private Button stopButton;
        [SerializeField] private Slider progressSlider;
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private TextMeshProUGUI timeText;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Image playPauseIcon;
        [SerializeField] private Sprite playIcon;
        [SerializeField] private Sprite pauseIcon;
        
        [Header("Screen Panel")]
        [Tooltip("The parent GameObject containing the video screen (for show/hide)")]
        [SerializeField] private GameObject videoScreenPanel;
        
        [Tooltip("Loading indicator shown while video is preparing")]
        [SerializeField] private GameObject loadingIndicator;

        [Header("Playback Settings")]
        [SerializeField] private bool loopVideo = false;
        [SerializeField] private bool autoHideScreenOnStop = false;

        // Components
        private VideoPlayer videoPlayer;
        private RenderTexture renderTexture;
        
        // State
        private bool isPlaying;
        private bool isPreparing;
        private bool isDraggingSlider;
        private string currentVideoPath;
        private Coroutine updateCoroutine;

        // Events
        public event Action<string> OnVideoStarted;
        public event Action OnVideoStopped;
        public event Action OnVideoPaused;
        public event Action OnVideoResumed;
        public event Action<string> OnVideoError;

        private void Awake()
        {
            videoPlayer = GetComponent<VideoPlayer>();
            SetupVideoPlayer();
            SetupUI();
        }

        private void OnEnable()
        {
            // Subscribe to video player events
            if (videoPlayer != null)
            {
                videoPlayer.prepareCompleted += OnPrepareCompleted;
                videoPlayer.loopPointReached += OnLoopPointReached;
                videoPlayer.errorReceived += OnErrorReceived;
            }
        }

        private void OnDisable()
        {
            // Unsubscribe from events
            if (videoPlayer != null)
            {
                videoPlayer.prepareCompleted -= OnPrepareCompleted;
                videoPlayer.loopPointReached -= OnLoopPointReached;
                videoPlayer.errorReceived -= OnErrorReceived;
            }
            
            StopUpdateCoroutine();
        }

        private void OnDestroy()
        {
            // Cleanup render texture
            if (renderTexture != null)
            {
                renderTexture.Release();
                Destroy(renderTexture);
            }
        }

        /// <summary>
        /// Initial setup of the VideoPlayer component
        /// </summary>
        private void SetupVideoPlayer()
        {
            // Create RenderTexture
            renderTexture = new RenderTexture(renderTextureWidth, renderTextureHeight, 0, RenderTextureFormat.ARGB32);
            renderTexture.Create();

            // Configure VideoPlayer
            videoPlayer.playOnAwake = false;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = renderTexture;
            videoPlayer.isLooping = loopVideo;
            
            // Configure audio
            if (audioSource != null)
            {
                videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
                videoPlayer.SetTargetAudioSource(0, audioSource);
                audioSource.spatialBlend = useSpatialAudio ? 1f : 0f;
                audioSource.volume = defaultVolume;
            }
            else
            {
                // Direct audio output (works on Quest)
                videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
                videoPlayer.SetDirectAudioVolume(0, defaultVolume);
            }

            // Assign RenderTexture to display
            if (videoDisplayImage != null)
            {
                videoDisplayImage.texture = renderTexture;
            }
            else if (videoDisplayRenderer != null)
            {
                // Create a new material instance to avoid modifying shared material
                Material mat = new Material(Shader.Find("Unlit/Texture"));
                mat.mainTexture = renderTexture;
                videoDisplayRenderer.material = mat;
            }
        }

        /// <summary>
        /// Setup UI button listeners
        /// </summary>
        private void SetupUI()
        {
            if (playPauseButton != null)
                playPauseButton.onClick.AddListener(TogglePlayPause);
            
            if (stopButton != null)
                stopButton.onClick.AddListener(Stop);
            
            if (progressSlider != null)
            {
                progressSlider.onValueChanged.AddListener(OnProgressSliderChanged);
            }
            
            if (volumeSlider != null)
            {
                volumeSlider.value = defaultVolume;
                volumeSlider.onValueChanged.AddListener(SetVolume);
            }

            // Initially hide loading indicator
            if (loadingIndicator != null)
                loadingIndicator.SetActive(false);
        }

        /// <summary>
        /// Plays a video from the given file path
        /// </summary>
        public void PlayVideo(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                Debug.LogError("[VRVideoScreenPlayer] Cannot play: file path is empty");
                return;
            }

            if (!System.IO.File.Exists(filePath))
            {
                Debug.LogError($"[VRVideoScreenPlayer] File does not exist: {filePath}");
                return;
            }

            Debug.Log($"[VRVideoScreenPlayer] Starting playback for: {filePath}");

            // Ensure VideoPlayer is set up
            if (videoPlayer == null)
            {
                videoPlayer = GetComponent<VideoPlayer>();
                if (videoPlayer == null)
                {
                    Debug.LogError("[VRVideoScreenPlayer] No VideoPlayer component found!");
                    return;
                }
            }

            // Ensure RenderTexture exists
            if (renderTexture == null)
            {
                renderTexture = new RenderTexture(renderTextureWidth, renderTextureHeight, 0, RenderTextureFormat.ARGB32);
                renderTexture.Create();
                videoPlayer.targetTexture = renderTexture;
                
                if (videoDisplayImage != null)
                    videoDisplayImage.texture = renderTexture;
                    
                Debug.Log("[VRVideoScreenPlayer] Created RenderTexture on demand");
            }

            // Show the video screen
            if (videoScreenPanel != null)
                videoScreenPanel.SetActive(true);

            // Stop any current playback
            if (videoPlayer.isPlaying)
            {
                videoPlayer.Stop();
            }

            currentVideoPath = filePath;
            isPreparing = true;

            // Show loading indicator
            if (loadingIndicator != null)
                loadingIndicator.SetActive(true);

            // Update title
            if (titleText != null)
            {
                titleText.text = System.IO.Path.GetFileNameWithoutExtension(filePath);
            }

            // Set video source - always use URL mode with file:// prefix
            string url;
            if (filePath.StartsWith("file://"))
            {
                url = filePath;
            }
            else
            {
                // Convert to proper file URL (works on all platforms)
                url = "file:///" + filePath.Replace("\\", "/");
            }
            
            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = url;

            Debug.Log($"[VRVideoScreenPlayer] Video URL: {url}");
            Debug.Log($"[VRVideoScreenPlayer] RenderTexture: {renderTexture.width}x{renderTexture.height}, assigned to display: {videoDisplayImage != null}");
            
            // Prepare the video (async)
            videoPlayer.Prepare();
        }

        /// <summary>
        /// Called when video is prepared and ready to play
        /// </summary>
        private void OnPrepareCompleted(VideoPlayer vp)
        {
            Debug.Log($"[VRVideoScreenPlayer] Video prepared! Duration: {vp.length:F1}s, Size: {vp.width}x{vp.height}");
            
            isPreparing = false;
            
            // Hide loading indicator
            if (loadingIndicator != null)
                loadingIndicator.SetActive(false);

            // === DEBUG: Check all display components ===
            Debug.Log($"[VRVideoScreenPlayer] === DISPLAY DIAGNOSTICS ===");
            Debug.Log($"  - RenderTexture exists: {renderTexture != null}");
            Debug.Log($"  - RenderTexture size: {renderTexture?.width}x{renderTexture?.height}");
            Debug.Log($"  - RenderTexture IsCreated: {renderTexture?.IsCreated()}");
            Debug.Log($"  - VideoPlayer.targetTexture: {videoPlayer.targetTexture}");
            Debug.Log($"  - videoDisplayImage (RawImage): {videoDisplayImage != null}");
            Debug.Log($"  - videoDisplayRenderer (MeshRenderer): {videoDisplayRenderer != null}");
            
            if (videoDisplayImage != null)
            {
                Debug.Log($"  - RawImage.texture: {videoDisplayImage.texture}");
                Debug.Log($"  - RawImage.enabled: {videoDisplayImage.enabled}");
                Debug.Log($"  - RawImage.gameObject.activeInHierarchy: {videoDisplayImage.gameObject.activeInHierarchy}");
                Debug.Log($"  - RawImage.color: {videoDisplayImage.color}");
                Debug.Log($"  - RawImage.rectTransform.sizeDelta: {videoDisplayImage.rectTransform.sizeDelta}");
            }
            
            if (videoDisplayRenderer != null)
            {
                Debug.Log($"  - MeshRenderer.enabled: {videoDisplayRenderer.enabled}");
                Debug.Log($"  - MeshRenderer.material.mainTexture: {videoDisplayRenderer.material?.mainTexture}");
            }
            Debug.Log($"[VRVideoScreenPlayer] === END DIAGNOSTICS ===");
            
            // Ensure render texture is assigned to VideoPlayer
            if (videoPlayer.targetTexture != renderTexture)
            {
                videoPlayer.targetTexture = renderTexture;
                Debug.Log("[VRVideoScreenPlayer] Fixed: Re-assigned RenderTexture to VideoPlayer.targetTexture");
            }

            // Ensure render texture is assigned to display
            if (videoDisplayImage != null)
            {
                if (videoDisplayImage.texture != renderTexture)
                {
                    videoDisplayImage.texture = renderTexture;
                    Debug.Log("[VRVideoScreenPlayer] Fixed: Re-assigned RenderTexture to RawImage");
                }
                
                // Ensure RawImage is fully visible
                if (videoDisplayImage.color.a < 1f)
                {
                    videoDisplayImage.color = Color.white;
                    Debug.Log("[VRVideoScreenPlayer] Fixed: Set RawImage color to white (was transparent)");
                }
            }
            else if (videoDisplayRenderer != null)
            {
                if (videoDisplayRenderer.material.mainTexture != renderTexture)
                {
                    videoDisplayRenderer.material.mainTexture = renderTexture;
                    Debug.Log("[VRVideoScreenPlayer] Fixed: Re-assigned RenderTexture to MeshRenderer");
                }
            }
            else
            {
                Debug.LogError("[VRVideoScreenPlayer] ERROR: No display target! Need videoDisplayImage OR videoDisplayRenderer!");
            }

            // Clear the render texture before playing
            RenderTexture.active = renderTexture;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = null;

            // Start playback
            videoPlayer.Play();
            isPlaying = true;

            Debug.Log($"[VRVideoScreenPlayer] Playback started. IsPlaying: {videoPlayer.isPlaying}");

            // Start UI update coroutine
            StartUpdateCoroutine();

            // Update UI
            UpdatePlayPauseIcon();

            OnVideoStarted?.Invoke(currentVideoPath);
            
            // Start a coroutine to check frame output
            StartCoroutine(CheckVideoFramesCoroutine());
        }
        
        /// <summary>
        /// Debug coroutine to check if video frames are being rendered
        /// </summary>
        private IEnumerator CheckVideoFramesCoroutine()
        {
            yield return new WaitForSeconds(0.5f);
            
            if (videoPlayer != null && videoPlayer.isPlaying)
            {
                Debug.Log($"[VRVideoScreenPlayer] After 0.5s:");
                Debug.Log($"  - VideoPlayer.isPlaying: {videoPlayer.isPlaying}");
                Debug.Log($"  - VideoPlayer.frame: {videoPlayer.frame}");
                Debug.Log($"  - VideoPlayer.frameCount: {videoPlayer.frameCount}");
                Debug.Log($"  - VideoPlayer.time: {videoPlayer.time:F2}s");
                
                // Check if render texture has content
                if (renderTexture != null)
                {
                    // Read a pixel from render texture to check if it has content
                    RenderTexture prev = RenderTexture.active;
                    RenderTexture.active = renderTexture;
                    Texture2D temp = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    temp.ReadPixels(new Rect(renderTexture.width / 2, renderTexture.height / 2, 1, 1), 0, 0);
                    temp.Apply();
                    Color centerPixel = temp.GetPixel(0, 0);
                    RenderTexture.active = prev;
                    Destroy(temp);
                    
                    Debug.Log($"  - RenderTexture center pixel: {centerPixel} (black = {Color.black}, not rendering)");
                }
            }
        }

        /// <summary>
        /// Called when video reaches the end
        /// </summary>
        private void OnLoopPointReached(VideoPlayer vp)
        {
            if (!loopVideo)
            {
                isPlaying = false;
                UpdatePlayPauseIcon();
                
                if (autoHideScreenOnStop && videoScreenPanel != null)
                {
                    videoScreenPanel.SetActive(false);
                }

                OnVideoStopped?.Invoke();
            }
        }

        /// <summary>
        /// Called when video player encounters an error
        /// </summary>
        private void OnErrorReceived(VideoPlayer vp, string message)
        {
            Debug.LogError($"[VRVideoScreenPlayer] Video error: {message}");
            isPreparing = false;
            isPlaying = false;
            
            if (loadingIndicator != null)
                loadingIndicator.SetActive(false);

            OnVideoError?.Invoke(message);
        }

        /// <summary>
        /// Toggles between play and pause
        /// </summary>
        public void TogglePlayPause()
        {
            if (isPreparing)
                return;

            if (isPlaying)
            {
                Pause();
            }
            else
            {
                Resume();
            }
        }

        /// <summary>
        /// Pauses video playback
        /// </summary>
        public void Pause()
        {
            if (videoPlayer != null && isPlaying)
            {
                videoPlayer.Pause();
                isPlaying = false;
                UpdatePlayPauseIcon();
                OnVideoPaused?.Invoke();
            }
        }

        /// <summary>
        /// Resumes video playback
        /// </summary>
        public void Resume()
        {
            if (videoPlayer != null && !isPlaying && videoPlayer.isPrepared)
            {
                videoPlayer.Play();
                isPlaying = true;
                UpdatePlayPauseIcon();
                StartUpdateCoroutine();
                OnVideoResumed?.Invoke();
            }
        }

        /// <summary>
        /// Stops video playback
        /// </summary>
        public void Stop()
        {
            if (videoPlayer != null)
            {
                videoPlayer.Stop();
                isPlaying = false;
                isPreparing = false;
                currentVideoPath = null;
                
                StopUpdateCoroutine();
                UpdatePlayPauseIcon();
                
                // Reset progress
                if (progressSlider != null)
                    progressSlider.value = 0;
                
                if (timeText != null)
                    timeText.text = "0:00 / 0:00";

                // Clear render texture to black
                if (renderTexture != null)
                {
                    RenderTexture.active = renderTexture;
                    GL.Clear(true, true, Color.black);
                    RenderTexture.active = null;
                }

                if (autoHideScreenOnStop && videoScreenPanel != null)
                {
                    videoScreenPanel.SetActive(false);
                }

                OnVideoStopped?.Invoke();
            }
        }

        /// <summary>
        /// Seeks to a specific time (in seconds)
        /// </summary>
        public void SeekTo(double timeInSeconds)
        {
            if (videoPlayer != null && videoPlayer.isPrepared)
            {
                videoPlayer.time = Math.Max(0, Math.Min(timeInSeconds, videoPlayer.length));
            }
        }

        /// <summary>
        /// Seeks to a normalized position (0-1)
        /// </summary>
        public void SeekToNormalized(float normalizedPosition)
        {
            if (videoPlayer != null && videoPlayer.isPrepared && videoPlayer.length > 0)
            {
                double time = normalizedPosition * videoPlayer.length;
                SeekTo(time);
            }
        }

        /// <summary>
        /// Sets the audio volume (0-1)
        /// </summary>
        public void SetVolume(float volume)
        {
            volume = Mathf.Clamp01(volume);
            
            if (audioSource != null)
            {
                audioSource.volume = volume;
            }
            else if (videoPlayer != null)
            {
                videoPlayer.SetDirectAudioVolume(0, volume);
            }
        }

        /// <summary>
        /// Gets current playback time in seconds
        /// </summary>
        public double GetCurrentTime()
        {
            return videoPlayer != null ? videoPlayer.time : 0;
        }

        /// <summary>
        /// Gets total video length in seconds
        /// </summary>
        public double GetDuration()
        {
            return videoPlayer != null ? videoPlayer.length : 0;
        }

        /// <summary>
        /// Gets current playback progress (0-1)
        /// </summary>
        public float GetProgress()
        {
            if (videoPlayer != null && videoPlayer.length > 0)
            {
                return (float)(videoPlayer.time / videoPlayer.length);
            }
            return 0;
        }

        /// <summary>
        /// Called when progress slider value changes (user interaction)
        /// </summary>
        private void OnProgressSliderChanged(float value)
        {
            // Only seek if this was a user interaction (not our update)
            if (isDraggingSlider)
            {
                SeekToNormalized(value);
            }
        }

        /// <summary>
        /// Call this when user starts dragging the progress slider
        /// </summary>
        public void OnProgressSliderPointerDown()
        {
            isDraggingSlider = true;
        }

        /// <summary>
        /// Call this when user stops dragging the progress slider
        /// </summary>
        public void OnProgressSliderPointerUp()
        {
            isDraggingSlider = false;
        }

        /// <summary>
        /// Updates the play/pause button icon
        /// </summary>
        private void UpdatePlayPauseIcon()
        {
            if (playPauseIcon != null)
            {
                playPauseIcon.sprite = isPlaying ? pauseIcon : playIcon;
            }
        }

        /// <summary>
        /// Starts the UI update coroutine
        /// </summary>
        private void StartUpdateCoroutine()
        {
            StopUpdateCoroutine();
            updateCoroutine = StartCoroutine(UpdateUICoroutine());
        }

        /// <summary>
        /// Stops the UI update coroutine
        /// </summary>
        private void StopUpdateCoroutine()
        {
            if (updateCoroutine != null)
            {
                StopCoroutine(updateCoroutine);
                updateCoroutine = null;
            }
        }

        /// <summary>
        /// Coroutine that updates UI elements during playback
        /// </summary>
        private IEnumerator UpdateUICoroutine()
        {
            WaitForSeconds wait = new WaitForSeconds(0.1f);
            
            while (isPlaying || isPreparing)
            {
                UpdateTimeDisplay();
                UpdateProgressSlider();
                yield return wait;
            }
        }

        /// <summary>
        /// Updates the time text display
        /// </summary>
        private void UpdateTimeDisplay()
        {
            if (timeText != null && videoPlayer != null && videoPlayer.isPrepared)
            {
                TimeSpan current = TimeSpan.FromSeconds(videoPlayer.time);
                TimeSpan total = TimeSpan.FromSeconds(videoPlayer.length);
                
                string currentStr = current.TotalHours >= 1 
                    ? current.ToString(@"h\:mm\:ss") 
                    : current.ToString(@"m\:ss");
                    
                string totalStr = total.TotalHours >= 1 
                    ? total.ToString(@"h\:mm\:ss") 
                    : total.ToString(@"m\:ss");
                
                timeText.text = $"{currentStr} / {totalStr}";
            }
        }

        /// <summary>
        /// Updates the progress slider position
        /// </summary>
        private void UpdateProgressSlider()
        {
            if (progressSlider != null && !isDraggingSlider && videoPlayer != null && videoPlayer.isPrepared)
            {
                progressSlider.value = GetProgress();
            }
        }

        /// <summary>
        /// Shows the video screen panel
        /// </summary>
        public void ShowScreen()
        {
            if (videoScreenPanel != null)
                videoScreenPanel.SetActive(true);
        }

        /// <summary>
        /// Hides the video screen panel
        /// </summary>
        public void HideScreen()
        {
            if (videoScreenPanel != null)
                videoScreenPanel.SetActive(false);
        }

        /// <summary>
        /// Returns true if currently playing
        /// </summary>
        public bool IsPlaying => isPlaying;

        /// <summary>
        /// Returns true if video is preparing
        /// </summary>
        public bool IsPreparing => isPreparing;

        /// <summary>
        /// Returns the current video file path
        /// </summary>
        public string CurrentVideoPath => currentVideoPath;

#if UNITY_EDITOR
        [ContextMenu("Test Play Sample Video")]
        private void TestPlaySample()
        {
            // For testing in editor, find any video in the project
            string[] videos = System.IO.Directory.GetFiles(Application.dataPath, "*.mp4", System.IO.SearchOption.AllDirectories);
            if (videos.Length > 0)
            {
                PlayVideo(videos[0]);
            }
            else
            {
                Debug.Log("[VRVideoScreenPlayer] No test videos found in project.");
            }
        }
#endif
    }
}

