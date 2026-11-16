using System;
using UnityEngine;

namespace Taxi
{
    /// <summary>
    /// Data-only description of the greybox city, transcribed from docs/city_sketch.png.
    /// Units are metres; Vector2 = (X east, Z north); heights are metres. Nothing here is instantiated at
    /// runtime: the editor's CityBuilder (Taxi > Build City) turns it into prefab instances.
    /// </summary>
    [CreateAssetMenu(menuName = "Taxi/City Layout", fileName = "CityLayout")]
    public class CityLayout : ScriptableObject
    {
        [Serializable] public class Road { public string name; public Vector2[] points; }
        [Serializable] public class Ring { public string name; public Vector2 center; public float radius; }
        [Serializable] public class Building { public string name; public int zone; public float height; public Vector2[] polygon; }
        [Serializable] public class Pocket
        {
            public string name; public int zone; public Vector2 position; public float yaw;
            [Tooltip("Built but inactive: no passengers, and the curb is closed in front of it.")]
            public bool disabled;
            [Tooltip("Tutorial mission N (1-based) picks up here; 0 = normal stop.")]
            public int tutorialMission;
        }
        [Serializable] public class Barrier
        {
            public string name; public int closesZone; public Vector2 position; public float yaw;
            [Tooltip("Zone on the near side; the barrier stays until both are open.")]
            public int otherZone = 1;
        }
        [Serializable] public class Zone { public int id; public string name; public int price; public Rect rect; }

        [Tooltip("Road width in metres. Pockets and barriers are sized from it.")]
        public float roadWidth = 10f;
        [Tooltip("Playable rectangle. Invisible walls are placed on its edges.")]
        public Rect bounds;
        public Rect park;
        public Rect parkingLot;
        public Vector2 playerSpawn;
        public float playerSpawnYaw;
        public Vector2 carSpawn;
        public float carSpawnYaw;

        [Tooltip("Zone 1 is open from the start. A road on a zone border belongs to the cheaper zone. The park is Zone 4 entirely.")]
        public Zone[] zones;
        [Tooltip("Road axes as polylines. Every vertex becomes an intersection pad.")]
        public Road[] roads;
        [Tooltip("Turnaround discs at dead ends.")]
        public Ring[] rings;
        [Tooltip("Convex footprints, extruded to 'height'. zone 0 = map edge.")]
        public Building[] buildings;
        [Tooltip("Parking pockets beside the road; each holds an empty PassengerStop marker.")]
        public Pocket[] pockets;
        [Tooltip("Physical barriers where a road enters a closed zone.")]
        public Barrier[] barriers;

        public int PriceOfZone(int zoneId)
        {
            if (zones == null) return 0;
            foreach (var z in zones) if (z.id == zoneId) return z.price;
            return 0;
        }

        public static Vector3 ToWorld(Vector2 p, float y = 0f) => new Vector3(p.x, y, p.y);
    }
}
