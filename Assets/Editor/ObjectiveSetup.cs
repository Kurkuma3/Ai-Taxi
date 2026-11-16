using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Taxi.EditorTools
{
    /// <summary>
    /// Adds the objective system to the open scene: ObjectiveTracker (and the objective log, debug panel, launch reset
    /// when present) on "Systems", wired to the trip manager, zones, upgrades, player state and HUD. Idempotent.
    /// Menu: Taxi > Setup Objectives. SceneBootstrap calls <see cref="Setup"/> too.
    /// </summary>
    public static class ObjectiveSetup
    {
        const string ConfigPath = "Assets/Config/GameplayConfig.asset";

        [MenuItem("Taxi/Setup Objectives")]
        public static void SetupFromMenu()
        {
            Setup();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        public static void Setup()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<GameplayConfig>(ConfigPath);
            var fsm = Object.FindAnyObjectByType<PlayerStateMachine>();
            var trips = Object.FindAnyObjectByType<TripManager>();
            var zones = Object.FindAnyObjectByType<ZoneProgress>();
            var upgrades = Object.FindAnyObjectByType<Upgrades>();
            var hud = Object.FindAnyObjectByType<Hud>();
            if (cfg == null || fsm == null || trips == null || zones == null || hud == null)
                throw new System.Exception("[Taxi] Run Taxi > Setup Trip System first");

            var systems = fsm.gameObject;
            var tracker = GetOrAdd<ObjectiveTracker>(systems);
            Set(tracker, "config", cfg);
            Set(tracker, "trips", trips);
            Set(tracker, "zones", zones);
            Set(tracker, "upgrades", upgrades);
            Set(tracker, "playerState", fsm);
            Set(tracker, "hud", hud);
            var log = GetOrAdd<ObjectiveLog>(systems);
            Set(log, "tracker", tracker);
            GetOrAdd<DebugPanel>(systems);
            EditorSceneManager.MarkSceneDirty(systems.scene);
            Debug.Log("[Taxi] Objectives set up on " + systems.name);
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
