using UnityEngine;

namespace Taxi
{
    /// <summary>
    /// Route hint: a flat arrow on the ground that circles the car (or the player on foot) and points straight at the
    /// target, plus a pulsing ring with a light beam on the target stop. Built procedurally in Awake from one material;
    /// the colour (pickup vs drop-off) is set through a MaterialPropertyBlock, so it costs no extra materials.
    /// </summary>
    public class RouteHint : MonoBehaviour
    {
        [SerializeField] GameplayConfig config;
        [SerializeField] Material material;
        [SerializeField] Color pickupColor = new Color(0.25f, 0.9f, 0.45f);
        [SerializeField] Color dropoffColor = new Color(1f, 0.72f, 0.15f);

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        Transform arrow, marker, ring, beam;
        Renderer[] renderers;
        MaterialPropertyBlock block;
        bool visible;
        Color current = Color.clear;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            arrow = MakePart("Arrow", ArrowMesh(), transform);
            marker = new GameObject("Marker").transform;
            marker.SetParent(transform, false);
            ring = MakePart("Ring", RingMesh(1.6f, 2.2f, 24), marker);
            beam = MakePart("Beam", BoxMesh(0.35f, 7f), marker);
            renderers = GetComponentsInChildren<Renderer>(true);
            Hide();
        }

        /// <summary>Points from 'from' to 'target'. Call every frame while a trip is active.</summary>
        public void Show(Vector3 from, Vector3 target, bool dropoff)
        {
            if (!visible) { visible = true; marker.gameObject.SetActive(true); }
            SetColor(dropoff ? dropoffColor : pickupColor);

            float t = Time.time;
            marker.position = new Vector3(target.x, 0.06f, target.z);
            float pulse = 1f + 0.08f * Mathf.Sin(t * 5f);
            ring.localScale = new Vector3(pulse, 1f, pulse);

            Vector3 d = target - from;
            d.y = 0f;
            float dist = d.magnitude;
            bool showArrow = dist > config.routeArrowHideDistance;
            if (arrow.gameObject.activeSelf != showArrow) arrow.gameObject.SetActive(showArrow);
            if (!showArrow) return;
            Vector3 dir = d / dist;
            float bob = 0.25f * Mathf.Sin(t * 6f);   // nudges toward the target so it reads as "go this way"
            arrow.position = new Vector3(from.x, 0.08f, from.z) + dir * (config.routeArrowRadius + bob);
            arrow.rotation = Quaternion.LookRotation(dir);
        }

        public void Hide()
        {
            visible = false;
            if (arrow.gameObject.activeSelf) arrow.gameObject.SetActive(false);
            if (marker.gameObject.activeSelf) marker.gameObject.SetActive(false);
        }

        void SetColor(Color c)
        {
            if (c == current) return;
            current = c;
            block.SetColor(BaseColorId, c);
            foreach (var r in renderers) r.SetPropertyBlock(block);
        }

        Transform MakePart(string name, Mesh mesh, Transform parent)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go.transform;
        }

        // ---- procedural meshes (built once) ------------------------------------------------------------

        static Mesh ArrowMesh()
        {
            // Flat chevron arrow lying on the ground, pointing +Z, about 1.6 x 1.8 m.
            var v = new[]
            {
                new Vector3(0f, 0f, 1.0f), new Vector3(0.8f, 0f, 0.1f), new Vector3(0.3f, 0f, 0.1f),
                new Vector3(0.3f, 0f, -0.8f), new Vector3(-0.3f, 0f, -0.8f), new Vector3(-0.3f, 0f, 0.1f),
                new Vector3(-0.8f, 0f, 0.1f),
            };
            var tris = new[] { 0, 1, 2, 0, 2, 5, 0, 5, 6, 2, 3, 4, 2, 4, 5 };
            return Build("RouteArrow", v, tris);
        }

        static Mesh RingMesh(float inner, float outer, int seg)
        {
            var v = new Vector3[seg * 2];
            var tris = new int[seg * 6];
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                v[i * 2] = dir * inner;
                v[i * 2 + 1] = dir * outer;
                int n = (i + 1) % seg, k = i * 6;
                tris[k] = i * 2; tris[k + 1] = i * 2 + 1; tris[k + 2] = n * 2 + 1;
                tris[k + 3] = i * 2; tris[k + 4] = n * 2 + 1; tris[k + 5] = n * 2;
            }
            return Build("RouteRing", v, tris);
        }

        static Mesh BoxMesh(float w, float h)
        {
            float x = w * 0.5f;
            var v = new[]
            {
                new Vector3(-x, 0, -x), new Vector3(x, 0, -x), new Vector3(x, 0, x), new Vector3(-x, 0, x),
                new Vector3(-x, h, -x), new Vector3(x, h, -x), new Vector3(x, h, x), new Vector3(-x, h, x),
            };
            var tris = new[] { 0, 4, 5, 0, 5, 1, 1, 5, 6, 1, 6, 2, 2, 6, 7, 2, 7, 3, 3, 7, 4, 3, 4, 0, 4, 7, 6, 4, 6, 5 };
            return Build("RouteBeam", v, tris);
        }

        static Mesh Build(string name, Vector3[] v, int[] tris)
        {
            var m = new Mesh { name = name, vertices = v, triangles = tris };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
