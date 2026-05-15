#if UNITY_EDITOR
using System;
using System.Reflection;
using SmartFarm;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TrainingRoom.Editor
{
    /// <summary>
    /// One-click fix for the most common reason a VR controller ray can't click
    /// a world-space UI button (the "Enter Training Room" button, the video
    /// player Play/Stop/Next buttons, etc.).
    ///
    /// What it does on every world-space <see cref="Canvas"/> in the active scene:
    ///   • Adds a <c>TrackedDeviceGraphicRaycaster</c> alongside the regular
    ///     <see cref="GraphicRaycaster"/> — without this XR rays don't hit UI.
    ///   • Tags the canvas hierarchy with the <c>UI</c> layer.
    ///   • Sets <see cref="Canvas.worldCamera"/> to the scene's main camera.
    ///
    /// On every XR interactor (ray, poke, near-far, gaze):
    ///   • Enables <c>m_EnableUIInteraction</c>.
    ///   • Adds the UI layer bit to <c>m_RaycastMask</c>.
    ///
    /// On every <see cref="Button"/> in the scene: forces its target graphic's
    /// <see cref="Graphic.raycastTarget"/> back to true.
    ///
    /// On the scene's <see cref="EventSystem"/>: adds <c>XRUIInputModule</c>.
    ///
    /// Finally, spawns a single <see cref="VRUIInteractionAutoFix"/> in the
    /// scene so the same self-heal runs at runtime too (covers scenes loaded
    /// additively or assets imported later).
    ///
    /// Menu: <i>Tools › Training Room › Fix VR Controller Buttons</i>
    /// </summary>
    public static class TrainingRoomVRUIFixer
    {
        [MenuItem("Tools/Training Room/Fix VR Controller Buttons (Click If Buttons Don't Respond)", priority = 9)]
        public static void FixVRControllerButtons()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Fix VR Controller Buttons",
                    "Please stop Play mode before running this fix.", "OK");
                return;
            }

            var trackedRaycasterType = ResolveType(
                "UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster",
                "Unity.XR.Interaction.Toolkit");
            var xrUIInputModuleType = ResolveType(
                "UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule",
                "Unity.XR.Interaction.Toolkit");

            if (trackedRaycasterType == null)
            {
                EditorUtility.DisplayDialog("Fix VR Controller Buttons",
                    "Could not find the XR Interaction Toolkit's TrackedDeviceGraphicRaycaster.\n\n" +
                    "Make sure 'XR Interaction Toolkit' is installed via the Package Manager.",
                    "OK");
                return;
            }

            int canvasesFixed = FixWorldCanvases(trackedRaycasterType);
            int buttonsFixed  = FixButtons();
            bool eventSysOk   = FixEventSystem(xrUIInputModuleType);
            int interactorsFixed = FixInteractors();
            EnsureRuntimeAutoFix();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            string detail =
                $"World canvases upgraded:   {canvasesFixed}\n" +
                $"Button raycasts revived:   {buttonsFixed}\n" +
                $"Event System updated:      {(eventSysOk ? "yes" : "no")}\n" +
                $"XR interactors fixed:      {interactorsFixed}";

            Debug.Log("[TrainingRoomVRUIFixer] " + detail.Replace("\n", "  ·  "));

            EditorUtility.DisplayDialog(
                "VR Controller Buttons — Fixed!",
                "Your scene's UI should now respond to VR controller rays and pokes.\n\n" +
                detail + "\n\n" +
                "A 'VRUIInteractionAutoFix' helper was added to the scene so this " +
                "will self-heal on Play in the future. Save the scene (Ctrl+S) to " +
                "keep these changes.",
                "OK");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Canvas / EventSystem / Buttons / Interactors
        // ─────────────────────────────────────────────────────────────────────

        private static int FixWorldCanvases(Type trackedRaycasterType)
        {
            int uiLayer = LayerMask.NameToLayer("UI");
            var cam     = Camera.main != null ? Camera.main : UnityEngine.Object.FindFirstObjectByType<Camera>();

            int count = 0;
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                var c = canvases[i];
                if (c == null || c.renderMode != RenderMode.WorldSpace) continue;

                bool changed = false;

                if (c.worldCamera == null && cam != null)
                {
                    Undo.RecordObject(c, "Set Canvas worldCamera");
                    c.worldCamera = cam;
                    changed = true;
                }

                if (c.GetComponent<GraphicRaycaster>() == null)
                {
                    Undo.AddComponent<GraphicRaycaster>(c.gameObject);
                    changed = true;
                }

                if (c.GetComponent(trackedRaycasterType) == null)
                {
                    Undo.AddComponent(c.gameObject, trackedRaycasterType);
                    changed = true;
                }

                if (uiLayer >= 0)
                {
                    if (c.gameObject.layer != uiLayer || HasChildOnOtherLayer(c.transform, uiLayer))
                    {
                        Undo.RegisterFullObjectHierarchyUndo(c.gameObject, "Set UI Layer");
                        SetLayerRecursive(c.gameObject, uiLayer);
                        changed = true;
                    }
                }

                if (changed) count++;
            }
            return count;
        }

        private static int FixButtons()
        {
            int count = 0;
            var buttons = UnityEngine.Object.FindObjectsByType<Button>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < buttons.Length; i++)
            {
                var btn = buttons[i];
                if (btn == null) continue;

                var g = btn.targetGraphic;
                if (g != null && !g.raycastTarget)
                {
                    Undo.RecordObject(g, "Enable Button RaycastTarget");
                    g.raycastTarget = true;
                    count++;
                }
                var img = btn.GetComponent<Image>();
                if (img != null && !img.raycastTarget)
                {
                    Undo.RecordObject(img, "Enable Image RaycastTarget");
                    img.raycastTarget = true;
                    count++;
                }
                if (!btn.interactable)
                {
                    Undo.RecordObject(btn, "Re-enable Button Interactable");
                    btn.interactable = true;
                    count++;
                }
            }
            return count;
        }

        private static bool FixEventSystem(Type xrUIInputModuleType)
        {
            var es = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("EventSystem");
                Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
                es = go.AddComponent<EventSystem>();
            }

            if (xrUIInputModuleType != null && es.GetComponent(xrUIInputModuleType) == null)
            {
                Undo.AddComponent(es.gameObject, xrUIInputModuleType);
            }

            // Keep StandaloneInputModule for editor mouse testing — don't remove it.
            return true;
        }

        private static int FixInteractors()
        {
            int uiLayer = LayerMask.NameToLayer("UI");
            int uiBit   = uiLayer >= 0 ? (1 << uiLayer) : 0;

            int count = 0;
            var allBehaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < allBehaviours.Length; i++)
            {
                var comp = allBehaviours[i];
                if (comp == null) continue;
                if (!comp.GetType().Name.Contains("Interactor")) continue;

                var so = new SerializedObject(comp);

                var uiProp = so.FindProperty("m_EnableUIInteraction");
                if (uiProp != null && !uiProp.boolValue)
                {
                    uiProp.boolValue = true;
                    count++;
                }

                if (uiBit > 0)
                {
                    OrLayerMaskProperty(so, "m_RaycastMask",  uiBit);
                    OrLayerMaskProperty(so, "m_BlockingMask", uiBit);
                }

                so.ApplyModifiedProperties();
            }
            return count;
        }

        private static void OrLayerMaskProperty(SerializedObject so, string propertyName, int bit)
        {
            var prop = so.FindProperty(propertyName);
            if (prop == null) return;
            var bits = prop.FindPropertyRelative("m_Bits") ?? prop;
            if (bits.propertyType == SerializedPropertyType.LayerMask
                || bits.propertyType == SerializedPropertyType.Integer)
            {
                int newVal = bits.intValue | bit;
                if (newVal != bits.intValue) bits.intValue = newVal;
            }
        }

        private static void EnsureRuntimeAutoFix()
        {
            if (UnityEngine.Object.FindFirstObjectByType<VRUIInteractionAutoFix>() != null) return;
            var go = new GameObject("VRUIInteractionAutoFix");
            Undo.RegisterCreatedObjectUndo(go, "Add VRUIInteractionAutoFix");
            go.AddComponent<VRUIInteractionAutoFix>();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static Type ResolveType(string fullName, string assemblyName)
        {
            var t = Type.GetType($"{fullName}, {assemblyName}");
            if (t != null) return t;
            var asms = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < asms.Length; i++)
            {
                t = asms[i].GetType(fullName, throwOnError: false, ignoreCase: false);
                if (t != null) return t;
            }
            return null;
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            if (go == null) return;
            go.layer = layer;
            for (int i = 0; i < go.transform.childCount; i++)
                SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
        }

        private static bool HasChildOnOtherLayer(Transform t, int layer)
        {
            for (int i = 0; i < t.childCount; i++)
            {
                var c = t.GetChild(i);
                if (c == null) continue;
                if (c.gameObject.layer != layer) return true;
                if (HasChildOnOtherLayer(c, layer)) return true;
            }
            return false;
        }
    }
}
#endif
