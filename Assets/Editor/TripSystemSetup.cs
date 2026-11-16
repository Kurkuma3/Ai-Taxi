using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Taxi.EditorTools
{
    /// <summary>
    /// Adds the trip system to the open scene without rebuilding it (the scene is also edited by hand):
    /// ZoneProgress, Wallet, TripManager on "Systems", a RouteHint object, and the fare label, money counter, coin
    /// template and audio source under the HUD canvas. Idempotent: existing objects are reused, only references are
    /// re-wired. Menu: Taxi > Setup Trip System. SceneBootstrap calls <see cref="Setup"/> too.
    /// Also: Taxi > Validate Passenger Stops (every stop must resolve to a zone).
    /// </summary>
    public static class TripSystemSetup
    {
        const string ConfigPath = "Assets/Config/GameplayConfig.asset";
        const string HintMatPath = "Assets/Art/Materials/RouteHint.mat";

        [MenuItem("Taxi/Setup Trip System")]
        public static void SetupFromMenu()
        {
            Setup();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            ValidateStops();
        }

        public static void Setup()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<GameplayConfig>(ConfigPath);
            if (cfg == null) throw new System.Exception("[Taxi] " + ConfigPath + " missing");
            var car = Find<CarController>();
            var fsm = Find<PlayerStateMachine>();
            var player = Find<PlayerController>();
            var hud = Find<Hud>();
            if (car == null || fsm == null || player == null || hud == null)
                throw new System.Exception("[Taxi] Scene must contain CarController, PlayerStateMachine, PlayerController and Hud");

            // ---- Systems ----
            var systems = fsm.gameObject;
            var zones = GetOrAdd<ZoneProgress>(systems);
            Set(zones, "config", cfg);
            var wallet = GetOrAdd<Wallet>(systems);
            Set(wallet, "config", cfg);

            // ---- Route hint ----
            var hintGo = GameObject.Find("RouteHint");
            if (hintGo == null) hintGo = new GameObject("RouteHint");
            var hint = GetOrAdd<RouteHint>(hintGo);
            Set(hint, "config", cfg);
            Set(hint, "material", HintMaterial());

            // ---- HUD ----
            var canvas = hud.GetComponent<Canvas>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var money = Child(canvas.transform, "Money");
            var moneyRt = money.GetComponent<RectTransform>();
            moneyRt.anchorMin = moneyRt.anchorMax = new Vector2(0.5f, 1f);
            moneyRt.pivot = new Vector2(0.5f, 1f);
            moneyRt.anchoredPosition = new Vector2(0f, -70f);
            moneyRt.sizeDelta = new Vector2(420f, 120f);
            var moneyText = GetOrAdd<Text>(money);
            Style(moneyText, font, 84, new Color(1f, 0.86f, 0.25f));
            moneyText.text = "$0";
            GetOrAdd<Outline>(money).effectDistance = new Vector2(4f, -4f);

            var fare = Child(canvas.transform, "FareLabel");
            var fareBg = GetOrAdd<Image>(fare);
            fareBg.color = new Color(0.1f, 0.1f, 0.12f, 0.75f);
            fareBg.raycastTarget = false;
            var fareRt = fare.GetComponent<RectTransform>();
            fareRt.sizeDelta = new Vector2(190f, 86f);
            fareRt.pivot = new Vector2(0.5f, 0f);
            var fareTextGo = Child(fare.transform, "Text");
            var fareTextRt = fareTextGo.GetComponent<RectTransform>();
            fareTextRt.anchorMin = Vector2.zero; fareTextRt.anchorMax = Vector2.one;
            fareTextRt.offsetMin = fareTextRt.offsetMax = Vector2.zero;
            var fareText = GetOrAdd<Text>(fareTextGo);
            Style(fareText, font, 60, new Color(0.55f, 1f, 0.55f));
            fareText.text = "$0";
            fare.SetActive(false);

            var coin = Child(canvas.transform, "CoinTemplate");
            var coinImg = GetOrAdd<Image>(coin);
            coinImg.color = new Color(1f, 0.8f, 0.2f);
            coinImg.raycastTarget = false;
            coin.GetComponent<RectTransform>().sizeDelta = new Vector2(56f, 56f);
            coin.SetActive(false);

            var sfx = GetOrAdd<AudioSource>(hud.gameObject);
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0f;

            Set(hud, "config", cfg);
            Set(hud, "fareLabel", fareRt);
            Set(hud, "fareText", fareText);
            Set(hud, "moneyRoot", moneyRt);
            Set(hud, "moneyText", moneyText);
            Set(hud, "coinTemplate", coinImg);
            Set(hud, "sfx", sfx);

            // ---- Trip manager ----
            var trips = GetOrAdd<TripManager>(systems);
            Set(trips, "config", cfg);
            Set(trips, "car", car);
            Set(trips, "playerState", fsm);
            Set(trips, "player", player);
            Set(trips, "zones", zones);
            Set(trips, "wallet", wallet);
            Set(trips, "hud", hud);
            Set(trips, "routeHint", hint);
            var prefabs = new[]
            {
                AssetDatabase.LoadAssetAtPath<GameObject>(PassengerAssets.PrefabA),
                AssetDatabase.LoadAssetAtPath<GameObject>(PassengerAssets.PrefabB),
            };
            if (prefabs[0] == null || prefabs[1] == null) throw new System.Exception("[Taxi] Passenger prefabs missing: run Taxi > Setup Passengers");
            var f = typeof(TripManager).GetField("passengerPrefabs", BindingFlags.Instance | BindingFlags.NonPublic);
            f.SetValue(trips, prefabs);
            EditorUtility.SetDirty(trips);

            EditorSceneManager.MarkSceneDirty(systems.scene);
            Debug.Log("[Taxi] Trip system set up in " + systems.scene.path);
        }

        [MenuItem("Taxi/Validate Passenger Stops")]
        public static void ValidateStops()
        {
            var counts = new int[ZoneProgress.ZoneCount + 1];
            int bad = 0;
            var all = new System.Collections.Generic.List<PassengerStop>();
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                all.AddRange(root.GetComponentsInChildren<PassengerStop>(true));
            foreach (var s in all)
            {
                int z = s.ResolvedZone;
                if (z < 1 || z > ZoneProgress.ZoneCount) { bad++; Debug.LogError("[Taxi] Stop without a valid zone: " + s.name, s); continue; }
                counts[z]++;
            }
            Debug.Log($"[Taxi] Passenger stops: Z1={counts[1]} Z2={counts[2]} Z3={counts[3]} Z4={counts[4]}, without zone={bad}");
        }

        // ---- helpers ----------------------------------------------------------------------------------------

        static Material HintMaterial()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(HintMatPath);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(m, HintMatPath);
            }
            m.SetColor("_BaseColor", Color.white);   // tinted per renderer through a MaterialPropertyBlock
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void Style(Text t, Font font, int size, Color color)
        {
            t.font = font;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
        }

        static T Find<T>() where T : Component
        {
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var c = root.GetComponentInChildren<T>(true);
                if (c != null) return c;
            }
            return null;
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        static GameObject Child(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t != null) return t.gameObject;
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        static void Set(Object target, string field, Object value)
        {
            var fi = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (fi == null) { Debug.LogError($"[Taxi] Field '{field}' not found on {target.GetType().Name}"); return; }
            fi.SetValue(target, value);
            EditorUtility.SetDirty(target);
        }
    }
}
