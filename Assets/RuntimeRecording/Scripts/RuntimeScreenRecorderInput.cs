using UnityEngine;

namespace RuntimeRecording
{
    /// <summary>
    /// Simple keyboard toggle for the recorder (Desktop/Editor).
    /// </summary>
    public sealed class RuntimeScreenRecorderInput : MonoBehaviour
    {
        public KeyCode toggleKey = KeyCode.F9;
        public RuntimeScreenRecorder recorder;

        private void Awake()
        {
            if (recorder == null)
                recorder = GetComponent<RuntimeScreenRecorder>();
        }

        private void Update()
        {
            if (recorder == null)
                return;

            if (Input.GetKeyDown(toggleKey))
            {
                if (recorder.IsRecording)
                    recorder.StopRecording();
                else
                    recorder.StartRecording();
            }
        }
    }
}


