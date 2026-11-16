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

        [Header("Luggage")]
        [Tooltip("Share of trips with a suitcase after the tutorial (tutorial mission 2 always has one, mission 1 never).")]
        [Range(0f, 1f)] public float luggageTripChance = 0.3f;
        [Tooltip("Added to the fare of a trip with a suitcase.")]
        public int luggageBonus = 5;
        [Tooltip("Stand within this distance of the suitcase / trunk / drop point to fill the ring.")]
        public float luggageInteractRadius = 1.6f;
        public float luggageHoldTime = 0.6f;
        [Tooltip("Trunk point: this far behind the car centre.")]
        public float trunkOffset = 2.7f;
        [Tooltip("Where the suitcase hangs relative to the player while carried (right hand side).")]
        public Vector3 carryLocalOffset = new Vector3(0.42f, 0.14f, 0.05f);
        [Tooltip("At the drop-off the suitcase goes to a point this far from the passenger, toward the car.")]
        public float luggageDropDistance = 1.2f;
        [Tooltip("Once the suitcase is picked up the passenger walks to the car door, if the car is within " +
                 "stopRadius + this of the stop (farther: they keep waiting at the stop).")]
        public float passengerFollowExtra = 3f;

        [Header("Passenger")]
        [Tooltip("The car must stop with its centre within this distance of the stop marker to pick up / drop off.")]
        public float stopRadius = 7f;
        [Tooltip("The waiting passenger waves while the car is within this distance.")]
        public float waveDistance = 30f;
        public float passengerWalkSpeed = 2.5f;
        [Tooltip("Boarding / getting out never takes longer than this; the passenger snaps to the end point.")]
        public float passengerWalkMaxTime = 1.5f;
        [Tooltip("Pause before an empty order slot gets a new waiting passenger (after one was picked up).")]
        public float nextTripDelay = 1.0f;
        [Tooltip("Fare label height above the passenger's feet, metres.")]
        public float fareLabelHeight = 2.4f;

        [Header("Orders (several passengers waiting at once)")]
        [Tooltip("How many passengers wait at the same time after the tutorial (the tutorial trips are single).")]
        [Range(1, 5)] public int offerCount = 3;
        [Tooltip("One colour per order slot: edge arrow, price pill and stop ring. Avoid amber, it is the drop-off colour.")]
        public Color[] offerColors =
        {
            new Color(0.30f, 0.90f, 0.45f),
            new Color(0.30f, 0.72f, 1.00f),
            new Color(1.00f, 0.45f, 0.78f),
        };
        [Tooltip("After a drop-off the new set of orders pops in one by one, this many seconds apart.")]
        public float offerSpawnStagger = 0.25f;
        [Tooltip("Price pill punch (scale up, then back) when an order's destination and fare change after a zone opens.")]
        public float offerPulseTime = 0.5f;
        [Tooltip("A passenger who was dropped off stays visible this long, then disappears.")]
        public float leavingPassengerTime = 3f;
        [Tooltip("Edge arrow size, reference pixels (1080x1920 canvas).")]
        public float offerArrowSize = 110f;
        [Tooltip("Gap between the screen side and the edge arrows, reference pixels.")]
        public float offerEdgeMargin = 80f;
        [Tooltip("Edge arrows stay below this fraction of the screen height from the top (keeps the money counter clear).")]
        [Range(0f, 0.5f)] public float offerEdgeTop = 0.13f;
        [Tooltip("Edge arrows stay above this fraction of the screen height from the bottom (the thumb / stick area).")]
        [Range(0f, 0.5f)] public float offerEdgeBottom = 0.3f;

        [Header("Payment")]
        [Tooltip("Money in the wallet when the game starts.")]
        public int startMoney = 50;
        public float moneyCountUpTime = 0.8f;
        public int coinsPerPayment = 8;
        public float coinFlightTime = 0.55f;
        [Tooltip("Delay between consecutive coins, seconds.")]
        public float coinStagger = 0.05f;

        [Header("Zones for sale")]
        [Tooltip("Price per zone (element 0 = Zone 1, free).")]
        public int[] zonePrices = { 0, 150, 300, 600 };
        [Tooltip("Buy area in front of a barrier: up to this far from it (along the road), on the open side.")]
        public float barrierBuyRadius = 6f;
        [Tooltip("Buy area half width across the road. Kept under the 5 m road half width so a car stopped at a " +
                 "nearby passenger pocket does not buy by accident.")]
        public float barrierBuyHalfWidth = 4.5f;
        [Tooltip("Seconds the car must stay stopped there for the ring to fill.")]
        public float barrierBuyTime = 1.0f;
        [Tooltip("Price sign blink rate (per second) when the player cannot afford the zone.")]
        public float barrierBlinkRate = 5f;
        [Tooltip("Barrier pop-out animation length, seconds.")]
        public float barrierVanishTime = 0.45f;

        [Header("Route hint")]
        [Tooltip("Distance from the car (or the player on foot) at which the ground arrow circles.")]
        public float routeArrowRadius = 3.6f;
        [Tooltip("The arrow hides when the target is closer than this.")]
        public float routeArrowHideDistance = 9f;
    }
}
