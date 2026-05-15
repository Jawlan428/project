#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using SmartFarm.Irrigation.Sustainability;
using SmartFarm.Irrigation.Sustainability.UI;
using SmartFarm.Irrigation.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SmartFarm.Irrigation.Editor
{
    /// <summary>
    /// Repair utilities for the Smart Irrigation Tablet:
    ///
    ///   <i>Tools › Smart Farm › Repair Zones Tab</i>
    ///     • Adds <b>Corn</b> and <b>Wheat</b> zones to <see cref="IrrigationZoneManager"/>
    ///       if they are missing.
    ///     • Re-wires the <see cref="IrrigationZonesPageUI"/> references in case the
    ///       tablet hierarchy was edited and lost them.
    ///     • Forces a Rebuild of the zones list so the Corn / Wheat cards appear
    ///       on the ZONES tab with per-zone Off / On / Auto controls.
    ///
    ///   <i>Tools › Smart Farm › Reposition Eco Alert Popup (Fix Tab Overlap)</i>
    ///     • Moves an existing <see cref="EcoAlertPopupUI"/> above the tablet so it
    ///       no longer covers the OVERVIEW / ZONES / ANALYTICS / ALERTS / SUSTAIN
    ///       tab buttons.
    /// </summary>
    public static class IrrigationZonesRepairEditor
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Repair Zones Tab
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("Tools/Smart Farm/Repair Zones Tab (Add Corn + Wheat)", priority = 40)]
        public static void RepairZonesTab()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Repair Zones Tab",
                    "Stop Play mode before running this repair.", "OK");
                return;
            }

            var manager = Object.FindFirstObjectByType<SmartIrrigationTabletManager>();
            if (manager == null)
            {
                EditorUtility.DisplayDialog("Repair Zones Tab",
                    "SmartIrrigationTabletManager not found.\n\n" +
                    "Run 'Tools › Smart Farm › Setup Smart Irrigation Tablet' first.",
                    "OK");
                return;
            }

            var zoneManager = (IrrigationZoneManager)GetFieldValue(manager, "zoneManager");
            if (zoneManager == null) zoneManager = Object.FindFirstObjectByType<IrrigationZoneManager>();
            if (zoneManager == null)
            {
                EditorUtility.DisplayDialog("Repair Zones Tab",
                    "IrrigationZoneManager not found on the hub.", "OK");
                return;
            }

            int added = EnsureDefaultZones(zoneManager);

            // Try to bind pipeRoot / sprinklerRoot from SmartIrrigationSceneVisuals.
            zoneManager.TryBindSceneVisualRoots();

            // Refresh the visual feedback cache so it picks up any newly-bound roots.
            var visuals = Object.FindFirstObjectByType<IrrigationVisualFeedback>();
            if (visuals != null) visuals.RefreshCache();

            int cardsBuilt = RebuildZonesPage();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            EditorUtility.DisplayDialog("Repair Zones Tab — Done",
                $"Zones in the manager now: {zoneManager.Zones.Count}\n" +
                $"Newly added by this repair: {added}\n" +
                $"Cards rebuilt on the ZONES tab: {cardsBuilt}\n\n" +
                "Open the ZONES tab in Play mode — each card has its own " +
                "OFF / ON / AUTO buttons so you can drive Corn and Wheat zones " +
                "individually. Save the scene (Ctrl+S) to keep these changes.",
                "OK");
        }

        private static int EnsureDefaultZones(IrrigationZoneManager zoneManager)
        {
            var zonesList = (List<IrrigationZone>)GetFieldValue(zoneManager, "zones");
            if (zonesList == null)
            {
                zonesList = new List<IrrigationZone>();
                SetField(zoneManager, "zones", zonesList);
            }

            int added = 0;

            if (!ZoneExists(zonesList, "zone_corn") && !ZoneOfTypeExists(zonesList, CropType.Corn))
            {
                zonesList.Add(new IrrigationZone
                {
                    id = "zone_corn",
                    displayName = "Corn Field",
                    cropType = CropType.Corn,
                    waterPerTick = 6f,
                    lowMoistureThreshold = 30f,
                    healthyMoistureThreshold = 60f,
                    overwaterThreshold = 92f
                });
                added++;
            }

            if (!ZoneExists(zonesList, "zone_wheat") && !ZoneOfTypeExists(zonesList, CropType.Wheat))
            {
                zonesList.Add(new IrrigationZone
                {
                    id = "zone_wheat",
                    displayName = "Wheat Field",
                    cropType = CropType.Wheat,
                    waterPerTick = 5f,
                    lowMoistureThreshold = 30f,
                    healthyMoistureThreshold = 60f,
                    overwaterThreshold = 92f
                });
                added++;
            }

            return added;
        }

        private static bool ZoneExists(List<IrrigationZone> list, string id)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].id == id) return true;
            return false;
        }

        private static bool ZoneOfTypeExists(List<IrrigationZone> list, CropType type)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].cropType == type) return true;
            return false;
        }

        private static int RebuildZonesPage()
        {
            var pages = Object.FindObjectsByType<IrrigationZonesPageUI>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            int total = 0;
            foreach (var page in pages)
            {
                if (page == null) continue;

                // Force-resolve children if references got lost.
                EnsurePageReferences(page);

                // Manager.ZoneSnapshots may be empty until Play, so build from the
                // zone list directly.
                var manager = SmartIrrigationTabletManager.Instance
                              ?? Object.FindFirstObjectByType<SmartIrrigationTabletManager>();
                if (manager == null || manager.Zones == null) continue;

                var snapshots = new List<IrrigationZoneSnapshot>();
                var zones = manager.Zones.Zones;
                for (int i = 0; i < zones.Count; i++)
                    if (zones[i] != null) snapshots.Add(zones[i].Snapshot(null));

                page.Rebuild(snapshots);
                total += snapshots.Count;
            }
            return total;
        }

        private static void EnsurePageReferences(IrrigationZonesPageUI page)
        {
            // Re-wire listRoot if the inspector reference was lost
            var listField = typeof(IrrigationZonesPageUI).GetField("listRoot",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (listField != null)
            {
                var current = listField.GetValue(page) as RectTransform;
                if (current == null)
                {
                    var t = page.transform.Find("ZoneScroll/Viewport/ListRoot");
                    if (t != null) listField.SetValue(page, t);
                }
            }

            // Re-wire cardTemplate
            var templateField = typeof(IrrigationZonesPageUI).GetField("cardTemplate",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (templateField != null)
            {
                var current = templateField.GetValue(page) as IrrigationZoneCardUI;
                if (current == null)
                {
                    var t = page.transform.Find("ZoneCardTemplate");
                    if (t != null) templateField.SetValue(page, t.GetComponent<IrrigationZoneCardUI>());
                }
            }

            // Re-wire empty-state label
            var emptyField = typeof(IrrigationZonesPageUI).GetField("emptyStateLabel",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (emptyField != null)
            {
                var current = emptyField.GetValue(page) as GameObject;
                if (current == null)
                {
                    var t = page.transform.Find("EmptyState");
                    if (t != null) emptyField.SetValue(page, t.gameObject);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Reposition Eco Alert Popup
        // ─────────────────────────────────────────────────────────────────────

        // ─────────────────────────────────────────────────────────────────────
        //  Merge Eco Alerts into the Alerts tab
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("Tools/Smart Farm/Move Eco Alerts Into Alerts Tab", priority = 42)]
        public static void MergeEcoAlertsIntoAlertsTab()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Move Eco Alerts",
                    "Stop Play mode before running this fix.", "OK");
                return;
            }

            var page = Object.FindFirstObjectByType<IrrigationAlertsPageUI>(FindObjectsInactive.Include);
            if (page == null)
            {
                EditorUtility.DisplayDialog("Move Eco Alerts",
                    "IrrigationAlertsPageUI not found.\nRun 'Tools › Smart Farm › Setup Smart Irrigation Tablet' first.",
                    "OK");
                return;
            }

            var eco = Object.FindFirstObjectByType<EcoAlertManager>(FindObjectsInactive.Include);
            if (eco == null)
            {
                EditorUtility.DisplayDialog("Move Eco Alerts",
                    "EcoAlertManager not found.\nRun 'Tools › Smart Farm › Setup Sustainability Monitor' first.",
                    "OK");
                return;
            }

            // Wire ecoAlerts into the Alerts page via SerializedObject so the
            // reference survives play / scene saves.
            var so = new SerializedObject(page);
            var prop = so.FindProperty("ecoAlerts");
            if (prop != null)
            {
                prop.objectReferenceValue = eco;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                page.SetEcoAlertManager(eco);
            }

            // Hide the floating popup overlay since alerts now live inside the
            // ALERTS tab. We disable rather than delete so it can be re-enabled
            // later if you change your mind.
            int hidden = 0;
            var popups = Object.FindObjectsByType<EcoAlertPopupUI>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < popups.Length; i++)
            {
                if (popups[i] == null) continue;
                Undo.RecordObject(popups[i].gameObject, "Hide Eco Alert Popup");
                popups[i].gameObject.SetActive(false);
                hidden++;
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            EditorUtility.DisplayDialog("Eco Alerts — Merged",
                $"Eco Alert Manager is now wired into the ALERTS tab.\n" +
                $"Hidden {hidden} floating popup overlay(s).\n\n" +
                "Press Play and open the ALERTS tab — every alert (irrigation + " +
                "eco) appears in a single sorted list, newest on top. The 'X alerts' " +
                "counter in the tablet header reflects irrigation alerts only.",
                "OK");
        }

        [MenuItem("Tools/Smart Farm/Reposition Eco Alert Popup (Fix Tab Overlap)", priority = 41)]
        public static void RepositionEcoAlertPopup()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Reposition Popup",
                    "Stop Play mode before running this fix.", "OK");
                return;
            }

            var popups = Object.FindObjectsByType<EcoAlertPopupUI>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (popups == null || popups.Length == 0)
            {
                EditorUtility.DisplayDialog("Reposition Popup",
                    "No EcoAlertPopupUI found in the scene.\n\n" +
                    "Run 'Tools › Smart Farm › Setup Sustainability Monitor' first.",
                    "OK");
                return;
            }

            int updated = 0;
            for (int i = 0; i < popups.Length; i++)
            {
                var popup = popups[i];
                if (popup == null) continue;
                var rt = popup.transform as RectTransform;
                if (rt == null) continue;

                Undo.RecordObject(rt, "Reposition Eco Alert Popup");
                rt.anchorMin       = new Vector2(0.15f, 1f);
                rt.anchorMax       = new Vector2(0.85f, 1f);
                rt.pivot           = new Vector2(0.5f, 0f);
                rt.sizeDelta       = new Vector2(0f, 70f);
                rt.anchoredPosition = new Vector2(0f, 12f);
                updated++;
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            EditorUtility.DisplayDialog("Reposition Popup — Done",
                $"Moved {updated} eco-alert popup(s) above the tab bar so they no " +
                "longer overlap OVERVIEW / ZONES / ANALYTICS / ALERTS / SUSTAIN.\n\n" +
                "Save the scene (Ctrl+S) to keep these changes.",
                "OK");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Reflection helpers
        // ─────────────────────────────────────────────────────────────────────

        private static object GetFieldValue(object target, string fieldName)
        {
            if (target == null) return null;
            var f = target.GetType().GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            return f?.GetValue(target);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            if (target == null) return;
            var f = target.GetType().GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            f?.SetValue(target, value);
        }
    }
}
#endif
