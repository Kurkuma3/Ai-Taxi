using UnityEngine;

namespace Taxi
{
    /// <summary>
    /// Top-down follow camera with zero yaw (screen-up = world +Z, the steering relies on it).
    /// On foot: close (cameraFootOffset). In the car: higher (cameraCarOffset), zooms out with speed
    /// (+cameraSpeedZoomHeight at max speed) and looks ahead along the direction of travel, so the road ahead is
    /// visible on a phone. Getting in/out blends between the two offsets; the pitch follows the blended offset.
    /// All numbers come from GameplayConfig (header Camera).
    /// </summary>
    public class TopDownCamera : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] GameplayConfig config;
        [SerializeField] CarController car;

        public Transform Target { get => target; set => target = value; }

        float carBlend;     // 0 = foot offset, 1 = car offset
        float zoom;         // 0..1, smoothed |speed| / maxSpeed
        Vector3 lookAhead;  // smoothed world offset in front of the car
        Vector3 basePos;    // smoothed follow position without shake
        float shakeStart = -10f, shakeAmp;

        void OnEnable() { if (car != null) car.Bumped += OnBump; }
        void OnDisable() { if (car != null) car.Bumped -= OnBump; }

        /// <summary>Shake on a hit: amplitude grows with the impact speed above cameraShakeMinImpact.</summary>
        void OnBump(float impact)
        {
            float k = Mathf.InverseLerp(config.cameraShakeMinImpact, config.cameraShakeFullImpact, impact);
            if (k <= 0f) return;   // light touch: no shake
            float amp = k * config.cameraShakeAmplitude;
            if (Time.time - shakeStart < config.cameraShakeTime && amp < shakeAmp) return;   // a stronger shake is running
            shakeAmp = amp;
            shakeStart = Time.time;
        }

        void Start()
        {
            carBlend = IsCarTarget ? 1f : 0f;
            Vector3 off = CurrentOffset();
            transform.rotation = Quaternion.LookRotation(-off.normalized, Vector3.up);
            if (target != null) transform.position = target.position + off;
            basePos = transform.position;
        }

        bool IsCarTarget => car != null && target == car.transform;

        void LateUpdate()
        {
            if (target == null || config == null) return;
            float dt = Time.deltaTime;

            carBlend = Mathf.Lerp(carBlend, IsCarTarget ? 1f : 0f, 1f - Mathf.Exp(-config.cameraModeBlendRate * dt));

            // Speed zoom and look-ahead only mean something while the camera follows the car.
            float speed01 = 0f;
            Vector3 aheadGoal = Vector3.zero;
            if (IsCarTarget)
            {
                float signed = car.Speed / Mathf.Max(0.1f, car.MaxSpeed);   // the current top speed (Engine upgrade)
                speed01 = Mathf.Clamp01(Mathf.Abs(signed));
                Vector3 fwd = car.transform.forward;
                fwd.y = 0f;
                aheadGoal = fwd.normalized * (config.cameraLookAhead * Mathf.Clamp(signed, -1f, 1f));   // reversing looks behind
            }
            float zoomRate = speed01 > zoom ? config.cameraZoomOutRate : config.cameraZoomInRate;
            zoom = Mathf.Lerp(zoom, speed01, 1f - Mathf.Exp(-zoomRate * dt));
            lookAhead = Vector3.Lerp(lookAhead, aheadGoal, 1f - Mathf.Exp(-config.cameraLookAheadRate * dt));

            Vector3 off = CurrentOffset();
            Vector3 goal = target.position + lookAhead * carBlend + off;
            float t = 1f - Mathf.Exp(-config.cameraFollowSmoothing * dt);
            basePos = Vector3.Lerp(basePos, goal, t);
            transform.position = basePos + ShakeOffset();
            transform.rotation = Quaternion.LookRotation(-off.normalized, Vector3.up);
        }

        /// <summary>Decaying jitter in the ground plane (Perlin, no allocations) for cameraShakeTime after a hit.</summary>
        Vector3 ShakeOffset()
        {
            float k = (Time.time - shakeStart) / Mathf.Max(0.01f, config.cameraShakeTime);
            if (k >= 1f) return Vector3.zero;
            float a = shakeAmp * (1f - k) * (1f - k);
            float f = Time.time * config.cameraShakeFrequency;
            return new Vector3((Mathf.PerlinNoise(f, 0.3f) - 0.5f) * 2f * a, 0f, (Mathf.PerlinNoise(0.7f, f) - 0.5f) * 2f * a);
        }

        Vector3 CurrentOffset()
        {
            if (config == null) return new Vector3(0f, 18f, -8f);
            Vector3 carOff = config.cameraCarOffset;
            if (carOff.y > 0.01f) carOff += carOff * (config.cameraSpeedZoomHeight * zoom / carOff.y);   // back off along the view line
            return Vector3.Lerp(config.cameraFootOffset, carOff, carBlend);
        }
    }
}
