using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Taxi.EditorTools
{
    /// <summary>
    /// Builds the greybox city from <see cref="CityLayout"/> data (Assets/Config/CityLayout.asset) using a handful of
    /// prefabs in Assets/Prefabs/City: RoadQuad, IntersectionQuad, Building (extruded footprint), PassengerStop
    /// (pocket + marker), ZoneBarrier, InvisibleWall. Menu: Taxi > Build City. Idempotent: the previous "City" root
    /// is destroyed first. The layout asset is filled from CitySketchData the first time (and on "Reset City Layout").
    /// </summary>
    public static class CityBuilder
    {
        public const string LayoutPath = "Assets/Config/CityLayout.asset";
        const string PrefabDir = "Assets/Prefabs/City";
        const string MeshDir = "Assets/Art/Models/City";
        const string MatDir = "Assets/Art/Materials";
        const string RootName = "City";

        // Layer heights: everything flat sits on the ground collider; visuals are stacked so nothing z-fights.
        // Road segments overlap each other at junctions, so each one gets its own tiny Y step (RoadY + k*RoadStep).
        const float RoadY = 0.02f, RoadStep = 0.0004f, BendY = 0.06f, PocketY = 0.07f, ParkY = 0.01f, LotY = 0.015f;

        [MenuItem("Taxi/Build City")]
        public static void BuildMenu()
        {
            var layout = LoadOrCreateLayout();
            var root = Build(layout);
            PlaceActors(layout);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log($"[Taxi] City built: {layout.roads.Length} roads, {layout.buildings.Length} buildings, {layout.pockets.Length} stops, {layout.barriers.Length} barriers");
        }

        [MenuItem("Taxi/Reset City Layout From Sketch")]
        public static void ResetLayoutMenu()
        {
            var layout = LoadOrCreateLayout();
            CitySketchData.Fill(layout);
            EditorUtility.SetDirty(layout);
            AssetDatabase.SaveAssets();
            Debug.Log("[Taxi] CityLayout refilled from sketch data");
        }

        public static CityLayout LoadOrCreateLayout()
        {
            var layout = AssetDatabase.LoadAssetAtPath<CityLayout>(LayoutPath);
            if (layout == null)
            {
                layout = ScriptableObject.CreateInstance<CityLayout>();
                CitySketchData.Fill(layout);
                AssetDatabase.CreateAsset(layout, LayoutPath);
                AssetDatabase.SaveAssets();
            }
            else if (layout.roads == null || layout.roads.Length == 0)
            {
                CitySketchData.Fill(layout);
                EditorUtility.SetDirty(layout);
                AssetDatabase.SaveAssets();
            }
            return layout;
        }

        /// <summary>Moves the existing Player and Car to the layout spawn points (parking lot).</summary>
        public static void PlaceActors(CityLayout L)
        {
            var player = GameObject.Find("Player");
            if (player != null)
            {
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                player.transform.SetPositionAndRotation(CityLayout.ToWorld(L.playerSpawn), Quaternion.Euler(0f, L.playerSpawnYaw, 0f));
                if (cc != null) cc.enabled = true;
            }
            var car = GameObject.Find("Car");
            if (car != null)
                car.transform.SetPositionAndRotation(CityLayout.ToWorld(L.carSpawn), Quaternion.Euler(0f, L.carSpawnYaw, 0f));
        }

        public static GameObject Build(CityLayout L)
        {
            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
            EnsureFolders();
            var P = Prefabs.Ensure();

            var root = new GameObject(RootName);
            float w = L.roadWidth;

            // ---- Ground (the only flat collider; cars and the player walk on it) -----------------------------
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(root.transform, false);
            var b = L.bounds;
            ground.transform.position = new Vector3(b.center.x, 0f, b.center.y);
            ground.transform.localScale = new Vector3((b.width + 60f) / 10f, 1f, (b.height + 60f) / 10f);
            ground.GetComponent<Renderer>().sharedMaterial = Mat("Ground", new Color(0.62f, 0.66f, 0.52f));
            MarkStatic(ground);

            // ---- Invisible walls on the bounds ----------------------------------------------------------------
            var walls = Group(root, "InvisibleWalls");
            Wall(P.wall, walls, new Vector3(b.center.x, 2.5f, b.yMin), new Vector3(b.width + 2f, 5f, 1f));
            Wall(P.wall, walls, new Vector3(b.center.x, 2.5f, b.yMax), new Vector3(b.width + 2f, 5f, 1f));
            Wall(P.wall, walls, new Vector3(b.xMin, 2.5f, b.center.y), new Vector3(1f, 5f, b.height + 2f));
            Wall(P.wall, walls, new Vector3(b.xMax, 2.5f, b.center.y), new Vector3(1f, 5f, b.height + 2f));

            // ---- Park and parking lot (decals under the roads) ----------------------------------------------
            var decals = Group(root, "Decals");
            Flat(P.road, decals, "Park", L.park, ParkY, Mat("Park", new Color(0.35f, 0.62f, 0.3f)));
            Flat(P.road, decals, "ParkingLot", L.parkingLot, LotY, Mat("ParkingLot", new Color(0.5f, 0.5f, 0.52f)));

            // ---- Roads: one quad per polyline segment, extended by w/2 at both ends so corners and T-junctions
            //      are filled by the segments themselves; a round pad covers the outer corner at every bend. ------
            var roads = Group(root, "Roads");
            int k = 0;
            var bends = new List<Vector2>();
            foreach (var r in L.roads)
            {
                var g = Group(roads, r.name);
                for (int i = 0; i < r.points.Length - 1; i++)
                {
                    Vector2 a = r.points[i], c = r.points[i + 1];
                    Vector2 d = (c - a).normalized;
                    Segment(P.road, g, $"{r.name}_{i}", a - d * (w * 0.5f), c + d * (w * 0.5f), w, RoadY + RoadStep * k++);
                    if (i > 0 && Vector2.Angle(r.points[i] - r.points[i - 1], d) > 5f) bends.Add(r.points[i]);
                }
            }
            var inter = Group(root, "Intersections");
            foreach (var p in bends)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(P.ring, inter.transform);
                go.name = $"Bend_{p.x:0}_{p.y:0}";
                go.transform.SetPositionAndRotation(CityLayout.ToWorld(p, BendY), Quaternion.identity);
                go.transform.localScale = new Vector3(w, 0.005f, w);
                MarkStatic(go);
            }
            foreach (var ring in L.rings)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(P.ring, inter.transform);
                go.name = ring.name;
                go.transform.SetPositionAndRotation(CityLayout.ToWorld(ring.center, BendY), Quaternion.identity);
                go.transform.localScale = new Vector3(ring.radius * 2f, 0.005f, ring.radius * 2f);
                MarkStatic(go);
            }

            // ---- Buildings ----------------------------------------------------------------------------------
            var buildings = Group(root, "Buildings");
            Material[] zoneMats =
            {
                Mat("BuildingEdge", new Color(0.55f, 0.55f, 0.58f)),
                Mat("BuildingZ1", new Color(0.80f, 0.74f, 0.64f)),
                Mat("BuildingZ2", new Color(0.82f, 0.70f, 0.58f)),
                Mat("BuildingZ3", new Color(0.78f, 0.66f, 0.70f)),
                Mat("BuildingZ4", new Color(0.70f, 0.66f, 0.80f)),
            };
            foreach (var bd in L.buildings)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(P.building, buildings.transform);
                go.name = bd.name;
                var mesh = BuildingMesh(bd);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                go.GetComponent<MeshCollider>().sharedMesh = mesh;
                go.GetComponent<MeshRenderer>().sharedMaterial = zoneMats[Mathf.Clamp(bd.zone, 0, 4)];
                MarkStatic(go);
            }

            // ---- Passenger stops (pocket + marker) ----------------------------------------------------------
            var stops = Group(root, "PassengerStops");
            foreach (var pk in L.pockets)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(P.stop, stops.transform);
                go.name = pk.name;
                go.transform.SetPositionAndRotation(CityLayout.ToWorld(pk.position, PocketY), Quaternion.Euler(0f, pk.yaw, 0f));
                var stop = go.GetComponentInChildren<PassengerStop>();
                stop.zone = pk.zone;
                stop.tutorialMission = pk.tutorialMission;
                MarkStatic(go);
                if (pk.disabled) go.SetActive(false);   // curbs (built after this) close inactive pockets
            }

            // ---- Zone barriers ------------------------------------------------------------------------------
            var barriers = Group(root, "Barriers");
            foreach (var br in L.barriers)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(P.barrier, barriers.transform);
                go.name = br.name;
                go.transform.SetPositionAndRotation(CityLayout.ToWorld(br.position), Quaternion.Euler(0f, br.yaw, 0f));
                var zb = go.GetComponent<ZoneBarrier>();
                zb.closesZone = br.closesZone;
                zb.SetPrice(L.PriceOfZone(br.closesZone));
            }

            // ---- Zone outlines as gizmo-only markers (no renderers) ------------------------------------------
            var zones = Group(root, "Zones");
            foreach (var z in L.zones)
            {
                var zg = new GameObject(z.name + " ($" + z.price + ")");
                zg.transform.SetParent(zones.transform, false);
                zg.transform.position = new Vector3(z.rect.center.x, 0f, z.rect.center.y);
                zg.transform.localScale = new Vector3(z.rect.width, 1f, z.rect.height);
            }
            return root;
        }

        // ---- helpers ------------------------------------------------------------------------------------

        static GameObject Group(GameObject parent, string name)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent.transform, false);
            return g;
        }

        static void Wall(GameObject prefab, GameObject parent, Vector3 pos, Vector3 size)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
            go.transform.position = pos;
            go.transform.localScale = size;
            MarkStatic(go);
        }

        static void Flat(GameObject quadPrefab, GameObject parent, string name, Rect r, float y, Material mat)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(quadPrefab, parent.transform);
            go.name = name;
            go.transform.SetPositionAndRotation(new Vector3(r.center.x, y, r.center.y), Quaternion.Euler(90f, 0f, 0f));
            go.transform.localScale = new Vector3(r.width, r.height, 1f);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            MarkStatic(go);
        }

        /// <summary>Quad lying flat from a to c, 'width' across. Quad local +Y runs along the segment.</summary>
        static void Segment(GameObject quadPrefab, GameObject parent, string name, Vector2 a, Vector2 c, float width, float y)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(quadPrefab, parent.transform);
            go.name = name;
            Vector3 wa = CityLayout.ToWorld(a, y), wc = CityLayout.ToWorld(c, y);
            Vector3 dir = (wc - wa).normalized;
            go.transform.SetPositionAndRotation((wa + wc) * 0.5f, Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(90f, 0f, 0f));
            go.transform.localScale = new Vector3(width, Vector3.Distance(wa, wc), 1f);
            MarkStatic(go);
        }

        /// <summary>Extrudes a convex footprint into a flat-shaded prism. Saved as a mesh asset so the scene can reference it.</summary>
        static Mesh BuildingMesh(CityLayout.Building bd)
        {
            string path = $"{MeshDir}/{bd.name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh();
            mesh.Clear();
            mesh.name = bd.name;

            // force clockwise order when seen from above (Unity front faces are clockwise)
            var poly = new List<Vector2>(bd.polygon);
            float area = 0f;
            for (int i = 0; i < poly.Count; i++) { var p = poly[i]; var q = poly[(i + 1) % poly.Count]; area += p.x * q.y - q.x * p.y; }
            if (area > 0f) poly.Reverse();

            var verts = new List<Vector3>(); var tris = new List<int>(); var normals = new List<Vector3>();
            float h = bd.height;
            // top cap (fan)
            int baseIdx = verts.Count;
            foreach (var p in poly) { verts.Add(new Vector3(p.x, h, p.y)); normals.Add(Vector3.up); }
            for (int i = 1; i < poly.Count - 1; i++) { tris.Add(baseIdx); tris.Add(baseIdx + i); tris.Add(baseIdx + i + 1); }
            // sides
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 p = poly[i], q = poly[(i + 1) % poly.Count];
                Vector3 p0 = new Vector3(p.x, 0f, p.y), p1 = new Vector3(q.x, 0f, q.y), p2 = new Vector3(q.x, h, q.y), p3 = new Vector3(p.x, h, p.y);
                Vector3 n = Vector3.Cross(p2 - p0, p1 - p0).normalized;
                int s = verts.Count;
                verts.Add(p0); verts.Add(p1); verts.Add(p2); verts.Add(p3);
                for (int k = 0; k < 4; k++) normals.Add(n);
                tris.Add(s); tris.Add(s + 2); tris.Add(s + 1);
                tris.Add(s); tris.Add(s + 3); tris.Add(s + 2);
            }
            mesh.SetVertices(verts); mesh.SetTriangles(tris, 0); mesh.SetNormals(normals);
            mesh.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(mesh, path); else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static void MarkStatic(GameObject go)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        }

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder(PrefabDir)) AssetDatabase.CreateFolder("Assets/Prefabs", "City");
            if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder("Assets/Art/Models", "City");
        }

        public static Material Mat(string name, Color color)
        {
            string path = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.color = color;
                m.SetFloat("_Smoothness", 0.1f);
                AssetDatabase.CreateAsset(m, path);
            }
            return m;
        }

        // ---- prefabs ------------------------------------------------------------------------------------

        class Prefabs
        {
            public GameObject road, ring, building, stop, barrier, wall;

            public static Prefabs Ensure()
            {
                var p = new Prefabs
                {
                    road = Load("RoadQuad", MakeRoadQuad),
                    ring = Load("Turnaround", MakeRing),
                    building = Load("Building", MakeBuilding),
                    stop = Load("PassengerStop", MakeStop),
                    barrier = Load("ZoneBarrier", MakeBarrier),
                    wall = Load("InvisibleWall", MakeWall),
                };
                return p;
            }

            static GameObject Load(string name, System.Func<GameObject> make)
            {
                string path = $"{PrefabDir}/{name}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null) return prefab;
                var temp = make();
                temp.name = name;
                prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
                Object.DestroyImmediate(temp);
                return prefab;
            }

            static GameObject Quad(Material mat)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.GetComponent<Renderer>().sharedMaterial = mat;
                q.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                return q;
            }

            static GameObject MakeRoadQuad() => Quad(Mat("Road", new Color(0.30f, 0.30f, 0.32f)));

            static GameObject MakeRing()
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.DestroyImmediate(c.GetComponent<Collider>());
                c.GetComponent<Renderer>().sharedMaterial = Mat("Road", new Color(0.30f, 0.30f, 0.32f));
                c.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                return c;
            }

            static GameObject MakeBuilding()
            {
                var go = new GameObject("Building", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
                go.GetComponent<MeshRenderer>().sharedMaterial = Mat("BuildingZ1", new Color(0.80f, 0.74f, 0.64f));
                return go;
            }

            static GameObject MakeStop()
            {
                var go = new GameObject("PassengerStop");
                var pocket = Quad(Mat("Pocket", new Color(0.42f, 0.42f, 0.45f)));
                pocket.name = "Pocket";
                pocket.transform.SetParent(go.transform, false);
                pocket.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                pocket.transform.localScale = new Vector3(3f, 8f, 1f);   // 3 m deep, 8 m along the road
                var marker = new GameObject("Marker");
                marker.transform.SetParent(go.transform, false);
                marker.AddComponent<PassengerStop>();
                return go;
            }

            static GameObject MakeBarrier()
            {
                var go = new GameObject("ZoneBarrier");
                var zb = go.AddComponent<ZoneBarrier>();
                var col = go.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, 0.6f, 0f);
                col.size = new Vector3(10f, 1.2f, 0.8f);   // spans the full road width, blocks car and player
                var coneMat = Mat("Barrier", new Color(0.95f, 0.45f, 0.1f));
                for (int i = 0; i < 4; i++)
                {
                    var cone = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cone.name = "Cone";
                    Object.DestroyImmediate(cone.GetComponent<Collider>());
                    cone.transform.SetParent(go.transform, false);
                    cone.transform.localPosition = new Vector3(-3.75f + 2.5f * i, 0.4f, 0f);
                    cone.transform.localScale = new Vector3(0.5f, 0.8f, 0.5f);
                    cone.GetComponent<Renderer>().sharedMaterial = coneMat;
                }
                var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bar.name = "Bar";
                Object.DestroyImmediate(bar.GetComponent<Collider>());
                bar.transform.SetParent(go.transform, false);
                bar.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                bar.transform.localScale = new Vector3(9.5f, 0.15f, 0.15f);
                bar.GetComponent<Renderer>().sharedMaterial = coneMat;
                // placeholder price sign: a post, a white board and a 3D text facing -Z (the open side)
                var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                post.name = "Post";
                Object.DestroyImmediate(post.GetComponent<Collider>());
                post.transform.SetParent(go.transform, false);
                post.transform.localPosition = new Vector3(0f, 1.6f, 0f);
                post.transform.localScale = new Vector3(0.15f, 1.6f, 0.15f);
                post.GetComponent<Renderer>().sharedMaterial = Mat("Dark", new Color(0.12f, 0.12f, 0.14f));
                var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
                board.name = "Sign";
                Object.DestroyImmediate(board.GetComponent<Collider>());
                board.transform.SetParent(go.transform, false);
                board.transform.localPosition = new Vector3(0f, 2.9f, 0f);
                board.transform.localScale = new Vector3(3f, 1.4f, 0.1f);
                board.GetComponent<Renderer>().sharedMaterial = Mat("Sign", Color.white);
                var textGo = new GameObject("PriceText");
                textGo.transform.SetParent(go.transform, false);
                textGo.transform.localPosition = new Vector3(0f, 2.9f, -0.07f);
                textGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                var tm = textGo.AddComponent<TextMesh>();
                tm.text = "$0";
                tm.fontSize = 64;
                tm.characterSize = 0.12f;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.color = new Color(0.1f, 0.1f, 0.12f);
                var so = new SerializedObject(zb);
                so.FindProperty("priceLabel").objectReferenceValue = tm;
                so.ApplyModifiedPropertiesWithoutUndo();
                return go;
            }

            static GameObject MakeWall()
            {
                var go = new GameObject("InvisibleWall");
                go.AddComponent<BoxCollider>();
                return go;
            }
        }
    }
}
