using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SmartFarm.GuideNPC.EditorTools
{
    /// <summary>
    /// Fixes the Gardner Avatar asset's broken rig import.
    ///
    /// The asset ships marked as <b>Humanoid</b> but with <b>no Avatar created</b>
    /// (avatarSetup = NoAvatar), so the model and clips can't retarget and the
    /// character slides with frozen legs.
    ///
    /// This tool sets every FBX under the Gardner folder to
    /// <b>Generic ▸ Create From This Model</b>, reimports them (which generates an
    /// Avatar for each rig), then assigns the matching Avatar onto the guide's
    /// Animator in the open scene. Generic is used because the Gardner rig fails
    /// Unity's Humanoid bone auto-mapping, and a single shared skeleton plays fine
    /// as Generic.
    ///
    /// Menu: Tools ▸ Smart Farm ▸ Guide NPC ▸ Fix Gardner Avatar Rig
    /// </summary>
    public static class GardnerRigFixer
    {
        private const string GardnerRoot = "Assets/GARDNER_AVATAR";

        [MenuItem("Tools/Smart Farm/Guide NPC/Fix Gardner Avatar Rig", priority = 10)]
        public static void FixRig()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[GuideNPC] Stop Play mode before fixing the rig.");
                return;
            }

            string root = AssetDatabase.IsValidFolder(GardnerRoot) ? GardnerRoot : FindGardnerRoot();
            if (string.IsNullOrEmpty(root))
            {
                EditorUtility.DisplayDialog("Fix Gardner Rig",
                    "Couldn't find the Gardner Avatar folder. Make sure it's imported under Assets/.",
                    "OK");
                return;
            }

            var modelGuids = AssetDatabase.FindAssets("t:Model", new[] { root });
            int count = 0;

            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var guid in modelGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!path.ToLowerInvariant().EndsWith(".fbx")) continue;

                    var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                    if (imp == null) continue;

                    // Generic (not Humanoid): the Gardner rig fails Unity's humanoid
                    // bone auto-mapping ("Hips/LeftLowerLeg not found"). Since the model
                    // and clips share one skeleton, Generic plays them directly by bone
                    // name with no mapping errors.
                    bool changed = false;
                    if (imp.animationType != ModelImporterAnimationType.Generic)
                    {
                        imp.animationType = ModelImporterAnimationType.Generic;
                        changed = true;
                    }
                    if (imp.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
                    {
                        imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                        changed = true;
                    }

                    // Make locomotion clips loop. Without this the Walk clip plays once
                    // ("two steps") then freezes on its last frame while the agent keeps
                    // moving. Idle should loop too.
                    if (EnableLoopOnLocomotionClips(imp)) changed = true;

                    if (changed)
                    {
                        imp.SaveAndReimport();
                        count++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            Debug.Log($"[GuideNPC] Fixed rig on {count} FBX file(s) under {root}. Avatars are now generated.");

            int assigned = AssignAvatarsToSceneGuides();
            if (assigned > 0)
                Debug.Log($"[GuideNPC] Assigned the matching Avatar to {assigned} guide Animator(s) in the scene.");
            else
                Debug.LogWarning("[GuideNPC] No guide found in the open scene to auto-assign. " +
                                 "Select your avatar's Animator and set the Avatar field to 'Gardner_Avatar_BaseAvatar' manually.");

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        /// <summary>
        /// For each guide in the scene, assign the Avatar generated from the SAME
        /// FBX its skinned mesh comes from (guarantees the skeleton matches).
        /// </summary>
        private static int AssignAvatarsToSceneGuides()
        {
            int assigned = 0;
            var guides = Object.FindObjectsByType<SmartFarmGuideNPC>(FindObjectsSortMode.None);

            // Fall back to any Animator-bearing humanoids if no guide component yet.
            var animators = new List<Animator>();
            if (guides != null && guides.Length > 0)
            {
                foreach (var g in guides)
                {
                    var a = g.GetComponentInChildren<Animator>();
                    if (a != null) animators.Add(a);
                }
            }

            foreach (var anim in animators)
            {
                if (anim.avatar != null && anim.avatar.isValid) continue; // already good

                var smr = anim.GetComponentInChildren<SkinnedMeshRenderer>();
                string fbxPath = null;
                if (smr != null && smr.sharedMesh != null)
                    fbxPath = AssetDatabase.GetAssetPath(smr.sharedMesh);

                Avatar avatar = !string.IsNullOrEmpty(fbxPath) ? LoadAvatarAt(fbxPath) : null;

                // Fall back to the base model avatar if the mesh path didn't resolve.
                if (avatar == null)
                    avatar = LoadAvatarAt(GardnerRoot + "/Model/Gardner_Avatar_Base.fbx");

                if (avatar != null)
                {
                    Undo.RecordObject(anim, "Assign Gardner Avatar");
                    anim.avatar = avatar;
                    EditorUtility.SetDirty(anim);
                    assigned++;
                }
            }

            return assigned;
        }

        /// <summary>
        /// Enables Loop Time on Idle/Walk clips inside a model so they don't freeze
        /// after a single play. Greet/wave/victory/transition clips are left one-shot.
        /// </summary>
        private static bool EnableLoopOnLocomotionClips(ModelImporter imp)
        {
            var clips = imp.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return false;

            bool changed = false;
            for (int i = 0; i < clips.Length; i++)
            {
                string n = clips[i].name.ToLowerInvariant();
                bool isLocomotion = (n.Contains("walk") || n.Contains("idle")) && !n.Contains("to_");
                if (isLocomotion && !clips[i].loopTime)
                {
                    clips[i].loopTime = true;
                    changed = true;
                }
            }

            if (changed) imp.clipAnimations = clips;
            return changed;
        }

        private static Avatar LoadAvatarAt(string fbxPath)
        {
            if (string.IsNullOrEmpty(fbxPath)) return null;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                if (obj is Avatar a) return a;
            return null;
        }

        private static string FindGardnerRoot()
        {
            // Search for a folder containing "GARDNER" if the default path moved.
            var dirs = AssetDatabase.GetSubFolders("Assets");
            foreach (var d in dirs)
                if (d.ToUpperInvariant().Contains("GARDNER")) return d;
            return null;
        }
    }
}
