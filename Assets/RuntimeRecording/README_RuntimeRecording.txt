Runtime Recording (Video + Audio)
================================

What you get
------------
- Press F9 to Start/Stop recording in Play Mode / Windows build.
- Video is recorded as JPG frames (frames/frame_000001.jpg, ...).
- Audio is recorded as a WAV file (audio.wav).
- If ffmpeg is available, an MP4 is automatically created on Stop.

Where files are saved
---------------------
By default this recorder saves into your Desktop (Windows):

  Desktop\UnityRecordings\Recording_YYYYMMDD_HHMMSS\

Inside that folder you will see:
- frames\frame_000000.jpg ...
- audio.wav (if Audio is enabled and an AudioListener exists)
- Recording_YYYYMMDD_HHMMSS.mp4 (if ffmpeg merge is enabled and ffmpeg exists)

If Desktop isn't available (Android/Quest), it falls back to:

  Application.persistentDataPath

Setup
-----
1) In Unity: Tools -> Runtime Recording -> Add Recorder To Scene
2) Press Play
3) Press F9 to start/stop

MP4 (ffmpeg)
------------
Unity does not include a built-in runtime MP4 encoder. This uses ffmpeg if installed.

Install ffmpeg on Windows:
- Put ffmpeg.exe on your PATH, or
- Set "Ffmpeg Path Override" on the RuntimeScreenRecorder component.

Notes / Performance
-------------------
- ReadPixels + JPG encoding is CPU heavy. Lower resolution/FPS for smoother gameplay.
- For VR/Quest, you usually won't have a keyboard (F9), so you should call StartRecording/StopRecording from UI instead.


