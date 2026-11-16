using UnityEngine;

namespace Taxi
{
    /// <summary>
    /// Visual body tilt: at Start the body parts (everything under the car except wheels and skid trails) move under a
    /// "BodyTilt" pivot on the ground; the pivot then rolls outward in turns (yaw rate x speed), dips the nose when
    /// braking and squats when accelerating (from the measured acceleration). Physics and colliders are untouched.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class CarBodyTilt : MonoBehaviour
    {
        [SerializeField] GameplayConfig config;

        CarController car;
        Transform pivot;
        float lastYaw, lastSpeed, roll, pitch;

        void Start()
        {
            car = GetComponent<CarController>();
            // The model lives under "Visual" (Body, Cabin, Sign, Wheels); the light plates are on the root.
            var visual = transform.Find("Visual");
            pivot = new GameObject("BodyTilt").transform;
            pivot.SetParent(visual != null ? visual : transform, false);
            MoveBodyParts(visual);
            MoveBodyParts(transform);
            lastYaw = transform.eulerAngles.y;
            lastSpeed = car.Speed;
        }

        /// <summary>Moves the children of 'from' under the pivot, except wheels, skid trails, Visual and the pivot itself.</summary>
        void MoveBodyParts(Transform from)
        {
            if (from == null) return;
            for (int i = from.childCount - 1; i >= 0; i--)
            {
                var c = from.GetChild(i);
                if (c == pivot || c.name == "Visual" || c.name.StartsWith("Wheel") || c.name.StartsWith("Skid_")) continue;
                c.SetParent(pivot, true);
            }
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            float yaw = transform.eulerAngles.y;
            float yawRate = Mathf.DeltaAngle(lastYaw, yaw) / dt;      // deg/s, + = turning right
            float accel = (car.Speed - lastSpeed) / dt;               // m/s^2, signed along the heading
            lastYaw = yaw;
            lastSpeed = car.Speed;

            float speed01 = Mathf.Clamp01(Mathf.Abs(car.Speed) / Mathf.Max(0.1f, car.MaxSpeed));
            // Turning right pushes the body outward: lean left (+Z roll). Braking: nose down (+X pitch).
            float rollGoal = Mathf.Clamp(yawRate * speed01 * config.tiltRollFactor, -config.tiltRollMax, config.tiltRollMax);
            float dir = car.Speed >= 0f ? 1f : -1f;
            float pitchGoal = Mathf.Clamp(-accel * dir * config.tiltPitchFactor, -config.tiltPitchMax, config.tiltPitchMax);
            float k = 1f - Mathf.Exp(-config.tiltResponse * dt);
            roll = Mathf.Lerp(roll, rollGoal, k);
            pitch = Mathf.Lerp(pitch, pitchGoal, k);
            pivot.localRotation = Quaternion.Euler(pitch, 0f, roll);
        }
    }
}
