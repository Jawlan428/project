#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEditor;
using TMPro;
using VRRecordings;
using Object = UnityEngine.Object;

namespace VRRecordingsEditor
{
    /// <summary>
    /// Editor helper to quickly set up the VR Recordings Gallery UI.
    /// Access via menu: GameObject > VR Recordings Gallery > Create Full Setup
    /// </summary>
    public class RecordingsGalleryEditorSetup
    {
        [MenuItem("GameObject/VR Recordings Gallery/Create Full Setup", false, 10)]
        public static void CreateFullSetup()
        {
            // Create parent object
            GameObject root = new GameObject("VR Recordings System");
            Undo.RegisterCreatedObjectUndo(root, "Create VR Recordings System");

            // Create Video Screen
            GameObject videoScreen = CreateVideoScreen(root.transform);
            
            // Create Gallery Panel
            GameObject gallery = CreateGalleryPanel(root.transform);

            // Create Recording Item Prefab
            CreateRecordingItemPrefab();

            // Wire up references
            var galleryManager = gallery.GetComponentInChildren<VRRecordingsGalleryManager>();
            var videoPlayer = videoScreen.GetComponentInChildren<VRVideoScreenPlayer>();
            var framePlayer = videoScreen.GetComponentInChildren<VRFrameSequencePlayer>();
            
            if (galleryManager != null)
            {
                var so = new SerializedObject(galleryManager);
                if (videoPlayer != null)
                    so.FindProperty("videoPlayer").objectReferenceValue = videoPlayer;
                if (framePlayer != null)
                    so.FindProperty("frameSequencePlayer").objectReferenceValue = framePlayer;
                so.ApplyModifiedProperties();
            }

            // Select the root object
            Selection.activeGameObject = root;

            Debug.Log("✅ VR Recordings Gallery created! See the Setup Guide for configuration details.");
            EditorUtility.DisplayDialog(
                "VR Recordings Gallery Created",
                "The recording gallery system has been created.\n\n" +
                "Next steps:\n" +
                "1. Position the Video Screen Canvas in your scene\n" +
                "2. Position the Gallery Canvas (e.g., on a wall)\n" +
                "3. Assign the RecordingItemPrefab to the Gallery Manager\n" +
                "4. Add XR Tracked Device Graphic Raycaster to canvases for VR input\n\n" +
                "See RECORDINGS_GALLERY_SETUP.md for detailed instructions.",
                "OK"
            );
        }

        [MenuItem("GameObject/VR Recordings Gallery/Create Video Screen Only", false, 11)]
        public static void CreateVideoScreenOnly()
        {
            GameObject videoScreen = CreateVideoScreen(null);
            Selection.activeGameObject = videoScreen;
        }

        [MenuItem("GameObject/VR Recordings Gallery/Create Gallery Panel Only", false, 12)]
        public static void CreateGalleryPanelOnly()
        {
            GameObject gallery = CreateGalleryPanel(null);
            Selection.activeGameObject = gallery;
        }

        private static GameObject CreateVideoScreen(Transform parent)
        {
            // Create Canvas
            GameObject canvasObj = new GameObject("Video Screen Canvas");
            if (parent != null) canvasObj.transform.SetParent(parent);
            
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10;
            
            // Add TrackedDeviceGraphicRaycaster for VR controller interaction (instead of GraphicRaycaster)
            canvasObj.AddComponent<TrackedDeviceGraphicRaycaster>();

            // Set canvas size
            RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1920, 1200);
            canvasRect.localScale = new Vector3(0.001f, 0.001f, 0.001f);
            canvasRect.localPosition = new Vector3(0, 1.5f, 2f);

            // Add VideoPlayer and our components
            VideoPlayer vp = canvasObj.AddComponent<UnityEngine.Video.VideoPlayer>();
            VRVideoScreenPlayer screenPlayer = canvasObj.AddComponent<VRVideoScreenPlayer>();
            VRFrameSequencePlayer framePlayer = canvasObj.AddComponent<VRFrameSequencePlayer>();

            // Create Video Display Panel
            GameObject screenPanel = CreateUIElement("Video Screen Panel", canvasObj.transform, typeof(Image));
            RectTransform panelRect = screenPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            Image panelBg = screenPanel.GetComponent<Image>();
            panelBg.color = new Color(0.05f, 0.05f, 0.08f, 1f);

            // Create Video Display RawImage
            GameObject videoDisplay = CreateUIElement("Video Display", screenPanel.transform, typeof(RawImage));
            RectTransform displayRect = videoDisplay.GetComponent<RectTransform>();
            displayRect.anchorMin = new Vector2(0, 0.1f);
            displayRect.anchorMax = new Vector2(1, 0.95f);
            displayRect.offsetMin = new Vector2(20, 0);
            displayRect.offsetMax = new Vector2(-20, -10);
            RawImage rawImage = videoDisplay.GetComponent<RawImage>();
            rawImage.color = Color.white;  // MUST be white to show video! Black would hide it.

            // Create Title Text
            GameObject titleObj = CreateTextElement("Title Text", screenPanel.transform, "Video Title");
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.95f);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.offsetMin = new Vector2(20, 0);
            titleRect.offsetMax = new Vector2(-20, 0);
            TextMeshProUGUI titleText = titleObj.GetComponent<TextMeshProUGUI>();
            titleText.alignment = TextAlignmentOptions.Left;
            titleText.fontSize = 36;

            // Create Controls Panel
            GameObject controlsPanel = CreateUIElement("Controls Panel", screenPanel.transform, typeof(Image));
            RectTransform controlsRect = controlsPanel.GetComponent<RectTransform>();
            controlsRect.anchorMin = new Vector2(0, 0);
            controlsRect.anchorMax = new Vector2(1, 0.1f);
            controlsRect.offsetMin = Vector2.zero;
            controlsRect.offsetMax = Vector2.zero;
            Image controlsBg = controlsPanel.GetComponent<Image>();
            controlsBg.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

            HorizontalLayoutGroup hlg = controlsPanel.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(20, 20, 10, 10);
            hlg.spacing = 20;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            // Play/Pause Button
            GameObject playPauseBtn = CreateButton("Play Pause Button", controlsPanel.transform, "Play");
            RectTransform ppRect = playPauseBtn.GetComponent<RectTransform>();
            ppRect.sizeDelta = new Vector2(80, 80);
            LayoutElement ppLayout = playPauseBtn.AddComponent<LayoutElement>();
            ppLayout.minWidth = 80;
            ppLayout.minHeight = 80;

            // Stop Button
            GameObject stopBtn = CreateButton("Stop Button", controlsPanel.transform, "Stop");
            RectTransform stopRect = stopBtn.GetComponent<RectTransform>();
            stopRect.sizeDelta = new Vector2(80, 80);
            LayoutElement stopLayout = stopBtn.AddComponent<LayoutElement>();
            stopLayout.minWidth = 80;
            stopLayout.minHeight = 80;

            // Progress Slider
            GameObject progressObj = CreateSlider("Progress Slider", controlsPanel.transform);
            LayoutElement progressLayout = progressObj.AddComponent<LayoutElement>();
            progressLayout.flexibleWidth = 1;
            progressLayout.minHeight = 40;

            // Time Text
            GameObject timeObj = CreateTextElement("Time Text", controlsPanel.transform, "0:00 / 0:00");
            RectTransform timeRect = timeObj.GetComponent<RectTransform>();
            timeRect.sizeDelta = new Vector2(200, 40);
            LayoutElement timeLayout = timeObj.AddComponent<LayoutElement>();
            timeLayout.minWidth = 200;
            TextMeshProUGUI timeText = timeObj.GetComponent<TextMeshProUGUI>();
            timeText.fontSize = 28;
            timeText.alignment = TextAlignmentOptions.Center;

            // Volume Slider
            GameObject volumeObj = CreateSlider("Volume Slider", controlsPanel.transform);
            LayoutElement volumeLayout = volumeObj.AddComponent<LayoutElement>();
            volumeLayout.minWidth = 150;
            volumeLayout.minHeight = 40;
            Slider volumeSlider = volumeObj.GetComponent<Slider>();
            volumeSlider.value = 0.8f;

            // Loading Indicator
            GameObject loadingObj = CreateTextElement("Loading Indicator", screenPanel.transform, "Loading...");
            RectTransform loadingRect = loadingObj.GetComponent<RectTransform>();
            loadingRect.anchorMin = new Vector2(0.5f, 0.5f);
            loadingRect.anchorMax = new Vector2(0.5f, 0.5f);
            loadingRect.sizeDelta = new Vector2(300, 100);
            TextMeshProUGUI loadingText = loadingObj.GetComponent<TextMeshProUGUI>();
            loadingText.fontSize = 48;
            loadingText.alignment = TextAlignmentOptions.Center;
            loadingObj.SetActive(false);

            // Wire up VRVideoScreenPlayer references
            var so = new SerializedObject(screenPlayer);
            so.FindProperty("videoDisplayImage").objectReferenceValue = rawImage;
            so.FindProperty("playPauseButton").objectReferenceValue = playPauseBtn.GetComponent<Button>();
            so.FindProperty("stopButton").objectReferenceValue = stopBtn.GetComponent<Button>();
            so.FindProperty("progressSlider").objectReferenceValue = progressObj.GetComponent<Slider>();
            so.FindProperty("volumeSlider").objectReferenceValue = volumeObj.GetComponent<Slider>();
            so.FindProperty("timeText").objectReferenceValue = timeText;
            so.FindProperty("titleText").objectReferenceValue = titleText;
            so.FindProperty("videoScreenPanel").objectReferenceValue = screenPanel;
            so.FindProperty("loadingIndicator").objectReferenceValue = loadingObj;
            so.ApplyModifiedProperties();
            
            // Wire up VRFrameSequencePlayer references (shares same display)
            var frameSo = new SerializedObject(framePlayer);
            frameSo.FindProperty("displayImage").objectReferenceValue = rawImage;
            frameSo.FindProperty("titleText").objectReferenceValue = titleText;
            frameSo.FindProperty("timeText").objectReferenceValue = timeText;
            frameSo.FindProperty("loadingIndicator").objectReferenceValue = loadingObj;
            frameSo.ApplyModifiedProperties();

            Undo.RegisterCreatedObjectUndo(canvasObj, "Create Video Screen");
            return canvasObj;
        }

        private static GameObject CreateGalleryPanel(Transform parent)
        {
            // Create Canvas
            GameObject canvasObj = new GameObject("Recordings Gallery Canvas");
            if (parent != null) canvasObj.transform.SetParent(parent);
            
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10;
            
            // Add TrackedDeviceGraphicRaycaster for VR controller interaction (instead of GraphicRaycaster)
            canvasObj.AddComponent<TrackedDeviceGraphicRaycaster>();

            // Set canvas size and position
            RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(800, 1000);
            canvasRect.localScale = new Vector3(0.001f, 0.001f, 0.001f);
            canvasRect.localPosition = new Vector3(-1.5f, 1.5f, 2f);

            // Add Gallery Manager
            VRRecordingsGalleryManager galleryManager = canvasObj.AddComponent<VRRecordingsGalleryManager>();

            // Create Open Button (always visible)
            GameObject openBtn = CreateButton("Open Gallery Button", canvasObj.transform, "Recordings");
            RectTransform openRect = openBtn.GetComponent<RectTransform>();
            openRect.anchorMin = new Vector2(0.5f, 1);
            openRect.anchorMax = new Vector2(0.5f, 1);
            openRect.pivot = new Vector2(0.5f, 1);
            openRect.anchoredPosition = new Vector2(0, 50);
            openRect.sizeDelta = new Vector2(300, 80);

            // Create Gallery Panel
            GameObject galleryPanel = CreateUIElement("Gallery Panel", canvasObj.transform, typeof(Image));
            RectTransform panelRect = galleryPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            Image panelBg = galleryPanel.GetComponent<Image>();
            panelBg.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);

            // Header
            GameObject header = CreateUIElement("Header", galleryPanel.transform, typeof(Image));
            RectTransform headerRect = header.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0, 0.9f);
            headerRect.anchorMax = Vector2.one;
            headerRect.offsetMin = Vector2.zero;
            headerRect.offsetMax = Vector2.zero;
            Image headerBg = header.GetComponent<Image>();
            headerBg.color = new Color(0.12f, 0.12f, 0.18f, 1f);

            HorizontalLayoutGroup headerHlg = header.AddComponent<HorizontalLayoutGroup>();
            headerHlg.padding = new RectOffset(20, 20, 10, 10);
            headerHlg.spacing = 10;
            headerHlg.childAlignment = TextAnchor.MiddleLeft;

            // Title
            GameObject titleObj = CreateTextElement("Title Text", header.transform, "Recordings");
            TextMeshProUGUI titleText = titleObj.GetComponent<TextMeshProUGUI>();
            titleText.fontSize = 40;
            titleText.fontStyle = FontStyles.Bold;
            LayoutElement titleLayout = titleObj.AddComponent<LayoutElement>();
            titleLayout.flexibleWidth = 1;

            // Count
            GameObject countObj = CreateTextElement("Count Text", header.transform, "0 Recordings");
            TextMeshProUGUI countText = countObj.GetComponent<TextMeshProUGUI>();
            countText.fontSize = 28;
            countText.color = new Color(0.7f, 0.7f, 0.7f);
            LayoutElement countLayout = countObj.AddComponent<LayoutElement>();
            countLayout.minWidth = 200;

            // Refresh Button
            GameObject refreshBtn = CreateButton("Refresh Button", header.transform, "Refresh");
            RectTransform refreshRect = refreshBtn.GetComponent<RectTransform>();
            refreshRect.sizeDelta = new Vector2(120, 70);
            LayoutElement refreshLayout = refreshBtn.AddComponent<LayoutElement>();
            refreshLayout.minWidth = 120;
            refreshLayout.minHeight = 70;

            // Close Button
            GameObject closeBtn = CreateButton("Close Button", header.transform, "X");
            RectTransform closeRect = closeBtn.GetComponent<RectTransform>();
            closeRect.sizeDelta = new Vector2(70, 70);
            LayoutElement closeLayout = closeBtn.AddComponent<LayoutElement>();
            closeLayout.minWidth = 70;
            closeLayout.minHeight = 70;

            // Scroll View
            GameObject scrollView = CreateScrollView("Recordings Scroll View", galleryPanel.transform);
            RectTransform scrollRect = scrollView.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0, 0.05f);
            scrollRect.anchorMax = new Vector2(1, 0.9f);
            scrollRect.offsetMin = new Vector2(10, 0);
            scrollRect.offsetMax = new Vector2(-10, -10);

            Transform content = scrollView.transform.Find("Viewport/Content");

            // No Recordings Text
            GameObject noRecText = CreateTextElement("No Recordings Text", galleryPanel.transform, "No recordings found.\nRecord a meeting to see it here.");
            RectTransform noRecRect = noRecText.GetComponent<RectTransform>();
            noRecRect.anchorMin = new Vector2(0.5f, 0.5f);
            noRecRect.anchorMax = new Vector2(0.5f, 0.5f);
            noRecRect.sizeDelta = new Vector2(600, 200);
            TextMeshProUGUI noRecTMP = noRecText.GetComponent<TextMeshProUGUI>();
            noRecTMP.fontSize = 32;
            noRecTMP.color = new Color(0.5f, 0.5f, 0.5f);
            noRecTMP.alignment = TextAlignmentOptions.Center;

            // Wire up references
            var so = new SerializedObject(galleryManager);
            so.FindProperty("galleryPanel").objectReferenceValue = galleryPanel;
            so.FindProperty("openGalleryButton").objectReferenceValue = openBtn.GetComponent<Button>();
            so.FindProperty("closeGalleryButton").objectReferenceValue = closeBtn.GetComponent<Button>();
            so.FindProperty("recordingListContent").objectReferenceValue = content;
            so.FindProperty("noRecordingsText").objectReferenceValue = noRecTMP;
            so.FindProperty("recordingsCountText").objectReferenceValue = countText;
            so.FindProperty("refreshButton").objectReferenceValue = refreshBtn.GetComponent<Button>();
            so.ApplyModifiedProperties();

            Undo.RegisterCreatedObjectUndo(canvasObj, "Create Gallery Panel");
            return canvasObj;
        }

        [MenuItem("GameObject/VR Recordings Gallery/Create Recording Item Prefab", false, 13)]
        public static void CreateRecordingItemPrefab()
        {
            // Create in scene first
            GameObject itemObj = new GameObject("Recording Item");
            
            // Background
            Image bg = itemObj.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);

            Button btn = itemObj.AddComponent<Button>();
            ColorBlock colors = btn.colors;
            colors.normalColor = new Color(0.15f, 0.15f, 0.2f, 0.9f);
            colors.highlightedColor = new Color(0.2f, 0.25f, 0.35f, 0.95f);
            colors.pressedColor = new Color(0.25f, 0.35f, 0.5f, 1f);
            btn.colors = colors;

            RectTransform itemRect = itemObj.GetComponent<RectTransform>();
            itemRect.sizeDelta = new Vector2(760, 120);

            // Layout
            HorizontalLayoutGroup hlg = itemObj.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(15, 15, 10, 10);
            hlg.spacing = 15;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childControlWidth = false;

            LayoutElement itemLayout = itemObj.AddComponent<LayoutElement>();
            itemLayout.minHeight = 120;
            itemLayout.preferredHeight = 120;
            itemLayout.flexibleWidth = 1;

            // Thumbnail placeholder
            GameObject thumbObj = CreateUIElement("Thumbnail", itemObj.transform, typeof(RawImage));
            RectTransform thumbRect = thumbObj.GetComponent<RectTransform>();
            thumbRect.sizeDelta = new Vector2(160, 90);
            LayoutElement thumbLayout = thumbObj.AddComponent<LayoutElement>();
            thumbLayout.minWidth = 160;
            thumbLayout.minHeight = 90;
            RawImage thumbImg = thumbObj.GetComponent<RawImage>();
            thumbImg.color = new Color(0.2f, 0.2f, 0.25f);

            // Text container
            GameObject textContainer = new GameObject("Text Container");
            textContainer.transform.SetParent(itemObj.transform);
            RectTransform textRect = textContainer.AddComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(400, 100);
            VerticalLayoutGroup vlg = textContainer.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleLeft;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 5;
            LayoutElement textLayout = textContainer.AddComponent<LayoutElement>();
            textLayout.flexibleWidth = 1;
            textLayout.minHeight = 100;

            // Title
            GameObject titleObj = CreateTextElement("Title Text", textContainer.transform, "Recording - Jan 15, 2026");
            TextMeshProUGUI titleText = titleObj.GetComponent<TextMeshProUGUI>();
            titleText.fontSize = 28;
            titleText.fontStyle = FontStyles.Bold;

            // Subtitle
            GameObject subObj = CreateTextElement("Subtitle Text", textContainer.transform, "Jan 15, 2026 14:30 • 125 MB");
            TextMeshProUGUI subText = subObj.GetComponent<TextMeshProUGUI>();
            subText.fontSize = 22;
            subText.color = new Color(0.6f, 0.6f, 0.65f);

            // Delete button
            GameObject deleteBtn = CreateButton("Delete Button", itemObj.transform, "Del");
            RectTransform delRect = deleteBtn.GetComponent<RectTransform>();
            delRect.sizeDelta = new Vector2(60, 60);
            LayoutElement delLayout = deleteBtn.AddComponent<LayoutElement>();
            delLayout.minWidth = 60;
            delLayout.minHeight = 60;
            Image delBg = deleteBtn.GetComponent<Image>();
            delBg.color = new Color(0.6f, 0.2f, 0.2f, 0.8f);

            // Add RecordingListItem component
            RecordingListItem listItem = itemObj.AddComponent<RecordingListItem>();
            var so = new SerializedObject(listItem);
            so.FindProperty("titleText").objectReferenceValue = titleText;
            so.FindProperty("subtitleText").objectReferenceValue = subText;
            so.FindProperty("thumbnailImage").objectReferenceValue = thumbImg;
            so.FindProperty("deleteButton").objectReferenceValue = deleteBtn.GetComponent<Button>();
            so.FindProperty("backgroundImage").objectReferenceValue = bg;
            so.ApplyModifiedProperties();

            // Save as prefab
            string prefabPath = "Assets/Prefabs";
            if (!AssetDatabase.IsValidFolder(prefabPath))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }

            string fullPath = prefabPath + "/RecordingItemPrefab.prefab";
            
            // Check if prefab already exists
            if (AssetDatabase.LoadAssetAtPath<GameObject>(fullPath) != null)
            {
                if (!EditorUtility.DisplayDialog("Overwrite Prefab?", 
                    "RecordingItemPrefab already exists. Overwrite it?", "Yes", "No"))
                {
                    Object.DestroyImmediate(itemObj);
                    return;
                }
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(itemObj, fullPath);
            Object.DestroyImmediate(itemObj);

            Debug.Log($"✅ Created prefab at: {fullPath}");
            Selection.activeObject = prefab;
        }

        // Helper methods
        private static GameObject CreateUIElement(string name, Transform parent, params System.Type[] components)
        {
            GameObject obj = new GameObject(name, components);
            obj.transform.SetParent(parent, false);
            return obj;
        }

        private static GameObject CreateTextElement(string name, Transform parent, string text)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            
            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 24;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Left;

            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200, 50);

            return obj;
        }

        private static GameObject CreateButton(string name, Transform parent, string label)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);

            Image img = obj.AddComponent<Image>();
            img.color = new Color(0.2f, 0.4f, 0.7f, 0.9f);

            Button btn = obj.AddComponent<Button>();
            ColorBlock colors = btn.colors;
            colors.normalColor = new Color(0.2f, 0.4f, 0.7f, 0.9f);
            colors.highlightedColor = new Color(0.3f, 0.5f, 0.8f, 1f);
            colors.pressedColor = new Color(0.15f, 0.3f, 0.6f, 1f);
            btn.colors = colors;

            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(150, 50);

            // Add label
            GameObject textObj = CreateTextElement("Label", obj.transform, label);
            TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 28;
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return obj;
        }

        private static GameObject CreateSlider(string name, Transform parent)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);

            Slider slider = obj.AddComponent<Slider>();
            RectTransform sliderRect = obj.GetComponent<RectTransform>();
            sliderRect.sizeDelta = new Vector2(300, 30);

            // Background
            GameObject bgObj = CreateUIElement("Background", obj.transform, typeof(Image));
            Image bgImg = bgObj.GetComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.25f);
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.25f);
            bgRect.anchorMax = new Vector2(1, 0.75f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // Fill Area
            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(obj.transform, false);
            RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0, 0.25f);
            fillAreaRect.anchorMax = new Vector2(1, 0.75f);
            fillAreaRect.offsetMin = new Vector2(5, 0);
            fillAreaRect.offsetMax = new Vector2(-5, 0);

            GameObject fillObj = CreateUIElement("Fill", fillArea.transform, typeof(Image));
            Image fillImg = fillObj.GetComponent<Image>();
            fillImg.color = new Color(0.3f, 0.6f, 0.9f);
            RectTransform fillRect = fillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            // Handle
            GameObject handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(obj.transform, false);
            RectTransform handleAreaRect = handleArea.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(10, 0);
            handleAreaRect.offsetMax = new Vector2(-10, 0);

            GameObject handleObj = CreateUIElement("Handle", handleArea.transform, typeof(Image));
            Image handleImg = handleObj.GetComponent<Image>();
            handleImg.color = Color.white;
            RectTransform handleRect = handleObj.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(20, 0);

            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImg;

            return obj;
        }

        private static GameObject CreateScrollView(string name, Transform parent)
        {
            GameObject scrollObj = new GameObject(name);
            scrollObj.transform.SetParent(parent, false);

            RectTransform scrollRect = scrollObj.AddComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = Vector2.zero;

            ScrollRect scroll = scrollObj.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.1f;

            Image scrollBg = scrollObj.AddComponent<Image>();
            scrollBg.color = new Color(0, 0, 0, 0);

            // Viewport
            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollObj.transform, false);
            RectTransform viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;

            viewport.AddComponent<Mask>().showMaskGraphic = false;
            Image viewportImg = viewport.AddComponent<Image>();
            viewportImg.color = Color.white;

            // Content
            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, 0);

            VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 10, 10);
            vlg.spacing = 10;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewportRect;
            scroll.content = contentRect;

            return scrollObj;
        }
    }
}
#endif

