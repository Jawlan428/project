using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using pmjo.NextGenRecorder;
using RuntimeRecording;
// using pmjo.NextGenRecorder.Sharing;

namespace pmjo.Examples
{
    public class SimpleRecorder : MonoBehaviour
    {
        public Button startRecordingButton;
        public Button stopRecordingButton;
        public Button saveRecordingButton;
        public Button viewRecordingButton;

        private long mLastSessionId;
#if UNITY_ANDROID
        [Header("Quest/Android fallback")]
        public QuestFrameSequenceRecorder questRecorder;
        private string _lastQuestSessionDir;
#endif

        void OnEnable()
        {
            Recorder.RecordingStarted += RecordingStarted;
            Recorder.RecordingPaused += RecordingPaused;
            Recorder.RecordingResumed += RecordingResumed;
            Recorder.RecordingStopped += RecordingStopped;
            Recorder.RecordingExported += RecordingExported;
        }

        void OnDisable()
        {
            Recorder.RecordingStarted -= RecordingStarted;
            Recorder.RecordingPaused -= RecordingPaused;
            Recorder.RecordingResumed -= RecordingResumed;
            Recorder.RecordingStopped -= RecordingStopped;
            Recorder.RecordingExported -= RecordingExported;
        }

        void Awake()
        {
            mLastSessionId = Recorder.GetLastRecordingSession();

            EnsureCanvasCanReceiveClicks();
            UpdateStartAndStopRecordingButton();
            UpdateSaveOrViewRecordingButton();
        }

        void Start()
        {
            CreateEventSystemIfItDoesNotExist();
        }

        private void EnsureCanvasCanReceiveClicks()
        {
            // The example prefab can render UI without a raycaster, but it won't receive pointer clicks.
            var canvas = GetComponent<Canvas>();
            if (canvas == null)
                canvas = GetComponentInParent<Canvas>();

            if (canvas != null && canvas.GetComponent<GraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        void RecordingStarted(long sessionId)
        {
            Debug.Log("Recording started, session id " + sessionId);

            UpdateStartAndStopRecordingButton();
            UpdateSaveOrViewRecordingButton();
        }

        void RecordingPaused(long sessionId)
        {
            Debug.Log("Recording paused, session id " + sessionId);
        }

        void RecordingResumed(long sessionId)
        {
            Debug.Log("Recording resumed, session id " + sessionId);
        }

        void RecordingStopped(long sessionId)
        {
            Debug.Log("Recording stopped, session id " + sessionId);

            mLastSessionId = sessionId;

            UpdateStartAndStopRecordingButton();
            UpdateSaveOrViewRecordingButton();
        }

        public void StartRecording()
        {
#if UNITY_ANDROID
            EnsureQuestRecorder();
            questRecorder.StartRecording();
            UpdateStartAndStopRecordingButton();
            UpdateSaveOrViewRecordingButton();
            return;
#else
            if (!Recorder.IsSupported)
            {
                Debug.LogWarning("[SimpleRecorder] Recording is not supported on this platform/runtime configuration. Build to a supported platform to enable recording.");
                return;
            }
            Recorder.StartRecording();
#endif
        }

        public void StopRecording()
        {
#if UNITY_ANDROID
            EnsureQuestRecorder();
            questRecorder.StopRecording();
            _lastQuestSessionDir = questRecorder.SessionDirectory;
            UpdateStartAndStopRecordingButton();
            UpdateSaveOrViewRecordingButton();
            return;
#else
            if (!Recorder.IsSupported)
            {
                Debug.LogWarning("[SimpleRecorder] Recording is not supported on this platform/runtime configuration. Build to a supported platform to enable recording.");
                return;
            }
            Recorder.StopRecording();
#endif
        }

        public  void ExportLastRecording()
        {
#if UNITY_ANDROID
            if (string.IsNullOrWhiteSpace(_lastQuestSessionDir))
            {
                Debug.LogWarning("[SimpleRecorder] No Quest recording available yet. Press Start/Stop first.");
                return;
            }

            Debug.Log($"[SimpleRecorder] Quest recording folder: {_lastQuestSessionDir}");
            Debug.Log("[SimpleRecorder] Pull the folder to your PC and merge frames+audio with ffmpeg (example): ffmpeg -y -framerate 24 -i frame_%06d.jpg -i audio.wav -c:v libx264 -pix_fmt yuv420p -c:a aac output.mp4");
            return;
#else
            if (!Recorder.IsSupported)
            {
                Debug.LogWarning("[SimpleRecorder] Recording export is not supported on this platform/runtime configuration. Build to a supported platform to enable recording.");
                return;
            }
            if (mLastSessionId > 0)
            {
                Recorder.ExportRecordingSession(mLastSessionId);
            }
#endif
        }

        void RecordingExported(long sessionId, string path, Recorder.ErrorCode errorCode)
        {
            if (errorCode == Recorder.ErrorCode.NoError)
            {
                Debug.Log("Recording exported to " + path + ", session id " + sessionId);

    #if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
                CopyFileToDesktop(path, "MyAwesomeRecording.mp4");
    #elif UNITY_IOS ||  UNITY_TVOS
                PlayVideo(path);
    #endif

                // Or save to photos using the Sharing API (triggers save to file dialog on macOS)
                // Remember to uncomment using pmjo.NextGenRecorder.Sharing at the top of the file
                // Sharing.SaveToPhotos(path, "My Awesome Album");

                // Or share using the Sharing API (only available on iOS)
                // Sharing.ShowShareSheet(path, true);
            }
            else
            {
                Debug.Log("Failed to export recording, error code " + errorCode + ", session id " + sessionId);
            }
        }

        private void UpdateStartAndStopRecordingButton()
        {
            // Keep the Start button clickable so the user gets feedback even when unsupported.
            // (Some platforms/plugins report IsSupported=false in the Editor.)
#if UNITY_ANDROID
            EnsureQuestRecorder();
            startRecordingButton.interactable = !questRecorder.IsRecording;
            stopRecordingButton.interactable = questRecorder.IsRecording;

            startRecordingButton.gameObject.SetActive(!questRecorder.IsRecording);
            stopRecordingButton.gameObject.SetActive(questRecorder.IsRecording);
#else
            startRecordingButton.interactable = !Recorder.IsRecording;
            stopRecordingButton.interactable = Recorder.IsRecording;

            startRecordingButton.gameObject.SetActive(!Recorder.IsRecording);
            stopRecordingButton.gameObject.SetActive(Recorder.IsRecording);
#endif
        }

        private void UpdateSaveOrViewRecordingButton()
        {
#if UNITY_ANDROID
            // On Quest/Android we save frames (and optional audio) into persistentDataPath.
            // Reuse the existing "Save" button as a "Show output path" button.
            saveRecordingButton.gameObject.SetActive(true);
            saveRecordingButton.interactable = !string.IsNullOrWhiteSpace(_lastQuestSessionDir) && !(questRecorder != null && questRecorder.IsRecording);
            viewRecordingButton.gameObject.SetActive(false);
#else
    #if UNITY_EDITOR ||  UNITY_STANDALONE
            saveRecordingButton.gameObject.SetActive(true);
            saveRecordingButton.interactable = (mLastSessionId > 0) && !Recorder.IsRecording;
            viewRecordingButton.gameObject.SetActive(false);
    #else
            viewRecordingButton.gameObject.SetActive(true);
            viewRecordingButton.interactable = (mLastSessionId > 0) && !Recorder.IsRecording;
            saveRecordingButton.gameObject.SetActive(false);
    #endif
#endif
        }

    #if UNITY_EDITOR_OSX ||  UNITY_STANDALONE_OSX
        private static void CopyFileToDesktop(string path, string fileName)
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string dstPath = Path.Combine(desktopPath, fileName);

            File.Copy(path, dstPath, true);

            Debug.Log("Recording " + fileName + " copied to the desktop");
        }

    #elif UNITY_IOS ||  UNITY_TVOS
        private static void PlayVideo(string path)
        {
            if (!path.Contains("file://"))
            {
                path = "file://" + path;
            }

            Handheld.PlayFullScreenMovie(path);
        }

    #endif

        private static void CreateEventSystemIfItDoesNotExist()
        {
            // Ensure an EventSystem exists and has a compatible input module.
            // On Unity 6 projects using the new Input System, StandaloneInputModule won't process input.
#if UNITY_2023_1_OR_NEWER
            EventSystem eventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
#else
            EventSystem eventSystem = FindObjectOfType<EventSystem>();
#endif

            if (eventSystem == null)
            {
                var go = new GameObject("EventSystem");
                eventSystem = go.AddComponent<EventSystem>();
            }

            // If there is no input module at all, add one that matches the project's input backend.
            if (eventSystem.GetComponent<BaseInputModule>() == null)
            {
#if ENABLE_INPUT_SYSTEM
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
#else
                eventSystem.gameObject.AddComponent<StandaloneInputModule>();
#endif
            }
        }

#if UNITY_ANDROID
        private void EnsureQuestRecorder()
        {
            if (questRecorder != null)
                return;

#if UNITY_2023_1_OR_NEWER
            questRecorder = UnityEngine.Object.FindFirstObjectByType<QuestFrameSequenceRecorder>();
#else
            questRecorder = FindObjectOfType<QuestFrameSequenceRecorder>();
#endif

            if (questRecorder == null)
                questRecorder = gameObject.AddComponent<QuestFrameSequenceRecorder>();
        }
#endif
    }
}
