using UnityEditor;
using UnityEngine;

namespace RuntimeRecording.Editor
{
    public static class RuntimeRecordingMenu
    {
        [MenuItem("Tools/Runtime Recording/Add Recorder To Scene")]
        private static void AddRecorderToScene()
        {
            var existing = Object.FindFirstObjectByType<RuntimeRecording.RuntimeScreenRecorder>();
            if (existing != null)
            {
                Selection.activeObject = existing.gameObject;
                return;
            }

            var go = new GameObject("RuntimeRecorder");
            Undo.RegisterCreatedObjectUndo(go, "Add Runtime Recorder");

            var recorder = go.AddComponent<RuntimeRecording.RuntimeScreenRecorder>();
            go.AddComponent<RuntimeRecording.RuntimeScreenRecorderInput>();
            go.AddComponent<RuntimeRecording.RuntimeScreenRecorderIMGUI>();

            recorder.captureCamera = Camera.main;

            var al = Object.FindFirstObjectByType<AudioListener>();
            recorder.audioListener = al;

            Selection.activeObject = go;
        }
    }
}


