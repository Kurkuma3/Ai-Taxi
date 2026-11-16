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
        [Tooltip("Fraction of speed kept after a head-on hit. Glancing hits keep proportionally more.")]
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
        [Tooltip("If both sides of the car are blocked, search for a free spot on the road up to this distance from the car.")]
        public float exitSearchRadius = 4.5f;

        [Header("Fares")]
        public float baseFare = 10f;
        [Tooltip("Dollars per metre of straight-line distance between the pickup and the drop-off stop.")]
        public float farePerMeter = 0.15f;
        [Tooltip("Fare multiplier per zone (element 0 = Zone 1). A trip uses the larger of its two zones.")]
        public float[] zoneFareMultipliers = { 1.0f, 1.2f, 1.4f, 1.7f };

        [Header("Trip length (straight line, metres)")]
        [Tooltip("Min/max trip distance by the highest open zone (element 0 = only Zone 1 open). Fitted to the current map: " +
                 "the longest stop pair is 68 m in Zone 1 and 209 m over the whole city.")]
        public Vector2[] tripDistanceByHighestZone =
        {
            new Vector2(40f, 70f), new Vector2(60f, 115f), new Vector2(80f, 190f), new Vector2(100f, 210f)
        };
        [Tooltip("If no pair of stops fits the range, pick at random among this many pairs closest to it " +
                 "(1 = always the single closest, which repeats the same route).")]
        [Range(1, 8)] public int fallbackPairChoices = 3;
        [Tooltip("The last N routes (A-B in either direction) are never offered again while any other pair exists. " +
                 "This beats the distance range: a shorter new route is preferred over a repeat.")]
        [Range(0, 8)] public int recentRoutesToAvoid = 2;
        [Tooltip("After a zone opens, this share of new trips starts or ends in it.")]
        [Range(0f, 1f)] public float newZoneTripChance = 0.5f;
        [Tooltip("How many trips after a zone opens the new-zone preference lasts.")]
        public int newZonePriorityTrips = 4;

        [Header("Tutorial")]
        [Tooltip("The first N trips pick up at the stop marked with PassengerStop.tutorialMission = 1..N (the taxi depot lot).")]
        public int tutorialTrips = 2;

        [Header("Passenger")]
        [Tooltip("The car must stop with its centre within this distance of the stop marker to pick up / drop off.")]
        public float stopRadius = 7f;
        [Tooltip("The waiting passenger waves while the car is within this distance.")]
        public float waveDistance = 30f;
        public float passengerWalkSpeed = 2.5f;
        [Tooltip("Boarding / getting out never takes longer than this; the passenger snaps to the end point.")]
        public float passengerWalkMaxTime = 1.5f;
        [Tooltip("Pause between a paid trip and the next passenger appearing.")]
        public float nextTripDelay = 1.0f;
        [Tooltip("Fare label height above the passenger's feet, metres.")]
        public float fareLabelHeight = 2.4f;

        [Header("Payment")]
        [Tooltip("Money in the wallet when the game starts.")]
        public int startMoney = 50;
        public float moneyCountUpTime = 0.8f;
        public int coinsPerPayment = 8;
        public float coinFlightTime = 0.55f;
        [Tooltip("Delay between consecutive coins, seconds.")]
        public float coinStagger = 0.05f;

        [Header("Route hint")]
        [Tooltip("Distance from the car (or the player on foot) at which the ground arrow circles.")]
        public float routeArrowRadius = 3.6f;
        [Tooltip("The arrow hides when the target is closer than this.")]
        public float routeArrowHideDistance = 9f;
    }
}
