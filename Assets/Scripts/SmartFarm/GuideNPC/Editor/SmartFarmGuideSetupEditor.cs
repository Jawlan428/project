using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;

namespace SmartFarm.GuideNPC.EditorTools
{
    /// <summary>
    /// One-click setup for the Smart Farm Guide NPC.
    ///
    /// Select the Gardner Avatar (a humanoid GameObject) in the scene, then run
    /// <b>Tools ▸ Smart Farm ▸ Guide NPC ▸ Setup Guide From Selection</b>. This:
    ///   • Adds a NavMeshAgent (walking speed only), capsule collider, AudioSource
    ///     and XR Simple Interactable.
    ///   • Builds an Animator Controller with Idle / Walk / Greet / Point states
    ///     (NO run state) and auto-assigns matching clips from the avatar.
    ///   • Creates the four destination markers (CropFieldTarget, MeetingAreaTarget,
    ///     SmartScreensTarget, TrainingRoomTarget) if they don't exist.
    ///   • Adds the floating VR menu and an XR-ready EventSystem.
    ///   • Wires everything together.
    /// </summary>
    public static class SmartFarmGuideSetupEditor
    {
        private const string GeneratedFolder = "Assets/Scripts/SmartFarm/GuideNPC/Generated";
        private const string ControllerPath  = GeneratedFolder + "/SmartFarmGuide.controller";
        private const string DestinationsRootName = "GuideDestinations";

        private const float SpeedThreshold = 0.1f;

        // ─────────────────────────────────────────────────────────────────────
        //  Menu items
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("Tools/Smart Farm/Guide NPC/Setup Guide From Selection", priority = 0)]
        public static void SetupFromSelection()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[GuideNPC] Stop Play mode before running setup.");
                return;
            }

            var avatar = Selection.activeGameObject;
            if (avatar == null || !avatar.scene.IsValid())
            {
                EditorUtility.DisplayDialog("Smart Farm Guide",
                    "Select the Gardner Avatar (a humanoid GameObject placed in the scene) first, then run this again.",
                    "OK");
                return;
            }

            SetupGuide(avatar);
            EditorSceneManager.MarkSceneDirty(avatar.scene);
            Selection.activeGameObject = avatar;
            Debug.Log("[GuideNPC] Smart Farm Guide setup complete on '" + avatar.name + "'.");
        }

        [MenuItem("Tools/Smart Farm/Guide NPC/Create Destination Targets", priority = 20)]
        public static void CreateDestinationsMenu()
        {
            if (Application.isPlaying) return;
            var anchor = Selection.activeGameObject != null ? Selection.activeGameObject.transform.position
                                                            : Vector3.zero;
            EnsureDestinations(anchor);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[GuideNPC] Destination targets created / verified.");
        }

        [MenuItem("Tools/Smart Farm/Guide NPC/Rebuild Animator Controller", priority = 21)]
        public static void RebuildControllerMenu()
        {
            if (Application.isPlaying) return;
            var avatar = Selection.activeGameObject;
            var folders = avatar != null ? ClipSearchFolders(avatar) : null;
            var controller = BuildAnimatorController(folders);
            if (avatar != null)
            {
                var anim = avatar.GetComponentInChildren<Animator>();
                if (anim != null) anim.runtimeAnimatorController = controller;
            }
            Debug.Log("[GuideNPC] Animator Controller rebuilt at " + ControllerPath);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Core setup
        // ─────────────────────────────────────────────────────────────────────

        public static void SetupGuide(GameObject avatar)
        {
            Undo.RegisterFullObjectHierarchyUndo(avatar, "Setup Smart Farm Guide");

            // 1. Animator -----------------------------------------------------
            var animator = avatar.GetComponentInChildren<Animator>();
            if (animator == null) animator = avatar.AddComponent<Animator>();
            animator.applyRootMotion = false; // NavMeshAgent drives position, not root motion.

            var folders = ClipSearchFolders(avatar);
            var controller = BuildAnimatorController(folders);
            animator.runtimeAnimatorController = controller;

            // 2. Collider (for XR ray/poke + general physics) -----------------
            var capsule = avatar.GetComponent<CapsuleCollider>();
            if (capsule == null) capsule = avatar.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.9f, 0f);
            capsule.height = 1.8f;
            capsule.radius = 0.3f;
            capsule.isTrigger = false;

            // 3. NavMeshAgent (walking only) ----------------------------------
            var agent = avatar.GetComponent<NavMeshAgent>();
            if (agent == null) agent = avatar.AddComponent<NavMeshAgent>();
            agent.speed = 1.4f;            // walking speed (1.2–1.8 recommended)
            agent.angularSpeed = 220f;     // smooth turning
            agent.acceleration = 6f;       // moderate
            agent.stoppingDistance = 1.5f;
            agent.radius = 0.3f;
            agent.height = 1.8f;
            agent.autoBraking = true;

            // 4. Audio --------------------------------------------------------
            var audio = avatar.GetComponent<AudioSource>();
            if (audio == null) audio = avatar.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;

            // 5. Guide controller --------------------------------------------
            var guide = avatar.GetComponent<SmartFarmGuideNPC>();
            if (guide == null) guide = avatar.AddComponent<SmartFarmGuideNPC>();

            // 6. Destinations -------------------------------------------------
            var targets = EnsureDestinations(avatar.transform.position);
            WireDestinations(guide, targets);

            // 7. Floating menu ------------------------------------------------
            var menu = avatar.GetComponentInChildren<GuideMenuUI>(true);
            if (menu == null)
            {
                var menuGO = new GameObject("GuideMenu");
                Undo.RegisterCreatedObjectUndo(menuGO, "Create Guide Menu");
                menuGO.transform.SetParent(avatar.transform, false);
                menu = menuGO.AddComponent<GuideMenuUI>();
            }

            // 8. Wire references on the guide --------------------------------
            SetField(guide, "animator", animator);
            SetField(guide, "menu", menu);
            var interactable = avatar.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            if (interactable == null)
                interactable = avatar.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            SetField(guide, "interactable", interactable);
            SetField(guide, "voiceSource", audio);
            EditorUtility.SetDirty(guide);

            // 9. XR-ready EventSystem ----------------------------------------
            EnsureXrEventSystem();

            // 10. Layer the avatar collider so XR ray interactors can hit it.
            //     (Leave the avatar on its own layer; XR ray masks usually include Default.)
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Animator Controller generation (Idle / Walk / Greet / Point — no Run)
        // ─────────────────────────────────────────────────────────────────────

        public static AnimatorController BuildAnimatorController(string[] searchFolders)
        {
            EnsureFolder(GeneratedFolder);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Greet", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Point", AnimatorControllerParameterType.Trigger);

            var sm = controller.layers[0].stateMachine;

            // Locate clips (run/jog/sprint are deliberately ignored).
            var idleClip  = FindClip(searchFolders, new[] { "idle", "stand", "breath" }, null);
            var walkClip  = FindClip(searchFolders, new[] { "walk" }, new[] { "run", "jog", "sprint", "back" });
            var greetClip = FindClip(searchFolders, new[] { "greet", "wave", "hello", "hi", "salute" }, null);
            var pointClip = FindClip(searchFolders, new[] { "point", "present", "show" }, null);

            // States.
            var idle = sm.AddState("Idle");
            idle.motion = idleClip;
            sm.defaultState = idle;

            var walk = sm.AddState("Walk");
            walk.motion = walkClip;

            // Idle ↔ Walk driven by Speed.
            var toWalk = idle.AddTransition(walk);
            toWalk.hasExitTime = false;
            toWalk.duration = 0.15f;
            toWalk.AddCondition(AnimatorConditionMode.Greater, SpeedThreshold, "Speed");

            var toIdle = walk.AddTransition(idle);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.2f;
            toIdle.AddCondition(AnimatorConditionMode.Less, SpeedThreshold, "Speed");

            // Greet (Any State → Greet → Idle), only if a clip exists.
            if (greetClip != null)
            {
                var greet = sm.AddState("Greet");
                greet.motion = greetClip;

                var anyToGreet = sm.AddAnyStateTransition(greet);
                anyToGreet.hasExitTime = false;
                anyToGreet.duration = 0.1f;
                anyToGreet.canTransitionToSelf = false;
                anyToGreet.AddCondition(AnimatorConditionMode.If, 0f, "Greet");

                var greetToIdle = greet.AddTransition(idle);
                greetToIdle.hasExitTime = true;
                greetToIdle.exitTime = 0.85f;
                greetToIdle.duration = 0.2f;
            }

            // Point (Any State → Point → Idle), only if a clip exists.
            if (pointClip != null)
            {
                var point = sm.AddState("Point");
                point.motion = pointClip;

                var anyToPoint = sm.AddAnyStateTransition(point);
                anyToPoint.hasExitTime = false;
                anyToPoint.duration = 0.1f;
                anyToPoint.canTransitionToSelf = false;
                anyToPoint.AddCondition(AnimatorConditionMode.If, 0f, "Point");

                var pointToIdle = point.AddTransition(idle);
                pointToIdle.hasExitTime = true;
                pointToIdle.exitTime = 0.85f;
                pointToIdle.duration = 0.2f;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            LogClipResult("Idle", idleClip);
            LogClipResult("Walk", walkClip);
            LogClipResult("Greet", greetClip);
            LogClipResult("Point", pointClip);

            return controller;
        }

        private static void LogClipResult(string slot, AnimationClip clip)
        {
            if (clip != null)
                Debug.Log($"[GuideNPC] {slot} clip → '{clip.name}'");
            else
                Debug.LogWarning($"[GuideNPC] No {slot} clip found. Assign one manually in {ControllerPath} if the avatar has it.");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Clip discovery
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Folders to search for clips: the avatar's source asset folder, if any.</summary>
        private static string[] ClipSearchFolders(GameObject avatar)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(avatar);
            string path = source != null ? AssetDatabase.GetAssetPath(source) : null;
            if (string.IsNullOrEmpty(path))
            {
                // Fall back to the model behind the Animator's avatar, if available.
                var anim = avatar.GetComponentInChildren<Animator>();
                if (anim != null && anim.avatar != null)
                    path = AssetDatabase.GetAssetPath(anim.avatar);
            }

            if (string.IsNullOrEmpty(path)) return null; // search whole project

            string folder = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder)) return null;

            // Search the asset folder and its parent so sibling "Animations" folders are covered.
            string parent = System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/');
            var folders = new List<string> { folder };
            if (!string.IsNullOrEmpty(parent) && parent.StartsWith("Assets")) folders.Add(parent);
            return folders.ToArray();
        }

        private static AnimationClip FindClip(string[] searchFolders, string[] include, string[] exclude)
        {
            string[] guids = searchFolders != null && searchFolders.Length > 0
                ? AssetDatabase.FindAssets("t:AnimationClip", searchFolders)
                : AssetDatabase.FindAssets("t:AnimationClip");

            var candidates = new List<AnimationClip>();
            foreach (var guid in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(p))
                {
                    if (obj is AnimationClip clip && !clip.name.StartsWith("__preview"))
                        candidates.Add(clip);
                }
            }

            AnimationClip best = null;
            foreach (var clip in candidates)
            {
                string n = clip.name.ToLowerInvariant();
                if (exclude != null && exclude.Any(x => n.Contains(x))) continue;
                if (!include.Any(inc => n.Contains(inc))) continue;

                // Prefer the shortest name (usually the cleanest base clip).
                if (best == null || clip.name.Length < best.name.Length)
                    best = clip;
            }
            return best;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Destinations
        // ─────────────────────────────────────────────────────────────────────

        public static Dictionary<GuideArea, GuideDestination> EnsureDestinations(Vector3 anchor)
        {
            var root = GameObject.Find(DestinationsRootName);
            if (root == null)
            {
                root = new GameObject(DestinationsRootName);
                Undo.RegisterCreatedObjectUndo(root, "Create Destinations Root");
            }

            var result = new Dictionary<GuideArea, GuideDestination>();
            var areas = new[] { GuideArea.CropField, GuideArea.MeetingArea, GuideArea.SmartScreens, GuideArea.TrainingRoom };

            // Spread the new targets out in a small fan around the anchor so they
            // don't overlap. Designers then drag each to its real location.
            Vector3[] offsets =
            {
                new Vector3( 4f, 0f,  4f),
                new Vector3(-4f, 0f,  4f),
                new Vector3( 4f, 0f, -4f),
                new Vector3(-4f, 0f, -4f),
            };

            for (int i = 0; i < areas.Length; i++)
            {
                string name = GuideAreaLabels.TargetName(areas[i]);
                var existing = GameObject.Find(name);
                GuideDestination marker;

                if (existing == null)
                {
                    var go = new GameObject(name);
                    Undo.RegisterCreatedObjectUndo(go, "Create " + name);
                    go.transform.SetParent(root.transform, false);
                    go.transform.position = anchor + offsets[i];
                    marker = go.AddComponent<GuideDestination>();
                }
                else
                {
                    marker = existing.GetComponent<GuideDestination>();
                    if (marker == null) marker = existing.AddComponent<GuideDestination>();
                }

                SetField(marker, "area", areas[i]);
                EditorUtility.SetDirty(marker);
                result[areas[i]] = marker;
            }

            return result;
        }

        private static void WireDestinations(SmartFarmGuideNPC guide, Dictionary<GuideArea, GuideDestination> targets)
        {
            var list = new List<GuideDestinationEntry>();
            foreach (var kv in targets)
                list.Add(new GuideDestinationEntry(kv.Key, GuideAreaLabels.For(kv.Key), kv.Value.transform));

            // Keep a stable, designer-friendly order.
            list = list.OrderBy(e => (int)e.area).ToList();

            SetField(guide, "destinations", list);
            EditorUtility.SetDirty(guide);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  XR EventSystem
        // ─────────────────────────────────────────────────────────────────────

        private static void EnsureXrEventSystem()
        {
            var es = Object.FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("EventSystem");
                Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
                es = go.AddComponent<EventSystem>();
            }

            // Prefer the XRI input module so ray/poke interactors drive UI.
            var xrModuleType = System.Type.GetType(
                "UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule, Unity.XR.Interaction.Toolkit");
            if (xrModuleType != null && es.GetComponent(xrModuleType) == null)
            {
                // Remove a plain StandaloneInputModule if present so the two don't fight.
                var standalone = es.GetComponent<StandaloneInputModule>();
                if (standalone != null) Object.DestroyImmediate(standalone);
                es.gameObject.AddComponent(xrModuleType);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Utilities
        // ─────────────────────────────────────────────────────────────────────

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parts = folder.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void SetField(object obj, string fieldName, object value)
        {
            if (obj == null) return;
            var f = obj.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            f?.SetValue(obj, value);
        }
    }
}
