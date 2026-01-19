using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace VRRecordings
{
    /// <summary>
    /// Plays frame-based recordings (JPEG sequences) on a virtual screen.
    /// Used for Quest recordings that haven't been converted to MP4 yet.
    /// </summary>
    public class VRFrameSequencePlayer : MonoBehaviour
    {
        [Header("Display")]
        [Tooltip("RawImage to display frames")]
        [SerializeField] private RawImage displayImage;
        
        [Tooltip("Alternative: MeshRenderer for 3D screen")]
        [SerializeField] private MeshRenderer displayRenderer;
        
        [Header("Audio")]
        [Tooltip("AudioSource for audio playback")]
        [SerializeField] private AudioSource audioSource;
        
        [Header("UI")]
        [Tooltip("Title text")]
        [SerializeField] private TextMeshProUGUI titleText;
        
        [Tooltip("Time display")]
        [SerializeField] private TextMeshProUGUI timeText;
        
        [Tooltip("Loading indicator")]
        [SerializeField] private GameObject loadingIndicator;
        
        [Header("Playback Settings")]
        [SerializeField] private float frameRate = 24f;
        [SerializeField] private bool loop = false;
        [SerializeField] private bool autoPlay = true;

        private List<string> framePaths = new List<string>();
        private List<Texture2D> loadedTextures = new List<Texture2D>();
        private int currentFrameIndex = 0;
        private bool isPlaying = false;
        private bool isPreparing = false;
        private float playbackStartTime = 0f;
        private string currentRecordingPath;
        private AudioClip audioClip;
        private Coroutine playbackCoroutine;

        public event Action OnPlaybackStarted;
        public event Action OnPlaybackStopped;
        public event Action<string> OnError;

        private void Awake()
        {
            if (displayImage == null && displayRenderer == null)
            {
                Debug.LogError("[VRFrameSequencePlayer] No display target assigned!");
            }
        }

        /// <summary>
        /// Loads and plays a frame-based recording from a folder path
        /// </summary>
        public void PlayRecording(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
            {
                Debug.LogError("[VRFrameSequencePlayer] Folder path is empty");
                return;
            }

            if (!Directory.Exists(folderPath))
            {
                Debug.LogError($"[VRFrameSequencePlayer] Folder does not exist: {folderPath}");
                OnError?.Invoke($"Folder not found: {folderPath}");
                return;
            }

            Debug.Log($"[VRFrameSequencePlayer] 🎬 Starting playback from: {folderPath}");
            
            // Stop any current playback
            StopPlayback();
            
            currentRecordingPath = folderPath;
            isPreparing = true;
            
            // Show the display
            if (displayImage != null && displayImage.gameObject != null)
            {
                displayImage.gameObject.SetActive(true);
            }
            
            if (loadingIndicator != null)
                loadingIndicator.SetActive(true);

            if (titleText != null)
            {
                string folderName = Path.GetFileName(folderPath);
                titleText.text = folderName;
            }

            StartCoroutine(LoadFramesCoroutine(folderPath));
        }

        private IEnumerator LoadFramesCoroutine(string folderPath)
        {
            // Find all frame files
            string[] allFiles = Directory.GetFiles(folderPath, "frame_*.jpg");
            
            if (allFiles.Length == 0)
            {
                Debug.LogError($"[VRFrameSequencePlayer] No frame files found in: {folderPath}");
                isPreparing = false;
                if (loadingIndicator != null)
                    loadingIndicator.SetActive(false);
                OnError?.Invoke("No frame files found");
                yield break;
            }

            // Sort frame files by number
            framePaths.Clear();
            framePaths.AddRange(allFiles);
            framePaths.Sort((a, b) =>
            {
                string nameA = Path.GetFileNameWithoutExtension(a);
                string nameB = Path.GetFileNameWithoutExtension(b);
                int numA = ExtractFrameNumber(nameA);
                int numB = ExtractFrameNumber(nameB);
                return numA.CompareTo(numB);
            });

            Debug.Log($"[VRFrameSequencePlayer] Found {framePaths.Count} frames");

            // Load frames progressively
            loadedTextures.Clear();
            int framesToLoadPerFrame = 5; // Load 5 frames per Unity frame
            
            for (int i = 0; i < framePaths.Count; i++)
            {
                try
                {
                    byte[] fileData = File.ReadAllBytes(framePaths[i]);
                    Texture2D tex = new Texture2D(2, 2);
                    tex.LoadImage(fileData);
                    loadedTextures.Add(tex);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[VRFrameSequencePlayer] Error loading frame {framePaths[i]}: {e.Message}");
                }
                
                // Yield outside try-catch
                if (i % framesToLoadPerFrame == 0)
                    yield return null; // Yield every few frames
            }

            Debug.Log($"[VRFrameSequencePlayer] Loaded {loadedTextures.Count} frames");

            // Load audio if available
            string audioPath = Path.Combine(folderPath, "audio.wav");
            if (File.Exists(audioPath))
            {
                yield return StartCoroutine(LoadAudioCoroutine(audioPath));
            }

            isPreparing = false;
            if (loadingIndicator != null)
                loadingIndicator.SetActive(false);

            // Start playback
            if (autoPlay)
            {
                StartPlayback();
            }
        }

        private int ExtractFrameNumber(string fileName)
        {
            // Extract number from "frame_000001" -> 1
            int underscoreIndex = fileName.LastIndexOf('_');
            if (underscoreIndex >= 0 && underscoreIndex < fileName.Length - 1)
            {
                string numberPart = fileName.Substring(underscoreIndex + 1);
                if (int.TryParse(numberPart, out int num))
                    return num;
            }
            return 0;
        }

        private IEnumerator LoadAudioCoroutine(string audioPath)
        {
            // Unity doesn't support loading WAV files directly on all platforms
            // For now, we'll skip audio loading on Quest
            // On PC, you could use a WAV loader library
            Debug.Log($"[VRFrameSequencePlayer] Audio file found but not loaded (WAV loading not implemented for Quest)");
            yield break;
        }

        public void StartPlayback()
        {
            if (loadedTextures.Count == 0)
            {
                Debug.LogWarning("[VRFrameSequencePlayer] No frames loaded");
                return;
            }

            if (isPlaying)
                StopPlayback();

            currentFrameIndex = 0;
            playbackStartTime = Time.time;
            isPlaying = true;

            playbackCoroutine = StartCoroutine(PlaybackCoroutine());
            
            // Play audio if available
            if (audioSource != null && audioClip != null)
            {
                audioSource.clip = audioClip;
                audioSource.Play();
            }

            OnPlaybackStarted?.Invoke();
        }

        public void StopPlayback()
        {
            isPlaying = false;
            
            if (playbackCoroutine != null)
            {
                StopCoroutine(playbackCoroutine);
                playbackCoroutine = null;
            }

            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }

            OnPlaybackStopped?.Invoke();
        }

        public void Pause()
        {
            isPlaying = false;
            if (audioSource != null && audioSource.isPlaying)
                audioSource.Pause();
        }

        public void Resume()
        {
            isPlaying = true;
            if (audioSource != null && audioClip != null && !audioSource.isPlaying)
                audioSource.Play();
        }

        private IEnumerator PlaybackCoroutine()
        {
            float frameInterval = 1f / frameRate;
            
            while (isPlaying && loadedTextures.Count > 0)
            {
                // Display current frame
                if (currentFrameIndex < loadedTextures.Count)
                {
                    Texture2D currentFrame = loadedTextures[currentFrameIndex];
                    
                    if (displayImage != null)
                    {
                        displayImage.texture = currentFrame;
                    }
                    else if (displayRenderer != null)
                    {
                        if (displayRenderer.material.mainTexture != currentFrame)
                            displayRenderer.material.mainTexture = currentFrame;
                    }
                }

                // Update time display
                if (timeText != null)
                {
                    float elapsed = Time.time - playbackStartTime;
                    int minutes = Mathf.FloorToInt(elapsed / 60);
                    int seconds = Mathf.FloorToInt(elapsed % 60);
                    int totalMinutes = Mathf.FloorToInt(loadedTextures.Count / frameRate / 60);
                    int totalSeconds = Mathf.FloorToInt((loadedTextures.Count / frameRate) % 60);
                    timeText.text = $"{minutes:00}:{seconds:00} / {totalMinutes:00}:{totalSeconds:00}";
                }

                // Wait for next frame
                yield return new WaitForSeconds(frameInterval);
                
                // Advance to next frame
                currentFrameIndex++;
                
                // Loop or stop at end
                if (currentFrameIndex >= loadedTextures.Count)
                {
                    if (loop)
                    {
                        currentFrameIndex = 0;
                        playbackStartTime = Time.time;
                    }
                    else
                    {
                        StopPlayback();
                        break;
                    }
                }
            }
        }

        private void OnDestroy()
        {
            StopPlayback();
            
            // Cleanup textures
            foreach (Texture2D tex in loadedTextures)
            {
                if (tex != null)
                    Destroy(tex);
            }
            loadedTextures.Clear();
            
            if (audioClip != null)
            {
                Destroy(audioClip);
            }
        }

        public bool IsPlaying => isPlaying;
        public bool IsPreparing => isPreparing;
        public int FrameCount => loadedTextures.Count;
        public int CurrentFrame => currentFrameIndex;
    }
}

