using UnityEngine;

namespace Taxi
{
    /// <summary>
    /// Tyre marks from the rear wheels: flat TrailRenderers that emit while the car skids (hard turn at speed or hard
    /// braking at speed) and fade out over skidFadeTime. Each wheel has a small pool of trails used in turn, so a new
    /// skid never joins the previous mark with a straight line and old marks keep fading. Built in Awake, one material.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class SkidMarks : MonoBehaviour
    {
        [SerializeField] GameplayConfig config;
        [Tooltip("Transparent dark material for the marks (vertex colour + alpha).")]
        [SerializeField] Material material;

        const int TrailsPerWheel = 3;
        CarController car;
        TrailRenderer[,] trails;
        int[] current = new int[2];
        bool skidding;

        void Awake()
        {
            car = GetComponent<CarController>();
            trails = new TrailRenderer[2, TrailsPerWheel];
            for (int w = 0; w < 2; w++)
            for (int k = 0; k < TrailsPerWheel; k++)
                trails[w, k] = MakeTrail(w == 0 ? -0.9f : 0.9f, w, k);
        }

        TrailRenderer MakeTrail(float x, int wheel, int k)
        {
            var go = new GameObject("Skid_" + (wheel == 0 ? "L" : "R") + k);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(x, 0.03f, -1.3f);   // under the rear wheel, just above the road
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);    // trail lies flat (faces up)
            var t = go.AddComponent<TrailRenderer>();
            t.sharedMaterial = material;
            t.alignment = LineAlignment.TransformZ;
            t.time = config.skidFadeTime;
            t.widthMultiplier = config.skidWidth;
            t.minVertexDistance = 0.25f;
            t.emitting = false;
            t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            t.receiveShadows = false;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.black, 0f), new GradientColorKey(Color.black, 1f) },
                      new[] { new GradientAlphaKey(config.skidAlpha, 0f), new GradientAlphaKey(0f, 1f) });
            t.colorGradient = g;
            return t;
        }

        void Update()
        {
            float speed = Mathf.Abs(car.Speed);
            bool hardTurn = speed >= config.skidMinSpeed && car.SteerAmount >= config.skidSteer;
            bool hardBrake = car.IsBraking && car.Speed >= config.skidBrakeMinSpeed;
            bool skid = hardTurn || hardBrake;
            if (skid == skidding) return;
            skidding = skid;
            for (int w = 0; w < 2; w++)
            {
                if (skid)
                {
                    current[w] = (current[w] + 1) % TrailsPerWheel;   // fresh trail: old marks keep fading on their own
                    var t = trails[w, current[w]];
                    t.Clear();
                    t.emitting = true;
                }
                else trails[w, current[w]].emitting = false;
            }
        }
    }
}
