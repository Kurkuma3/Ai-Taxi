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
    /// Output: City/Curbs with one combined low visual mesh and invisible tall colliders on the "Curb" layer
    /// (collides with the car only; the "Player" layer ignores it) using a frictionless physics material.
    /// Smoothness rules (the car must slide along, never snag):
    /// - a run ends exactly where the road edge meets an opening (binary search, not the 0.5 m sampling grid) and is
    ///   pulled back by the cap radius, so nothing pokes into a junction, a pocket mouth or a ring entrance;
    /// - every piece is a box without end overlap plus a vertical capsule at each end: rounded ends and rounded
    ///   corners where two runs meet;
    /// - collinear touching pieces (from neighbouring road quads) are merged, so there are no seams to catch on.
    /// Build() logs how much curb footprint lies on drivable surface (should be ~0).
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

        struct Piece { public Vector2 a, b, n; public bool openA, openB; }   // curb along a→b, outward normal n; open = ends at an opening

        const float CapRadius = Thickness * 0.5f;

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
            pieces = MergeCollinear(pieces);
            TrimOpenEnds(pieces);

            var root = new GameObject(RootName);
            root.transform.SetParent(city.transform, false);
            var phys = LoadOrCreatePhysics();
            var combine = new List<CombineInstance>();
            var cubeTemp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var cubeMesh = cubeTemp.GetComponent<MeshFilter>().sharedMesh;

            var cylTemp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            var cylMesh = cylTemp.GetComponent<MeshFilter>().sharedMesh;
            int i = 0;
            foreach (var p in pieces)
            {
                Vector2 dir = p.b - p.a;
                float len = dir.magnitude;                       // no end overlap: the caps round the ends instead
                if (len < 0.05f) continue;
                Vector2 mid = (p.a + p.b) * 0.5f + p.n * CapRadius;
                var rot = Quaternion.LookRotation(new Vector3(p.n.x, 0f, p.n.y), Vector3.up);   // local Z = outward

                var go = new GameObject("Curb_" + i++);
                go.layer = curbLayer;
                go.transform.SetParent(root.transform, false);
                go.transform.SetPositionAndRotation(new Vector3(mid.x, 0f, mid.y), rot);
                var col = go.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, ColliderHeight * 0.5f, 0f);
                col.size = new Vector3(len, ColliderHeight, Thickness);
                col.sharedMaterial = phys;
                // Rounded ends: a vertical capsule (a post) at each end, tangent to the road edge.
                for (int e = -1; e <= 1; e += 2)
                {
                    var cap = go.AddComponent<CapsuleCollider>();
                    cap.direction = 1;
                    cap.radius = CapRadius;
                    cap.height = ColliderHeight;
                    cap.center = new Vector3(e * len * 0.5f, ColliderHeight * 0.5f, 0f);
                    cap.sharedMaterial = phys;
                }
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);

                combine.Add(new CombineInstance
                {
                    mesh = cubeMesh,
                    transform = Matrix4x4.TRS(new Vector3(mid.x, VisualHeight * 0.5f, mid.y), rot, new Vector3(len, VisualHeight, Thickness)),
                });
                for (int e = -1; e <= 1; e += 2)
                {
                    Vector2 end = mid + dir.normalized * (e * len * 0.5f);
                    combine.Add(new CombineInstance
                    {
                        mesh = cylMesh,   // unit cylinder is 2 high: scale y by half the curb height
                        transform = Matrix4x4.TRS(new Vector3(end.x, VisualHeight * 0.5f, end.y), Quaternion.identity,
                            new Vector3(Thickness, VisualHeight * 0.5f, Thickness)),
                    });
                }
            }
            Object.DestroyImmediate(cubeTemp);
            Object.DestroyImmediate(cylTemp);
            LogIntrusion(pieces, shapes);

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
                        // Exact ends: where the outside switches between open road and nothing (an opening),
                        // instead of the cell boundary. Corner ends of the rectangle stay where they are.
                        bool openA = runStart > 0, openB = i < cells;
                        float ta = openA ? Transition(a, dir, n, (runStart - 0.5f) * cell, (runStart + 0.5f) * cell, shapes) : 0f;
                        float tb = openB ? Transition(a, dir, n, (i + 0.5f) * cell, (i - 0.5f) * cell, shapes) : len;
                        if (tb - ta > 0.05f)
                            outList.Add(new Piece { a = a + dir * ta, b = a + dir * tb, n = n, openA = openA, openB = openB });
                        runStart = -1;
                    }
                }
            }
        }

        /// <summary>
        /// Along the edge, between tCovered (outside is road) and tFree (outside is nothing), the distance where it
        /// switches, to about 1 cm. Returned on the free side, so the curb never extends over open road.
        /// </summary>
        static float Transition(Vector2 a, Vector2 dir, Vector2 n, float tCovered, float tFree, List<Shape> shapes)
        {
            for (int k = 0; k < 10; k++)
            {
                float m = (tCovered + tFree) * 0.5f;
                if (Covered(a + dir * m + n * Probe, shapes)) tCovered = m; else tFree = m;
            }
            return tFree;
        }

        /// <summary>Ends at an opening are pulled back by the cap radius, so the rounded cap sits fully outside the opening.</summary>
        static void TrimOpenEnds(List<Piece> pieces)
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                var p = pieces[i];
                Vector2 d = p.b - p.a;
                float len = d.magnitude;
                if (len < 0.01f) continue;
                Vector2 dir = d / len;
                float trimA = p.openA ? CapRadius : 0f, trimB = p.openB ? CapRadius : 0f;
                if (len - trimA - trimB < 0.05f) { pieces.RemoveAt(i--); continue; }
                p.a += dir * trimA;
                p.b -= dir * trimB;
                pieces[i] = p;
            }
        }

        /// <summary>Joins pieces on the same line, facing the same way, that touch or overlap (neighbouring road quads).</summary>
        static List<Piece> MergeCollinear(List<Piece> pieces)
        {
            var list = new List<Piece>(pieces);
            bool merged = true;
            while (merged)
            {
                merged = false;
                for (int i = 0; i < list.Count && !merged; i++)
                for (int j = i + 1; j < list.Count && !merged; j++)
                {
                    var p = list[i]; var q = list[j];
                    if (Vector2.Dot(p.n, q.n) < 0.999f) continue;
                    Vector2 d = p.b - p.a;
                    float len = d.magnitude;
                    if (len < 0.01f) continue;
                    Vector2 dir = d / len;
                    // same line: both ends of q lie on the line of p
                    if (Mathf.Abs(Vector2.Dot(q.a - p.a, p.n)) > 0.03f || Mathf.Abs(Vector2.Dot(q.b - p.a, p.n)) > 0.03f) continue;
                    float qa = Vector2.Dot(q.a - p.a, dir), qb = Vector2.Dot(q.b - p.a, dir);
                    float qmin = Mathf.Min(qa, qb), qmax = Mathf.Max(qa, qb);
                    if (qmin > len + 0.05f || qmax < -0.05f) continue;   // apart along the line
                    float lo = Mathf.Min(0f, qmin), hi = Mathf.Max(len, qmax);
                    bool openLo = lo < 0f ? (qa < qb ? q.openA : q.openB) : p.openA;
                    bool openHi = hi > len ? (qa < qb ? q.openB : q.openA) : p.openB;
                    list[i] = new Piece { a = p.a + dir * lo, b = p.a + dir * hi, n = p.n, openA = openLo, openB = openHi };
                    list.RemoveAt(j);
                    merged = true;
                }
            }
            return list;
        }

        /// <summary>Logs the curb footprint (box + caps) that lies on drivable surface; it should be close to zero.</summary>
        static void LogIntrusion(List<Piece> pieces, List<Shape> shapes)
        {
            int samples = 0, onRoad = 0, badPieces = 0;
            foreach (var p in pieces)
            {
                Vector2 d = p.b - p.a;
                float len = d.magnitude;
                if (len < 0.05f) continue;
                Vector2 dir = d / len;
                int bad = 0;
                // Sample the footprint from 5 cm outside the road edge outward, round caps included.
                for (float t = -CapRadius; t <= len + CapRadius + 1e-3f; t += 0.1f)
                for (float o = 0.05f; o <= Thickness - 0.05f + 1e-3f; o += 0.1f)
                {
                    Vector2 pt = p.a + dir * t + p.n * o;
                    if (t < 0f || t > len)
                    {
                        Vector2 c = (t < 0f ? p.a : p.b) + p.n * CapRadius;
                        if ((pt - c).sqrMagnitude > CapRadius * CapRadius) continue;   // outside the round cap
                    }
                    samples++;
                    if (Covered(pt, shapes)) { onRoad++; bad++; }
                }
                if (bad > 0) badPieces++;
            }
            LastReport = "[Taxi] Curbs: " + pieces.Count + " pieces; footprint samples on drivable surface: " + onRoad + "/" + samples
                         + " (" + (samples > 0 ? 100f * onRoad / samples : 0f).ToString("0.0") + "%), pieces touching road: " + badPieces;
            Debug.Log(LastReport);
        }

        /// <summary>The last intrusion report of Build() (for tools that cannot read the console).</summary>
        public static string LastReport { get; private set; }

        /// <summary>Same check for the curbs currently in the scene, read from their colliders (before/after comparisons).</summary>
        public static string MeasureScene()
        {
            var city = GameObject.Find("City");
            var shapes = CollectShapes(city.transform);
            var curbs = city.transform.Find(RootName);
            int samples = 0, onRoad = 0, bad = 0, n = 0;
            foreach (var box in curbs.GetComponentsInChildren<BoxCollider>())
            {
                n++;
                var tr = box.transform;
                int b = 0;
                for (float x = -box.size.x * 0.5f; x <= box.size.x * 0.5f + 1e-3f; x += 0.1f)
                for (float z = -box.size.z * 0.5f + 0.05f; z <= box.size.z * 0.5f - 0.05f + 1e-3f; z += 0.1f)
                {
                    Vector3 w = tr.TransformPoint(new Vector3(x, 0f, z) + new Vector3(box.center.x, 0f, box.center.z));
                    samples++;
                    if (Covered(new Vector2(w.x, w.z), shapes)) { onRoad++; b++; }
                }
                foreach (var cap in box.GetComponents<CapsuleCollider>())
                for (float x = -cap.radius; x <= cap.radius + 1e-3f; x += 0.1f)
                for (float z = -cap.radius; z <= cap.radius + 1e-3f; z += 0.1f)
                {
                    if (x * x + z * z > cap.radius * cap.radius) continue;
                    Vector3 w = tr.TransformPoint(new Vector3(cap.center.x + x, 0f, cap.center.z + z));
                    samples++;
                    if (Covered(new Vector2(w.x, w.z), shapes)) { onRoad++; b++; }
                }
                if (b > 0) bad++;
            }
            return n + " curb objects; footprint samples on drivable surface " + onRoad + "/" + samples + " ("
                   + (samples > 0 ? 100f * onRoad / samples : 0f).ToString("0.0") + "%), objects touching road " + bad;
        }

        static void DiscEdges(Shape s, List<Shape> shapes, List<Piece> outList)
        {
            int cells = Mathf.Max(12, Mathf.CeilToInt(2f * Mathf.PI * s.r / 0.75f));
            float da = 2f * Mathf.PI / cells;
            var open = new bool[cells];   // outside of this arc cell is other road (a ring entrance)
            for (int i = 0; i < cells; i++)
            {
                float am = (i + 0.5f) * da;
                open[i] = Covered(s.c + new Vector2(Mathf.Cos(am), Mathf.Sin(am)) * (s.r + Probe), shapes);
            }
            for (int i = 0; i < cells; i++)
            {
                if (open[i]) continue;
                float am = (i + 0.5f) * da;
                Vector2 nm = new Vector2(Mathf.Cos(am), Mathf.Sin(am));
                Vector2 a = s.c + new Vector2(Mathf.Cos(i * da), Mathf.Sin(i * da)) * s.r;
                Vector2 b = s.c + new Vector2(Mathf.Cos((i + 1) * da), Mathf.Sin((i + 1) * da)) * s.r;
                // Ends next to an entrance get trimmed (TrimOpenEnds) so the cap does not poke into it.
                outList.Add(new Piece { a = a, b = b, n = nm, openA = open[(i + cells - 1) % cells], openB = open[(i + 1) % cells] });
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
