using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Taxi.EditorTools
{
    /// <summary>
    /// Builds the whole greybox scene from primitives and wires every reference.
    /// Menu: Taxi > Build Main Scene. Also runs headless: -executeMethod Taxi.EditorTools.SceneBootstrap.Build
    /// </summary>
    public static class SceneBootstrap
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string ConfigPath = "Assets/Config/GameplayConfig.asset";
        const string MatDir = "Assets/Art/Materials";

        [MenuItem("Taxi/Build Main Scene")]
        public static void Build()
        {
            // Assets first, then force an import so they have GUIDs before the scene references them.
            var cfg = LoadOrCreate<GameplayConfig>(ConfigPath);
            var carPhysics = LoadOrCreatePhysics("Assets/Config/CarPhysics.physicMaterial");
            var matTaxi = Mat("Taxi", new Color(1f, 0.78f, 0.1f));
            var matDark = Mat("Dark", new Color(0.12f, 0.12f, 0.14f));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            cfg = AssetDatabase.LoadAssetAtPath<GameplayConfig>(ConfigPath);
            if (cfg == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(cfg)))
                throw new System.Exception("[Taxi] GameplayConfig asset not imported");
            Debug.Log($"[Taxi] cfg path={AssetDatabase.GetAssetPath(cfg)} type={cfg.GetType().AssemblyQualifiedName} persistent={EditorUtility.IsPersistent(cfg)}");

            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // ---- World: greybox city from CityLayout (ground, roads, buildings, stops, barriers) ----------
            var layout = CityBuilder.LoadOrCreateLayout();
            CityBuilder.Build(layout);

            // ---- Car ----------------------------------------------------------------
            var car = new GameObject("Car");
            car.transform.SetPositionAndRotation(CityLayout.ToWorld(layout.carSpawn), Quaternion.Euler(0f, layout.carSpawnYaw, 0f));
            var rb = car.AddComponent<Rigidbody>();
            rb.mass = 1200f;
            rb.linearDamping = 0f;
            rb.angularDamping = 2f;
            var carCol = car.AddComponent<BoxCollider>();
            carCol.center = new Vector3(0, 0.5f, 0);
            carCol.size = new Vector3(1.9f, 1.0f, 4.0f);
            carCol.material = carPhysics;
            var carVisual = new GameObject("Visual").transform;
            carVisual.SetParent(car.transform, false);
            Prim(PrimitiveType.Cube, "Body", new Vector3(0, 0.55f, 0), new Vector3(1.8f, 0.7f, 3.8f), matTaxi, carVisual, false);
            Prim(PrimitiveType.Cube, "Cabin", new Vector3(0, 1.1f, -0.3f), new Vector3(1.5f, 0.5f, 1.7f), matDark, carVisual, false);
            Prim(PrimitiveType.Cube, "Sign", new Vector3(0, 1.45f, -0.3f), new Vector3(0.6f, 0.2f, 0.3f), matTaxi, carVisual, false);
            foreach (var w in new[] { new Vector3(-0.9f, 0.3f, 1.3f), new Vector3(0.9f, 0.3f, 1.3f), new Vector3(-0.9f, 0.3f, -1.3f), new Vector3(0.9f, 0.3f, -1.3f) })
                Prim(PrimitiveType.Cube, "Wheel", w, new Vector3(0.3f, 0.6f, 0.6f), matDark, carVisual, false);
            var carCtrl = car.AddComponent<CarController>();
            Set(carCtrl, "config", cfg);

            // ---- Player -------------------------------------------------------------
            var player = new GameObject("Player");
            player.transform.SetPositionAndRotation(CityLayout.ToWorld(layout.playerSpawn), Quaternion.Euler(0f, layout.playerSpawnYaw, 0f));
            var cc = player.AddComponent<CharacterController>();
            cc.center = new Vector3(0, 0.9f, 0);
            cc.radius = 0.35f;
            cc.height = 1.8f;
            var playerVisual = new GameObject("Visual");
            playerVisual.transform.SetParent(player.transform, false);
            var driver = TaxiDriverAssets.Setup();
            var model = (GameObject)PrefabUtility.InstantiatePrefab(driver.prefab, playerVisual.transform);
            model.name = "TaxiDriver";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = driver.controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            var playerCtrl = player.AddComponent<PlayerController>();
            Set(playerCtrl, "config", cfg);
            Set(playerCtrl, "visual", playerVisual);
            var playerAnim = player.AddComponent<PlayerAnimator>();
            Set(playerAnim, "config", cfg);
            Set(playerAnim, "player", playerCtrl);
            Set(playerAnim, "animator", animator);

            // ---- Camera & light -----------------------------------------------------
            var cam = Camera.main;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.72f, 0.84f, 0.95f);
            cam.fieldOfView = 50f;
            var rig = cam.gameObject.AddComponent<TopDownCamera>();
            Set(rig, "target", player.transform);
            var light = Object.FindFirstObjectByType<Light>();
            if (light != null) light.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            // ---- UI -----------------------------------------------------------------
            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            var ringBg = Img("EnterRingBg", canvasGo.transform, new Vector2(150, 150), new Color(1, 1, 1, 0.3f));
            var ringFill = Img("EnterRingFill", canvasGo.transform, new Vector2(150, 150), new Color(1f, 0.85f, 0.2f, 1f));
            ringFill.type = Image.Type.Filled;
            ringFill.fillMethod = Image.FillMethod.Radial360;
            ringFill.fillOrigin = (int)Image.Origin360.Top;
            ringFill.fillClockwise = true;

            var stickBase = Img("StickBase", canvasGo.transform, new Vector2(260, 260), new Color(1, 1, 1, 0.35f));
            var stickKnob = Img("StickKnob", canvasGo.transform, new Vector2(110, 110), new Color(1, 1, 1, 0.7f));

            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var uiModule = es.GetComponent<InputSystemUIInputModule>();

            var hud = canvasGo.AddComponent<Hud>();
            Set(hud, "canvas", canvas);
            Set(hud, "worldCamera", cam);
            Set(hud, "enterRingBackground", ringBg);
            Set(hud, "enterRingFill", ringFill);
            Set(hud, "stickBase", stickBase);
            Set(hud, "stickKnob", stickKnob);
            Set(hud, "uiInput", uiModule);

            // ---- Systems ------------------------------------------------------------
            var systems = new GameObject("Systems");
            var input = systems.AddComponent<InputReader>();
            Set(input, "config", cfg);
            var fsm = systems.AddComponent<PlayerStateMachine>();
            Set(fsm, "config", cfg);
            Set(fsm, "car", carCtrl);
            Set(fsm, "player", playerCtrl);
            Set(fsm, "cameraRig", rig);
            Set(fsm, "hud", hud);

            // ---- Save & project settings ------------------------------------------
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.companyName = "AiTaxi";
            PlayerSettings.productName = "Taxi Driver";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.aitaxi.taxidriver");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            AssetDatabase.SaveAssets();

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                bool ok = EditorUserBuildSettings.SwitchActiveBuildTarget(NamedBuildTarget.Android, BuildTarget.Android);
                Debug.Log("[Taxi] Switch to Android: " + (ok ? "OK" : "FAILED (module missing?)"));
            }
            Debug.Log("[Taxi] Scene built: " + ScenePath);
        }

        // ---- helpers ----------------------------------------------------------------

        static GameObject Prim(PrimitiveType type, string name, Vector3 localPos, Vector3 scale, Material mat, Transform parent, bool keepCollider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static Image Img(string name, Transform parent, Vector2 size, Color color)
        {
            var img = new GameObject(name, typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(parent, false);
            img.rectTransform.sizeDelta = size;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static Material Mat(string name, Color color)
        {
            string path = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.color = color;
            EditorUtility.SetDirty(m);
            return m;
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null)
            {
                a = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(a, path);
            }
            return a;
        }

        static PhysicsMaterial LoadOrCreatePhysics(string path)
        {
            var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (pm == null)
            {
                pm = new PhysicsMaterial("CarPhysics") { dynamicFriction = 0.05f, staticFriction = 0.05f, bounciness = 0f, frictionCombine = PhysicsMaterialCombine.Minimum };
                AssetDatabase.CreateAsset(pm, path);
            }
            return pm;
        }

        /// <summary>Assign a private [SerializeField] directly and mark the object dirty so the scene saves it.</summary>
        static void Set(Object target, string field, Object value)
        {
            var f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f == null) { Debug.LogError($"[Taxi] Field '{field}' not found on {target.GetType().Name}"); return; }
            if (value != null && !f.FieldType.IsInstanceOfType(value))
                Debug.LogError($"[Taxi] '{field}' expects {f.FieldType} but got {value.GetType()} from {value.GetType().Assembly.GetName().Name}");
            f.SetValue(target, value);
            EditorUtility.SetDirty(target);
        }
    }
}
