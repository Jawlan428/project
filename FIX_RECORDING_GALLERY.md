# Fix: Recordings Not Showing in Gallery UI

## Problem
After recording in VR, the saved recording doesn't appear in the recording UI screen/gallery.

## ✅ What Was Fixed

### 1. Improved Gallery Refresh Logic
- **Increased wait time** for marker file creation (up to 5 seconds)
- **Better detection** of when recording is complete
- **Auto-finds gallery manager** if not assigned
- **Verifies** recording appears in gallery after refresh

### 2. Enhanced Recording Detection
- **More robust frame-based recording detection** (checks subdirectories)
- **Better age calculation** (uses both LastWriteTime and CreationTime)
- **Improved marker file checking** with multiple attempts

### 3. Better Logging
- Added detailed debug logs to track:
  - When gallery refresh is triggered
  - Marker file detection
  - Recording discovery
  - Verification that recording appears in gallery

## 🔍 How to Verify It's Working

### Check Console Logs
When you stop a recording, you should see:
```
[VRRecordingUI] ✅ Quest recording saved to: [path]
[VRRecordingUI] Marker file exists: True/False
[VRRecordingUI] Refreshing gallery now...
[VRRecordingUI] ✅ Gallery refreshed
[VRRecordingUI] ✅ Recording found in gallery! Total recordings: X
```

### Manual Refresh
If recordings still don't appear:
1. **Open the gallery panel** (if it has a refresh button, click it)
2. **Close and reopen** the gallery panel
3. The gallery auto-refreshes when opened

## 🛠️ Troubleshooting

### If recordings still don't appear:

1. **Check the recording folder exists:**
   - On Quest: `/storage/emulated/0/Android/data/[your.app.package]/files/QuestRecordings/`
   - Look for folders named `Recording_YYYY-MM-DD_HH-mm-ss`

2. **Verify files are saved:**
   - Check that `frame_*.jpg` files exist in the recording folder
   - Check for `encoding_complete.marker` file

3. **Check gallery manager reference:**
   - In Unity Inspector, verify `VRRecordingUIController` has `galleryManager` assigned
   - Or it will auto-find it, but manual assignment is more reliable

4. **Manual refresh:**
   - The gallery has a refresh button - try clicking it
   - Or close and reopen the gallery panel

5. **Check console for errors:**
   - Look for any error messages about file access or path issues

## 📝 Code Changes Made

### VRRecordingUIController.cs
- Improved `OnQuestRecordingStopped()` to wait for marker file
- Enhanced `RefreshGalleryDelayed()` with better detection
- Added verification that recording appears in gallery
- Auto-finds gallery manager if not assigned

### VRRecordingsGalleryManager.cs
- Better frame-based recording detection (checks all depths)
- Improved age calculation for incomplete recordings
- More robust marker file checking

## ✅ Expected Behavior

1. **Record video** in VR
2. **Stop recording** - processing begins
3. **Wait 2-5 seconds** - marker file is created
4. **Gallery auto-refreshes** - recording appears in list
5. **Recording is playable** - click to play

---

**Status:** Fixed! Recordings should now appear in the gallery automatically after recording stops.

