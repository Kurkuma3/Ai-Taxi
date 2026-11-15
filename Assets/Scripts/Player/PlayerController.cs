using UnityEngine;

namespace Taxi
{
    /// <summary>
    /// On-foot movement with one finger: while the stick is held the player walks forward;
    /// dragging the stick turns them toward that screen direction (camera looks along +Z).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] GameplayConfig config;
        [SerializeField] GameObject visual;

        public bool InputEnabled { get; set; }
        /// <summary>Horizontal speed of the CharacterController, m/s. Zero while hidden (driving).</summary>
        public float Speed { get; private set; }

        CharacterController cc;
        float verticalVelocity;

        void Awake() { cc = GetComponent<CharacterController>(); }

        void Update()
        {
            if (!cc.enabled) { Speed = 0f; return; }
            var i = InputReader.I;
            Vector3 move = Vector3.zero;

            if (InputEnabled && i != null && i.StickActive)
            {
                Vector2 s = i.StickVector;
                Vector3 dir = s.sqrMagnitude > config.stickDeadzone * config.stickDeadzone
                    ? new Vector3(s.x, 0f, s.y).normalized
                    : transform.forward;
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, Quaternion.LookRotation(dir, Vector3.up),
                    config.playerTurnRateDegPerSec * Time.deltaTime);
                move = transform.forward * config.walkSpeed;
            }

            verticalVelocity = cc.isGrounded ? -1f : verticalVelocity - 20f * Time.deltaTime;
            cc.Move((move + Vector3.up * verticalVelocity) * Time.deltaTime);
            Vector3 v = cc.velocity;
            Speed = new Vector2(v.x, v.z).magnitude;
        }

        public void SetVisible(bool visible)
        {
            visual.SetActive(visible);
            cc.enabled = visible;
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            bool wasEnabled = cc.enabled;
            cc.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            cc.enabled = wasEnabled;
        }
    }
}
