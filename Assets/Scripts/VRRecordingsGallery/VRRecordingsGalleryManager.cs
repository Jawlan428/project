using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace VRRecordings
{
    /// <summary>
    /// Manages the in-VR recordings gallery. Lists available recordings from device storage
    /// and allows playback on a virtual screen. Works on Meta Quest 3 (Android) and Editor.
    /// </summary>
    public class VRRecordingsGalleryManager : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Parent panel that contains the gallery UI (World Space Canvas)")]
        [SerializeField] private GameObject galleryPanel;
        
        [Tooltip("Button to open/close the recordings gallery")]
        [SerializeField] private Button openGalleryButton;
        
        [Tooltip("Button to close the gallery panel")]
        [SerializeField] private Button closeGalleryButton;
        
        [Tooltip("Content container for the recording list (inside ScrollRect)")]
        [SerializeField] private Transform recordingListContent;
        
        [Tooltip("Prefab for each recording list item")]
        [SerializeField] private GameObject recordingItemPrefab;
        
        [Tooltip("Text to show when no recordings are found")]
        [SerializeField] private TextMeshProUGUI noRecordingsText;
        
        [Tooltip("Text showing total recordings count")]
        [SerializeField] private TextMeshProUGUI recordingsCountText;
        
        [Tooltip("Button to refresh the recordings list")]
        [SerializeField] private Button refreshButton;

        [Header("Video Player")]
        [Tooltip("Reference to the VR Video Screen Player component (for MP4 videos)")]
        [SerializeField] private VRVideoScreenPlayer videoPlayer;
        
        [Tooltip("Reference to the VR Frame Sequence Player component (for frame-based recordings)")]
        [SerializeField] private VRFrameSequencePlayer frameSequencePlayer;

        [Header("Storage Settings")]
        [Tooltip("Folder name where recordings are stored")]
        [SerializeField] private string recordingsFolderName = "QuestRecordings";
        
        [Tooltip("Additional search paths (Editor/Desktop only)")]
        [SerializeField] private string[] additionalSearchPaths;

        [Header("Supported Formats")]
        [SerializeField] private string[] supportedVideoExtensions = { ".mp4", ".webm", ".mov" };
        
        [Tooltip("Also detect frame-based recordings (folders with frame_*.jpg files)")]
        [SerializeField] private bool detectFrameBasedRecordings = true;

        // Internal state
        private List<RecordingInfo> recordings = new List<RecordingInfo>();
        private List<GameObject> listItemInstances = new List<GameObject>();
        private RecordingInfo currentlyPlaying;

        /// <summary>
        /// Information about a single recording
        /// </summary>
        [Serializable]
        public class RecordingInfo
        {
            public string FilePath;
            public string FileName;
            public string DisplayName;
            public DateTime CreatedDate;
            public long FileSizeBytes;
            public string FormattedSize;
            public string FormattedDate;
            public Texture2D Thumbnail; // Optional thumbnail
        }

        private void Awake()
        {
            // Setup button listeners
            if (openGalleryButton != null)
                openGalleryButton.onClick.AddListener(OpenGallery);
            
            if (closeGalleryButton != null)
                closeGalleryButton.onClick.AddListener(CloseGallery);
            
            if (refreshButton != null)
                refreshButton.onClick.AddListener(RefreshRecordingsList);

            // Start with gallery closed
            if (galleryPanel != null)
                galleryPanel.SetActive(false);
        }

        private void Start()
        {
            // Initial scan for recordings
            RefreshRecordingsList();
        }

        /// <summary>
        /// Opens the recordings gallery panel
        /// </summary>
        public void OpenGallery()
        {
            if (galleryPanel != null)
            {
                galleryPanel.SetActive(true);
                RefreshRecordingsList();
            }
        }

        /// <summary>
        /// Closes the recordings gallery panel
        /// </summary>
        public void CloseGallery()
        {
            if (galleryPanel != null)
                galleryPanel.SetActive(false);
        }

        /// <summary>
        /// Toggles the gallery panel visibility
        /// </summary>
        public void ToggleGallery()
        {
            if (galleryPanel != null)
            {
                if (galleryPanel.activeSelf)
                    CloseGallery();
                else
                    OpenGallery();
            }
        }

        /// <summary>
        /// Scans storage for recordings and updates the UI list
        /// </summary>
        public void RefreshRecordingsList()
        {
            recordings.Clear();
            ClearListUI();

            Debug.Log("[VRRecordingsGallery] === Starting refresh ===");

            // Get all search paths
            List<string> searchPaths = GetRecordingSearchPaths();
            
            Debug.Log($"[VRRecordingsGallery] Searching {searchPaths.Count} paths:");

            foreach (string basePath in searchPaths)
            {
                if (string.IsNullOrEmpty(basePath))
                {
                    Debug.LogWarning("[VRRecordingsGallery] Empty path, skipping");
                    continue;
                }
                
                if (!Directory.Exists(basePath))
                {
                    Debug.LogWarning($"[VRRecordingsGallery] Path does not exist: {basePath}");
                    continue;
                }

                Debug.Log($"[VRRecordingsGallery] Scanning: {basePath}");
                
                try
                {
                    int beforeCount = recordings.Count;
                    ScanDirectoryForRecordings(basePath);
                    int found = recordings.Count - beforeCount;
                    Debug.Log($"[VRRecordingsGallery] Found {found} recordings in {basePath}");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[VRRecordingsGallery] Error scanning path {basePath}: {e.Message}");
                }
            }

            // Sort by date, newest first
            recordings = recordings.OrderByDescending(r => r.CreatedDate).ToList();

            // Update UI
            PopulateListUI();
            UpdateCountText();

            Debug.Log($"[VRRecordingsGallery] === Refresh complete: {recordings.Count} total recordings ===");
        }

        /// <summary>
        /// Gets all paths to search for recordings
        /// </summary>
        private List<string> GetRecordingSearchPaths()
        {
            List<string> paths = new List<string>();

#if UNITY_ANDROID && !UNITY_EDITOR
            // Quest/Android: Use persistent data path
            string questPath = Path.Combine(Application.persistentDataPath, recordingsFolderName);
            paths.Add(questPath);
            
            // Also check for recordings directly in persistentDataPath
            paths.Add(Application.persistentDataPath);
            
            Debug.Log($"[VRRecordingsGallery] Quest storage path: {questPath}");
#else
            // Editor/Desktop: Check multiple locations
            
            // 1. Project root QuestRecordings folder
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (!string.IsNullOrEmpty(projectRoot))
            {
                paths.Add(Path.Combine(projectRoot, recordingsFolderName));
            }

            // 2. Desktop MeetingRecordings folder (from MeetingVideoRecorder)
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            if (!string.IsNullOrEmpty(desktopPath))
            {
                paths.Add(Path.Combine(desktopPath, "MeetingRecordings"));
            }

            // 3. Application persistent data path
            paths.Add(Path.Combine(Application.persistentDataPath, recordingsFolderName));

            // 4. Additional configured paths
            if (additionalSearchPaths != null)
            {
                paths.AddRange(additionalSearchPaths.Where(p => !string.IsNullOrEmpty(p)));
            }
#endif
            return paths;
        }

        /// <summary>
        /// Recursively scans a directory for video recordings
        /// </summary>
        private void ScanDirectoryForRecordings(string directoryPath, int maxDepth = 3, int currentDepth = 0)
        {
            if (currentDepth > maxDepth || !Directory.Exists(directoryPath))
                return;

            try
            {
                // Search for video files in current directory
                foreach (string extension in supportedVideoExtensions)
                {
                    string[] files = Directory.GetFiles(directoryPath, $"*{extension}");
                    foreach (string filePath in files)
                    {
                        AddRecordingFromFile(filePath);
                    }
                }
                
                // Also detect frame-based recordings (Quest recordings)
                if (detectFrameBasedRecordings && currentDepth == 0)
                {
                    // Check if this directory contains frame files
                    string[] frameFiles = Directory.GetFiles(directoryPath, "frame_*.jpg");
                    Debug.Log($"[VRRecordingsGallery] Checking {directoryPath} for frames: found {frameFiles.Length} frame files");
                    
                    if (frameFiles.Length > 0)
                    {
                        // This is a frame-based recording folder
                        Debug.Log($"[VRRecordingsGallery] ✅ Detected frame-based recording: {directoryPath} ({frameFiles.Length} frames)");
                        AddFrameBasedRecording(directoryPath, frameFiles.Length);
                    }
                }

                // Recurse into subdirectories
                string[] subdirs = Directory.GetDirectories(directoryPath);
                foreach (string subdir in subdirs)
                {
                    // Skip system/hidden directories
                    string dirName = Path.GetFileName(subdir);
                    if (dirName.StartsWith(".") || dirName.Equals("frames", StringComparison.OrdinalIgnoreCase))
                        continue;

                    ScanDirectoryForRecordings(subdir, maxDepth, currentDepth + 1);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VRRecordingsGallery] Error scanning {directoryPath}: {e.Message}");
            }
        }

        /// <summary>
        /// Adds a frame-based recording (Quest recording with JPEG frames)
        /// </summary>
        private void AddFrameBasedRecording(string folderPath, int frameCount)
        {
            // Skip if already added
            if (recordings.Any(r => r.FilePath.Equals(folderPath, StringComparison.OrdinalIgnoreCase)))
            {
                Debug.Log($"[VRRecordingsGallery] Already added: {folderPath}");
                return;
            }

            try
            {
                DirectoryInfo dirInfo = new DirectoryInfo(folderPath);
                
                // Check for encoding_complete.marker
                string markerPath = Path.Combine(folderPath, "encoding_complete.marker");
                bool hasMarker = File.Exists(markerPath);
                
                Debug.Log($"[VRRecordingsGallery] Checking recording: {folderPath}");
                Debug.Log($"[VRRecordingsGallery]   - Has marker: {hasMarker}");
                Debug.Log($"[VRRecordingsGallery]   - Frame count: {frameCount}");
                Debug.Log($"[VRRecordingsGallery]   - Last write: {dirInfo.LastWriteTime}");
                
                if (!hasMarker)
                {
                    // Check if recording is very recent (might still be saving)
                    TimeSpan age = DateTime.Now - dirInfo.LastWriteTime;
                    Debug.Log($"[VRRecordingsGallery]   - Age: {age.TotalSeconds:F1} seconds");
                    
                    if (age.TotalSeconds < 3)
                    {
                        Debug.Log($"[VRRecordingsGallery] ⏳ Skipping {folderPath}: recording may still be in progress (age: {age.TotalSeconds:F1}s)");
                        return;
                    }
                    else
                    {
                        Debug.Log($"[VRRecordingsGallery] ⚠️ No marker but old enough ({age.TotalSeconds:F1}s), adding anyway");
                    }
                }
                
                // Calculate total size
                long totalSize = 0;
                FileInfo[] files = dirInfo.GetFiles();
                foreach (FileInfo file in files)
                {
                    totalSize += file.Length;
                }
                
                RecordingInfo info = new RecordingInfo
                {
                    FilePath = folderPath, // Store folder path, not file path
                    FileName = dirInfo.Name,
                    DisplayName = GenerateDisplayNameFromFolder(folderPath, dirInfo),
                    CreatedDate = dirInfo.CreationTime,
                    FileSizeBytes = totalSize,
                    FormattedSize = FormatFileSize(totalSize),
                    FormattedDate = dirInfo.CreationTime.ToString("MMM dd, yyyy HH:mm")
                };
                
                // Mark as frame-based by storing frame count in a custom way
                // We'll use a special prefix in the path to identify frame-based recordings
                recordings.Add(info);
                Debug.Log($"[VRRecordingsGallery] Found frame-based recording: {info.DisplayName} ({frameCount} frames)");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VRRecordingsGallery] Error reading frame-based recording {folderPath}: {e.Message}");
            }
        }

        /// <summary>
        /// Creates a RecordingInfo from a file path and adds it to the list
        /// </summary>
        private void AddRecordingFromFile(string filePath)
        {
            // Skip if already added (avoid duplicates)
            if (recordings.Any(r => r.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
                return;

            try
            {
                FileInfo fileInfo = new FileInfo(filePath);
                
                // Skip very small files (probably corrupted or still encoding)
                if (fileInfo.Length < 100000) // 100KB minimum for a valid video
                {
                    Debug.Log($"[VRRecordingsGallery] Skipping {filePath}: file too small ({fileInfo.Length} bytes), may be incomplete");
                    return;
                }
                
                // Skip files that were modified in the last 5 seconds (likely still encoding)
                TimeSpan age = DateTime.Now - fileInfo.LastWriteTime;
                if (age.TotalSeconds < 5)
                {
                    Debug.Log($"[VRRecordingsGallery] Skipping {filePath}: file was modified {age.TotalSeconds:F1}s ago, may still be encoding");
                    return;
                }
                
                // Check for encoding_complete.marker file (created by MeetingVideoRecorder when done)
                string parentDir = fileInfo.Directory?.FullName;
                if (!string.IsNullOrEmpty(parentDir))
                {
                    string markerPath = Path.Combine(parentDir, "encoding_complete.marker");
                    bool hasMarker = File.Exists(markerPath);
                    
                    // If there's a convert.bat but no marker, encoding might still be in progress
                    string batPath = Path.Combine(parentDir, "convert.bat");
                    if (File.Exists(batPath) && !hasMarker && age.TotalSeconds < 60)
                    {
                        Debug.Log($"[VRRecordingsGallery] Skipping {filePath}: convert.bat exists but no encoding_complete.marker yet");
                        return;
                    }
                }

                RecordingInfo info = new RecordingInfo
                {
                    FilePath = filePath,
                    FileName = fileInfo.Name,
                    DisplayName = GenerateDisplayName(filePath, fileInfo),
                    CreatedDate = fileInfo.CreationTime,
                    FileSizeBytes = fileInfo.Length,
                    FormattedSize = FormatFileSize(fileInfo.Length),
                    FormattedDate = fileInfo.CreationTime.ToString("MMM dd, yyyy HH:mm")
                };

                recordings.Add(info);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VRRecordingsGallery] Error reading file {filePath}: {e.Message}");
            }
        }

        /// <summary>
        /// Generates a human-readable display name from a folder path (for frame-based recordings)
        /// </summary>
        private string GenerateDisplayNameFromFolder(string folderPath, DirectoryInfo dirInfo)
        {
            string folderName = dirInfo.Name;
            
            // Check if folder has timestamp format (Recording_YYYY-MM-DD_HH-mm-ss)
            if (folderName.StartsWith("Recording_") && folderName.Length >= 19)
            {
                try
                {
                    string timestampPart = folderName.Substring(10); // Remove "Recording_"
                    if (DateTime.TryParseExact(timestampPart, "yyyy-MM-dd_HH-mm-ss", 
                        System.Globalization.CultureInfo.InvariantCulture, 
                        System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
                    {
                        return $"Recording - {parsedDate:MMM dd, yyyy HH:mm}";
                    }
                }
                catch { }
            }
            
            // Fallback: use folder name with formatted date
            return $"{folderName} ({dirInfo.CreationTime:MMM dd HH:mm})";
        }

        /// <summary>
        /// Generates a human-readable display name from the file path
        /// </summary>
        private string GenerateDisplayName(string filePath, FileInfo fileInfo)
        {
            // Try to extract meaningful name from folder structure
            // e.g., "Recording_2026-01-15_14-30-00/meeting.mp4" -> "Meeting - Jan 15, 2026 14:30"
            
            string parentDir = fileInfo.Directory?.Name ?? "";
            string fileName = Path.GetFileNameWithoutExtension(fileInfo.Name);

            // Check if parent folder has timestamp format (Recording_YYYY-MM-DD_HH-mm-ss)
            if (parentDir.StartsWith("Recording_") && parentDir.Length >= 19)
            {
                try
                {
                    string timestampPart = parentDir.Substring(10); // Remove "Recording_"
                    if (DateTime.TryParseExact(timestampPart, "yyyy-MM-dd_HH-mm-ss", 
                        System.Globalization.CultureInfo.InvariantCulture, 
                        System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
                    {
                        return $"Recording - {parsedDate:MMM dd, yyyy HH:mm}";
                    }
                }
                catch { }
            }

            // Fallback: use filename with formatted date
            return $"{fileName} ({fileInfo.CreationTime:MMM dd HH:mm})";
        }

        /// <summary>
        /// Formats file size to human-readable string
        /// </summary>
        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// Clears all list item instances from the UI
        /// </summary>
        private void ClearListUI()
        {
            foreach (GameObject item in listItemInstances)
            {
                if (item != null)
                    Destroy(item);
            }
            listItemInstances.Clear();
        }

        /// <summary>
        /// Creates UI elements for all recordings
        /// </summary>
        private void PopulateListUI()
        {
            if (recordingListContent == null || recordingItemPrefab == null)
            {
                Debug.LogWarning("[VRRecordingsGallery] Missing recordingListContent or recordingItemPrefab reference");
                return;
            }

            // Show/hide no recordings message
            if (noRecordingsText != null)
                noRecordingsText.gameObject.SetActive(recordings.Count == 0);

            foreach (RecordingInfo recording in recordings)
            {
                GameObject itemObj = Instantiate(recordingItemPrefab, recordingListContent);
                listItemInstances.Add(itemObj);

                // Setup the list item component
                RecordingListItem listItem = itemObj.GetComponent<RecordingListItem>();
                if (listItem != null)
                {
                    listItem.Setup(recording, this);
                }
                else
                {
                    // Fallback: try to find text components manually
                    SetupListItemFallback(itemObj, recording);
                }
            }
        }

        /// <summary>
        /// Fallback setup if RecordingListItem component is not present
        /// </summary>
        private void SetupListItemFallback(GameObject itemObj, RecordingInfo recording)
        {
            // Try to find text components
            TextMeshProUGUI[] texts = itemObj.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length > 0)
            {
                texts[0].text = recording.DisplayName;
                if (texts.Length > 1)
                    texts[1].text = $"{recording.FormattedDate} • {recording.FormattedSize}";
            }

            // Setup button click
            Button button = itemObj.GetComponent<Button>();
            if (button == null)
                button = itemObj.GetComponentInChildren<Button>();
            
            if (button != null)
            {
                string path = recording.FilePath; // Capture for closure
                button.onClick.AddListener(() => PlayRecording(path));
            }
        }

        /// <summary>
        /// Updates the recordings count text
        /// </summary>
        private void UpdateCountText()
        {
            if (recordingsCountText != null)
            {
                string plural = recordings.Count == 1 ? "" : "s";
                recordingsCountText.text = $"{recordings.Count} Recording{plural}";
            }
        }

        /// <summary>
        /// Plays a recording by file path (can be MP4 file or folder path for frame-based recordings)
        /// </summary>
        public void PlayRecording(string filePath)
        {
            Debug.Log($"[VRRecordingsGallery] PlayRecording called with: {filePath}");
            
            if (string.IsNullOrEmpty(filePath))
            {
                Debug.LogError("[VRRecordingsGallery] Cannot play: file path is empty");
                return;
            }

            RecordingInfo recording = recordings.FirstOrDefault(r => r.FilePath == filePath);
            currentlyPlaying = recording;

            // Check if it's a directory (frame-based recording) or a file (MP4)
            bool isDirectory = Directory.Exists(filePath);
            bool isFile = File.Exists(filePath);

            if (isDirectory)
            {
                // Frame-based recording (Quest recording)
                Debug.Log($"[VRRecordingsGallery] Detected frame-based recording: {filePath}");
                
                if (frameSequencePlayer != null)
                {
                    frameSequencePlayer.PlayRecording(filePath);
                }
                else
                {
                    Debug.LogError("[VRRecordingsGallery] No VRFrameSequencePlayer assigned! Frame-based recordings need this component.");
                }
            }
            else if (isFile)
            {
                // MP4 video file
                Debug.Log($"[VRRecordingsGallery] Detected MP4 video: {filePath}");
                
                if (videoPlayer != null)
                {
                    videoPlayer.PlayVideo(filePath);
                }
                else
                {
                    Debug.LogError("[VRRecordingsGallery] No VRVideoScreenPlayer assigned! Please assign it in the Inspector.");
                }
            }
            else
            {
                Debug.LogError($"[VRRecordingsGallery] Path does not exist: {filePath}");
            }
        }

        /// <summary>
        /// Plays a recording by index
        /// </summary>
        public void PlayRecording(int index)
        {
            if (index >= 0 && index < recordings.Count)
            {
                PlayRecording(recordings[index].FilePath);
            }
        }

        /// <summary>
        /// Stops the current playback
        /// </summary>
        public void StopPlayback()
        {
            if (videoPlayer != null)
            {
                videoPlayer.Stop();
            }
            currentlyPlaying = null;
        }

        /// <summary>
        /// Gets the currently playing recording info
        /// </summary>
        public RecordingInfo GetCurrentlyPlaying() => currentlyPlaying;

        /// <summary>
        /// Gets all discovered recordings
        /// </summary>
        public List<RecordingInfo> GetAllRecordings() => new List<RecordingInfo>(recordings);

        /// <summary>
        /// Deletes a recording file (with confirmation in production)
        /// </summary>
        public bool DeleteRecording(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    // Stop playback if this is the current recording
                    if (currentlyPlaying != null && currentlyPlaying.FilePath == filePath)
                    {
                        StopPlayback();
                    }

                    File.Delete(filePath);
                    Debug.Log($"[VRRecordingsGallery] Deleted: {filePath}");
                    
                    // Refresh the list
                    RefreshRecordingsList();
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[VRRecordingsGallery] Error deleting {filePath}: {e.Message}");
            }
            return false;
        }

#if UNITY_EDITOR
        [ContextMenu("Debug: Print Storage Paths")]
        private void DebugPrintStoragePaths()
        {
            Debug.Log("=== Recording Search Paths ===");
            foreach (string path in GetRecordingSearchPaths())
            {
                bool exists = Directory.Exists(path);
                Debug.Log($"[{(exists ? "EXISTS" : "MISSING")}] {path}");
            }
        }

        [ContextMenu("Debug: Refresh and Print Recordings")]
        private void DebugRefreshAndPrint()
        {
            RefreshRecordingsList();
            Debug.Log($"=== Found {recordings.Count} Recordings ===");
            foreach (var r in recordings)
            {
                Debug.Log($"  {r.DisplayName} | {r.FormattedSize} | {r.FilePath}");
            }
        }
#endif
    }
}

