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

        void Start()
        {
            carBlend = IsCarTarget ? 1f : 0f;
            Vector3 off = CurrentOffset();
            transform.rotation = Quaternion.LookRotation(-off.normalized, Vector3.up);
            if (target != null) transform.position = target.position + off;
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
                float signed = car.Speed / Mathf.Max(0.1f, config.maxSpeed);
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
            transform.position = Vector3.Lerp(transform.position, goal, t);
            transform.rotation = Quaternion.LookRotation(-off.normalized, Vector3.up);
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
