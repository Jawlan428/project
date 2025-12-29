using UnityEngine;

namespace RuntimeRecording
{
    /// <summary>
    /// Minimal on-screen status overlay (IMGUI). Useful for quick verification in desktop builds.
    /// </summary>
    public sealed class RuntimeScreenRecorderIMGUI : MonoBehaviour
    {
        public RuntimeScreenRecorder recorder;
        public bool show = true;

        private void Awake()
        {
            if (recorder == null)
                recorder = GetComponent<RuntimeScreenRecorder>();
        }

        private void OnGUI()
        {
            if (!show || recorder == null)
                return;

            var r = new Rect(12, 12, 520, 80);
            GUI.Box(r, "");

            var status = recorder.IsRecording ? "RECORDING (F9 to stop)" : "Idle (F9 to start)";
            GUI.Label(new Rect(24, 22, 500, 24), $"Runtime Recorder: {status}");
            GUI.Label(new Rect(24, 44, 500, 24), $"Output: {recorder.CurrentOutputDirectory}");
        }
    }
}


