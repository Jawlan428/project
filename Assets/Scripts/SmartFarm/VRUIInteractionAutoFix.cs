using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SmartFarm
{
    /// <summary>
    /// Runtime self-healing component that makes every world-space Canvas in the
    /// scene respond to VR controller rays and pokes.
    ///
    /// Drop ONE of these anywhere in the scene (or let the
    /// <c>Tools › Training Room › Fix VR Controller Buttons</c> menu add it
    /// automatically). On <see cref="Awake"/> it does five things:
    ///
    ///   1. For every <see cref="Canvas"/> in <c>RenderMode.WorldSpace</c>:
    ///        • Adds a <c>UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster</c>
    ///          alongside the existing <see cref="GraphicRaycaster"/> (via reflection so
    ///          this file compiles even if XRI is missing or its assembly name changes
    ///          between versions).
    ///        • Sets <c>Canvas.worldCamera</c> to the main camera (improves draw order).
    ///        • Tags the canvas hierarchy with the "UI" layer so XRI raycast masks
    ///          actually include it.
    ///   2. Ensures every <see cref="Image"/> on a <see cref="Button"/>'s target
    ///      graphic has <c>raycastTarget = true</c> (Unity silently disables this
    ///      sometimes when assets are imported with optimisation flags).
    ///   3. Ensures the scene has an <see cref="EventSystem"/> and that it carries
    ///      an <c>XRUIInputModule</c> in addition to / instead of the legacy
    ///      <c>StandaloneInputModule</c>.
    ///   4. Iterates every XRI interactor in the scene (ray, poke, near-far, gaze)
    ///      and forces <c>m_EnableUIInteraction = true</c>; it also adds the UI
    ///      layer to its <c>m_RaycastMask</c>.
    ///   5. Wakes up disabled raycasters created by the editor setup wizards.
    ///
    /// Quest VR friendly: scans run ONCE on Awake (no per-frame work). Reflection
    /// failures are silenced — the script is a no-op when the XR Interaction
    /// Toolkit isn't present.
    /// </summary>
    [AddComponentMenu("SmartFarm/VR/UI Interaction Auto Fix")]
    [DefaultExecutionOrder(-100)]
    public class VRUIInteractionAutoFix : MonoBehaviour
    {
        [Header("Behaviour")]
        [Tooltip("Run the fix when the scene loads. Disable if you only want to invoke it from code.")]
        [SerializeField] private bool runOnAwake = true;

        [Tooltip("Re-run the fix every time this component is enabled. Helpful in additive-scene loading flows.")]
        [SerializeField] private bool runOnEnable = false;

        [Tooltip("Log a summary of what was fixed to the Console.")]
        [SerializeField] private bool verboseLogging = true;

        [Header("Layer")]
        [Tooltip("Layer name applied to every world canvas hierarchy so XR raycasters can see them.")]
        [SerializeField] private string uiLayerName = "UI";

        // ── Cached reflection lookups ────────────────────────────────────────

        private static Type _trackedRaycasterType;
        private static Type _xrUIInputModuleType;
        private static bool _reflectionResolved;

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (runOnAwake) Run();
        }

        private void OnEnable()
        {
            if (runOnEnable) Run();
        }

        [ContextMenu("Run Fix Now")]
        public void Run()
        {
            ResolveReflection();

            int canvasesFixed     = FixWorldCanvases();
            int buttonsRevived    = ReviveButtonRaycasts();
            bool eventSystemFixed = FixEventSystem();
            int interactorsFixed  = FixInteractors();

            if (verboseLogging)
            {
                Debug.Log(
                    $"[VRUIAutoFix] Done. Canvases fixed: {canvasesFixed}, " +
                    $"buttons revived: {buttonsRevived}, " +
                    $"event system updated: {eventSystemFixed}, " +
                    $"XR interactors fixed: {interactorsFixed}.");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  World Canvases
        // ─────────────────────────────────────────────────────────────────────

        private int FixWorldCanvases()
        {
            int uiLayer = LayerMask.NameToLayer(uiLayerName);
            var camera  = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();

            int count = 0;
            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                var c = canvases[i];
                if (c == null || c.renderMode != RenderMode.WorldSpace) continue;

                bool changed = false;

                if (c.worldCamera == null && camera != null)
                {
                    c.worldCamera = camera;
                    changed = true;
                }

                // Ensure a GraphicRaycaster exists (XR raycaster derives from it indirectly).
                if (c.GetComponent<GraphicRaycaster>() == null)
                {
                    c.gameObject.AddComponent<GraphicRaycaster>();
                    changed = true;
                }

                // Add the XR TrackedDeviceGraphicRaycaster if available + missing.
                if (_trackedRaycasterType != null
                    && c.GetComponent(_trackedRaycasterType) == null)
                {
                    try
                    {
                        c.gameObject.AddComponent(_trackedRaycasterType);
                        changed = true;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[VRUIAutoFix] Failed to add TrackedDeviceGraphicRaycaster to '{c.name}': {ex.Message}");
                    }
                }

                if (uiLayer >= 0)
                {
                    SetLayerRecursive(c.gameObject, uiLayer);
                }

                if (changed) count++;
            }
            return count;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Button raycast revival
        // ─────────────────────────────────────────────────────────────────────

        private static int ReviveButtonRaycasts()
        {
            int count = 0;
            var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < buttons.Length; i++)
            {
                var btn = buttons[i];
                if (btn == null) continue;
                var graphic = btn.targetGraphic;
                if (graphic != null && !graphic.raycastTarget)
                {
                    graphic.raycastTarget = true;
                    count++;
                }
                // Many setups also disable raycast on the Image directly.
                var img = btn.GetComponent<Image>();
                if (img != null && !img.raycastTarget)
                {
                    img.raycastTarget = true;
                    count++;
                }
            }
            return count;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Event System / XR UI Input Module
        // ─────────────────────────────────────────────────────────────────────

        private bool FixEventSystem()
        {
            var es = FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("EventSystem");
                es = go.AddComponent<EventSystem>();
                if (verboseLogging)
                    Debug.Log("[VRUIAutoFix] Spawned a new EventSystem (none existed in the scene).");
            }

            if (_xrUIInputModuleType == null) return true;

            // Add the XR module if missing; keep StandaloneInputModule too so editor
            // testing with mouse still works.
            if (es.GetComponent(_xrUIInputModuleType) == null)
            {
                try
                {
                    es.gameObject.AddComponent(_xrUIInputModuleType);
                    if (verboseLogging) Debug.Log("[VRUIAutoFix] Added XRUIInputModule to the EventSystem.");
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[VRUIAutoFix] Failed to add XRUIInputModule: {ex.Message}");
                    return false;
                }
            }
            return true;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  XR Interactors
        // ─────────────────────────────────────────────────────────────────────

        private int FixInteractors()
        {
            int count = 0;
            int uiLayer = LayerMask.NameToLayer(uiLayerName);
            int uiBit   = uiLayer >= 0 ? (1 << uiLayer) : 0;

            var allBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < allBehaviours.Length; i++)
            {
                var comp = allBehaviours[i];
                if (comp == null) continue;
                var t = comp.GetType();

                // We only touch components whose type name resembles an XRI interactor —
                // cheap heuristic so we don't reflect every single script.
                string typeName = t.Name;
                if (!typeName.Contains("Interactor")) continue;

                if (TrySetBoolField(comp, "m_EnableUIInteraction", true)
                    | TrySetBoolField(comp, "enableUIInteraction", true))
                {
                    count++;
                }

                // OR the UI layer into the raycast mask if exposed.
                if (uiBit > 0)
                {
                    AddBitToLayerMaskField(comp, "m_RaycastMask",   uiBit);
                    AddBitToLayerMaskField(comp, "raycastMask",     uiBit);
                    AddBitToLayerMaskField(comp, "m_BlockingMask",  uiBit);
                }
            }
            return count;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Reflection helpers
        // ─────────────────────────────────────────────────────────────────────

        private static void ResolveReflection()
        {
            if (_reflectionResolved) return;
            _reflectionResolved = true;

            _trackedRaycasterType = ResolveType(
                "UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster",
                "Unity.XR.Interaction.Toolkit");

            _xrUIInputModuleType = ResolveType(
                "UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule",
                "Unity.XR.Interaction.Toolkit");
        }

        private static Type ResolveType(string fullName, string assemblyName)
        {
            // Try fully-qualified first
            var t = Type.GetType($"{fullName}, {assemblyName}");
            if (t != null) return t;

            // Fall back to scanning loaded assemblies (handles renames across XRI versions)
            var asms = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < asms.Length; i++)
            {
                t = asms[i].GetType(fullName, throwOnError: false, ignoreCase: false);
                if (t != null) return t;
            }
            return null;
        }

        private static bool TrySetBoolField(object obj, string fieldName, bool value)
        {
            if (obj == null) return false;
            var f = obj.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f != null && f.FieldType == typeof(bool))
            {
                if ((bool)f.GetValue(obj) == value) return false;
                f.SetValue(obj, value);
                return true;
            }
            // Try property setter as well
            var p = obj.GetType().GetProperty(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (p != null && p.CanWrite && p.PropertyType == typeof(bool))
            {
                if ((bool)p.GetValue(obj) == value) return false;
                p.SetValue(obj, value);
                return true;
            }
            return false;
        }

        private static void AddBitToLayerMaskField(object obj, string fieldName, int bit)
        {
            if (obj == null) return;
            var f = obj.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f == null) return;
            if (f.FieldType == typeof(LayerMask))
            {
                var lm = (LayerMask)f.GetValue(obj);
                int newVal = lm.value | bit;
                if (lm.value != newVal)
                {
                    f.SetValue(obj, (LayerMask)newVal);
                }
            }
            else if (f.FieldType == typeof(int))
            {
                int val = (int)f.GetValue(obj);
                int newVal = val | bit;
                if (val != newVal) f.SetValue(obj, newVal);
            }
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            if (go == null) return;
            go.layer = layer;
            for (int i = 0; i < go.transform.childCount; i++)
                SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
        }
    }
}
