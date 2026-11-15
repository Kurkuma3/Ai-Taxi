using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Taxi.EditorTools
{
    /// <summary>
    /// Menu: Taxi > Build Curbs. Generates curbs along the outer edge of the drivable area, read from the scene as it
    /// is (so hand-edited roads are respected): every renderer under City/Roads, City/Intersections,
    /// City/PassengerStops and the City/Decals/ParkingLot quad is a drivable shape (cylinders are discs, everything
    /// else an oriented rectangle). A point on a shape's edge gets a curb when the point just outside it is not covered
    /// by any other shape, so junctions and pocket entrances stay open automatically.
    /// Output: City/Curbs with one combined low visual mesh and invisible tall BoxColliders on the "Curb" layer
    /// (collides with the car only; the "Player" layer ignores it) using a frictionless physics material.
    /// </summary>
    public static class CurbBuilder
    {
        public const string CurbLayerName = "Curb";
        public const string PlayerLayerName = "Player";
        const string RootName = "Curbs";
        const string PhysicsPath = "Assets/Config/CurbPhysics.physicMaterial";
        const string MeshPath = "Assets/Art/Models/City/Curbs.asset";

        const float Step = 0.5f;          // edge sampling step, m
        const float Probe = 0.3f;         // how far outside the edge we test for other road surface
        const float Thickness = 0.4f;     // curb depth, placed outside the road edge
        const float VisualHeight = 0.2f;
        const float ColliderHeight = 2f;  // tall and invisible so the car can never hop over

        struct Shape
        {
            public bool disc;
            public Vector2 c, ax, ay;     // centre, unit axes (rectangles)
            public float hx, hy, r;       // half extents / radius

            public bool Contains(Vector2 p)
            {
                Vector2 d = p - c;
                if (disc) return d.sqrMagnitude <= r * r;
                return Mathf.Abs(Vector2.Dot(d, ax)) <= hx && Mathf.Abs(Vector2.Dot(d, ay)) <= hy;
            }
        }

        struct Piece { public Vector2 a, b, n; }   // curb along a→b, outward normal n

        [MenuItem("Taxi/Build Curbs")]
        public static void BuildMenu()
        {
            int n = Build();
            Debug.Log($"[Taxi] Curbs built: {n} colliders");
        }

        public static int Build()
        {
            var city = GameObject.Find("City");
            if (city == null) { Debug.LogError("[Taxi] Build Curbs: no 'City' root in the scene"); return 0; }

            int curbLayer = EnsureLayer(CurbLayerName);
            int playerLayer = EnsureLayer(PlayerLayerName);
            Physics.IgnoreLayerCollision(curbLayer, playerLayer, true);
            AssignPlayerLayer(playerLayer);

            var old = city.transform.Find(RootName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var shapes = CollectShapes(city.transform);
            var pieces = new List<Piece>();
            foreach (var s in shapes)
            {
                if (s.disc) DiscEdges(s, shapes, pieces);
                else RectEdges(s, shapes, pieces);
            }

            var root = new GameObject(RootName);
            root.transform.SetParent(city.transform, false);
            var phys = LoadOrCreatePhysics();
            var combine = new List<CombineInstance>();
            var cubeTemp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var cubeMesh = cubeTemp.GetComponent<MeshFilter>().sharedMesh;

            int i = 0;
            foreach (var p in pieces)
            {
                Vector2 dir = p.b - p.a;
                float len = dir.magnitude + Thickness;          // overlap at both ends closes corners
                if (len < 0.3f) continue;
                Vector2 mid = (p.a + p.b) * 0.5f + p.n * (Thickness * 0.5f);
                var rot = Quaternion.LookRotation(new Vector3(p.n.x, 0f, p.n.y), Vector3.up);   // local Z = outward

                var go = new GameObject("Curb_" + i++);
                go.layer = curbLayer;
                go.transform.SetParent(root.transform, false);
                go.transform.SetPositionAndRotation(new Vector3(mid.x, 0f, mid.y), rot);
                var col = go.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, ColliderHeight * 0.5f, 0f);
                col.size = new Vector3(len, ColliderHeight, Thickness);
                col.sharedMaterial = phys;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);

                combine.Add(new CombineInstance
                {
                    mesh = cubeMesh,
                    transform = Matrix4x4.TRS(new Vector3(mid.x, VisualHeight * 0.5f, mid.y), rot, new Vector3(len, VisualHeight, Thickness)),
                });
            }
            Object.DestroyImmediate(cubeTemp);

            // one renderer for all curb visuals (mobile: a single draw call)
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh { name = "Curbs" };
            mesh.Clear();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.CombineMeshes(combine.ToArray(), true, true);
            if (isNew) AssetDatabase.CreateAsset(mesh, MeshPath); else EditorUtility.SetDirty(mesh);
            var vis = new GameObject("CurbVisual", typeof(MeshFilter), typeof(MeshRenderer));
            vis.transform.SetParent(root.transform, false);
            vis.GetComponent<MeshFilter>().sharedMesh = mesh;
            var mr = vis.GetComponent<MeshRenderer>();
            mr.sharedMaterial = CityBuilder.Mat("Curb", new Color(0.78f, 0.78f, 0.74f));
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(vis, StaticEditorFlags.BatchingStatic);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(city.scene);
            return i;
        }

        // ---- geometry ---------------------------------------------------------------------------------

        static List<Shape> CollectShapes(Transform city)
        {
            var list = new List<Shape>();
            foreach (var groupName in new[] { "Roads", "Intersections", "PassengerStops" })
            {
                var g = city.Find(groupName);
                if (g == null) continue;
                foreach (var mf in g.GetComponentsInChildren<MeshFilter>())
                    if (mf.sharedMesh != null && mf.GetComponent<MeshRenderer>() != null) list.Add(ToShape(mf));
            }
            var lot = city.Find("Decals/ParkingLot");
            if (lot != null && lot.GetComponent<MeshFilter>() != null) list.Add(ToShape(lot.GetComponent<MeshFilter>()));
            return list;
        }

        static Shape ToShape(MeshFilter mf)
        {
            var t = mf.transform;
            var b = mf.sharedMesh.bounds;
            Vector3 cw = t.TransformPoint(b.center);
            var s = new Shape { c = new Vector2(cw.x, cw.z) };
            if (mf.sharedMesh.name.StartsWith("Cylinder"))
            {
                Vector3 ls = t.lossyScale;
                s.disc = true;
                s.r = 0.5f * Mathf.Max(Mathf.Abs(ls.x) * b.size.x, Mathf.Abs(ls.z) * b.size.z);
                return s;
            }
            // the two local axes that lie most flat in the world are the footprint of the rectangle
            Vector3[] axes = { t.TransformVector(new Vector3(b.size.x, 0, 0)), t.TransformVector(new Vector3(0, b.size.y, 0)), t.TransformVector(new Vector3(0, 0, b.size.z)) };
            System.Array.Sort(axes, (u, v) => new Vector2(v.x, v.z).sqrMagnitude.CompareTo(new Vector2(u.x, u.z).sqrMagnitude));
            Vector2 a0 = new Vector2(axes[0].x, axes[0].z), a1 = new Vector2(axes[1].x, axes[1].z);
            s.ax = a0.normalized; s.hx = a0.magnitude * 0.5f;
            s.ay = new Vector2(-s.ax.y, s.ax.x);
            s.hy = Mathf.Abs(Vector2.Dot(a1, s.ay)) * 0.5f;
            return s;
        }

        static bool Covered(Vector2 p, List<Shape> shapes)
        {
            for (int i = 0; i < shapes.Count; i++) if (shapes[i].Contains(p)) return true;
            return false;
        }

        static void RectEdges(Shape s, List<Shape> shapes, List<Piece> outList)
        {
            Vector2 ex = s.ax * s.hx, ey = s.ay * s.hy;
            Vector2[] corners = { s.c - ex - ey, s.c + ex - ey, s.c + ex + ey, s.c - ex + ey };
            for (int k = 0; k < 4; k++)
            {
                Vector2 a = corners[k], b = corners[(k + 1) % 4];
                Vector2 d = b - a;
                float len = d.magnitude;
                if (len < 0.01f) continue;
                Vector2 dir = d / len;
                Vector2 n = new Vector2(dir.y, -dir.x);                 // corners go counter-clockwise → right side is outside
                if (Vector2.Dot(n, (a + b) * 0.5f - s.c) < 0f) n = -n;
                int cells = Mathf.Max(1, Mathf.CeilToInt(len / Step));
                float cell = len / cells;
                int runStart = -1;
                for (int i = 0; i <= cells; i++)
                {
                    bool edge = i < cells && !Covered(a + dir * ((i + 0.5f) * cell) + n * Probe, shapes);
                    if (edge && runStart < 0) runStart = i;
                    if (!edge && runStart >= 0)
                    {
                        outList.Add(new Piece { a = a + dir * (runStart * cell), b = a + dir * (i * cell), n = n });
                        runStart = -1;
                    }
                }
            }
        }

        static void DiscEdges(Shape s, List<Shape> shapes, List<Piece> outList)
        {
            int cells = Mathf.Max(12, Mathf.CeilToInt(2f * Mathf.PI * s.r / 0.75f));
            float da = 2f * Mathf.PI / cells;
            for (int i = 0; i < cells; i++)
            {
                float am = (i + 0.5f) * da;
                Vector2 nm = new Vector2(Mathf.Cos(am), Mathf.Sin(am));
                if (Covered(s.c + nm * (s.r + Probe), shapes)) continue;
                Vector2 a = s.c + new Vector2(Mathf.Cos(i * da), Mathf.Sin(i * da)) * s.r;
                Vector2 b = s.c + new Vector2(Mathf.Cos((i + 1) * da), Mathf.Sin((i + 1) * da)) * s.r;
                outList.Add(new Piece { a = a, b = b, n = nm });
            }
        }

        // ---- project setup -------------------------------------------------------------------------------

        static PhysicsMaterial LoadOrCreatePhysics()
        {
            var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(PhysicsPath);
            if (pm == null)
            {
                pm = new PhysicsMaterial("CurbPhysics");
                AssetDatabase.CreateAsset(pm, PhysicsPath);
            }
            pm.dynamicFriction = 0f;
            pm.staticFriction = 0f;
            pm.bounciness = 0f;
            pm.frictionCombine = PhysicsMaterialCombine.Minimum;   // car slides along the curb instead of sticking
            pm.bounceCombine = PhysicsMaterialCombine.Minimum;
            EditorUtility.SetDirty(pm);
            return pm;
        }

        /// <summary>Returns the index of a named user layer, creating it in the first free slot (8..31) if needed.</summary>
        public static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0) return existing;
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 8; i < 32; i++)
            {
                var sp = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(sp.stringValue))
                {
                    sp.stringValue = name;
                    tagManager.ApplyModifiedPropertiesWithoutUndo();
                    return i;
                }
            }
            throw new System.Exception("[Taxi] no free layer slot for " + name);
        }

        public static void AssignPlayerLayer(int playerLayer)
        {
            var player = GameObject.Find("Player");
            if (player == null) return;
            foreach (var t in player.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = playerLayer;
        }
    }
}
