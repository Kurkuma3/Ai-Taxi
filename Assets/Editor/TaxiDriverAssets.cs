using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Taxi.EditorTools
{
    /// <summary>
    /// Import settings and derived assets for the taxi driver character (Blender source: ArtSource/TaxiDriver.blend).
    /// Idempotent: configures the FBX and palette importers, the URP material and the Animator controller.
    /// Menu: Taxi > Setup Taxi Driver. SceneBootstrap calls <see cref="Setup"/> while building the scene.
    /// </summary>
    public static class TaxiDriverAssets
    {
        public const string FbxPath = "Assets/Art/Models/TaxiDriver.fbx";
        public const string PalettePath = "Assets/Art/Textures/TaxiDriverPalette.png";
        public const string MaterialPath = "Assets/Art/Materials/TaxiDriver.mat";
        public const string ControllerPath = "Assets/Art/Animations/TaxiDriver.controller";
        const string SpeedParam = "Speed";
        const float WalkThreshold = 0.1f;

        public struct Result
        {
            public GameObject prefab;
            public RuntimeAnimatorController controller;
        }

        [MenuItem("Taxi/Setup Taxi Driver")]
        public static void SetupFromMenu()
        {
            var r = Setup();
            Debug.Log($"[Taxi] Taxi driver ready: prefab={r.prefab.name}, controller={r.controller.name}");
        }

        public static Result Setup()
        {
            var palette = SetupPalette();
            var material = SetupMaterial(palette);
            SetupModelImporter(material);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (prefab == null) throw new System.Exception("[Taxi] " + FbxPath + " did not import");
            var clips = AssetDatabase.LoadAllAssetRepresentationsAtPath(FbxPath)
                .OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToList();
            var idle = clips.FirstOrDefault(c => c.name == "Idle");
            var walk = clips.FirstOrDefault(c => c.name == "Walk");
            if (idle == null || walk == null)
                throw new System.Exception("[Taxi] Idle/Walk clips not found in " + FbxPath + ": " + string.Join(", ", clips.Select(c => c.name)));

            var controller = BuildController(idle, walk);
            AssetDatabase.SaveAssets();
            return new Result { prefab = prefab, controller = controller };
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

        static void SetupModelImporter(Material material)
        {
            var mi = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (mi == null) throw new System.Exception("[Taxi] model missing: " + FbxPath);

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
            mi.animationType = ModelImporterAnimationType.Generic;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = true;
            mi.animationCompression = ModelImporterAnimationCompression.Optimal;
            mi.importConstraints = false;

            // Blender material "TaxiDriver" -> our URP material (no embedded materials).
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "TaxiDriver"), material);

            // One clip per NLA strip ("TaxiDriverRig|Idle", "TaxiDriverRig|Walk"), both looping.
            var defaults = mi.defaultClipAnimations;
            if (defaults.Length == 0)
            {
                mi.SaveAndReimport();
                defaults = mi.defaultClipAnimations;
            }
            foreach (var c in defaults)
            {
                string take = c.takeName;
                c.name = take.EndsWith("Walk") ? "Walk" : take.EndsWith("Idle") ? "Idle" : take;
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

        static AnimatorController BuildController(AnimationClip idle, AnimationClip walk)
        {
            string dir = System.IO.Path.GetDirectoryName(ControllerPath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/Art", "Animations");
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
                AssetDatabase.DeleteAsset(ControllerPath);

            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            ctrl.AddParameter(SpeedParam, AnimatorControllerParameterType.Float);
            var sm = ctrl.layers[0].stateMachine;

            var idleState = sm.AddState("Idle", new Vector3(260, 60, 0));
            idleState.motion = idle;
            var walkState = sm.AddState("Walk", new Vector3(260, 180, 0));
            walkState.motion = walk;
            walkState.speedParameterActive = true;   // playback rate follows the actual walk speed
            walkState.speedParameter = SpeedParam;
            sm.defaultState = idleState;

            var toWalk = idleState.AddTransition(walkState);
            toWalk.hasExitTime = false;
            toWalk.duration = 0.15f;
            toWalk.AddCondition(AnimatorConditionMode.Greater, WalkThreshold, SpeedParam);

            var toIdle = walkState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.15f;
            toIdle.AddCondition(AnimatorConditionMode.Less, WalkThreshold, SpeedParam);

            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }
    }
}
