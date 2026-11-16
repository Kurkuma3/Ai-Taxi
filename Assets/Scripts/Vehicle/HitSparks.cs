using UnityEngine;

namespace Taxi
{
    /// <summary>
    /// Sparks and dust where the car hits a curb or wall: one world-space ParticleSystem (built in Awake, capped at
    /// 96 particles) moved to the contact point and burst with Emit(count) on CarController.Bumped. Count grows with
    /// the impact speed. No allocations per hit.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class HitSparks : MonoBehaviour
    {
        [SerializeField] GameplayConfig config;
        [Tooltip("Transparent particle material (vertex colour).")]
        [SerializeField] Material material;

        CarController car;
        ParticleSystem ps;

        void Awake()
        {
            car = GetComponent<CarController>();
            var go = new GameObject("HitSparks");   // not parented: world space, stays where the hit happened
            go.transform.SetParent(null);
            ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 96;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.18f);
            main.gravityModifier = 1.2f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.35f), new Color(0.75f, 0.72f, 0.65f));   // spark .. dust

            var emission = ps.emission;
            emission.enabled = false;   // bursts only, from Emit()

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;   // opens along +Z of the system = away from the obstacle
            shape.angle = 55f;
            shape.radius = 0.15f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        void OnEnable() { if (car != null) car.Bumped += OnBump; }
        void OnDisable() { if (car != null) car.Bumped -= OnBump; }

        void OnBump(float impact)
        {
            float k = Mathf.InverseLerp(config.bumpMinImpactSpeed, config.cameraShakeFullImpact, impact);
            int count = Mathf.RoundToInt(Mathf.Lerp(config.sparksMin, config.sparksMax, k));
            if (count <= 0) return;
            Vector3 n = car.LastBumpNormal;
            Vector3 up = n + Vector3.up * 0.6f;   // fan out and a bit upward
            ps.transform.SetPositionAndRotation(car.LastBumpPoint + Vector3.up * 0.3f, Quaternion.LookRotation(up.normalized));
            ps.Emit(count);
        }

        void OnDestroy()
        {
            if (ps != null) Destroy(ps.gameObject);
        }
    }
}
