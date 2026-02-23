using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VRRecordings;

/// <summary>
/// Universal Recording UI Controller that works on both PC (using MeetingVideoRecorder) 
/// and Quest (using QuestVRRecorder). Automatically selects the appropriate recorder.
/// </summary>
namespace VRRecordings
{
    public class VRRecordingUIController : MonoBehaviour
    {
    [Header("References")]
    [Tooltip("Start/Record button")]
    public Button startButton;
    
    [Tooltip("Stop button (optional - can use same button)")]
    public Button stopButton;
    
    [Tooltip("Text on start button")]
    public TextMeshProUGUI startButtonText;
    
    [Tooltip("Text on stop button")]
    public TextMeshProUGUI stopButtonText;
    
    [Tooltip("Status text display")]
    public TextMeshProUGUI statusText;

    [Header("Recorder References")]
    [Tooltip("Quest/VR recorder (used on Android/Quest)")]
    public QuestVRRecorder questRecorder;
    
    [Tooltip("PC recorder (used in Editor/Windows)")]
    public MeetingVideoRecorder pcRecorder;

    [Header("Gallery Integration")]
    [Tooltip("Gallery manager to refresh after recording")]
    public VRRecordingsGalleryManager galleryManager;

    [Header("Button Visuals")]
    public Color readyColor = new Color(0.2f, 0.8f, 0.3f, 1f);
    public Color recordingColor = new Color(0.9f, 0.2f, 0.2f, 1f);
    public Color processingColor = new Color(0.3f, 0.5f, 0.9f, 1f);

    private Image startButtonImage;
    private Image stopButtonImage;
    private float recordingStartTime;
    private bool useQuestRecorder;

    private void Start()
    {
        // Determine which recorder to use based on platform
#if UNITY_ANDROID && !UNITY_EDITOR
        useQuestRecorder = true;
        Debug.Log("[VRRecordingUI] Using Quest recorder (Android)");
#else
        useQuestRecorder = false;
        Debug.Log("[VRRecordingUI] Using PC recorder (Windows/Editor)");
#endif

        // Auto-find recorders if not assigned
        if (questRecorder == null)
            questRecorder = FindFirstObjectByType<QuestVRRecorder>();
        
        if (pcRecorder == null)
            pcRecorder = FindFirstObjectByType<MeetingVideoRecorder>();

        // Get button images
        if (startButton != null)
        {
            startButtonImage = startButton.GetComponent<Image>();
            startButton.onClick.AddListener(OnStartClicked);
        }
        
        if (stopButton != null)
        {
            stopButtonImage = stopButton.GetComponent<Image>();
            stopButton.onClick.AddListener(OnStopClicked);
        }

        // Subscribe to Quest recorder events
        if (questRecorder != null)
        {
            questRecorder.OnRecordingStarted += OnQuestRecordingStarted;
            questRecorder.OnRecordingStopped += OnQuestRecordingStopped;
            questRecorder.OnRecordingError += OnQuestRecordingError;
        }

        // Auto-find gallery
        if (galleryManager == null)
            galleryManager = FindFirstObjectByType<VRRecordingsGalleryManager>();

        UpdateUI();
    }

    private void OnDestroy()
    {
        if (questRecorder != null)
        {
            questRecorder.OnRecordingStarted -= OnQuestRecordingStarted;
            questRecorder.OnRecordingStopped -= OnQuestRecordingStopped;
            questRecorder.OnRecordingError -= OnQuestRecordingError;
        }
    }

    private void Update()
    {
        UpdateUI();
    }

    private void OnStartClicked()
    {
        if (IsRecording() || IsProcessing())
            return;

        Debug.Log("[VRRecordingUI] Start button clicked");
        
        if (useQuestRecorder && questRecorder != null)
        {
            questRecorder.StartRecording();
        }
        else if (pcRecorder != null)
        {
            pcRecorder.StartRecording();
        }
        else
        {
            Debug.LogError("[VRRecordingUI] No recorder available!");
        }
        
        recordingStartTime = Time.time;
    }

    private void OnStopClicked()
    {
        if (!IsRecording())
            return;

        Debug.Log("[VRRecordingUI] Stop button clicked");
        
        if (useQuestRecorder && questRecorder != null)
        {
            questRecorder.StopRecording();
        }
        else if (pcRecorder != null)
        {
            pcRecorder.StopRecording();
        }
    }

    private void OnQuestRecordingStarted()
    {
        Debug.Log("[VRRecordingUI] Quest recording started");
        recordingStartTime = Time.time;
    }

    private void OnQuestRecordingStopped(string recordingPath)
    {
        Debug.Log($"[VRRecordingUI] ✅ Quest recording saved to: {recordingPath}");
        Debug.Log($"[VRRecordingUI] Recording folder exists: {System.IO.Directory.Exists(recordingPath)}");
        
        // Verify files exist
        if (System.IO.Directory.Exists(recordingPath))
        {
            string[] frameFiles = System.IO.Directory.GetFiles(recordingPath, "frame_*.jpg");
            Debug.Log($"[VRRecordingUI] Frame files in recording: {frameFiles.Length}");
        }
        
        // Auto-find gallery manager if not assigned
        if (galleryManager == null)
        {
            galleryManager = FindFirstObjectByType<VRRecordingsGalleryManager>();
            Debug.Log($"[VRRecordingUI] Auto-found gallery manager: {galleryManager != null}");
        }
        
        // Refresh gallery after a short delay (wait for marker file to be created)
        if (galleryManager != null)
        {
            Debug.Log("[VRRecordingUI] Refreshing gallery...");
            StartCoroutine(RefreshGalleryDelayed(recordingPath));
        }
        else
        {
            Debug.LogWarning("[VRRecordingUI] Gallery Manager is null! Cannot refresh gallery.");
        }
    }

    private void OnQuestRecordingError(string error)
    {
        Debug.LogError($"[VRRecordingUI] Recording error: {error}");
        if (statusText != null)
            statusText.text = $"Error: {error}";
    }

    private System.Collections.IEnumerator RefreshGalleryDelayed(string recordingPath = null)
    {
        // Wait for encoding_complete.marker file if we have a path
        if (!string.IsNullOrEmpty(recordingPath) && System.IO.Directory.Exists(recordingPath))
        {
            string markerPath = System.IO.Path.Combine(recordingPath, "encoding_complete.marker");
            float waitTime = 0f;
            float maxWait = 5f; // Wait up to 5 seconds for marker
            
            Debug.Log($"[VRRecordingUI] Waiting for marker file: {markerPath}");
            
            while (!System.IO.File.Exists(markerPath) && waitTime < maxWait)
            {
                yield return new WaitForSeconds(0.5f);
                waitTime += 0.5f;
            }
            
            if (System.IO.File.Exists(markerPath))
            {
                Debug.Log($"[VRRecordingUI] ✅ Marker file found after {waitTime}s");
            }
            else
            {
                Debug.Log($"[VRRecordingUI] ⚠️ Marker file not found after {maxWait}s, refreshing anyway");
            }
        }
        else
        {
            // Fallback: just wait 2 seconds
            yield return new WaitForSeconds(2f);
        }
        
        if (galleryManager != null)
        {
            Debug.Log("[VRRecordingUI] Refreshing gallery now...");
            galleryManager.RefreshRecordingsList();
            Debug.Log("[VRRecordingUI] ✅ Gallery refreshed");
        }
        else
        {
            Debug.LogError("[VRRecordingUI] Gallery Manager is null!");
        }
    }

    private bool IsRecording()
    {
        if (useQuestRecorder && questRecorder != null)
            return questRecorder.isRecording;
        if (pcRecorder != null)
            return pcRecorder.isRecording;
        return false;
    }

    private bool IsProcessing()
    {
        if (useQuestRecorder && questRecorder != null)
            return questRecorder.isProcessing;
        if (pcRecorder != null)
            return pcRecorder.isProcessing;
        return false;
    }

    private void UpdateUI()
    {
        bool recording = IsRecording();
        bool processing = IsProcessing();

        // Update start button
        if (startButton != null)
        {
            startButton.interactable = !recording && !processing;
            
            if (startButtonImage != null)
                startButtonImage.color = recording ? processingColor : readyColor;
            
            if (startButtonText != null)
            {
                if (processing)
                    startButtonText.text = "PROCESSING...";
                else if (recording)
                    startButtonText.text = "RECORDING...";
                else
                    startButtonText.text = "START RECORDING";
            }
        }

        // Update stop button
        if (stopButton != null)
        {
            stopButton.interactable = recording && !processing;
            
            if (stopButtonImage != null)
                stopButtonImage.color = recording ? recordingColor : Color.gray;
            
            if (stopButtonText != null)
            {
                stopButtonText.text = "STOP RECORDING";
            }
        }

        // Update status text
        if (statusText != null)
        {
            if (processing)
            {
                statusText.text = "Processing video...\nPlease wait";
            }
            else if (recording)
            {
                float elapsed = Time.time - recordingStartTime;
                int minutes = Mathf.FloorToInt(elapsed / 60);
                int seconds = Mathf.FloorToInt(elapsed % 60);
                string dot = (Mathf.FloorToInt(Time.time * 2) % 2 == 0) ? "●" : "○";
                
                int frameCount = 0;
                if (useQuestRecorder && questRecorder != null)
                    frameCount = questRecorder.recordedFrameCount;
                
                statusText.text = $"{dot} Recording {minutes:00}:{seconds:00}\nFrames: {frameCount}";
            }
            else
            {
                statusText.text = "Ready to record\nPress START to begin";
            }
        }
    }
}
}
