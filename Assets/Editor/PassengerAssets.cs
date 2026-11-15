using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Taxi.EditorTools
{
    /// <summary>
    /// Import settings and prefabs for the passengers and the suitcase (Blender source: ArtSource/Passengers.blend).
    /// Mirrors <see cref="TaxiDriverAssets"/>: point-filtered palette, one URP material, Generic rig, looping clips.
    /// Both passenger FBX files export their armature as "PassengerRig" with the same 11 bones, so one
    /// Animator controller (built from Passenger_A clips) drives both models.
    /// Idempotent. Menu: Taxi > Setup Passengers. Does not touch the scene.
    /// </summary>
    public static class PassengerAssets
    {
        public const string FbxA = "Assets/Art/Models/Passenger_A.fbx";
        public const string FbxB = "Assets/Art/Models/Passenger_B.fbx";
        public const string FbxSuitcase = "Assets/Art/Models/Suitcase.fbx";
        public const string PalettePath = "Assets/Art/Textures/PassengerPalette.png";
        public const string MaterialPath = "Assets/Art/Materials/Passenger.mat";
        public const string ControllerPath = "Assets/Art/Animations/Passenger.controller";
        public const string PrefabA = "Assets/Prefabs/Characters/Passenger_A.prefab";
        public const string PrefabB = "Assets/Prefabs/Characters/Passenger_B.prefab";
        public const string PrefabSuitcase = "Assets/Prefabs/Props/Suitcase.prefab";
        const string BlenderMaterialName = "Passenger";
        const string SpeedParam = "Speed";
        const string WaveParam = "Wave";
        const float WalkThreshold = 0.1f;

        [MenuItem("Taxi/Setup Passengers")]
        public static void SetupFromMenu()
        {
            Setup();
            Debug.Log("[Taxi] Passengers ready: " + PrefabA + ", " + PrefabB + ", " + PrefabSuitcase);
        }

        public static void Setup()
        {
            var palette = SetupPalette();
            var material = SetupMaterial(palette);
            SetupModelImporter(FbxA, material, true);
            SetupModelImporter(FbxB, material, true);
            SetupModelImporter(FbxSuitcase, material, false);

            var clips = AssetDatabase.LoadAllAssetRepresentationsAtPath(FbxA)
                .OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToList();
            AnimationClip Clip(string n) => clips.FirstOrDefault(c => c.name == n)
                ?? throw new System.Exception("[Taxi] clip " + n + " not found in " + FbxA + ": " + string.Join(", ", clips.Select(c => c.name)));
            var controller = BuildController(Clip("Idle"), Clip("Walk"), Clip("Wave"));

            EnsureFolder("Assets/Prefabs", "Characters");
            EnsureFolder("Assets/Prefabs", "Props");
            MakeCharacterPrefab(FbxA, PrefabA, controller);
            MakeCharacterPrefab(FbxB, PrefabB, controller);
            MakeSuitcasePrefab();
            AssetDatabase.SaveAssets();
        }

        static Texture2D SetupPalette()
        {
            var ti = AssetImporter.GetAtPath(PalettePath) as TextureImporter;
            if (ti == null) throw new System.Exception("[Taxi] palette texture missing: " + PalettePath);
            bool dirty = ti.filterMode != FilterMode.Point || ti.mipmapEnabled || ti.textureCompression != TextureImporterCompression.Uncompressed
                         || ti.wrapMode != TextureWrapMode.Clamp || !ti.sRGBTexture;
            if (dirty)
            {
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = true;
                ti.filterMode = FilterMode.Point;     // flat colour cells, no bleeding between them
                ti.mipmapEnabled = false;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(PalettePath);
        }

        static Material SetupMaterial(Texture2D palette)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, MaterialPath);
            }
            m.SetTexture("_BaseMap", palette);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.05f);
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void SetupModelImporter(string path, Material material, bool rigged)
        {
            var mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi == null) throw new System.Exception("[Taxi] model missing: " + path);

            mi.globalScale = 1f;
            mi.useFileScale = true;      // Blender exports metres with unit scale applied, so 1 unit = 1 m
            mi.useFileUnits = true;
            mi.importBlendShapes = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importVisibility = false;
            mi.isReadable = false;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.importNormals = ModelImporterNormals.Import;   // flat shading authored in Blender
            mi.importTangents = ModelImporterTangents.None;
            mi.importConstraints = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), BlenderMaterialName), material);

            if (!rigged)
            {
                mi.animationType = ModelImporterAnimationType.None;
                mi.importAnimation = false;
                mi.SaveAndReimport();
                return;
            }

            mi.animationType = ModelImporterAnimationType.Generic;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = true;
            mi.animationCompression = ModelImporterAnimationCompression.Optimal;

            // One clip per NLA strip ("PassengerRig|Idle", "|Walk", "|Wave"), all looping.
            var defaults = mi.defaultClipAnimations;
            if (defaults.Length == 0)
            {
                mi.SaveAndReimport();
                defaults = mi.defaultClipAnimations;
            }
            foreach (var c in defaults)
            {
                string take = c.takeName;
                c.name = take.EndsWith("Walk") ? "Walk" : take.EndsWith("Idle") ? "Idle" : take.EndsWith("Wave") ? "Wave" : take;
                c.loopTime = true;
                c.loopPose = false;
                c.lockRootRotation = true;
                c.lockRootHeightY = true;
                c.lockRootPositionXZ = true;
                c.keepOriginalPositionY = true;
                c.keepOriginalPositionXZ = true;
                c.keepOriginalOrientation = true;
            }
            mi.clipAnimations = defaults;
            mi.SaveAndReimport();
        }

        static AnimatorController BuildController(AnimationClip idle, AnimationClip walk, AnimationClip wave)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (existing != null) return existing;   // keep the asset (and its GUID) stable

            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            ctrl.AddParameter(SpeedParam, AnimatorControllerParameterType.Float);
            ctrl.AddParameter(WaveParam, AnimatorControllerParameterType.Bool);
            var sm = ctrl.layers[0].stateMachine;

            var idleState = sm.AddState("Idle", new Vector3(260, 60, 0));
            idleState.motion = idle;
            var walkState = sm.AddState("Walk", new Vector3(260, 180, 0));
            walkState.motion = walk;
            walkState.speedParameterActive = true;   // playback rate follows the actual walk speed
            walkState.speedParameter = SpeedParam;
            var waveState = sm.AddState("Wave", new Vector3(520, 60, 0));
            waveState.motion = wave;
            sm.defaultState = idleState;

            AddTransition(idleState, walkState, AnimatorConditionMode.Greater, WalkThreshold, SpeedParam);
            AddTransition(walkState, idleState, AnimatorConditionMode.Less, WalkThreshold, SpeedParam);
            // Wave only while standing; starting to walk always ends the wave.
            AddTransition(idleState, waveState, AnimatorConditionMode.If, 0f, WaveParam);
            AddTransition(waveState, idleState, AnimatorConditionMode.IfNot, 0f, WaveParam);
            AddTransition(waveState, walkState, AnimatorConditionMode.Greater, WalkThreshold, SpeedParam);

            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        static void AddTransition(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, float threshold, string param)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = 0.2f;
            t.AddCondition(mode, threshold, param);
        }

        static void MakeCharacterPrefab(string fbx, string prefabPath, RuntimeAnimatorController controller)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (model == null) throw new System.Exception("[Taxi] " + fbx + " did not import");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                var animator = go.GetComponent<Animator>();
                if (animator == null) animator = go.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;   // off-screen passengers cost almost nothing
                foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                    smr.quality = SkinQuality.Bone1;   // rigid binding: one bone per vertex
                PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            }
            finally { Object.DestroyImmediate(go); }
        }

        static void MakeSuitcasePrefab()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxSuitcase);
            if (model == null) throw new System.Exception("[Taxi] " + FbxSuitcase + " did not import");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                if (go.transform.Find("HandlePoint") == null)
                    Debug.LogWarning("[Taxi] Suitcase has no HandlePoint child");
                PrefabUtility.SaveAsPrefabAsset(go, PrefabSuitcase);
            }
            finally { Object.DestroyImmediate(go); }
        }

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
