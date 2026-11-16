using UnityEngine;

namespace Taxi
{
    /// <summary>
    /// Visual side of one passenger model (Passenger_A / Passenger_B prefab instance): walk, wave, face, hide.
    /// Moves by transform (no physics): walks are short, straight and time-limited by the trip manager.
    /// </summary>
    public class Passenger : MonoBehaviour
    {
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int WaveId = Animator.StringToHash("Wave");
        const float TurnRateDeg = 540f;

        Animator animator;
        GameplayConfig config;

        public void Init(GameplayConfig cfg)
        {
            config = cfg;
            animator = GetComponentInChildren<Animator>();
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        }

        public void SetVisible(bool on)
        {
            if (gameObject.activeSelf != on) gameObject.SetActive(on);
        }

        public void PlaceAt(Vector3 pos, float yaw)
        {
            transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            SetWalking(false);
            SetWave(false);
        }

        public void SetWave(bool on)
        {
            if (animator.GetBool(WaveId) != on) animator.SetBool(WaveId, on);
        }

        public void TurnTowards(Vector3 point, float dt)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return;
            var target = Quaternion.LookRotation(d);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, TurnRateDeg * dt);
        }

        /// <summary>Walks toward a ground point; returns true once there.</summary>
        public bool WalkTowards(Vector3 point, float dt)
        {
            point.y = transform.position.y;
            Vector3 d = point - transform.position;
            float step = config.passengerWalkSpeed * dt;
            if (d.sqrMagnitude <= step * step)
            {
                transform.position = point;
                SetWalking(false);
                return true;
            }
            TurnTowards(point, dt);
            transform.position += d.normalized * step;
            SetWalking(true);
            return false;
        }

        public void SetWalking(bool walking)
        {
            // Same mapping as the driver's PlayerAnimator: 1 = the authored 1 s walk cycle at walkSpeed.
            float s = walking ? config.passengerWalkSpeed / Mathf.Max(0.01f, config.walkSpeed) * config.walkAnimSpeedMultiplier : 0f;
            animator.SetFloat(SpeedId, s);
            if (walking) SetWave(false);
        }
    }
}
