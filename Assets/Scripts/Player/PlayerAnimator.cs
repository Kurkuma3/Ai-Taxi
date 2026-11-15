using UnityEngine;

namespace Taxi
{
    /// <summary>
    /// Feeds the on-foot character's Animator. One float parameter, "Speed":
    /// planar CharacterController speed divided by walkSpeed, times walkAnimSpeedMultiplier.
    /// The controller switches Idle/Walk on it and uses it as the Walk playback rate, so the
    /// step frequency follows the actual speed instead of sliding.
    /// </summary>
    public class PlayerAnimator : MonoBehaviour
    {
        [SerializeField] GameplayConfig config;
        [SerializeField] PlayerController player;
        [SerializeField] Animator animator;

        static readonly int SpeedHash = Animator.StringToHash("Speed");

        void Update()
        {
            if (animator == null || !animator.isActiveAndEnabled) return;
            float normalized = config.walkSpeed > 0f ? player.Speed / config.walkSpeed : 0f;
            animator.SetFloat(SpeedHash, normalized * config.walkAnimSpeedMultiplier, config.walkAnimDampTime, Time.deltaTime);
        }
    }
}
