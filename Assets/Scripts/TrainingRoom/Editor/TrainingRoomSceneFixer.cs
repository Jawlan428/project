#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VRRecordings;

namespace TrainingRoom.Editor
{
    /// <summary>
    /// Patches an existing Training Room scene setup.
    /// Finds the ScreenVideoController (or adds it), wires all buttons,
    /// and populates the video list from StreamingAssets/TrainingVideos/.
    ///
    /// Menu: Tools → Training Room → Fix / Wire Scene (Run After Setup)
    /// </summary>
    public static class TrainingRoomSceneFixer
    {
        private static readonly Color DoorColor = new Color(0.28f, 0.20f, 0.12f);
        private static readonly Color WindowFrameColor = new Color(0.10f, 0.10f, 0.12f);
        private static readonly Color WindowGlassColor = new Color(0.40f, 0.65f, 0.75f, 0.22f);

        /// <summary>
        /// Patches the TrainingScreen material in the scene to use URP _BaseMap.
        /// Fixes the "video playing but screen is black" issue.
        /// </summary>
        [MenuItem("Tools/Training Room/Fix Screen Material (URP Black Screen Fix)", priority = 3)]
        public static void FixScreenMaterial()
        {
            var screenObj = GameObject.Find("TrainingScreen");
            if (screenObj == null)
            {
                EditorUtility.DisplayDialog("Fix Screen Material",
                    "Could not find 'TrainingScreen' in the scene.\nMake sure the Training Room is set up.", "OK");
                return;
            }

            var mr = screenObj.GetComponent<MeshRenderer>();
            if (mr == null) return;

            // Re-create the material with URP Unlit shader
            Shader urpUnlit = Shader.Find("Universal Render Pipeline/Unlit")
                           ?? Shader.Find("Unlit/Texture");

            if (urpUnlit == null)
            {
                Debug.LogError("[TrainingRoomFixer] Could not find URP Unlit shader.");
                return;
            }

            var newMat = new Material(urpUnlit);
            newMat.name = "TrainingScreen_VideoMat_URP";

            // Try to save over existing material asset
            string existingPath = "Assets/TrainingRoom/Prefabs/TrainingScreen_VideoMat.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(existingPath) != null)
            {
                // Copy shader to existing asset
                var existingMat = AssetDatabase.LoadAssetAtPath<Material>(existingPath);
                existingMat.shader = urpUnlit;
                EditorUtility.SetDirty(existingMat);
                mr.sharedMaterial = existingMat;
                Debug.Log("[TrainingRoomFixer] Updated existing material shader to URP Unlit.");
            }
            else
            {
                // Save new material
                AssetDatabase.CreateAsset(newMat, existingPath);
                mr.sharedMaterial = newMat;
                Debug.Log("[TrainingRoomFixer] Created new URP material for TrainingScreen.");
            }

            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            EditorUtility.DisplayDialog("Screen Material Fixed!",
                "TrainingScreen material is now using URP Unlit shader.\n\n" +
                "Press Play and click ▶ Play — the video should now appear on the screen.",
                "OK");
        }

        /// <summary>
        /// Rescans StreamingAssets/TrainingVideos/ and updates ScreenVideoController
        /// with every MP4 found — call this any time you add/remove video files.
        /// </summary>
        [MenuItem("Tools/Training Room/Refresh Video Playlist", priority = 4)]
        public static void RefreshVideoPlaylist()
        {
            var controller = Object.FindFirstObjectByType<ScreenVideoController>();
            if (controller == null)
            {
                EditorUtility.DisplayDialog("Refresh Playlist",
                    "ScreenVideoController not found in the scene.\n\n" +
                    "Run: Tools → Training Room → Fix + Wire Scene first.",
                    "OK");
                return;
            }

            string streamingFolder = Path.Combine(Application.streamingAssetsPath, "TrainingVideos");
            if (!Directory.Exists(streamingFolder))
            {
                EditorUtility.DisplayDialog("Refresh Playlist",
                    "Folder not found:\nAssets/StreamingAssets/TrainingVideos/\n\n" +
                    "Create it and drop your MP4 files inside.", "OK");
                return;
            }

            string[] mp4Files = Directory.GetFiles(streamingFolder, "*.mp4")
                                         .Select(Path.GetFileName)
                                         .OrderBy(f => f)
                                         .ToArray();

            if (mp4Files.Length == 0)
            {
                EditorUtility.DisplayDialog("Refresh Playlist",
                    "No MP4 files found in:\nAssets/StreamingAssets/TrainingVideos/\n\n" +
                    "Drop your MP4 files there first.", "OK");
                return;
            }

            string[] titles = BuildTitles(mp4Files);

            var so        = new SerializedObject(controller);
            var filesProp = so.FindProperty("videoFileNames");
            var titlesProp = so.FindProperty("videoTitles");

            filesProp.ClearArray();
            titlesProp.ClearArray();

            for (int i = 0; i < mp4Files.Length; i++)
            {
                filesProp.InsertArrayElementAtIndex(i);
                filesProp.GetArrayElementAtIndex(i).stringValue = mp4Files[i];

                titlesProp.InsertArrayElementAtIndex(i);
                titlesProp.GetArrayElementAtIndex(i).stringValue = titles[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            string fileList = string.Join("\n  ", mp4Files.Select((f, i) => $"{i + 1}. {titles[i]}  [{f}]"));
            EditorUtility.DisplayDialog(
                "Playlist Refreshed!",
                $"Found {mp4Files.Length} video(s):\n\n  {fileList}\n\n" +
                "Save the scene (Ctrl+S) then press Play.\n" +
                "Use Next/Prev buttons to cycle through all videos.",
                "OK");

            Debug.Log($"[TrainingRoom] Playlist updated with {mp4Files.Length} video(s):\n  " +
                      string.Join("\n  ", mp4Files));
        }

        [MenuItem("Tools/Training Room/Fix + Wire Scene (Run If Screen Is Black)", priority = 2)]
        public static void FixScene()
        {
            // ── 1. Find or create ScreenVideoController ───────────────────────
            var controller = Object.FindFirstObjectByType<ScreenVideoController>();

            // Find the VideoPlayer_Root in the scene
            var vrPlayer = Object.FindFirstObjectByType<VRVideoScreenPlayer>();
            if (vrPlayer == null)
            {
                EditorUtility.DisplayDialog("Training Room Fixer",
                    "Could not find VRVideoScreenPlayer in the scene.\n\n" +
                    "Please run:\nTools → Training Room → ★ Full Setup first.",
                    "OK");
                return;
            }

            // Add ScreenVideoController to VideoPlayer_Root if missing
            if (controller == null)
            {
                controller = vrPlayer.gameObject.AddComponent<ScreenVideoController>();
                Debug.Log("[TrainingRoomFixer] Added ScreenVideoController to " + vrPlayer.gameObject.name);
            }

            var so = new SerializedObject(controller);

            // ── 2. Assign VRVideoScreenPlayer ─────────────────────────────────
            SetObjField(so, "screenPlayer", vrPlayer);

            // ── 3. Discover video files from StreamingAssets/TrainingVideos/ ──
            string streamingFolder = Path.Combine(Application.streamingAssetsPath, "TrainingVideos");
            string[] mp4Files = Directory.Exists(streamingFolder)
                ? Directory.GetFiles(streamingFolder, "*.mp4")
                          .Select(Path.GetFileName)
                          .OrderBy(f => f)
                          .ToArray()
                : new string[0];

            if (mp4Files.Length == 0)
            {
                Debug.LogWarning("[TrainingRoomFixer] No MP4 files found in StreamingAssets/TrainingVideos/");
                mp4Files = new[] { "15909399-uhd_3840_2160_25fps.mp4" }; // fallback to known file
            }

            // Build matching titles from TrainingVideoEntry assets
            string[] titles = BuildTitles(mp4Files);

            // Assign fileNames array
            var filesProp = so.FindProperty("videoFileNames");
            filesProp.ClearArray();
            for (int i = 0; i < mp4Files.Length; i++)
            {
                filesProp.InsertArrayElementAtIndex(i);
                filesProp.GetArrayElementAtIndex(i).stringValue = mp4Files[i];
            }

            // Assign titles array
            var titlesProp = so.FindProperty("videoTitles");
            titlesProp.ClearArray();
            for (int i = 0; i < titles.Length; i++)
            {
                titlesProp.InsertArrayElementAtIndex(i);
                titlesProp.GetArrayElementAtIndex(i).stringValue = titles[i];
            }

            // ── 4. Find + wire buttons from ControlsCanvas ────────────────────
            var controlsCanvas = FindByName("ControlsCanvas");
            if (controlsCanvas != null)
            {
                var allButtons  = controlsCanvas.GetComponentsInChildren<Button>(true);
                var allLabels   = controlsCanvas.GetComponentsInChildren<TextMeshProUGUI>(true);

                Button ppBtn   = FindButtonByLabel(allButtons, "Play",  "Pause", "▶",  "⏸");
                Button stopBtn = FindButtonByLabel(allButtons, "Stop",  "⏹");
                Button prevBtn = FindButtonByLabel(allButtons, "Prev",  "◀");
                Button nextBtn = FindButtonByLabel(allButtons, "Next",  "▶▶");

                SetObjField(so, "playPauseButton", ppBtn);
                SetObjField(so, "stopButton",      stopBtn);
                SetObjField(so, "prevButton",      prevBtn);
                SetObjField(so, "nextButton",      nextBtn);

                // Find "No video selected" / NowPlayingLabel
                var nowLabel = allLabels.FirstOrDefault(t =>
                    t.gameObject.name.ToLower().Contains("nowplaying") ||
                    t.text.Contains("No video") ||
                    t.gameObject.name.ToLower().Contains("label"));
                SetObjField(so, "nowPlayingLabel", nowLabel);

                Debug.Log($"[TrainingRoomFixer] Wired buttons — PP:{ppBtn != null}, Stop:{stopBtn != null}, " +
                          $"Prev:{prevBtn != null}, Next:{nextBtn != null}, Label:{nowLabel != null}");

                // ── Remove duplicate onClick listeners from VRVideoScreenPlayer ──
                if (ppBtn != null)
                {
                    ppBtn.onClick.RemoveAllListeners();
                    Debug.Log("[TrainingRoomFixer] Cleared old onClick listeners on PlayPause button");
                }
                if (stopBtn != null)
                {
                    stopBtn.onClick.RemoveAllListeners();
                }
            }
            else
            {
                Debug.LogWarning("[TrainingRoomFixer] ControlsCanvas not found in scene.");
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            // Mark scene dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            // ── 5. Report ─────────────────────────────────────────────────────
            string fileList = string.Join("\n  • ", mp4Files);
            EditorUtility.DisplayDialog(
                "Training Room — Fixed!",
                $"ScreenVideoController is now wired and ready.\n\n" +
                $"Videos found ({mp4Files.Length}):\n  • {fileList}\n\n" +
                $"Press PLAY in Unity, then click the ▶ Play button on the screen.\n\n" +
                $"Save the scene (Ctrl+S) to keep these changes.",
                "OK");
        }

        [MenuItem("Tools/Training Room/Add Door + Windows", priority = 7)]
        public static void AddDoorAndWindows()
        {
            var shell = GameObject.Find("RoomShell");
            if (shell == null)
            {
                EditorUtility.DisplayDialog(
                    "Add Door + Windows",
                    "Could not find 'RoomShell' in the scene.\nRun Full Setup first.",
                    "OK");
                return;
            }

            var existing = FindDeepChild(shell.transform, "ArchitecturalDetails");
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }

            var architectural = new GameObject("ArchitecturalDetails");
            architectural.transform.SetParent(shell.transform, false);

            AddBox(architectural.transform, "BackDoor",
                new Vector3(3.2f, 1.05f, -3.93f), new Vector3(1.2f, 2.1f, 0.10f), DoorColor);
            AddBox(architectural.transform, "BackDoorFrameTop",
                new Vector3(3.2f, 2.13f, -3.89f), new Vector3(1.35f, 0.08f, 0.08f), WindowFrameColor);
            AddBox(architectural.transform, "BackDoorFrameLeft",
                new Vector3(2.56f, 1.05f, -3.89f), new Vector3(0.08f, 2.1f, 0.08f), WindowFrameColor);
            AddBox(architectural.transform, "BackDoorFrameRight",
                new Vector3(3.84f, 1.05f, -3.89f), new Vector3(0.08f, 2.1f, 0.08f), WindowFrameColor);
            AddBox(architectural.transform, "BackDoorHandle",
                new Vector3(3.65f, 1.05f, -3.87f), new Vector3(0.06f, 0.06f, 0.03f), new Color(0.75f, 0.67f, 0.30f));

            BuildSideWindow(architectural.transform, "WindowLeft_01",
                new Vector3(-5.93f, 2.15f, -1.8f), new Vector3(0.06f, 1.4f, 2.3f));
            BuildSideWindow(architectural.transform, "WindowLeft_02",
                new Vector3(-5.93f, 2.15f, 1.7f), new Vector3(0.06f, 1.4f, 2.3f));
            BuildSideWindow(architectural.transform, "WindowRight_01",
                new Vector3(5.93f, 2.15f, -1.8f), new Vector3(0.06f, 1.4f, 2.3f));
            BuildSideWindow(architectural.transform, "WindowRight_02",
                new Vector3(5.93f, 2.15f, 1.7f), new Vector3(0.06f, 1.4f, 2.3f));

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            EditorUtility.DisplayDialog(
                "Door + Windows Added",
                "Added one back door and four side windows to RoomShell.\nSave the scene (Ctrl+S) to keep changes.",
                "OK");
        }

        [MenuItem("Tools/Training Room/Add Teleport Entry Button", priority = 8)]
        public static void AddTeleportEntryButton()
        {
            var roomRoot = GameObject.Find("TrainingRoom_Root");
            if (roomRoot == null)
            {
                EditorUtility.DisplayDialog(
                    "Add Teleport Entry Button",
                    "Could not find 'TrainingRoom_Root'.\nRun Full Setup first.",
                    "OK");
                return;
            }

            var destination = FindDeepChild(roomRoot.transform, "TrainingRoomTeleportDestination");
            if (destination == null)
            {
                var destinationGO = new GameObject("TrainingRoomTeleportDestination");
                destinationGO.transform.SetParent(roomRoot.transform, false);
                destinationGO.transform.localPosition = new Vector3(0f, 0.15f, -1.2f);
                destinationGO.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
                destination = destinationGO.transform;
            }

            var oldUi = FindDeepChild(roomRoot.transform, "TrainingRoomTeleportUI");
            if (oldUi != null)
                UnityEngine.Object.DestroyImmediate(oldUi.gameObject);

            var uiRoot = new GameObject("TrainingRoomTeleportUI");
            uiRoot.transform.SetParent(roomRoot.transform, false);
            uiRoot.transform.localPosition = new Vector3(0f, 1.45f, -4.35f);
            uiRoot.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            uiRoot.transform.localScale = Vector3.one * 0.004f;

            var canvas = uiRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var scaler = uiRoot.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 100f;
            uiRoot.AddComponent<GraphicRaycaster>();

            var rootRT = uiRoot.GetComponent<RectTransform>();
            rootRT.sizeDelta = new Vector2(520f, 140f);

            var bg = new GameObject("Background");
            bg.transform.SetParent(uiRoot.transform, false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.06f, 0.06f, 0.08f, 0.94f);
            var bgRT = bg.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.sizeDelta = Vector2.zero;

            var btnGO = new GameObject("EnterRoomButton");
            btnGO.transform.SetParent(uiRoot.transform, false);
            var btnRT = btnGO.AddComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.08f, 0.18f);
            btnRT.anchorMax = new Vector2(0.92f, 0.82f);
            btnRT.sizeDelta = Vector2.zero;

            var btnImage = btnGO.AddComponent<Image>();
            btnImage.color = new Color(0.18f, 0.52f, 0.22f, 1f);
            var button = btnGO.AddComponent<Button>();
            button.targetGraphic = btnImage;

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(btnGO.transform, false);
            var labelRT = labelGO.AddComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.sizeDelta = Vector2.zero;

            var label = labelGO.AddComponent<TextMeshProUGUI>();
            label.text = "Enter Training Room";
            label.fontSize = 38f;
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;

            var teleporter = btnGO.AddComponent<TrainingRoomTeleportTrigger>();
            var so = new SerializedObject(teleporter);
            SetObjField(so, "destination", destination);
            SetObjField(so, "triggerButton", button);
            so.ApplyModifiedPropertiesWithoutUndo();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            EditorUtility.DisplayDialog(
                "Teleport Button Added",
                "Created a world-space 'Enter Training Room' button and wired teleport destination.\n\n" +
                "Use your XR UI pointer to press it and teleport into the room.\n" +
                "Save the scene (Ctrl+S) to keep changes.",
                "OK");
        }

        // ── helpers ───────────────────────────────────────────────────────────

        private static string[] BuildTitles(string[] fileNames)
        {
            // Load all TrainingVideoEntry assets once
            var guids   = AssetDatabase.FindAssets("t:TrainingVideoEntry");
            var entries = guids
                .Select(g => AssetDatabase.LoadAssetAtPath<TrainingVideoEntry>(
                                 AssetDatabase.GUIDToAssetPath(g)))
                .Where(e => e != null)
                .ToArray();

            return fileNames.Select(fn =>
            {
                // Try to match by filename
                var match = entries.FirstOrDefault(e =>
                    string.Equals(e.streamingAssetsFileName, fn,
                                  System.StringComparison.OrdinalIgnoreCase));
                return match != null ? match.title : Path.GetFileNameWithoutExtension(fn);
            }).ToArray();
        }

        /// <summary>
        /// Rebuilds the VideoTitleBar with a single-line layout where the badge
        /// and title text share identical height, anchor, and vertical alignment.
        /// Menu: Tools → Training Room → Fix Title Bar Layout (Same Line)
        /// </summary>
        [MenuItem("Tools/Training Room/Fix Title Bar Layout (Same Line)", priority = 5)]
        public static void FixTitlePosition()
        {
            // ── 1. Find parent ────────────────────────────────────────────────
            var screenObj   = GameObject.Find("TrainingScreen");
            var roomRoot    = GameObject.Find("TrainingRoom_Root");
            Transform titleParent = roomRoot  != null ? roomRoot.transform
                                  : screenObj != null ? screenObj.transform.parent
                                  : null;

            if (titleParent == null)
            {
                EditorUtility.DisplayDialog("Fix Title Bar",
                    "Could not find TrainingRoom_Root in the scene.\nRun Full Setup first.", "OK");
                return;
            }

            // ── 2. Clean up ControlsCanvas old label ──────────────────────────
            var controlsCanvas = GameObject.Find("ControlsCanvas");
            if (controlsCanvas != null)
            {
                var oldLabel = FindDeepChild(controlsCanvas.transform, "NowPlayingLabel");
                if (oldLabel != null)
                {
                    var t = oldLabel.GetComponent<TextMeshProUGUI>();
                    if (t != null) t.text = "";
                    oldLabel.gameObject.SetActive(false);
                }
                var buttonRow = FindDeepChild(controlsCanvas.transform, "ButtonRow");
                if (buttonRow != null)
                {
                    var rt = buttonRow.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchorMin = new Vector2(0.01f, 0.05f);
                        rt.anchorMax = new Vector2(0.99f, 0.95f);
                        rt.sizeDelta = Vector2.zero;
                    }
                }
            }

            // ── 3. Destroy old title bar ──────────────────────────────────────
            var old = GameObject.Find("VideoTitleBar");
            if (old != null) UnityEngine.Object.DestroyImmediate(old);

            // ── 4. Compute position just above the screen ─────────────────────
            Vector3    titlePos = new Vector3(0f, 3.62f, 3.88f);
            Quaternion titleRot = Quaternion.Euler(0f, 180f, 0f);
            if (screenObj != null)
            {
                var sr = screenObj.transform;
                float top = sr.localPosition.y + sr.localScale.y * 0.5f + 0.10f;
                titlePos   = new Vector3(sr.localPosition.x, top, sr.localPosition.z);
                titleRot   = sr.localRotation;
            }

            // ── 5. Build the canvas ───────────────────────────────────────────
            // Canvas height = 70 units.  Everything uses anchorMin.y=0 → anchorMax.y=1
            // so every element is exactly the same height — guaranteed same line.
            const float BAR_W  = 1350f;
            const float BAR_H  = 70f;
            const float SCALE  = 0.004f;
            const float FONT   = 34f;          // single font size for all text

            var titleBar = new GameObject("VideoTitleBar");
            titleBar.transform.SetParent(titleParent, false);
            titleBar.transform.localPosition = titlePos;
            titleBar.transform.localRotation = titleRot;
            titleBar.transform.localScale    = Vector3.one * SCALE;

            var canvas = titleBar.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var csScaler = titleBar.AddComponent<CanvasScaler>();
            csScaler.dynamicPixelsPerUnit = 100f;
            titleBar.AddComponent<GraphicRaycaster>();
            var rootRT = titleBar.GetComponent<RectTransform>();
            rootRT.sizeDelta = new Vector2(BAR_W, BAR_H);

            // ── Full-width dark background ────────────────────────────────────
            MakeImage(titleBar.transform, "Background",
                new Vector2(0f,    0f), new Vector2(1f,    1f), Vector2.zero,
                new Color(0.06f, 0.06f, 0.08f, 0.92f));

            // ── Green left accent stripe (4 px wide) ──────────────────────────
            var accentRT = MakeImage(titleBar.transform, "AccentStripe",
                new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(6f, 0f),
                new Color(0.25f, 0.78f, 0.32f)).GetComponent<RectTransform>();
            accentRT.pivot = new Vector2(0f, 0.5f);

            // ── "Training" badge: fixed pixel width, full height ──────────────
            // anchors: left edge at 0.7%, right edge at 0.7% + badge width fraction
            const float BADGE_W_PX  = 120f;
            float       badgeRight  = (BADGE_W_PX + 12f) / BAR_W;   // ≈0.098

            MakeImage(titleBar.transform, "BadgeBg",
                new Vector2(0.007f,  0f), new Vector2(badgeRight, 1f), Vector2.zero,
                new Color(0.18f, 0.52f, 0.22f));

            var badgeTxt = MakeTMP(titleBar.transform, "BadgeText",
                new Vector2(0.007f, 0f), new Vector2(badgeRight, 1f), Vector2.zero,
                "Training", FONT * 0.72f, Color.white,
                FontStyles.Bold, TextAlignmentOptions.Midline);
            badgeTxt.textWrappingMode = TextWrappingModes.NoWrap;

            // ── Title text: from badge right edge to index left edge ──────────
            const float INDEX_W_PX = 110f;
            float       indexLeft  = 1f - (INDEX_W_PX + 10f) / BAR_W;   // ≈0.911

            var titleTMP = MakeTMP(titleBar.transform, "TitleText",
                new Vector2(badgeRight + 0.008f, 0f), new Vector2(indexLeft, 1f), Vector2.zero,
                "No video selected", FONT, Color.white,
                FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            titleTMP.textWrappingMode = TextWrappingModes.NoWrap;
            titleTMP.overflowMode     = TextOverflowModes.Ellipsis;

            // ── Index counter e.g. "1 / 3" ────────────────────────────────────
            var indexTMP = MakeTMP(titleBar.transform, "IndexLabel",
                new Vector2(indexLeft, 0f), new Vector2(1f, 1f), Vector2.zero,
                "", FONT * 0.76f, new Color(0.60f, 0.60f, 0.60f),
                FontStyles.Normal, TextAlignmentOptions.MidlineRight);
            indexTMP.textWrappingMode = TextWrappingModes.NoWrap;

            // ── 6. Rewire ScreenVideoController ──────────────────────────────
            var controller = Object.FindFirstObjectByType<ScreenVideoController>();
            if (controller != null)
            {
                var so = new SerializedObject(controller);
                SetObjField(so, "nowPlayingLabel", titleTMP);
                SetObjField(so, "indexLabel",      indexTMP);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            EditorUtility.DisplayDialog("Title Bar Rebuilt!",
                "All elements share the same height and vertical center.\n\n" +
                "Layout:  [ Training ]  Video Title Here          1 / 3\n\n" +
                "Save (Ctrl+S) then press Play.",
                "OK");
        }

        // ── Small UI builder helpers ──────────────────────────────────────────

        private static GameObject MakeImage(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta, Color color)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            var rt  = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.sizeDelta = sizeDelta;
            return go;
        }

        private static TextMeshProUGUI MakeTMP(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta,
            string text, float fontSize, Color color,
            FontStyles style, TextAlignmentOptions alignment)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt  = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.sizeDelta = sizeDelta;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = fontSize;
            tmp.color     = color;
            tmp.fontStyle = style;
            tmp.alignment = alignment;
            return tmp;
        }

        /// <summary>
        /// Replaces unicode arrow/icon characters in button labels with plain ASCII text
        /// so they render on one clean line with any TMP font.
        /// Menu: Tools → Training Room → Fix Button Labels (One Line)
        /// </summary>
        [MenuItem("Tools/Training Room/Fix Button Labels (One Line)", priority = 6)]
        public static void FixButtonLabels()
        {
            var controlsCanvas = GameObject.Find("ControlsCanvas");
            if (controlsCanvas == null)
            {
                EditorUtility.DisplayDialog("Fix Button Labels",
                    "'ControlsCanvas' not found in the scene.\nRun Full Setup first.", "OK");
                return;
            }

            // Map: button GameObject name → desired clean label
            var labelMap = new System.Collections.Generic.Dictionary<string, string>(
                System.StringComparer.OrdinalIgnoreCase)
            {
                { "PrevButton",      "< Prev"  },
                { "PlayPauseButton", "Play"    },
                { "StopButton",      "Stop"    },
                { "NextButton",      "Next >"  },
            };

            int fixed_ = 0;
            var allButtons = controlsCanvas.GetComponentsInChildren<Button>(true);
            foreach (var btn in allButtons)
            {
                if (!labelMap.TryGetValue(btn.gameObject.name, out string newLabel)) continue;

                var label = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (label == null) continue;

                label.text             = newLabel;
                label.textWrappingMode = TextWrappingModes.NoWrap;   // never wrap
                label.overflowMode     = TextOverflowModes.Ellipsis;
                label.fontSize         = 32f;
                label.fontStyle        = FontStyles.Bold;
                label.alignment        = TextAlignmentOptions.Center;
                EditorUtility.SetDirty(label);
                fixed_++;
            }

            // Also fix the title bar badge and any other labels that might wrap
            var titleBar = GameObject.Find("VideoTitleBar");
            if (titleBar != null)
            {
                var badgeText = FindDeepChild(titleBar.transform, "BadgeText");
                if (badgeText != null)
                {
                    var tmp = badgeText.GetComponent<TextMeshProUGUI>();
                    if (tmp != null)
                    {
                        tmp.textWrappingMode = TextWrappingModes.NoWrap;
                        tmp.fontStyle        = FontStyles.Bold;
                        EditorUtility.SetDirty(tmp);
                    }
                }

                var titleText = FindDeepChild(titleBar.transform, "TitleText");
                if (titleText != null)
                {
                    var tmp = titleText.GetComponent<TextMeshProUGUI>();
                    if (tmp != null)
                    {
                        tmp.textWrappingMode = TextWrappingModes.NoWrap;
                        tmp.overflowMode     = TextOverflowModes.Ellipsis;
                        tmp.fontStyle        = FontStyles.Bold;
                        tmp.fontSize         = 38f;
                        EditorUtility.SetDirty(tmp);
                    }
                }
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            EditorUtility.DisplayDialog(
                "Button Labels Fixed!",
                $"Updated {fixed_} button(s) to plain single-line labels:\n\n" +
                "  < Prev  |  Play  |  Stop  |  Next >\n\n" +
                "Save the scene (Ctrl+S) and press Play.",
                "OK");
        }

        private static Transform FindDeepChild(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == childName) return child;
                var found = FindDeepChild(child, childName);
                if (found != null) return found;
            }
            return null;
        }

        private static GameObject FindByName(string name)
        {
            return GameObject.Find(name);
        }

        private static Button FindButtonByLabel(Button[] buttons, params string[] keywords)
        {
            foreach (var btn in buttons)
            {
                var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp == null) continue;
                string lbl = tmp.text.ToLower();
                if (keywords.Any(k => lbl.Contains(k.ToLower())))
                    return btn;
            }
            // Fallback: search by GameObject name
            foreach (var btn in buttons)
            {
                string n = btn.gameObject.name.ToLower();
                if (keywords.Any(k => n.Contains(k.ToLower())))
                    return btn;
            }
            return null;
        }

        private static void AddBox(Transform parent, string name, Vector3 localPos, Vector3 localScale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;

            var renderer = go.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = color;
            renderer.sharedMaterial = mat;

            var collider = go.GetComponent<BoxCollider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
        }

        private static void BuildSideWindow(Transform parent, string name, Vector3 center, Vector3 glassScale)
        {
            var windowRoot = new GameObject(name);
            windowRoot.transform.SetParent(parent, false);
            windowRoot.transform.localPosition = center;

            AddBox(windowRoot.transform, "Glass", Vector3.zero, glassScale, WindowGlassColor);
            AddBox(windowRoot.transform, "FrameTop",
                new Vector3(0f, 0.72f, 0f), new Vector3(0.08f, 0.07f, 2.45f), WindowFrameColor);
            AddBox(windowRoot.transform, "FrameBottom",
                new Vector3(0f, -0.72f, 0f), new Vector3(0.08f, 0.07f, 2.45f), WindowFrameColor);
            AddBox(windowRoot.transform, "FrameFront",
                new Vector3(0f, 0f, 1.17f), new Vector3(0.08f, 1.45f, 0.07f), WindowFrameColor);
            AddBox(windowRoot.transform, "FrameBack",
                new Vector3(0f, 0f, -1.17f), new Vector3(0.08f, 1.45f, 0.07f), WindowFrameColor);
            AddBox(windowRoot.transform, "FrameMiddle",
                new Vector3(0f, 0f, 0f), new Vector3(0.08f, 1.35f, 0.05f), WindowFrameColor);
        }

        private static void SetObjField(SerializedObject so, string field, Object value)
        {
            var prop = so.FindProperty(field);
            if (prop != null) prop.objectReferenceValue = value;
        }
    }
}
#endif
