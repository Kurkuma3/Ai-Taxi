using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Taxi.EditorTools
{
    /// <summary>
    /// Adds the garage upgrades to the open scene: Upgrades + UpgradeShop on "Systems", references on the car and the
    /// trip manager, and an "Upgrades" root with three pads (Engine, Steering, Fare Meter) in front of the garage, each
    /// with a disc and a sign. Idempotent: the pads are rebuilt, components are reused. Menu: Taxi > Setup Upgrades.
    /// SceneBootstrap calls <see cref="Setup"/> too.
    /// </summary>
    public static class UpgradeSetup
    {
        const string ConfigPath = "Assets/Config/GameplayConfig.asset";
        const string MatDir = "Assets/Art/Materials/";

        // Layout, not gameplay tuning: a row on the paved part of the start lot, below the garage, between the
        // Stop_Parking pocket (z 24.5..27.5) and the Stop_Depot pocket (z 37..40), west of the exit road and of the
        // car spawn; the player spawns right next to it.
        // 4 m apart so the one-line signs ("Fare Meter Lv1 · $120" is ~3.5 m) fit side by side; the row ends at x 69.3,
        // clear of the car spawn (body from x 70).
        static readonly Vector3[] PadPositions = { new Vector3(59.4f, 0f, 31.8f), new Vector3(63.4f, 0f, 31.8f), new Vector3(67.4f, 0f, 31.8f) };
        static readonly UpgradeType[] PadTypes = { UpgradeType.Engine, UpgradeType.Steering, UpgradeType.FareMeter };
        const float SignTilt = 55f;   // degrees back, so the board reads from the high top-down camera

        [MenuItem("Taxi/Setup Upgrades")]
        public static void SetupFromMenu()
        {
            Setup();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        public static void Setup()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<GameplayConfig>(ConfigPath);
            var fsm = Object.FindAnyObjectByType<PlayerStateMachine>();
            var car = Object.FindAnyObjectByType<CarController>();
            var player = Object.FindAnyObjectByType<PlayerController>();
            var hud = Object.FindAnyObjectByType<Hud>();
            var wallet = Object.FindAnyObjectByType<Wallet>();
            var trips = Object.FindAnyObjectByType<TripManager>();
            if (cfg == null || fsm == null || car == null || player == null || hud == null || wallet == null || trips == null)
                throw new System.Exception("[Taxi] Run Taxi > Setup Trip System first (needs config, car, player, HUD, wallet, trips)");

            var systems = fsm.gameObject;
            var upgrades = GetOrAdd<Upgrades>(systems);
            Set(upgrades, "config", cfg);
            var shop = GetOrAdd<UpgradeShop>(systems);
            Set(shop, "config", cfg);
            Set(shop, "playerState", fsm);
            Set(shop, "player", player);
            Set(shop, "wallet", wallet);
            Set(shop, "hud", hud);
            Set(shop, "upgrades", upgrades);
            Set(car, "upgrades", upgrades);
            Set(trips, "upgrades", upgrades);

            var root = GameObject.Find("Upgrades");
            if (root == null) root = new GameObject("Upgrades");
            for (int i = root.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

            var padMat = Mat("UpgradePad", "Universal Render Pipeline/Unlit", new Color(1f, 0.78f, 0.2f));
            var boardMat = Mat("UpgradeSign", "Universal Render Pipeline/Lit", Color.white);
            var postMat = Mat("UpgradePost", "Universal Render Pipeline/Lit", new Color(0.12f, 0.12f, 0.14f));
            for (int i = 0; i < PadPositions.Length; i++) BuildPad(root.transform, PadTypes[i], PadPositions[i], padMat, boardMat, postMat);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log("[Taxi] Upgrades set up: 3 pads in front of the garage");
        }

        static void BuildPad(Transform root, UpgradeType type, Vector3 pos, Material padMat, Material boardMat, Material postMat)
        {
            var go = new GameObject("Pad_" + type);
            go.transform.SetParent(root, false);
            go.transform.position = pos;
            var pad = go.AddComponent<UpgradePad>();
            pad.type = type;

            // Disc: an empty holder (punch-scaled at runtime) with a flat cylinder under it.
            var disc = new GameObject("Disc").transform;
            disc.SetParent(go.transform, false);
            var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cyl.name = "Plate";
            Object.DestroyImmediate(cyl.GetComponent<Collider>());
            cyl.transform.SetParent(disc, false);
            cyl.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            cyl.transform.localScale = new Vector3(2.6f, 0.02f, 2.6f);
            var mr = cyl.GetComponent<MeshRenderer>();
            mr.sharedMaterial = padMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Sign behind the pad (toward the garage) so it never hides the player standing on the pad.
            var sign = new GameObject("Sign").transform;
            sign.SetParent(go.transform, false);
            sign.localPosition = new Vector3(0f, 0f, 1.7f);
            var post = Cube("Post", sign, new Vector3(0f, 1.1f, 0.05f), new Vector3(0.15f, 2.2f, 0.15f), postMat);
            post.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var tilt = new GameObject("Tilt").transform;
            tilt.SetParent(sign, false);
            tilt.localPosition = new Vector3(0f, 2.3f, 0f);
            tilt.localRotation = Quaternion.Euler(SignTilt, 0f, 0f);
            Cube("Board", tilt, Vector3.zero, new Vector3(3.7f, 0.8f, 0.08f), boardMat);
            var textGo = new GameObject("Label");
            textGo.transform.SetParent(tilt, false);
            textGo.transform.localPosition = new Vector3(0f, 0f, -0.06f);
            var tm = textGo.AddComponent<TextMesh>();
            tm.text = Upgrades.DisplayName(type);
            tm.fontSize = 64;
            tm.characterSize = 0.055f;
            tm.lineSpacing = 0.9f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.1f, 0.1f, 0.12f);

            var so = new SerializedObject(pad);
            so.FindProperty("label").objectReferenceValue = tm;
            so.FindProperty("disc").objectReferenceValue = disc;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameObject Cube(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
            c.name = name;
            Object.DestroyImmediate(c.GetComponent<Collider>());
            c.transform.SetParent(parent, false);
            c.transform.localPosition = localPos;
            c.transform.localScale = scale;
            c.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return c;
        }

        static Material Mat(string name, string shader, Color color)
        {
            string path = MatDir + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find(shader)) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(m);
            return m;
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) throw new System.Exception("[Taxi] " + target.GetType().Name + "." + field + " not found");
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
