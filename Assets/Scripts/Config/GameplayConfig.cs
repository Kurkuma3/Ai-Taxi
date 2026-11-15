using UnityEngine;

namespace Taxi
{
    /// <summary>All tunable gameplay numbers live here, never hardcoded in behaviours.</summary>
    [CreateAssetMenu(menuName = "Taxi/Gameplay Config", fileName = "GameplayConfig")]
    public class GameplayConfig : ScriptableObject
    {
        [Header("Car")]
        public float maxSpeed = 12f;
        public float acceleration = 10f;
        public float brakeDeceleration = 18f;
        [Tooltip("Speed multiplier at full steering lock.")]
        [Range(0.1f, 1f)] public float turningSpeedFactor = 0.75f;
        public float turnRateDegPerSec = 130f;
        [Tooltip("Angle between heading and stick direction at which steering is at full lock.")]
        public float steerFullAngle = 35f;
        [Tooltip("Turn rate reaches 100% at this speed. Below it, it blends down to minTurnFactor.")]
        public float fullTurnSpeed = 4f;
        [Tooltip("Turn rate fraction when almost stopped (lets you steer away from a wall).")]
        [Range(0f, 1f)] public float minTurnFactor = 0.5f;
        [Tooltip("Fraction of speed kept after hitting something.")]
        [Range(0f, 1f)] public float collisionSpeedKeep = 0.3f;
        public float stoppedSpeedThreshold = 0.25f;

        [Header("Car reverse (automatic: stick pointing behind the car)")]
        [Tooltip("Stick this many degrees or more away from the heading means 'back up', not 'turn around'.")]
        [Range(90f, 180f)] public float reverseEnterAngle = 120f;
        [Tooltip("While backing up, the stick swinging back to within this angle of the heading switches to forward.")]
        [Range(0f, 90f)] public float reverseExitAngle = 80f;
        [Tooltip("Backing up starts only once forward speed is below this; faster than that a backward stick just brakes.")]
        public float reverseEnterSpeed = 1.5f;
        public float reverseMaxSpeed = 5f;

        [Header("Stick (shared by car and player)")]
        [Tooltip("Virtual stick radius as a fraction of screen height.")]
        [Range(0.03f, 0.2f)] public float stickRadiusScreenFraction = 0.07f;
        [Tooltip("Below this deflection: player keeps heading and walks straight, car brakes.")]
        [Range(0f, 0.9f)] public float stickDeadzone = 0.25f;

        [Header("Player on foot")]
        public float walkSpeed = 4f;
        public float playerTurnRateDegPerSec = 540f;
        [Tooltip("Walk clip playback rate at full walk speed (1 = the 1 s cycle authored in Blender). Raise it if the feet slide.")]
        public float walkAnimSpeedMultiplier = 1.2f;
        [Tooltip("Smoothing of the Animator 'Speed' parameter, seconds.")]
        public float walkAnimDampTime = 0.08f;

        [Header("Enter / exit")]
        public float enterRadius = 2.4f;
        public float enterHoldTime = 1.0f;
        public float enterExitTransition = 0.25f;
        [Tooltip("To get out: hold a finger within this radius (fraction of screen height) of the stopped car.")]
        [Range(0.05f, 0.3f)] public float exitHoldRadiusScreenFraction = 0.1f;
        public Vector3 exitLocalOffset = new Vector3(-1.8f, 0f, 0f);
    }
}
