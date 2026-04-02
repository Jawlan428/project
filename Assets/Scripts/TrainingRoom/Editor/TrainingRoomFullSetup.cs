#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;
using VRRecordings;

namespace TrainingRoom.Editor
{
    /// <summary>
    /// Full one-click Training Room setup.
    /// Menu: Tools → Training Room → ★ Full Setup (Run This First)
    ///
    /// Creates in one shot:
    ///   • Room shell  (floor, ceiling, 4 walls, screen wall trim)
    ///   • Cinema seats (3 rows of 4 — simple primitives)
    ///   • Big projection screen (Quad + Unlit material)
    ///   • 360-video sphere (inverted normals, hidden by default)
    ///   • VideoPlayer + VRVideoScreenPlayer + AudioSource
    ///   • Room lighting (ambient + 2 fill spots + screen glow point)
    ///   • World-space Controls Bar (Play/Pause/Stop/Prev/Next + progress)
    ///   • World-space Playlist Panel (scroll list of video rows)
    ///   • TrainingRoomManager — every field auto-wired
    ///   • 4 sample TrainingVideoEntry ScriptableObject assets
    ///   • PlaylistRow prefab saved to Assets/TrainingRoom/Prefabs/
    ///   • TrainingRoomTabletPage added & ready for tablet integration
    /// </summary>
    public static class TrainingRoomFullSetup
    {
        // ─── folder constants ─────────────────────────────────────────────────
        private const string ROOT_FOLDER      = "Assets/TrainingRoom";
        private const string PREFABS_FOLDER   = "Assets/TrainingRoom/Prefabs";
        private const string ENTRIES_FOLDER   = "Assets/TrainingRoom/VideoEntries";
        private const string STREAMING_FOLDER = "Assets/StreamingAssets/TrainingVideos";

        // ─── colors ───────────────────────────────────────────────────────────
        private static readonly Color WallColor    = new Color(0.22f, 0.22f, 0.24f);
        private static readonly Color FloorColor   = new Color(0.12f, 0.12f, 0.14f);
        private static readonly Color CeilingColor = new Color(0.18f, 0.18f, 0.20f);
        private static readonly Color SeatColor    = new Color(0.18f, 0.28f, 0.18f);
        private static readonly Color DoorColor    = new Color(0.28f, 0.20f, 0.12f);
        private static readonly Color WindowFrameColor = new Color(0.10f, 0.10f, 0.12f);
        private static readonly Color WindowGlassColor = new Color(0.40f, 0.65f, 0.75f, 0.22f);
        private static readonly Color BtnGreen     = new Color(0.20f, 0.55f, 0.25f);
        private static readonly Color BtnDark      = new Color(0.10f, 0.10f, 0.12f, 0.95f);
        private static readonly Color PanelBg      = new Color(0.06f, 0.06f, 0.08f, 0.96f);
        private static readonly Color RowNormal    = new Color(0.13f, 0.13f, 0.16f);
        private static readonly Color RowHighlight = new Color(0.15f, 0.42f, 0.20f);
        private static readonly Color TextWhite    = new Color(0.95f, 0.95f, 0.95f);
        private static readonly Color TextGrey     = new Color(0.65f, 0.65f, 0.65f);
        private static readonly Color AccentGreen  = new Color(0.30f, 0.75f, 0.35f);

        // ─── menu entry ──────────────────────────────────────────────────────
        [MenuItem("Tools/Training Room/★ Full Setup (Run This First)", priority = 1)]
        public static void RunFullSetup()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Training Room — Full Setup",
                "This will build the complete Training Room in the current scene:\n\n" +
                "  • Room shell (walls, floor, ceiling)\n" +
                "  • Projection screen + VideoPlayer\n" +
                "  • Cinema seats  (3 rows × 4)\n" +
                "  • 360-video sphere\n" +
                "  • World-space Controls Bar\n" +
                "  • World-space Playlist Panel\n" +
                "  • Teleport entry button\n" +
                "  • Room lighting (3 lights)\n" +
                "  • 4 sample video entries\n" +
                "  • PlaylistRow prefab\n" +
                "  • TrainingRoomManager — all fields wired\n\n" +
                "Position the result using the TrainingRoom_Root transform.",
                "Build It!", "Cancel");

            if (!confirmed) return;

            Undo.SetCurrentGroupName("Training Room Full Setup");
            int undoGroup = Undo.GetCurrentGroup();

            EnsureFolders();

            // ── build scene objects ──────────────────────────────────────────
            var root          = new GameObject("TrainingRoom_Root");
            Undo.RegisterCreatedObjectUndo(root, "TR Root");

            var roomShell     = BuildRoomShell(root.transform);
            var screenObj     = BuildProjectionScreen(root.transform);
            var sphere360     = Build360Sphere(root.transform);
            var playerGO      = BuildVideoPlayer(root.transform, screenObj);
            var lights        = BuildRoomLights(root.transform);
            var controlCanvas = BuildControlsCanvas(root.transform);
            var playlistPanel = BuildPlaylistPanel(root.transform);
            var subtitleText  = BuildSubtitleText(root.transform);
            var tabletPage    = BuildTabletPageObject(root.transform);
            BuildTeleportEntryUI(root.transform);

            // ── sample video entries ─────────────────────────────────────────
            var entries = CreateSampleVideoEntries();

            // ── playlist row prefab ──────────────────────────────────────────
            var rowPrefab = CreatePlaylistRowPrefab();

            // ── wire TrainingRoomManager ─────────────────────────────────────
            var manager = WireManager(root, playerGO, screenObj, sphere360,
                                      lights, subtitleText, entries);

            // ── wire TrainingRoomTabletPage ──────────────────────────────────
            WireTabletPage(tabletPage, manager, playlistPanel, controlCanvas,
                           rowPrefab, entries);

            Undo.CollapseUndoOperations(undoGroup);

            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);

            EditorUtility.DisplayDialog(
                "Training Room — Setup Complete!",
                "Everything is ready.\n\n" +
                "NEXT STEPS:\n" +
                "1. Drop real MP4 files into:\n" +
                "   Assets/StreamingAssets/TrainingVideos/\n\n" +
                "2. Open each VideoEntry in:\n" +
                "   Assets/TrainingRoom/VideoEntries/\n" +
                "   and set the correct MP4 filename.\n\n" +
                "3. In your Smart Tablet (TabletAppController):\n" +
                "   • Add a 'Training' tab button\n" +
                "   • Assign TrainingPage as the page target\n\n" +
                "4. (Optional) Add NetworkObject + VideoPlaybackNetworkSync\n" +
                "   to TrainingRoom_Root for multiplayer sync.\n\n" +
                "Tip: Use 'Enter Training Room' teleport button to jump inside quickly.",
                "Got it!");
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  FOLDER CREATION
        // ═══════════════════════════════════════════════════════════════════════

        private static void EnsureFolders()
        {
            EnsureAssetFolder(ROOT_FOLDER);
            EnsureAssetFolder(PREFABS_FOLDER);
            EnsureAssetFolder(ENTRIES_FOLDER);

            // StreamingAssets lives on disk
            string sPath = Path.Combine(Application.dataPath, "StreamingAssets", "TrainingVideos");
            Directory.CreateDirectory(sPath);
            AssetDatabase.Refresh();
        }

        private static void EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf   = Path.GetFileName(path);
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  ROOM SHELL
        // ═══════════════════════════════════════════════════════════════════════

        private static GameObject BuildRoomShell(Transform parent)
        {
            var shell = new GameObject("RoomShell");
            shell.transform.SetParent(parent, false);

            // Room dimensions: 12 m wide × 8 m deep × 4 m tall
            const float W = 12f, D = 8f, H = 4f;

            // Floor
            AddBox(shell.transform, "Floor",
                new Vector3(0, 0, 0), new Vector3(W, 0.1f, D), FloorColor);

            // Ceiling
            AddBox(shell.transform, "Ceiling",
                new Vector3(0, H, 0), new Vector3(W, 0.1f, D), CeilingColor);

            // Back wall (behind seats)
            AddBox(shell.transform, "WallBack",
                new Vector3(0, H * 0.5f, -D * 0.5f), new Vector3(W, H, 0.15f), WallColor);

            // Front wall (screen wall)
            AddBox(shell.transform, "WallFront",
                new Vector3(0, H * 0.5f, D * 0.5f), new Vector3(W, H, 0.15f), WallColor);

            // Left wall
            AddBox(shell.transform, "WallLeft",
                new Vector3(-W * 0.5f, H * 0.5f, 0), new Vector3(0.15f, H, D), WallColor);

            // Right wall
            AddBox(shell.transform, "WallRight",
                new Vector3(W * 0.5f, H * 0.5f, 0), new Vector3(0.15f, H, D), WallColor);

            // Screen trim / frame (dark border around screen area on front wall)
            AddBox(shell.transform, "ScreenFrameTop",
                new Vector3(0, 3.45f, D * 0.5f - 0.05f), new Vector3(5.6f, 0.15f, 0.08f), new Color(0.05f, 0.05f, 0.05f));
            AddBox(shell.transform, "ScreenFrameBottom",
                new Vector3(0, 1.15f, D * 0.5f - 0.05f), new Vector3(5.6f, 0.15f, 0.08f), new Color(0.05f, 0.05f, 0.05f));
            AddBox(shell.transform, "ScreenFrameLeft",
                new Vector3(-2.8f, 2.3f, D * 0.5f - 0.05f), new Vector3(0.15f, 2.45f, 0.08f), new Color(0.05f, 0.05f, 0.05f));
            AddBox(shell.transform, "ScreenFrameRight",
                new Vector3( 2.8f, 2.3f, D * 0.5f - 0.05f), new Vector3(0.15f, 2.45f, 0.08f), new Color(0.05f, 0.05f, 0.05f));

            // Cinema seats — 3 rows × 4 columns
            BuildSeats(shell.transform);
            BuildDoorAndWindows(shell.transform);

            return shell;
        }

        private static void BuildSeats(Transform parent)
        {
            var seatParent = new GameObject("Seats");
            seatParent.transform.SetParent(parent, false);

            int cols = 4;
            int rows = 3;
            float colSpacing = 1.4f;
            float rowSpacing = 1.3f;
            float startX     = -(cols - 1) * colSpacing * 0.5f;
            float startZ     = -2.0f;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    float x = startX + c * colSpacing;
                    float z = startZ - r * rowSpacing;
                    float raise = r * 0.18f; // tiered rows rise slightly

                    var seat = new GameObject($"Seat_R{r}C{c}");
                    seat.transform.SetParent(seatParent.transform, false);
                    seat.transform.localPosition = new Vector3(x, raise, z);

                    // Seat cushion
                    AddBox(seat.transform, "Cushion",
                        new Vector3(0, 0.26f, 0), new Vector3(0.55f, 0.10f, 0.52f), SeatColor);

                    // Backrest
                    AddBox(seat.transform, "Back",
                        new Vector3(0, 0.65f, -0.22f), new Vector3(0.55f, 0.70f, 0.08f), SeatColor * 0.85f);

                    // Legs
                    AddBox(seat.transform, "Legs",
                        new Vector3(0, 0.10f, 0), new Vector3(0.52f, 0.20f, 0.10f),
                        new Color(0.08f, 0.08f, 0.08f));
                }
            }
        }

        private static void BuildDoorAndWindows(Transform parent)
        {
            var architectural = new GameObject("ArchitecturalDetails");
            architectural.transform.SetParent(parent, false);

            // Door on the back wall, slightly right of center.
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

            // Windows on the left and right walls.
            BuildSideWindow(architectural.transform, "WindowLeft_01",
                new Vector3(-5.93f, 2.15f, -1.8f), new Vector3(0.06f, 1.4f, 2.3f));
            BuildSideWindow(architectural.transform, "WindowLeft_02",
                new Vector3(-5.93f, 2.15f, 1.7f), new Vector3(0.06f, 1.4f, 2.3f));
            BuildSideWindow(architectural.transform, "WindowRight_01",
                new Vector3(5.93f, 2.15f, -1.8f), new Vector3(0.06f, 1.4f, 2.3f));
            BuildSideWindow(architectural.transform, "WindowRight_02",
                new Vector3(5.93f, 2.15f, 1.7f), new Vector3(0.06f, 1.4f, 2.3f));
        }

        private static void BuildSideWindow(Transform parent, string name, Vector3 center, Vector3 glassScale)
        {
            var windowRoot = new GameObject(name);
            windowRoot.transform.SetParent(parent, false);
            windowRoot.transform.localPosition = center;

            // Glass pane.
            AddBox(windowRoot.transform, "Glass",
                Vector3.zero, glassScale, WindowGlassColor);

            // Frame pieces.
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

        // ═══════════════════════════════════════════════════════════════════════
        //  PROJECTION SCREEN
        // ═══════════════════════════════════════════════════════════════════════

        private static GameObject BuildProjectionScreen(Transform parent)
        {
            // Screen sits on the front wall, centered, 16:9 aspect ratio
            var screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
            screen.name = "TrainingScreen";
            screen.transform.SetParent(parent, false);
            screen.transform.localPosition = new Vector3(0f, 2.3f, 3.92f);
            screen.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // face inward
            screen.transform.localScale    = new Vector3(5.33f, 3.0f, 1f);  // 16:9

            Shader screenShader = Shader.Find("Universal Render Pipeline/Unlit")
                                ?? Shader.Find("Unlit/Texture");
            var mat = new Material(screenShader);
            mat.name  = "TrainingScreen_VideoMat";
            // Start with black so screen looks off before video plays
            if (mat.HasProperty("_BaseColor"))  mat.SetColor("_BaseColor", Color.black);
            if (mat.HasProperty("_Color"))       mat.SetColor("_Color",     Color.black);
            screen.GetComponent<MeshRenderer>().sharedMaterial = mat;
            AssetDatabase.CreateAsset(mat, $"{PREFABS_FOLDER}/TrainingScreen_VideoMat.mat");

            // Remove collider
            UnityEngine.Object.DestroyImmediate(screen.GetComponent<MeshCollider>());

            return screen;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  360 SPHERE
        // ═══════════════════════════════════════════════════════════════════════

        private static GameObject Build360Sphere(Transform parent)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "VideoSphere360";
            sphere.transform.SetParent(parent, false);
            sphere.transform.localPosition = Vector3.zero;
            sphere.transform.localScale    = Vector3.one * 50f; // large enough to surround player

            // Flip normals via negative scale on X so inside is visible
            sphere.transform.localScale = new Vector3(-50f, 50f, 50f);

            Shader sphereShader = Shader.Find("Universal Render Pipeline/Unlit")
                                ?? Shader.Find("Unlit/Texture");
            var mat360 = new Material(sphereShader);
            mat360.name = "VideoSphere360_Mat";
            sphere.GetComponent<MeshRenderer>().sharedMaterial = mat360;
            AssetDatabase.CreateAsset(mat360, $"{PREFABS_FOLDER}/VideoSphere360_Mat.mat");

            UnityEngine.Object.DestroyImmediate(sphere.GetComponent<SphereCollider>());

            sphere.SetActive(false); // Hidden until a 360 video is selected

            return sphere;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  VIDEO PLAYER
        // ═══════════════════════════════════════════════════════════════════════

        private static GameObject BuildVideoPlayer(Transform parent, GameObject screenObj)
        {
            var go = new GameObject("VideoPlayer_Root");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 3.5f, 3f); // "projector" position near ceiling

            // Decorative projector body
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "ProjectorBody";
            body.transform.SetParent(go.transform, false);
            body.transform.localScale    = new Vector3(0.4f, 0.15f, 0.6f);
            SetPrimitiveColor(body, new Color(0.1f, 0.1f, 0.1f));
            UnityEngine.Object.DestroyImmediate(body.GetComponent<BoxCollider>());

            // VideoPlayer component
            var vp = go.AddComponent<VideoPlayer>();
            vp.playOnAwake     = false;
            vp.renderMode      = VideoRenderMode.RenderTexture;
            vp.audioOutputMode = VideoAudioOutputMode.AudioSource;
            vp.isLooping       = false;

            // AudioSource — 3D spatial
            var audio = go.AddComponent<AudioSource>();
            audio.spatialBlend = 1f;
            audio.rolloffMode  = AudioRolloffMode.Linear;
            audio.minDistance  = 1f;
            audio.maxDistance  = 18f;
            audio.volume       = 0.9f;
            audio.playOnAwake  = false;

            // VRVideoScreenPlayer
            var player = go.AddComponent<VRVideoScreenPlayer>();
            SetField(player, "videoDisplayRenderer", screenObj.GetComponent<MeshRenderer>());
            SetField(player, "audioSource",          audio);
            SetField(player, "renderTextureWidth",   1920);
            SetField(player, "renderTextureHeight",  1080);
            SetField(player, "useSpatialAudio",      true);
            SetField(player, "defaultVolume",        0.9f);

            return go;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  ROOM LIGHTS
        // ═══════════════════════════════════════════════════════════════════════

        private static Light[] BuildRoomLights(Transform parent)
        {
            var lightParent = new GameObject("RoomLights");
            lightParent.transform.SetParent(parent, false);

            // Ambient fill — overhead center
            var fill = AddLight(lightParent.transform, "Light_FillCenter",
                new Vector3(0, 3.8f, 0), LightType.Point,
                new Color(0.9f, 0.92f, 0.85f), 1.2f, 14f);

            // Left spot
            var left = AddLight(lightParent.transform, "Light_SpotLeft",
                new Vector3(-4f, 3.7f, -1f), LightType.Spot,
                new Color(0.85f, 0.88f, 0.80f), 0.8f, 12f);
            left.transform.localRotation = Quaternion.Euler(70f, 30f, 0f);
            left.spotAngle = 55f;

            // Right spot
            var right = AddLight(lightParent.transform, "Light_SpotRight",
                new Vector3(4f, 3.7f, -1f), LightType.Spot,
                new Color(0.85f, 0.88f, 0.80f), 0.8f, 12f);
            right.transform.localRotation = Quaternion.Euler(70f, -30f, 0f);
            right.spotAngle = 55f;

            // Screen glow — subtle blue-white point in front of screen
            var glow = AddLight(lightParent.transform, "Light_ScreenGlow",
                new Vector3(0f, 2.3f, 3.0f), LightType.Point,
                new Color(0.70f, 0.80f, 1.0f), 0.5f, 5f);

            return new Light[] { fill, left, right, glow };
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  CONTROLS CANVAS  (Play / Pause / Stop / Prev / Next + progress bar)
        // ═══════════════════════════════════════════════════════════════════════

        private static GameObject BuildControlsCanvas(Transform parent)
        {
            var root = CreateWorldCanvas("ControlsCanvas", parent,
                new Vector3(0f, 0.75f, 3.88f),
                new Vector2(1100f, 140f), 0.0035f,
                Quaternion.Euler(0f, 180f, 0f));

            // Background
            AddPanelBg(root.transform, PanelBg, Vector2.zero, Vector2.one, Vector2.zero);

            // Button row
            var row = AddHLayoutGroup("ButtonRow", root.transform,
                new Vector2(0.03f, 0.12f), new Vector2(0.97f, 0.88f), 18f);

            var btnPrev      = MakeButton(row, "PrevButton",      "◀◀ Prev",  BtnDark);
            var btnPlayPause = MakeButton(row, "PlayPauseButton", "▶  Play",  BtnGreen);
            var btnStop      = MakeButton(row, "StopButton",      "⏹ Stop",   new Color(0.7f, 0.18f, 0.18f));
            var btnNext      = MakeButton(row, "NextButton",      "Next ▶▶",  BtnDark);

            // Decorate play/pause button slightly larger weight
            var ppRT = btnPlayPause.GetComponent<RectTransform>();
            var le   = btnPlayPause.AddComponent<LayoutElement>();
            le.flexibleWidth = 2f;

            // Progress bar row
            var progressBar = AddProgressBar(root.transform);

            // Now-playing label
            var nowLabel = AddLabel(root.transform, "NowPlayingLabel",
                "No video selected",
                new Vector2(0.02f, 0.88f), new Vector2(0.98f, 1.0f),
                18f, AccentGreen, TextAlignmentOptions.Left);

            return root;
        }

        private static GameObject AddProgressBar(Transform parent)
        {
            var go = new GameObject("ProgressBar");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.02f, 0.0f);
            rt.anchorMax = new Vector2(0.98f, 0.10f);
            rt.sizeDelta = Vector2.zero;
            rt.offsetMin = new Vector2(0, 4);
            rt.offsetMax = new Vector2(0, -4);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.2f, 0.22f);

            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(go.transform, false);
            var fillRT = fillGO.AddComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = new Vector2(0f, 1f); // width driven at runtime
            fillRT.sizeDelta = Vector2.zero;
            var fill = fillGO.AddComponent<Image>();
            fill.color = AccentGreen;

            // Slider on top
            var sliderGO = new GameObject("Slider");
            sliderGO.transform.SetParent(go.transform, false);
            var slider = sliderGO.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value    = 0f;
            var sliderRT = sliderGO.GetComponent<RectTransform>();
            sliderRT.anchorMin = Vector2.zero;
            sliderRT.anchorMax = Vector2.one;
            sliderRT.sizeDelta = Vector2.zero;

            // Transparent background for slider hit area
            var sliderBg = sliderGO.AddComponent<Image>();
            sliderBg.color = Color.clear;
            slider.targetGraphic = sliderBg;

            slider.fillRect  = fillRT;
            slider.direction = Slider.Direction.LeftToRight;

            return go;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  PLAYLIST PANEL  (world-space scroll list on the right wall)
        // ═══════════════════════════════════════════════════════════════════════

        private static GameObject BuildPlaylistPanel(Transform parent)
        {
            var root = CreateWorldCanvas("PlaylistPanel", parent,
                new Vector3(4.8f, 2.3f, 0.5f),
                new Vector2(460f, 680f), 0.004f,
                Quaternion.Euler(0f, -90f, 0f));

            // Panel background
            AddPanelBg(root.transform, PanelBg, Vector2.zero, Vector2.one, Vector2.zero);

            // Header
            AddLabel(root.transform, "HeaderLabel", "Agricultural Training Videos",
                new Vector2(0.04f, 0.91f), new Vector2(0.96f, 1.0f),
                20f, AccentGreen, TextAlignmentOptions.Center);

            // Divider
            var divider = new GameObject("Divider");
            divider.transform.SetParent(root.transform, false);
            var divRT = divider.AddComponent<RectTransform>();
            divRT.anchorMin  = new Vector2(0.02f, 0.90f);
            divRT.anchorMax  = new Vector2(0.98f, 0.905f);
            divRT.sizeDelta  = Vector2.zero;
            var divImg = divider.AddComponent<Image>();
            divImg.color = AccentGreen * 0.6f;

            // Scroll view
            var scrollView = BuildScrollView("VideoListScroll", root.transform,
                new Vector2(0.01f, 0.01f), new Vector2(0.99f, 0.89f));

            return root;
        }

        private static GameObject BuildScrollView(string name, Transform parent,
                                                   Vector2 anchorMin, Vector2 anchorMax)
        {
            // Scroll view root
            var svGO = new GameObject(name);
            svGO.transform.SetParent(parent, false);
            var svRT = svGO.AddComponent<RectTransform>();
            svRT.anchorMin = anchorMin;
            svRT.anchorMax = anchorMax;
            svRT.sizeDelta = Vector2.zero;
            var svImg = svGO.AddComponent<Image>();
            svImg.color = Color.clear;

            var scrollRect = svGO.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical   = true;

            // Viewport
            var vpGO = new GameObject("Viewport");
            vpGO.transform.SetParent(svGO.transform, false);
            var vpRT = vpGO.AddComponent<RectTransform>();
            vpRT.anchorMin = Vector2.zero;
            vpRT.anchorMax = Vector2.one;
            vpRT.sizeDelta = Vector2.zero;
            var vpImg = vpGO.AddComponent<Image>();
            vpImg.color = Color.clear;
            var mask = vpGO.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            // Content
            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(vpGO.transform, false);
            var contentRT = contentGO.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot     = new Vector2(0.5f, 1f);
            contentRT.sizeDelta = new Vector2(0, 0);

            var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
            vlg.spacing             = 6f;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(6, 6, 6, 6);

            var csf = contentGO.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content  = contentRT;
            scrollRect.viewport = vpRT;

            return contentGO; // Return content so we can add rows to it
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  SUBTITLE TEXT
        // ═══════════════════════════════════════════════════════════════════════

        private static GameObject BuildSubtitleText(Transform parent)
        {
            var go = new GameObject("SubtitleText");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 1.15f, 3.85f);
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            go.transform.localScale    = Vector3.one * 0.005f;

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text               = "";
            tmp.fontSize          = 52f;
            tmp.alignment         = TextAlignmentOptions.Center;
            tmp.color             = Color.white;
            tmp.fontStyle         = FontStyles.Bold;
            tmp.textWrappingMode  = TextWrappingModes.Normal;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1000, 180);

            // Subtle backdrop
            var bgGO = new GameObject("SubtitleBG");
            bgGO.transform.SetParent(go.transform, false);
            var bgMesh = bgGO.AddComponent<MeshFilter>();
            var bgRenderer = bgGO.AddComponent<MeshRenderer>();
            var bgRT = bgGO.AddComponent<RectTransform>();
            bgRT.sizeDelta = new Vector2(1040, 200);

            go.SetActive(false);
            return go;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  TABLET PAGE OBJECT
        // ═══════════════════════════════════════════════════════════════════════

        private static GameObject BuildTabletPageObject(Transform parent)
        {
            // Standalone GameObject — user will drag this into their tablet hierarchy
            var go = new GameObject("TrainingPage_ForTablet");
            go.transform.SetParent(parent, false);
            go.AddComponent<TrainingRoomTabletPage>();
            go.SetActive(false); // hidden until wired into tablet
            return go;
        }

        private static GameObject BuildTeleportEntryUI(Transform parent)
        {
            var destination = new GameObject("TrainingRoomTeleportDestination");
            destination.transform.SetParent(parent, false);
            destination.transform.localPosition = new Vector3(0f, 0.15f, -1.2f);
            destination.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);

            var uiRoot = CreateWorldCanvas("TrainingRoomTeleportUI", parent,
                new Vector3(0f, 1.45f, -4.35f),
                new Vector2(520f, 140f), 0.004f,
                Quaternion.Euler(0f, 180f, 0f));

            AddPanelBg(uiRoot.transform, new Color(0.06f, 0.06f, 0.08f, 0.94f),
                Vector2.zero, Vector2.one, Vector2.zero);

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
            SetField(teleporter, "destination", destination.transform);
            SetField(teleporter, "triggerButton", button);

            return uiRoot;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  SAMPLE VIDEO ENTRIES
        // ═══════════════════════════════════════════════════════════════════════

        private static TrainingVideoEntry[] CreateSampleVideoEntries()
        {
            var data = new (string title, string desc, VideoCategoryType cat, string file)[]
            {
                ("Harvest Techniques",
                 "Learn modern crop harvesting methods for wheat, rice, and vegetables.",
                 VideoCategoryType.HarvestTechniques,
                 "harvest_techniques.mp4"),

                ("Smart Irrigation Methods",
                 "Drip irrigation, sprinkler systems, and soil-moisture-based scheduling.",
                 VideoCategoryType.IrrigationMethods,
                 "irrigation_methods.mp4"),

                ("Pest & Disease Detection",
                 "Identify common crop pests, fungal diseases, and prevention strategies.",
                 VideoCategoryType.PestDetection,
                 "pest_detection.mp4"),

                ("Farm Equipment Usage",
                 "Safe operation of tractors, ploughs, seed drills, and combine harvesters.",
                 VideoCategoryType.FarmEquipment,
                 "farm_equipment.mp4"),
            };

            var entries = new TrainingVideoEntry[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                var (title, desc, cat, file) = data[i];
                var entry = ScriptableObject.CreateInstance<TrainingVideoEntry>();
                entry.title                   = title;
                entry.description             = desc;
                entry.category                = cat;
                entry.streamingAssetsFileName = file;
                entry.durationLabel           = "";
                entry.language                = "en";

                string safeName = title.Replace(" ", "").Replace("&", "And");
                string path     = AssetDatabase.GenerateUniqueAssetPath(
                                    $"{ENTRIES_FOLDER}/{safeName}.asset");
                AssetDatabase.CreateAsset(entry, path);
                entries[i] = entry;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[TrainingRoom] Created {entries.Length} sample video entries in {ENTRIES_FOLDER}");
            return entries;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  PLAYLIST ROW PREFAB
        // ═══════════════════════════════════════════════════════════════════════

        private static GameObject CreatePlaylistRowPrefab()
        {
            var rowGO = new GameObject("PlaylistRow");

            // Background image (the whole row is clickable)
            var rowImage = rowGO.AddComponent<Image>();
            rowImage.color = RowNormal;

            var rowRT = rowGO.GetComponent<RectTransform>();
            rowRT.sizeDelta = new Vector2(0, 82f);

            var rowLE = rowGO.AddComponent<LayoutElement>();
            rowLE.minHeight      = 82f;
            rowLE.preferredHeight = 82f;

            // Full-row button
            var rowBtn = rowGO.AddComponent<Button>();
            rowBtn.targetGraphic = rowImage;
            var cb = rowBtn.colors;
            cb.normalColor      = RowNormal;
            cb.highlightedColor = new Color(0.20f, 0.35f, 0.22f);
            cb.pressedColor     = RowHighlight;
            rowBtn.colors = cb;

            // Highlight border (green left strip)
            var hlGO = new GameObject("HighlightBorder");
            hlGO.transform.SetParent(rowGO.transform, false);
            var hlImg = hlGO.AddComponent<Image>();
            hlImg.color = AccentGreen;
            var hlRT = hlGO.GetComponent<RectTransform>();
            hlRT.anchorMin = new Vector2(0,   0);
            hlRT.anchorMax = new Vector2(0,   1);
            hlRT.sizeDelta = new Vector2(5f,  0);
            hlRT.pivot     = new Vector2(0, 0.5f);
            hlGO.SetActive(false);

            // Category tag (small colored box on left)
            var tagGO = new GameObject("CategoryTag");
            tagGO.transform.SetParent(rowGO.transform, false);
            var tagImg = tagGO.AddComponent<Image>();
            tagImg.color = new Color(0.22f, 0.55f, 0.27f);
            var tagRT = tagGO.GetComponent<RectTransform>();
            tagRT.anchorMin = new Vector2(0.03f, 0.18f);
            tagRT.anchorMax = new Vector2(0.03f, 0.18f);
            tagRT.sizeDelta = new Vector2(72f, 22f);
            tagRT.pivot = new Vector2(0, 0);

            var tagTxt = AddTMPChild(tagGO.transform, "CategoryText",
                "General", 13f, AccentGreen, TextAlignmentOptions.Center);
            var tagTxtRT = tagTxt.GetComponent<RectTransform>();
            tagTxtRT.anchorMin = Vector2.zero;
            tagTxtRT.anchorMax = Vector2.one;
            tagTxtRT.sizeDelta = Vector2.zero;

            // Title text
            var titleTxt = AddTMPChild(rowGO.transform, "TitleText",
                "Video Title", 17f, TextWhite, TextAlignmentOptions.Left);
            var titleRT = titleTxt.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.06f, 0.52f);
            titleRT.anchorMax = new Vector2(0.82f, 0.95f);
            titleRT.sizeDelta = Vector2.zero;
            var titleComp = titleTxt.GetComponent<TextMeshProUGUI>();
            titleComp.fontStyle = FontStyles.Bold;

            // Duration text (right side)
            var durTxt = AddTMPChild(rowGO.transform, "DurationText",
                "0:00", 14f, TextGrey, TextAlignmentOptions.Right);
            var durRT = durTxt.GetComponent<RectTransform>();
            durRT.anchorMin = new Vector2(0.82f, 0.52f);
            durRT.anchorMax = new Vector2(0.98f, 0.95f);
            durRT.sizeDelta = Vector2.zero;

            // Description text
            var descTxt = AddTMPChild(rowGO.transform, "DescriptionText",
                "Description here...", 12f, TextGrey, TextAlignmentOptions.Left);
            var descRT = descTxt.GetComponent<RectTransform>();
            descRT.anchorMin = new Vector2(0.06f, 0.05f);
            descRT.anchorMax = new Vector2(0.98f, 0.50f);
            descRT.sizeDelta = Vector2.zero;

            // PlaylistRowUI component
            var rowUI = rowGO.AddComponent<PlaylistRowUI>();
            SetField(rowUI, "titleText",      titleTxt.GetComponent<TextMeshProUGUI>());
            SetField(rowUI, "categoryText",   tagTxt.GetComponent<TextMeshProUGUI>());
            SetField(rowUI, "durationText",   durTxt.GetComponent<TextMeshProUGUI>());
            SetField(rowUI, "selectButton",   rowBtn);
            SetField(rowUI, "highlightBorder",hlGO);

            // Save as prefab
            string prefabPath = $"{PREFABS_FOLDER}/PlaylistRow.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(rowGO, prefabPath);
            UnityEngine.Object.DestroyImmediate(rowGO);

            Debug.Log($"[TrainingRoom] PlaylistRow prefab saved to {prefabPath}");
            return prefab;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  WIRE TrainingRoomManager
        // ═══════════════════════════════════════════════════════════════════════

        private static TrainingRoomManager WireManager(
            GameObject root,
            GameObject playerGO,
            GameObject screenObj,
            GameObject sphere360,
            Light[] lights,
            GameObject subtitleText,
            TrainingVideoEntry[] entries)
        {
            var manager = root.AddComponent<TrainingRoomManager>();

            // Flat screen player
            SetField(manager, "flatScreenPlayer", playerGO.GetComponent<VRVideoScreenPlayer>());
            SetField(manager, "flatScreenMesh",   screenObj.GetComponent<MeshRenderer>());

            // 360 sphere
            SetField(manager, "sphere360Renderer", sphere360.GetComponent<MeshRenderer>());
            SetField(manager, "sphere360Material", sphere360.GetComponent<MeshRenderer>().sharedMaterial);

            // Lights
            SetField(manager, "roomLights",     lights);
            SetField(manager, "dimmedIntensity", 0.12f);

            // Subtitle
            SetField(manager, "subtitleText", subtitleText.GetComponent<TextMeshPro>());

            // Video library via SerializedObject list
            var so   = new SerializedObject(manager);
            var list = so.FindProperty("videoLibrary");
            list.ClearArray();
            for (int i = 0; i < entries.Length; i++)
            {
                list.InsertArrayElementAtIndex(i);
                list.GetArrayElementAtIndex(i).objectReferenceValue = entries[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            return manager;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  WIRE TrainingRoomTabletPage
        // ═══════════════════════════════════════════════════════════════════════

        private static void WireTabletPage(
            GameObject pageGO,
            TrainingRoomManager manager,
            GameObject playlistPanel,
            GameObject controlsCanvas,
            GameObject rowPrefab,
            TrainingVideoEntry[] entries)
        {
            var page = pageGO.GetComponent<TrainingRoomTabletPage>();
            if (page == null) page = pageGO.AddComponent<TrainingRoomTabletPage>();

            SetField(page, "trainingRoomManager", manager);

            // Playlist content = the Content transform inside the scroll view
            var content = playlistPanel.transform.Find("Viewport/Content")
                       ?? FindDeepChild(playlistPanel.transform, "Content");
            if (content != null)
                SetField(page, "playlistContent", content);

            // Row prefab
            SetField(page, "playlistRowPrefab", rowPrefab);

            // Controls — find buttons by name in the controls canvas
            var pp   = FindDeepChild(controlsCanvas.transform, "PlayPauseButton");
            var stop = FindDeepChild(controlsCanvas.transform, "StopButton");
            var prev = FindDeepChild(controlsCanvas.transform, "PrevButton");
            var next = FindDeepChild(controlsCanvas.transform, "NextButton");

            if (pp   != null) SetField(page, "playPauseButton", pp.GetComponent<Button>());
            if (stop != null) SetField(page, "stopButton",      stop.GetComponent<Button>());
            if (prev != null) SetField(page, "prevButton",      prev.GetComponent<Button>());
            if (next != null) SetField(page, "nextButton",      next.GetComponent<Button>());

            // Wire the screen player's own controls from the controls canvas
            var vrPlayer = manager.GetComponent<VRVideoScreenPlayer>()
                        ?? manager.GetComponentInChildren<VRVideoScreenPlayer>();
            if (vrPlayer != null)
            {
                if (pp   != null) SetField(vrPlayer, "playPauseButton", pp.GetComponent<Button>());
                if (stop != null) SetField(vrPlayer, "stopButton",      stop.GetComponent<Button>());

                var slider = FindDeepChild(controlsCanvas.transform, "Slider");
                if (slider != null) SetField(vrPlayer, "progressSlider", slider.GetComponent<Slider>());

                var nowLabel = FindDeepChild(controlsCanvas.transform, "NowPlayingLabel");
                if (nowLabel != null) SetField(vrPlayer, "titleText", nowLabel.GetComponent<TextMeshProUGUI>());
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  HELPERS — primitives, UI, lights
        // ═══════════════════════════════════════════════════════════════════════

        private static void AddBox(Transform parent, string name, Vector3 pos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale    = scale;
            SetPrimitiveColor(go, color);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<BoxCollider>());
        }

        private static void SetPrimitiveColor(GameObject go, Color color)
        {
            var mr  = go.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                               ?? Shader.Find("Standard"));
            mat.color = color;
            mr.sharedMaterial = mat;
        }

        private static Light AddLight(Transform parent, string name, Vector3 pos,
                                       LightType type, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type      = type;
            l.color     = color;
            l.intensity = intensity;
            l.range     = range;
            return l;
        }

        private static GameObject CreateWorldCanvas(string name, Transform parent,
            Vector3 localPos, Vector2 sizeDelta, float scale, Quaternion localRot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale    = Vector3.one * scale;

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var cs = go.AddComponent<CanvasScaler>();
            cs.dynamicPixelsPerUnit = 100f;

            go.AddComponent<GraphicRaycaster>();

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = sizeDelta;

            return go;
        }

        private static void AddPanelBg(Transform parent, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta)
        {
            var go = new GameObject("Background");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.sizeDelta = sizeDelta;
        }

        private static GameObject AddHLayoutGroup(string name, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax, float spacing)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.sizeDelta = Vector2.zero;
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing             = spacing;
            hlg.childForceExpandWidth  = true;
            hlg.childForceExpandHeight = true;
            hlg.padding = new RectOffset(10, 10, 6, 6);
            return go;
        }

        private static GameObject MakeButton(GameObject parent, string name,
                                              string label, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);

            var img = go.AddComponent<Image>();
            img.color = color;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var cb = btn.colors;
            cb.normalColor      = color;
            cb.highlightedColor = color * 1.25f;
            cb.pressedColor     = color * 0.75f;
            cb.colorMultiplier  = 1f;
            btn.colors = cb;

            var txtGO = new GameObject("Label");
            txtGO.transform.SetParent(go.transform, false);
            var tmp = txtGO.AddComponent<TextMeshProUGUI>();
            tmp.text      = label;
            tmp.fontSize  = 28f;
            tmp.color     = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            var tRT = txtGO.GetComponent<RectTransform>();
            tRT.anchorMin = Vector2.zero;
            tRT.anchorMax = Vector2.one;
            tRT.sizeDelta = Vector2.zero;

            return go;
        }

        private static TextMeshProUGUI AddLabel(Transform parent, string name, string text,
            Vector2 anchorMin, Vector2 anchorMax,
            float fontSize, Color color, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.sizeDelta = Vector2.zero;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = fontSize;
            tmp.color     = color;
            tmp.alignment = alignment;
            return tmp;
        }

        private static GameObject AddTMPChild(Transform parent, string name, string text,
            float fontSize, Color color, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text             = text;
            tmp.fontSize         = fontSize;
            tmp.color            = color;
            tmp.alignment        = alignment;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            return go;
        }

        // ── Deep child search ──────────────────────────────────────────────────

        private static Transform FindDeepChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                var found = FindDeepChild(child, name);
                if (found != null) return found;
            }
            return null;
        }

        // ── SerializedObject field setter ──────────────────────────────────────

        private static void SetField(Object target, string fieldName, Object value)
        {
            if (target == null) return;
            var so   = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[TrainingRoomSetup] Field '{fieldName}' not found on {target.GetType().Name}");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetField(Object target, string fieldName, float value)
        {
            if (target == null) return;
            var so   = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop != null) { prop.floatValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        private static void SetField(Object target, string fieldName, int value)
        {
            if (target == null) return;
            var so   = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop != null) { prop.intValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        private static void SetField(Object target, string fieldName, bool value)
        {
            if (target == null) return;
            var so   = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop != null) { prop.boolValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        private static void SetField(Object target, string fieldName, Light[] lights)
        {
            if (target == null || lights == null) return;
            var so   = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null) return;
            prop.ClearArray();
            for (int i = 0; i < lights.Length; i++)
            {
                prop.InsertArrayElementAtIndex(i);
                prop.GetArrayElementAtIndex(i).objectReferenceValue = lights[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
