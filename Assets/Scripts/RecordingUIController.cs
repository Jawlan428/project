using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RecordingUIController : MonoBehaviour
{
    [Header("References")]
    public MeetingVideoRecorder recorder;
    public Button recordButton;
    public TextMeshProUGUI buttonText;
    public TextMeshProUGUI statusText;
    
    [Header("Button Colors")]
    public Color greenColor = new Color(0.2f, 0.7f, 0.3f, 1f);
    public Color redColor = new Color(0.8f, 0.2f, 0.2f, 1f);
    public Color blueColor = new Color(0.2f, 0.4f, 0.8f, 1f);
    
    private Image buttonImage;
    private float recordingStartTime;

    void Start()
    {
        // Find recorder if not assigned
        if (recorder == null)
            recorder = FindFirstObjectByType<MeetingVideoRecorder>();
        
        // Get button image component
        if (recordButton != null)
            buttonImage = recordButton.GetComponent<Image>();
        
        // Add button click listener
        if (recordButton != null)
            recordButton.onClick.AddListener(OnRecordButtonClicked);
        
        UpdateUI();
    }

    void Update()
    {
        UpdateUI();
    }

    void OnRecordButtonClicked()
    {
        if (recorder == null) return;
        
        if (recorder.isRecording)
        {
            // Stop recording
            recorder.StopRecording();
        }
        else if (!recorder.isProcessing)
        {
            // Start recording
            recorder.StartRecording();
            recordingStartTime = Time.time;
        }
    }

    void UpdateUI()
    {
        if (recorder == null || recordButton == null) return;
        
        if (recorder.isProcessing)
        {
            // Processing/Success state
            if (buttonImage != null)
            {
                if (recorder.isRecording == false && statusText != null && statusText.text.Contains("SUCCESSFUL"))
                    buttonImage.color = greenColor;
                else
                    buttonImage.color = blueColor;
            }
            
            if (buttonText != null)
            {
                if (statusText != null && statusText.text.Contains("SUCCESSFUL"))
                    buttonText.text = "✅ SUCCESS";
                else
                    buttonText.text = "⏳ PROCESSING";
            }
            
            recordButton.interactable = false;
        }
        else if (recorder.isRecording)
        {
            // Recording state - show STOP
            if (buttonImage != null)
                buttonImage.color = redColor;
            
            if (buttonText != null)
                buttonText.text = "⏹ STOP";
            
            recordButton.interactable = true;
            
            // Update status with recording time
            if (statusText != null)
            {
                float elapsed = Time.time - recordingStartTime;
                int minutes = Mathf.FloorToInt(elapsed / 60);
                int seconds = Mathf.FloorToInt(elapsed % 60);
                string dot = (Mathf.FloorToInt(Time.time * 2) % 2 == 0) ? "🔴" : "⚫";
                
                statusText.text = string.Format("{0} Recording {1:00}:{2:00}\n", dot, minutes, seconds);
                
                if (recorder.recordMicrophone && !string.IsNullOrEmpty(recorder.microphoneDevice))
                    statusText.text += "🎤 Recording audio\n";
                
                if (recorder.recordingCamera != null)
                    statusText.text += "Cam: " + recorder.recordingCamera.name;
            }
        }
        else
        {
            // Ready to record state - show RECORD
            if (buttonImage != null)
                buttonImage.color = greenColor;
            
            if (buttonText != null)
                buttonText.text = "⏺ RECORD";
            
            recordButton.interactable = true;
            
            // Update status with ready info
            if (statusText != null)
            {
                statusText.text = "Ready to record\n";
                
                if (recorder.recordMicrophone && !string.IsNullOrEmpty(recorder.microphoneDevice))
                    statusText.text += "🎤 Mic ready\n";
                else if (recorder.recordMicrophone)
                    statusText.text += "🎤 No mic detected\n";
                
                if (recorder.autoCreateMP4)
                    statusText.text += "🎬 Auto MP4: ON\n";
                
                if (recorder.recordingCamera != null)
                    statusText.text += "Cam: " + recorder.recordingCamera.name;
                else
                    statusText.text += "Cam: None!";
            }
        }
        
        // Show processing status from recorder
        if (recorder.isProcessing && !string.IsNullOrEmpty(GetProcessingStatus()))
        {
            if (statusText != null)
                statusText.text = GetProcessingStatus();
        }
    }
    
    string GetProcessingStatus()
    {
        // Access processing status through reflection since it's private
        var field = typeof(MeetingVideoRecorder).GetField("processingStatus", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
            return field.GetValue(recorder) as string;
        return "";
    }
}

