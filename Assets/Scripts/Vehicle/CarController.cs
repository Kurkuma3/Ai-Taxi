using UnityEngine;

namespace Taxi
{
    /// <summary>
    /// Arcade car driven by one floating stick: stick direction = where to go, stick deflection = throttle,
    /// released (or inside the deadzone) = brake to a stop. Rotation on X/Z is frozen so it can never flip.
    ///
    /// Reverse is automatic: pull the stick to the sector behind the car (reverseEnterAngle or more off the
    /// heading). If the car is still rolling forward it brakes first; once slow it backs up, aiming its rear
    /// at the stick, so a car stuck nose-first in a wall can always be pulled back out. Swinging the stick
    /// back toward the front (inside reverseExitAngle) returns to forward driving.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CarController : MonoBehaviour
    {
        [SerializeField] GameplayConfig config;
        [Tooltip("Engine and Steering upgrades scale acceleration and turn rate. Optional.")]
        [SerializeField] Upgrades upgrades;

        public bool InputEnabled { get; set; }
        /// <summary>Signed: positive forward, negative while backing up.</summary>
        public float Speed { get; private set; }
        public bool HasInput { get; private set; }
        public bool IsReversing { get; private set; }
        /// <summary>Current forward top speed (config.maxSpeed, raised by the Engine upgrade).</summary>
        public float MaxSpeed => upgrades != null ? upgrades.MaxSpeed : config.maxSpeed;
        public bool IsStopped =>!HasInput && Mathf.Abs(Speed) < config.stoppedSpeedThreshold;

        Rigidbody rb;
        float targetSpeed;
        float steer; // -1 left, +1 right

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        void Update()
        {
            var i = InputReader.I;
            Vector2 s = InputEnabled && i != null && i.StickActive ? i.StickVector : Vector2.zero;
            HasInput = s.sqrMagnitude > config.stickDeadzone * config.stickDeadzone;
            if (!HasInput)
            {
                steer = 0f;
                targetSpeed = 0f;
                if (Speed >= 0f) IsReversing = false; // keep the mode only while still rolling backward
                return;
            }

            // Screen up = world +Z.
            float heading = transform.eulerAngles.y;
            float desiredYaw = Mathf.Atan2(s.x, s.y) * Mathf.Rad2Deg;
            float offAngle = Mathf.Abs(Mathf.DeltaAngle(heading, desiredYaw)); // 0 = ahead, 180 = behind

            // Hysteresis between enter/exit angles so the mode never flickers near the boundary.
            if (IsReversing) { if (offAngle < config.reverseExitAngle) IsReversing = false; }
            else if (offAngle >= config.reverseEnterAngle) IsReversing = true;

            float throttle = Mathf.InverseLerp(config.stickDeadzone, 1f, s.magnitude);

            if (IsReversing && Speed > config.reverseEnterSpeed)
            {
                // Still rolling forward: a backward stick is the brake. Straight, so it feels predictable.
                steer = 0f;
                targetSpeed = 0f;
                return;
            }

            // Forward: aim the nose at the stick. Reverse: aim the rear at the stick.
            float aimFrom = IsReversing ? heading + 180f : heading;
            float delta = Mathf.DeltaAngle(aimFrom, desiredYaw);
            steer = Mathf.Clamp(delta / config.steerFullAngle, -1f, 1f);

            // Throttle from deflection; slow down a bit while turning hard.
            float top = IsReversing ? -config.reverseMaxSpeed : MaxSpeed;
            targetSpeed = top * throttle * Mathf.Lerp(1f, config.turningSpeedFactor, Mathf.Abs(steer));
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            // Accelerate only when target and current speed agree in direction and the target is further out;
            // changing direction or slowing down always uses the brake rate.
            bool speedingUp = targetSpeed * Speed >= 0f && Mathf.Abs(targetSpeed) > Mathf.Abs(Speed);
            float rate = speedingUp ? config.acceleration * (upgrades != null ? upgrades.AccelerationMultiplier : 1f)
                                    : config.brakeDeceleration;
            Speed = Mathf.MoveTowards(Speed, targetSpeed, rate * dt);

            if (steer != 0f && HasInput)
            {
                float k = Mathf.Lerp(config.minTurnFactor, 1f, Mathf.Clamp01(Mathf.Abs(Speed) / config.fullTurnSpeed));
                float turnRate = config.turnRateDegPerSec * (upgrades != null ? upgrades.TurnMultiplier : 1f);
                rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, steer * turnRate * k * dt, 0f));
            }

            Vector3 v = transform.forward * Speed;
            v.y = rb.linearVelocity.y; // keep gravity
            rb.linearVelocity = v;
        }

        void OnCollisionEnter(Collision c)
        {
            // Lose speed in proportion to how head-on the impact is: a frontal hit keeps collisionSpeedKeep,
            // grazing a curb keeps almost everything, so the car slides along edges instead of stopping.
            if (c.rigidbody != null && !c.rigidbody.isKinematic) return;
            Vector3 moveDir = transform.forward * (Speed >= 0f ? 1f : -1f);
            Vector3 n = c.GetContact(0).normal;
            n.y = 0f;
            float headOn = n.sqrMagnitude > 1e-4f ? Mathf.Clamp01(Vector3.Dot(moveDir, -n.normalized)) : 1f;
            Speed *= Mathf.Lerp(1f, config.collisionSpeedKeep, headOn);
        }
    }
}
