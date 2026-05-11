#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SmartFarm.MeetingRoom.EditorTools
{
    /// <summary>
    /// Editor window that scaffolds the full VR Smart Farm meeting area into the
    /// current scene with one click. Optionally creates the six default report
    /// assets on disk if they don't already exist.
    /// </summary>
    public class MeetingRoomSetupWindow : EditorWindow
    {
        private const string ReportFolder = "Assets/SmartFarm/MeetingRoom/Reports";

        private GameObject _tablePrefab;
        private GameObject _chairPrefab;
        private int _chairCount = 6;
        private float _tableRadius = 1.0f;
        private float _sitDistance = 0.7f;
        private bool _createSampleReports = true;
        private int _documentCount = 6;
        private float _documentWidth = 0.14f;
        private float _documentHeight = 0.20f;
        private float _tableTopY = 0.78f;

        [MenuItem("Tools/Smart Farm/Setup Meeting Room…", priority = 100)]
        public static void Open()
        {
            var w = GetWindow<MeetingRoomSetupWindow>(true, "Smart Farm Meeting Room Setup", true);
            w.minSize = new Vector2(420, 460);
            w.LoadDefaultPrefabs();
        }

        [MenuItem("Tools/Smart Farm/Generate Default Reports", priority = 110)]
        public static void GenerateReportsMenu()
        {
            EnsureReportFolder();
            CreateSampleReports();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Smart Farm", "Default farming reports created under " + ReportFolder, "OK");
        }

        private void LoadDefaultPrefabs()
        {
            if (_tablePrefab == null)
                _tablePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Tables and Chairs/Prefabs/Table4.prefab");
            if (_chairPrefab == null)
                _chairPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Tables and Chairs/Prefabs/Chair2.prefab");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("VR Smart Farm — Meeting Room", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Builds a complete interactive meeting area into the active scene:\n" +
                                    "• Round table + ring of chairs (sit/stand enabled)\n" +
                                    "• Six grabbable farming documents (live data)\n" +
                                    "• Reader proximity zoom + ambience driver\n" +
                                    "• MeetingInteractionManager wiring it all together.", MessageType.Info);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Prefabs", EditorStyles.boldLabel);
            _tablePrefab = (GameObject)EditorGUILayout.ObjectField("Table Prefab", _tablePrefab, typeof(GameObject), false);
            _chairPrefab = (GameObject)EditorGUILayout.ObjectField("Chair Prefab", _chairPrefab, typeof(GameObject), false);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Layout", EditorStyles.boldLabel);
            _chairCount = Mathf.Clamp(EditorGUILayout.IntSlider("Chair Count", _chairCount, 2, 12), 2, 12);
            _tableRadius = EditorGUILayout.Slider("Table Radius (m)", _tableRadius, 0.5f, 2.5f);
            _sitDistance = EditorGUILayout.Slider("Chair Distance From Table", _sitDistance, 0.4f, 1.5f);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Documents", EditorStyles.boldLabel);
            _documentCount = Mathf.Clamp(EditorGUILayout.IntSlider("Document Count", _documentCount, 1, 8), 1, 8);
            _documentWidth = EditorGUILayout.Slider(new GUIContent("Document Width (m)", "Width of each printed report in metres."), _documentWidth, 0.08f, 0.32f);
            _documentHeight = EditorGUILayout.Slider(new GUIContent("Document Height (m)", "Height of each printed report in metres."), _documentHeight, 0.10f, 0.45f);
            _tableTopY = EditorGUILayout.Slider(new GUIContent("Table Top Height (m)", "World Y for the document rest pose. Adjust to match your table's top surface."), _tableTopY, 0.4f, 1.4f);
            _createSampleReports = EditorGUILayout.Toggle(new GUIContent("Create Sample Reports", "If the report assets are missing they'll be generated under Assets/SmartFarm/MeetingRoom/Reports"), _createSampleReports);

            EditorGUILayout.Space(12);
            using (new EditorGUI.DisabledScope(_tablePrefab == null || _chairPrefab == null))
            {
                if (GUILayout.Button("Build Meeting Area in Scene", GUILayout.Height(36)))
                {
                    BuildMeetingArea();
                }
            }

            if (_tablePrefab == null || _chairPrefab == null)
                EditorGUILayout.HelpBox("Assign both prefabs to enable Build.", MessageType.Warning);

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Generate Default Reports Only"))
            {
                GenerateReportsMenu();
            }

            if (GUILayout.Button(new GUIContent("Resize Existing Documents in Scene", "Applies the Document Width/Height fields to every VRDocumentInteractable in the active scene.")))
            {
                ResizeExistingDocuments(_documentWidth, _documentHeight);
            }

            if (GUILayout.Button(new GUIContent("Snap Documents To Table Top", "Finds the nearest MeetingTable, measures its top surface, then places every document on it in an evenly spaced ring.")))
            {
                SnapDocumentsToTable();
            }
        }

        // ── Build ─────────────────────────────────────────────────────────────

        private void BuildMeetingArea()
        {
            var reports = LoadOrCreateReports(_createSampleReports);

            var rootGO = new GameObject("VR Smart Farm Meeting Area");
            Undo.RegisterCreatedObjectUndo(rootGO, "Create Meeting Area");

            // Place it slightly in front of the scene view camera if possible, otherwise origin.
            var sv = SceneView.lastActiveSceneView;
            if (sv != null)
            {
                Vector3 fwd = sv.camera.transform.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
                rootGO.transform.position = sv.camera.transform.position + fwd.normalized * 2.5f;
                rootGO.transform.position = new Vector3(rootGO.transform.position.x, 0f, rootGO.transform.position.z);
            }

            // Table
            var table = (GameObject)PrefabUtility.InstantiatePrefab(_tablePrefab, rootGO.transform);
            table.name = "MeetingTable";
            table.transform.localPosition = Vector3.zero;
            EnsureTag(table, "MeetingTable");

            // Auto-detect the actual table top height from its renderer bounds so the
            // documents are placed exactly on the surface regardless of which table
            // prefab the user picks.
            float detectedTop = TryMeasureTableTop(table);
            if (detectedTop > 0f)
            {
                _tableTopY = detectedTop - rootGO.transform.position.y;
            }

            // Chairs around the table
            var chairsParent = new GameObject("Chairs");
            chairsParent.transform.SetParent(rootGO.transform, false);
            for (int i = 0; i < _chairCount; i++)
            {
                float angle = (360f / _chairCount) * i;
                var rot = Quaternion.Euler(0f, angle, 0f);
                Vector3 pos = rot * (Vector3.forward * (_tableRadius + _sitDistance));
                Quaternion chairRot = Quaternion.Euler(0f, angle + 180f, 0f);

                var chair = (GameObject)PrefabUtility.InstantiatePrefab(_chairPrefab, chairsParent.transform);
                chair.name = $"Chair_{i + 1}";
                chair.transform.localPosition = pos;
                chair.transform.localRotation = chairRot;

                if (chair.GetComponent<ChairSitSystem>() == null)
                    chair.AddComponent<ChairSitSystem>();
            }

            // Documents on the table — arranged in an inner ring sized so they fit comfortably on the table top.
            var docsParent = new GameObject("Documents");
            docsParent.transform.SetParent(rootGO.transform, false);
            int docCount = Mathf.Min(_documentCount, reports.Count);

            // Inner ring sized so each document's bounding box fits with margin.
            float minRadius = (_documentWidth + _documentHeight) * 0.6f + 0.04f;
            float maxRadius = Mathf.Max(minRadius, _tableRadius - Mathf.Max(_documentWidth, _documentHeight) * 0.6f - 0.05f);
            float docRing = Mathf.Clamp(_tableRadius * 0.5f, minRadius, maxRadius);

            for (int i = 0; i < docCount; i++)
            {
                float angle = (360f / docCount) * i;
                var rot = Quaternion.Euler(0f, angle, 0f);
                Vector3 pos = rot * (Vector3.forward * docRing);
                // A tiny lift above the surface so the snap raycast at runtime finds the table.
                pos.y = _tableTopY + 0.01f;

                var doc = new GameObject($"Doc_{reports[i].title}");
                doc.transform.SetParent(docsParent.transform, false);
                doc.transform.localPosition = pos;
                // Documents face inward so users on either side can read them.
                doc.transform.localRotation = Quaternion.Euler(0f, angle + 180f, 0f);

                var inter = doc.AddComponent<VRDocumentInteractable>();
                var so = new SerializedObject(inter);
                so.FindProperty("report").objectReferenceValue = reports[i];
                so.FindProperty("pageWidth").floatValue = _documentWidth;
                so.FindProperty("pageHeight").floatValue = _documentHeight;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // Decorations placeholder
            var deco = new GameObject("Decorations");
            deco.transform.SetParent(rootGO.transform, false);

            // Sub-systems
            var managerGO = new GameObject("MeetingInteractionManager");
            managerGO.transform.SetParent(rootGO.transform, false);
            managerGO.AddComponent<MeetingInteractionManager>();
            managerGO.AddComponent<SmartFarmReportManager>();
            managerGO.AddComponent<DocumentReaderSystem>();
            managerGO.AddComponent<MeetingAmbience>();

            Selection.activeGameObject = rootGO;
            EditorGUIUtility.PingObject(rootGO);

            EditorUtility.DisplayDialog(
                "Smart Farm Meeting Room",
                $"Created meeting area with {_chairCount} chairs and {docCount} documents.\n\n" +
                "Next steps:\n" +
                "1. Drag your XR Origin / Player Rig into the ChairSitSystem 'Player Rig' field (or rely on auto-find).\n" +
                "2. Adjust the table top height — set each Doc_* Y position to match your table's surface.\n" +
                "3. Press Play and grab a document with your VR controllers.",
                "Got it");
        }

        // ── Reports ───────────────────────────────────────────────────────────

        private static List<SmartFarmReportData> LoadOrCreateReports(bool createIfMissing)
        {
            var list = new List<SmartFarmReportData>();
            EnsureReportFolder();

            string[] guids = AssetDatabase.FindAssets("t:SmartFarmReportData", new[] { ReportFolder });
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var a = AssetDatabase.LoadAssetAtPath<SmartFarmReportData>(path);
                if (a != null) list.Add(a);
            }

            if (list.Count == 0 && createIfMissing)
            {
                CreateSampleReports();
                guids = AssetDatabase.FindAssets("t:SmartFarmReportData", new[] { ReportFolder });
                foreach (var g in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(g);
                    var a = AssetDatabase.LoadAssetAtPath<SmartFarmReportData>(path);
                    if (a != null) list.Add(a);
                }
            }

            return list;
        }

        private static void EnsureReportFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/SmartFarm"))
                AssetDatabase.CreateFolder("Assets", "SmartFarm");
            if (!AssetDatabase.IsValidFolder("Assets/SmartFarm/MeetingRoom"))
                AssetDatabase.CreateFolder("Assets/SmartFarm", "MeetingRoom");
            if (!AssetDatabase.IsValidFolder(ReportFolder))
                AssetDatabase.CreateFolder("Assets/SmartFarm/MeetingRoom", "Reports");
        }

        private static void CreateSampleReports()
        {
            EnsureReportFolder();

            CreateReport("CropHealth", SmartFarmReportType.CropHealth, "Crop Health Report",
                "Daily summary", new Color(0.96f, 0.94f, 0.86f), new Color(0.15f, 0.45f, 0.25f));

            CreateReport("Irrigation", SmartFarmReportType.Irrigation, "Smart Irrigation Report",
                "Water cycle status", new Color(0.94f, 0.96f, 0.98f), new Color(0.15f, 0.35f, 0.6f));

            CreateReport("WeatherForecast", SmartFarmReportType.WeatherForecast, "Weather Forecast",
                "24-hour outlook", new Color(0.92f, 0.96f, 0.99f), new Color(0.35f, 0.45f, 0.7f));

            CreateReport("HarvestPlanning", SmartFarmReportType.HarvestPlanning, "Harvest Planning",
                "Projected cycle output", new Color(0.99f, 0.96f, 0.88f), new Color(0.7f, 0.45f, 0.15f));

            CreateReport("SoilAnalysis", SmartFarmReportType.SoilAnalysis, "Soil Analysis",
                "Sensor composite", new Color(0.96f, 0.92f, 0.84f), new Color(0.45f, 0.3f, 0.18f));

            CreateReport("WaterUsage", SmartFarmReportType.WaterUsage, "Water Usage Analytics",
                "Resource consumption", new Color(0.93f, 0.97f, 0.99f), new Color(0.18f, 0.6f, 0.8f));
        }

        private static void CreateReport(string fileName, SmartFarmReportType type, string title, string subtitle, Color page, Color accent)
        {
            string path = Path.Combine(ReportFolder, fileName + ".asset").Replace("\\", "/");
            if (File.Exists(path)) return;

            var asset = ScriptableObject.CreateInstance<SmartFarmReportData>();
            asset.reportId = type.ToString();
            asset.reportType = type;
            asset.title = title;
            asset.subtitle = subtitle;
            asset.pageColor = page;
            asset.accentColor = accent;
            asset.body = "Live data will appear here once the simulation runs.";
            asset.recommendations = "—";

            AssetDatabase.CreateAsset(asset, path);
        }

        private static float TryMeasureTableTop(GameObject table)
        {
            if (table == null) return 0f;
            var rends = table.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0) return 0f;
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b.max.y;
        }

        private void SnapDocumentsToTable()
        {
            // Find the MeetingTable to snap onto.
            GameObject table = null;
            var manager = Object.FindFirstObjectByType<MeetingInteractionManager>();
            if (manager != null)
            {
                var t = manager.transform.Find("MeetingTable");
                if (t != null) table = t.gameObject;
            }
            if (table == null)
            {
                var tagged = GameObject.FindGameObjectsWithTag("Untagged");
                foreach (var g in tagged)
                {
                    if (g != null && g.name == "MeetingTable") { table = g; break; }
                }
            }
            if (table == null)
            {
                try
                {
                    var tagHit = GameObject.FindGameObjectWithTag("MeetingTable");
                    if (tagHit != null) table = tagHit;
                }
                catch { /* tag may not exist */ }
            }

            if (table == null)
            {
                EditorUtility.DisplayDialog("Smart Farm",
                    "Could not find a GameObject named 'MeetingTable' (or tagged 'MeetingTable') in the active scene.",
                    "OK");
                return;
            }

            float top = TryMeasureTableTop(table);
            if (top <= 0f)
            {
                EditorUtility.DisplayDialog("Smart Farm",
                    "The MeetingTable has no Renderer children — cannot measure its top.",
                    "OK");
                return;
            }

            var docs = Object.FindObjectsByType<VRDocumentInteractable>(FindObjectsSortMode.None);
            if (docs == null || docs.Length == 0)
            {
                EditorUtility.DisplayDialog("Smart Farm", "No VRDocumentInteractable found in the active scene.", "OK");
                return;
            }

            // Compute a ring radius that keeps the documents on the table top.
            float tableRadius = 0.6f;
            var rends = table.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                tableRadius = Mathf.Min(b.extents.x, b.extents.z);
            }
            float ring = Mathf.Max(0.05f, tableRadius - Mathf.Max(_documentWidth, _documentHeight) * 0.55f - 0.02f);

            Vector3 center = table.transform.position;
            for (int i = 0; i < docs.Length; i++)
            {
                var d = docs[i];
                if (d == null) continue;
                Undo.RecordObject(d.transform, "Snap Document To Table");
                float angle = (360f / docs.Length) * i;
                var rot = Quaternion.Euler(0f, angle, 0f);
                Vector3 pos = center + rot * (Vector3.forward * ring);
                pos.y = top + 0.005f;
                d.transform.position = pos;
                d.transform.rotation = Quaternion.Euler(0f, angle + 180f, 0f);
                if (Application.isPlaying) d.SnapDownToSurface();
                EditorUtility.SetDirty(d.transform);
            }

            EditorUtility.DisplayDialog("Smart Farm",
                $"Placed {docs.Length} document(s) on the table top (y ≈ {top:0.00} m).",
                "OK");
        }

        private static void ResizeExistingDocuments(float width, float height)
        {
            var docs = Object.FindObjectsByType<VRDocumentInteractable>(FindObjectsSortMode.None);
            if (docs == null || docs.Length == 0)
            {
                EditorUtility.DisplayDialog("Smart Farm", "No VRDocumentInteractable found in the active scene.", "OK");
                return;
            }

            int changed = 0;
            foreach (var d in docs)
            {
                if (d == null) continue;
                Undo.RecordObject(d, "Resize Document");
                var so = new SerializedObject(d);
                so.FindProperty("pageWidth").floatValue = width;
                so.FindProperty("pageHeight").floatValue = height;
                so.ApplyModifiedProperties();

                // If we're in Play mode the runtime canvas exists; otherwise the
                // values will take effect when the scene is entered.
                if (Application.isPlaying) d.SetPageSize(width, height);

                EditorUtility.SetDirty(d);
                changed++;
            }

            EditorUtility.DisplayDialog("Smart Farm",
                $"Resized {changed} document(s) to {width:0.##} m × {height:0.##} m.\n\n" +
                "If the scene is not in Play mode, the new size will be applied on the next Awake.",
                "OK");
        }

        private static void EnsureTag(GameObject go, string tag)
        {
            try
            {
                if (!TagExists(tag)) AddTag(tag);
                go.tag = tag;
            }
            catch
            {
                // If the project doesn't allow runtime tag manipulation, fall back silently.
            }
        }

        private static bool TagExists(string tag)
        {
            var tags = UnityEditorInternal.InternalEditorUtility.tags;
            for (int i = 0; i < tags.Length; i++) if (tags[i] == tag) return true;
            return false;
        }

        private static void AddTag(string tag)
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var tagsProp = tagManager.FindProperty("tags");
            tagsProp.arraySize++;
            tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
            tagManager.ApplyModifiedProperties();
        }
    }
}
#endif
