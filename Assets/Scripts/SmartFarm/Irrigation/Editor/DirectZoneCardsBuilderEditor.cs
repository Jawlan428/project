#if UNITY_EDITOR
using System.Collections.Generic;
using SmartFarm.Irrigation.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SmartFarm.Irrigation.Editor
{
    /// <summary>
    /// Re-creates the Smart Irrigation Tablet's ZONES tab as two FIXED cards
    /// (Corn on the left, Wheat on the right), each with its own moisture %,
    /// health %, water-used readout, soil-state pill, flow bar, and
    /// ON / OFF / AUTO buttons. Bypasses the original template-clone path so
    /// the tab can never end up blank.
    ///
    /// Menu: <i>Tools › Smart Farm › Build Corn + Wheat Zone Cards (Force)</i>
    /// </summary>
    public static class DirectZoneCardsBuilderEditor
    {
        // ── Theme (same palette as the rest of the tablet) ──────────────────
        private static readonly Color BgCard        = new Color(0.07f, 0.16f, 0.18f, 0.96f);
        private static readonly Color BgBarTrack    = new Color(0.10f, 0.22f, 0.24f, 1f);
        private static readonly Color AccentGreen   = new Color(0.30f, 0.85f, 0.55f, 1f);
        private static readonly Color AccentBlue    = new Color(0.40f, 0.75f, 1f, 1f);
        private static readonly Color AccentRed     = new Color(0.92f, 0.30f, 0.25f, 1f);
        private static readonly Color TextPrimary   = new Color(0.94f, 1f, 0.96f, 1f);
        private static readonly Color TextSecondary = new Color(0.65f, 0.85f, 0.78f, 1f);

        [MenuItem("Tools/Smart Farm/Build Corn + Wheat Zone Cards (Force)", priority = 42)]
        public static void BuildDirectZoneCards()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Build Zone Cards",
                    "Stop Play mode before running this command.", "OK");
                return;
            }

            var tablet = GameObject.Find("SmartIrrigationTablet");
            if (tablet == null)
            {
                EditorUtility.DisplayDialog("Build Zone Cards",
                    "SmartIrrigationTablet not found.\nRun 'Tools › Smart Farm › Setup Smart Irrigation Tablet' first.",
                    "OK");
                return;
            }

            // ── Locate the ZonesPage and the controller ─────────────────────
            var ctrl    = tablet.GetComponent<SmartIrrigationTabletAppController>();
            var content = tablet.transform.Find("Content");
            var zonesPage = content != null ? content.Find("ZonesPage") : null;
            if (zonesPage == null)
            {
                EditorUtility.DisplayDialog("Build Zone Cards",
                    "Could not find Content/ZonesPage on the tablet.\n" +
                    "Run 'Tools › Smart Farm › Rebuild Smart Irrigation Tablet' first.",
                    "OK");
                return;
            }

            // ── Ensure the zone manager has Corn + Wheat ────────────────────
            var manager = SmartIrrigationTabletManager.Instance
                          ?? Object.FindFirstObjectByType<SmartIrrigationTabletManager>();
            var zoneManager = manager != null ? manager.Zones : Object.FindFirstObjectByType<IrrigationZoneManager>();
            if (zoneManager != null) EnsureCornAndWheat(zoneManager);

            // ── Wipe everything in the ZonesPage so we can rebuild ──────────
            Undo.RegisterFullObjectHierarchyUndo(zonesPage.gameObject, "Rebuild Zones Tab");
            for (int i = zonesPage.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(zonesPage.GetChild(i).gameObject);

            // Remove any prior page UI components so only the new one is active.
            var oldDirect = zonesPage.GetComponent<DirectZoneCardsUI>();
            if (oldDirect != null) Object.DestroyImmediate(oldDirect);
            var oldTemplate = zonesPage.GetComponent<IrrigationZonesPageUI>();
            if (oldTemplate != null) Object.DestroyImmediate(oldTemplate);

            // ── Title ───────────────────────────────────────────────────────
            MakeText(zonesPage, "ZonesTitle", "IRRIGATION ZONES",
                22, FontStyles.Bold,
                new Vector2(0.02f, 0.92f), new Vector2(0.98f, 0.99f),
                AccentGreen, TextAlignmentOptions.Left);

            // ── Build a card per zone (Corn left, Wheat right) ──────────────
            var direct = zonesPage.gameObject.AddComponent<DirectZoneCardsUI>();
            direct.SetManager(manager);

            var widgetsList = new List<DirectZoneCardsUI.ZoneCardWidgets>();

            // Two columns side-by-side, full-height available.
            var cornCard  = BuildCard(zonesPage, "CornZoneCard",  new Vector2(0.02f, 0.08f), new Vector2(0.49f, 0.90f),
                                     "Corn Field", "Corn", "zone_corn", out var cornWidgets);
            cornWidgets.zoneId = "zone_corn";
            widgetsList.Add(cornWidgets);

            var wheatCard = BuildCard(zonesPage, "WheatZoneCard", new Vector2(0.51f, 0.08f), new Vector2(0.98f, 0.90f),
                                     "Wheat Field", "Wheat", "zone_wheat", out var wheatWidgets);
            wheatWidgets.zoneId = "zone_wheat";
            widgetsList.Add(wheatWidgets);

            direct.SetCards(widgetsList);

            // ── XR layer so VR rays can click these buttons ─────────────────
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) SetLayerRecursive(zonesPage.gameObject, uiLayer);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            // Make sure ZONES tab still routes here.
            if (ctrl != null)
            {
                var serObj = new SerializedObject(ctrl);
                var pageProp = serObj.FindProperty("zonesPage");
                if (pageProp != null)
                {
                    pageProp.objectReferenceValue = zonesPage.gameObject;
                    serObj.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            EditorUtility.DisplayDialog("Zone Cards — Built",
                "ZONES tab now contains TWO cards:\n" +
                "  • Corn Field  → drives zone 'zone_corn'\n" +
                "  • Wheat Field → drives zone 'zone_wheat'\n\n" +
                "Each card has its own ON / OFF / AUTO buttons. Press Play and " +
                "open the ZONES tab — you can now control each zone independently.\n\n" +
                "Save the scene (Ctrl+S) to keep these changes.",
                "OK");

            Selection.activeGameObject = zonesPage.gameObject;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Zone seed
        // ─────────────────────────────────────────────────────────────────────

        private static void EnsureCornAndWheat(IrrigationZoneManager zm)
        {
            var listField = typeof(IrrigationZoneManager).GetField("zones",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (listField == null) return;
            var list = listField.GetValue(zm) as List<IrrigationZone>;
            if (list == null) { list = new List<IrrigationZone>(); listField.SetValue(zm, list); }

            if (!HasZone(list, "zone_corn"))
                list.Add(new IrrigationZone
                {
                    id = "zone_corn", displayName = "Corn Field",
                    cropType = CropType.Corn, waterPerTick = 6f,
                    lowMoistureThreshold = 30f, healthyMoistureThreshold = 60f, overwaterThreshold = 92f
                });
            if (!HasZone(list, "zone_wheat"))
                list.Add(new IrrigationZone
                {
                    id = "zone_wheat", displayName = "Wheat Field",
                    cropType = CropType.Wheat, waterPerTick = 5f,
                    lowMoistureThreshold = 30f, healthyMoistureThreshold = 60f, overwaterThreshold = 92f
                });
        }

        private static bool HasZone(List<IrrigationZone> list, string id)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].id == id) return true;
            return false;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Card builder
        // ─────────────────────────────────────────────────────────────────────

        private static GameObject BuildCard(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax,
            string displayName, string cropName, string zoneId,
            out DirectZoneCardsUI.ZoneCardWidgets widgets)
        {
            var card = MakePanel(parent, name, anchorMin, anchorMax, BgCard);

            widgets = new DirectZoneCardsUI.ZoneCardWidgets { zoneId = zoneId };

            // Header strip: name + crop + status pill
            widgets.zoneNameText = MakeText(card.transform, "ZoneName", displayName,
                22, FontStyles.Bold,
                new Vector2(0.04f, 0.85f), new Vector2(0.60f, 0.97f),
                TextPrimary, TextAlignmentOptions.Left);
            widgets.cropTypeText = MakeText(card.transform, "CropType", cropName,
                14, FontStyles.Italic,
                new Vector2(0.04f, 0.76f), new Vector2(0.60f, 0.86f),
                TextSecondary, TextAlignmentOptions.Left);

            widgets.statusLed = MakeColoredCircle(card.transform, "StatusLed",
                new Vector2(0.62f, 0.88f), new Vector2(0.70f, 0.97f),
                new Color(0.45f, 0.55f, 0.65f, 1f));
            widgets.statusText = MakeText(card.transform, "Status", "STANDBY",
                14, FontStyles.Bold,
                new Vector2(0.70f, 0.85f), new Vector2(0.97f, 0.97f),
                AccentGreen, TextAlignmentOptions.Right);

            // Soil-state pill (top right under status)
            var pill = MakePanel(card.transform, "SoilStatePill",
                new Vector2(0.62f, 0.75f), new Vector2(0.97f, 0.84f),
                AccentGreen);
            widgets.pillImage = pill.GetComponent<Image>();
            widgets.pillLabel = MakeText(pill.transform, "PillLabel", "Healthy",
                12, FontStyles.Bold,
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                Color.white, TextAlignmentOptions.Center);

            // Stats row: Moisture | Health | Water Used
            widgets.moistureText = MakeText(card.transform, "MoistureText",
                "<size=70%>Moisture</size>\n50%",
                15, FontStyles.Normal,
                new Vector2(0.04f, 0.50f), new Vector2(0.34f, 0.72f),
                TextPrimary, TextAlignmentOptions.Left);
            widgets.healthText = MakeText(card.transform, "HealthText",
                "<size=70%>Health</size>\n100%",
                15, FontStyles.Normal,
                new Vector2(0.34f, 0.50f), new Vector2(0.64f, 0.72f),
                TextPrimary, TextAlignmentOptions.Left);
            widgets.waterUsedText = MakeText(card.transform, "WaterUsedText",
                "<size=70%>Water Used</size>\n0 u",
                15, FontStyles.Normal,
                new Vector2(0.64f, 0.50f), new Vector2(0.97f, 0.72f),
                TextPrimary, TextAlignmentOptions.Left);

            // Moisture + Health progress bars (simple)
            widgets.moistureFill = BuildSimpleBar(card.transform, "MoistureBar",
                new Vector2(0.04f, 0.43f), new Vector2(0.64f, 0.49f), AccentBlue);
            widgets.healthFill = BuildSimpleBar(card.transform, "HealthBar",
                new Vector2(0.66f, 0.43f), new Vector2(0.97f, 0.49f), AccentGreen);

            // Reason
            widgets.reasonText = MakeText(card.transform, "ReasonText", "Auto: standby",
                12, FontStyles.Italic,
                new Vector2(0.04f, 0.34f), new Vector2(0.97f, 0.42f),
                new Color(0.65f, 0.85f, 0.78f, 0.85f),
                TextAlignmentOptions.Left);

            // Flow bar (full width)
            widgets.flowBar = BuildAnimatedFlowBar(card.transform, "FlowBar",
                new Vector2(0.04f, 0.22f), new Vector2(0.97f, 0.32f));

            // Mode buttons row
            widgets.onButton   = BuildLabeledButton(card.transform, "OnButton",   "ON",
                new Vector2(0.04f, 0.04f), new Vector2(0.32f, 0.18f), AccentGreen);
            widgets.offButton  = BuildLabeledButton(card.transform, "OffButton",  "OFF",
                new Vector2(0.34f, 0.04f), new Vector2(0.64f, 0.18f), AccentRed);
            widgets.autoButton = BuildLabeledButton(card.transform, "AutoButton", "AUTO",
                new Vector2(0.66f, 0.04f), new Vector2(0.97f, 0.18f), AccentBlue);

            return card;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Widget helpers
        // ─────────────────────────────────────────────────────────────────────

        private static GameObject MakePanel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = color;
            return go;
        }

        private static TMP_Text MakeText(Transform parent, string name, string value,
            float size, FontStyles style,
            Vector2 anchorMin, Vector2 anchorMax,
            Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = value;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            return t;
        }

        private static Image MakeColoredCircle(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            return img;
        }

        private static Image BuildSimpleBar(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var bg = MakePanel(parent, name, anchorMin, anchorMax, BgBarTrack);
            bg.GetComponent<Image>().raycastTarget = false;
            var fill = MakePanel(bg.transform, "Fill", Vector2.zero, Vector2.one, color);
            var fillImg = fill.GetComponent<Image>();
            fillImg.type        = Image.Type.Filled;
            fillImg.fillMethod  = Image.FillMethod.Horizontal;
            fillImg.fillOrigin  = (int)Image.OriginHorizontal.Left;
            fillImg.fillAmount  = 0.5f;
            fillImg.raycastTarget = false;
            return fillImg;
        }

        private static AnimatedFlowBar BuildAnimatedFlowBar(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var bar = MakePanel(parent, name, anchorMin, anchorMax, BgBarTrack);
            var trackImg = bar.GetComponent<Image>();
            trackImg.raycastTarget = false;

            var fill = MakePanel(bar.transform, "Fill", Vector2.zero, Vector2.one, AccentBlue);
            var fillImg = fill.GetComponent<Image>();
            fillImg.type        = Image.Type.Filled;
            fillImg.fillMethod  = Image.FillMethod.Horizontal;
            fillImg.fillOrigin  = (int)Image.OriginHorizontal.Left;
            fillImg.fillAmount  = 0f;
            fillImg.raycastTarget = false;

            var shineGO = new GameObject("Shine", typeof(RectTransform));
            shineGO.transform.SetParent(bar.transform, false);
            var shineRect = (RectTransform)shineGO.transform;
            shineRect.anchorMin = new Vector2(0f, 0f);
            shineRect.anchorMax = new Vector2(0.18f, 1f);
            shineRect.offsetMin = shineRect.offsetMax = Vector2.zero;
            var shineImg = shineGO.AddComponent<Image>();
            shineImg.color = new Color(1f, 1f, 1f, 0.18f);
            shineImg.raycastTarget = false;

            var animated = bar.AddComponent<AnimatedFlowBar>();
            animated.SetReferences(fillImg, trackImg, shineRect, shineImg);
            return animated;
        }

        private static Button BuildLabeledButton(Transform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color = color;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var textGO = new GameObject("Text", typeof(RectTransform));
            textGO.transform.SetParent(go.transform, false);
            var tr = (RectTransform)textGO.transform;
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = tr.offsetMax = Vector2.zero;
            var t = textGO.AddComponent<TextMeshProUGUI>();
            t.text          = label;
            t.fontSize      = 18;
            t.fontStyle     = FontStyles.Bold;
            t.color         = Color.white;
            t.alignment     = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            return btn;
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            for (int i = 0; i < go.transform.childCount; i++)
                SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
        }
    }
}
#endif
