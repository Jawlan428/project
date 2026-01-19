# VR Recordings Gallery - Setup Guide

A complete in-VR video player and recordings gallery system for Meta Quest 3.

## Overview

This system provides:
- **World-Space UI Gallery Panel** - Browse and select recordings
- **Virtual Video Screen** - Play recordings on an in-VR screen
- **Full Playback Controls** - Play/Pause, Stop, Seek, Volume
- **Quest 3 Compatible** - Works with on-device storage

---

## Quick Setup (5 Minutes)

### Step 1: Create the Video Screen

1. **Create a World Space Canvas** for the video screen:
   - `GameObject > UI > Canvas`
   - Set **Render Mode** to `World Space`
   - Scale: `(0.001, 0.001, 0.001)` (for 1 unit = 1 meter)
   - Position in scene where you want the video screen

2. **Add a RawImage** for the video display:
   - Create child: `UI > Raw Image`
   - Name it `VideoDisplay`
   - Size: `1920 x 1080` (or your preferred aspect ratio)

3. **Add Video Player component**:
   - Select the Canvas
   - `Add Component > Video Player`
   - `Add Component > VRVideoScreenPlayer` (our script)

4. **Configure VRVideoScreenPlayer**:
   - Drag the RawImage to `Video Display Image`
   - Set resolution to match your RawImage

### Step 2: Create the Recordings Gallery Panel

1. **Create a World Space Canvas** for the gallery:
   - `GameObject > UI > Canvas`
   - Set **Render Mode** to `World Space`
   - Scale: `(0.001, 0.001, 0.001)`
   - Position near the player (e.g., on a wall or floating tablet)

2. **Build the Gallery UI Structure**:

```
GalleryCanvas (Canvas - World Space)
├── GalleryPanel (Panel - the main container)
│   ├── Header (Panel)
│   │   ├── TitleText (TextMeshPro) - "📁 Recordings"
│   │   ├── CountText (TextMeshPro) - "5 Recordings"
│   │   ├── RefreshButton (Button)
│   │   └── CloseButton (Button)
│   │
│   ├── ScrollView (Scroll Rect)
│   │   └── Viewport
│   │       └── Content (Vertical Layout Group)
│   │           └── [Recording items will be spawned here]
│   │
│   └── NoRecordingsText (TextMeshPro) - "No recordings found"
│
└── OpenGalleryButton (Button - always visible)
```

3. **Add VRRecordingsGalleryManager**:
   - Select the GalleryCanvas
   - `Add Component > VRRecordingsGalleryManager`
   - Wire up all the UI references

### Step 3: Create the Recording Item Prefab

1. **Create the prefab structure**:

```
RecordingItem (Button)
├── Background (Image)
├── TitleText (TextMeshPro) - "Recording - Jan 15, 2026"
├── SubtitleText (TextMeshPro) - "Jan 15, 2026 14:30 • 125 MB"
├── PlayButton (Button) [Optional - or use the whole item]
├── DeleteButton (Button)
└── DeleteConfirmPanel (Panel - hidden by default)
    ├── ConfirmText - "Delete this recording?"
    ├── ConfirmDeleteButton (Button) - "Delete"
    └── CancelDeleteButton (Button) - "Cancel"
```

2. **Add RecordingListItem component**:
   - Attach `RecordingListItem` script to the root
   - Wire up all text and button references

3. **Save as Prefab**:
   - Drag to `Assets/Prefabs/RecordingItemPrefab`
   - Assign to `VRRecordingsGalleryManager.recordingItemPrefab`

### Step 4: Add Playback Controls to Video Screen

Add these controls to your Video Screen Canvas:

```
VideoScreenCanvas
├── VideoDisplay (RawImage)
├── ControlsPanel (Panel - at bottom of screen)
│   ├── PlayPauseButton (Button)
│   │   └── Icon (Image) - play/pause sprites
│   ├── StopButton (Button)
│   ├── ProgressSlider (Slider)
│   ├── TimeText (TextMeshPro) - "1:23 / 5:00"
│   └── VolumeSlider (Slider)
├── TitleText (TextMeshPro) - current video name
└── LoadingIndicator (GameObject with spinner)
```

---

## Component Reference

### VRRecordingsGalleryManager

Main controller for the recordings gallery.

| Property | Description |
|----------|-------------|
| `galleryPanel` | The panel containing the gallery UI |
| `openGalleryButton` | Button to open the gallery |
| `closeGalleryButton` | Button to close the gallery |
| `recordingListContent` | Transform where recording items are spawned |
| `recordingItemPrefab` | Prefab for each recording entry |
| `noRecordingsText` | Text shown when no recordings exist |
| `recordingsCountText` | Text showing total count |
| `refreshButton` | Button to refresh the list |
| `videoPlayer` | Reference to VRVideoScreenPlayer |
| `recordingsFolderName` | Folder name to search (default: "QuestRecordings") |

**Key Methods:**
```csharp
// Open/Close gallery
galleryManager.OpenGallery();
galleryManager.CloseGallery();
galleryManager.ToggleGallery();

// Play a recording
galleryManager.PlayRecording(filePath);
galleryManager.PlayRecording(index);

// Refresh the list
galleryManager.RefreshRecordingsList();

// Get recordings
List<RecordingInfo> all = galleryManager.GetAllRecordings();
```

### VRVideoScreenPlayer

Handles video playback on the virtual screen.

| Property | Description |
|----------|-------------|
| `videoDisplayImage` | RawImage for video output |
| `videoDisplayRenderer` | Alternative: MeshRenderer |
| `renderTextureWidth/Height` | Resolution (default: 1920x1080) |
| `audioSource` | AudioSource for spatial audio |
| `useSpatialAudio` | Enable 3D positioned sound |
| `defaultVolume` | Initial volume (0-1) |
| `playPauseButton` | UI button for play/pause |
| `stopButton` | UI button to stop |
| `progressSlider` | Slider for seek control |
| `volumeSlider` | Slider for volume |
| `timeText` | Text showing current time |
| `titleText` | Text showing video name |
| `playPauseIcon` | Image that shows play/pause state |
| `playIcon/pauseIcon` | Sprites for the icon |
| `videoScreenPanel` | Parent panel to show/hide |
| `loadingIndicator` | Loading spinner |

**Key Methods:**
```csharp
// Playback control
videoPlayer.PlayVideo(filePath);
videoPlayer.TogglePlayPause();
videoPlayer.Pause();
videoPlayer.Resume();
videoPlayer.Stop();

// Seeking
videoPlayer.SeekTo(timeInSeconds);
videoPlayer.SeekToNormalized(0.5f); // 50% of video

// Volume
videoPlayer.SetVolume(0.8f);

// State
bool playing = videoPlayer.IsPlaying;
double time = videoPlayer.GetCurrentTime();
double duration = videoPlayer.GetDuration();
float progress = videoPlayer.GetProgress();
```

**Events:**
```csharp
videoPlayer.OnVideoStarted += (path) => { };
videoPlayer.OnVideoStopped += () => { };
videoPlayer.OnVideoPaused += () => { };
videoPlayer.OnVideoResumed += () => { };
videoPlayer.OnVideoError += (error) => { };
```

---

## Storage Paths

### On Quest 3 (Android Build)

Recordings are stored in:
```
/storage/emulated/0/Android/data/[your.bundle.id]/files/QuestRecordings/
```

This is `Application.persistentDataPath + "/QuestRecordings/"`

### On Editor/Desktop

The system searches these locations (in order):
1. `[ProjectRoot]/QuestRecordings/`
2. `Desktop/MeetingRecordings/` (from MeetingVideoRecorder)
3. `Application.persistentDataPath/QuestRecordings/`
4. Any additional paths you configure

### Recommended Recording Setup

Update your recording script to save to the correct path:

```csharp
// In your MeetingVideoRecorder or QuestFrameSequenceRecorder:

string GetRecordingBasePath()
{
#if UNITY_ANDROID && !UNITY_EDITOR
    // Quest: Use persistent data path
    return Path.Combine(Application.persistentDataPath, "QuestRecordings");
#else
    // Desktop: Use project folder or desktop
    return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "MeetingRecordings");
#endif
}
```

---

## Quest 3 Specific Notes

### Permissions

For Quest builds, ensure these permissions are set in your Android manifest:

```xml
<!-- Already default for Unity apps -->
<uses-permission android:name="android.permission.INTERNET" />
<uses-permission android:name="android.permission.READ_EXTERNAL_STORAGE" />
<uses-permission android:name="android.permission.WRITE_EXTERNAL_STORAGE" />

<!-- For microphone recording -->
<uses-permission android:name="android.permission.RECORD_AUDIO" />
```

### Video Formats

Supported formats on Quest:
- **MP4** (H.264/AVC) - ✅ Best compatibility
- **WebM** (VP8/VP9) - ✅ Supported
- **MOV** - ⚠️ Limited support

**Recommended encoding settings:**
- Codec: H.264 (libx264)
- Resolution: 1920x1080 or 1280x720
- Frame rate: 30fps
- Bitrate: 8-15 Mbps for quality

### Video Encoding on Quest

Since ffmpeg isn't available on Quest, recordings made on-device save as frames + audio. Options:

1. **Use Pre-encoded MP4**: Record and encode on PC, transfer to Quest
2. **Transfer frames to PC**: Use ADB to pull frames, encode with ffmpeg
3. **Use Quest's built-in recording**: Meta provides screen recording APIs
4. **Native Plugin**: Use NatCorder or similar for on-device encoding

**ADB commands to transfer recordings:**
```bash
# List recordings on Quest
adb shell ls /storage/emulated/0/Android/data/[bundle.id]/files/QuestRecordings/

# Pull a recording session to PC
adb pull /storage/emulated/0/Android/data/[bundle.id]/files/QuestRecordings/Recording_20260115_143000/ ./

# Push an encoded MP4 back to Quest
adb push meeting.mp4 /storage/emulated/0/Android/data/[bundle.id]/files/QuestRecordings/Recording_20260115_143000/
```

---

## XR Interaction Setup

### For XR Interaction Toolkit

1. **Add XR Ray Interactor** to your hand/controller
2. **Canvas Setup**:
   - Add `Tracked Device Graphic Raycaster` component to each World Space Canvas
   - Enable "Block on 3D Collision" if desired

3. **Button Interaction**:
   - Ensure buttons have `XRI TMP Button Fill Affordance` (from your existing prefabs)
   - Or use standard Unity UI buttons with `EventTrigger` components

### Progress Slider Interaction

To properly handle slider drag for seeking:

```csharp
// Add EventTrigger to your progress slider
// PointerDown -> videoPlayer.OnProgressSliderPointerDown()
// PointerUp -> videoPlayer.OnProgressSliderPointerUp()
```

---

## Example: Minimal Setup Code

If you prefer to set this up via code:

```csharp
using UnityEngine;
using VRRecordings;

public class RecordingsGallerySetup : MonoBehaviour
{
    void Start()
    {
        // Create video screen
        var screenGO = new GameObject("VideoScreen");
        var videoPlayer = screenGO.AddComponent<VRVideoScreenPlayer>();
        
        // Create gallery manager
        var galleryGO = new GameObject("RecordingsGallery");
        var galleryManager = galleryGO.AddComponent<VRRecordingsGalleryManager>();
        
        // Wire them up (via inspector is easier!)
        // galleryManager.videoPlayer = videoPlayer;
    }
}
```

---

## Troubleshooting

### "No recordings found"

1. Check the storage path: `Debug > Debug: Print Storage Paths` (context menu in Editor)
2. Ensure recordings are in the correct folder structure
3. On Quest, use ADB to verify files exist:
   ```bash
   adb shell ls -la /storage/emulated/0/Android/data/[bundle.id]/files/
   ```

### Video won't play

1. Check video format (must be H.264 MP4 for best compatibility)
2. Verify file exists and isn't corrupted
3. Check Unity console for VideoPlayer errors
4. On Quest, try a known-good test video first

### No audio

1. Ensure AudioSource is assigned (or use Direct mode)
2. Check volume slider value
3. Verify the video has an audio track
4. On Quest, check audio routing settings

### UI not responding to XR input

1. Add `Tracked Device Graphic Raycaster` to World Space canvases
2. Ensure XR Ray Interactor is properly configured
3. Check that canvas has correct sorting order

---

## File Structure

```
Assets/Scripts/VRRecordingsGallery/
├── VRRecordingsGalleryManager.cs   # Main gallery controller
├── VRVideoScreenPlayer.cs          # Video playback handler
├── RecordingListItem.cs            # Individual list item
├── QuestVideoEncoder.cs            # Encoding utilities
└── RECORDINGS_GALLERY_SETUP.md     # This file
```

---

## Integration with Existing Recording System

Your existing `QuestFrameSequenceRecorder` already saves to the correct location on Quest. To ensure MP4s are playable in the gallery:

1. **Update the output path** (already correct in your code):
   ```csharp
   // In QuestFrameSequenceRecorder.GetRootOutputDirectory()
   // On Quest, uses Application.persistentDataPath ✅
   ```

2. **Ensure MP4 is created**: The recorder creates MP4 via ffmpeg on Desktop. For Quest, either:
   - Encode on PC and transfer to Quest
   - Use a native encoding plugin

3. **The gallery will find recordings** in both locations automatically.

---

## Support

For issues or feature requests, check the Unity console for debug logs prefixed with `[VRRecordingsGallery]` or `[VRVideoScreenPlayer]`.

