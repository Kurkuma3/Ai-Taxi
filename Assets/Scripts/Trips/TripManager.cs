using System.Collections.Generic;
using UnityEngine;

namespace Taxi
{
    /// <summary>
    /// One passenger at a time. States:
    ///   NoTrip    -> (delay) plan a trip -> Waiting
    ///   Waiting   passenger at the pickup stop with the fare overhead; waves while the car is near.
    ///             Car stopped within stopRadius with the driver inside -> Boarding
    ///   Boarding  passenger walks to the nearest car door (time-limited). Car drives off -> back to Waiting.
    ///             Reaches the door -> Riding
    ///   Riding    route hint to the drop-off. Car stopped within stopRadius (driver inside or not) -> Alighting
    ///   Alighting passenger is paid out, steps out and walks to the stop (time-limited) -> NoTrip
    /// A trip is a snapshot: opening a zone during a ride does not change it. Invalid requests are ignored by the
    /// state checks in Update; no state waits on something the player cannot do (walks are time-limited).
    /// </summary>
    public class TripManager : MonoBehaviour
    {
        public enum State { NoTrip, Waiting, Boarding, Riding, Alighting }

        [SerializeField] GameplayConfig config;
        [SerializeField] CarController car;
        [SerializeField] PlayerStateMachine playerState;
        [SerializeField] PlayerController player;
        [SerializeField] ZoneProgress zones;
        [SerializeField] Wallet wallet;
        [SerializeField] Hud hud;
        [SerializeField] RouteHint routeHint;
        [Tooltip("Passenger model prefabs (Passenger_A, Passenger_B). One of them is used per trip.")]
        [SerializeField] GameObject[] passengerPrefabs;

        public State Current { get; private set; }
        public Trip CurrentTrip { get; private set; }
        public bool HasTrip => Current != State.NoTrip;

        readonly List<PassengerStop> stops = new List<PassengerStop>(32);
        readonly List<StopInfo> stopInfos = new List<StopInfo>(32);
        List<int> scratch;
        Passenger[] passengers;
        Passenger passenger;   // the one used by the current trip
        System.Random rng;
        TripRules rules;
        System.Func<int, bool> isZoneOpen;
        float timer;
        int lastDropoff = -1;
        bool warnedNoTrip;
        int[] recentRoutes;     // ring buffer of TripPlanner.RouteKey values, newest overwrites oldest
        int recentCount, recentNext;

        void Start()
        {
            rng = new System.Random(System.Environment.TickCount);
            rules = TripRules.From(config);
            isZoneOpen = zones.IsOpen;
            CollectStops();
            scratch = new List<int>(stops.Count * stops.Count);
            recentRoutes = new int[Mathf.Max(0, config.recentRoutesToAvoid)];
            hud.SetMoney(wallet.Money);

            passengers = new Passenger[passengerPrefabs.Length];
            for (int i = 0; i < passengerPrefabs.Length; i++)
            {
                var go = Instantiate(passengerPrefabs[i], transform);
                go.name = passengerPrefabs[i].name;
                var p = go.AddComponent<Passenger>();
                p.Init(config);
                p.SetVisible(false);
                passengers[i] = p;
            }
            Enter(State.NoTrip);
            timer = 0.3f;   // first passenger almost immediately
        }

        void CollectStops()
        {
            stops.Clear();
            stopInfos.Clear();
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                root.GetComponentsInChildren(false, stopBuffer);
                foreach (var s in stopBuffer)
                {
                    int z = s.ResolvedZone;
                    if (z < 1) { Debug.LogWarning("[Taxi] PassengerStop without a zone, ignored: " + PassengerStopPath(s), s); continue; }
                    stops.Add(s);
                    Vector3 p = s.transform.position;
                    stopInfos.Add(new StopInfo(new Vector2(p.x, p.z), z));
                }
            }
            if (stops.Count < 2) Debug.LogError("[Taxi] Need at least two PassengerStops with a zone; found " + stops.Count);
        }
        readonly List<PassengerStop> stopBuffer = new List<PassengerStop>(32);

        static string PassengerStopPath(PassengerStop s) =>
            (s.transform.parent != null ? s.transform.parent.name + "/" : "") + s.name;

        void Update()
        {
            float dt = Time.deltaTime;
            bool driving = playerState.Current == PlayerStateMachine.State.Driving;
            Vector3 carPos = car.transform.position;
            Vector3 hintFrom = driving || playerState.Current == PlayerStateMachine.State.Entering ? carPos : player.transform.position;

            switch (Current)
            {
                case State.NoTrip:
                    timer -= dt;
                    if (timer <= 0f) PlanNext();
                    break;

                case State.Waiting:
                {
                    Vector3 stop = PickupPos;
                    float d = Flat(carPos, stop);
                    // Back from an aborted boarding: walk to the stop first, then wait facing the car.
                    if (Flat(passenger.transform.position, stop) > 0.05f) passenger.WalkTowards(stop, dt);
                    else
                    {
                        bool wave = driving && d <= config.waveDistance;
                        passenger.SetWave(wave);
                        passenger.TurnTowards(carPos, dt);
                    }
                    hud.ShowFare(CurrentTrip.fare, passenger.transform.position + Vector3.up * config.fareLabelHeight);
                    routeHint.Show(hintFrom, stop, false);
                    if (driving && car.IsStopped && d <= config.stopRadius) Enter(State.Boarding);
                    break;
                }

                case State.Boarding:
                {
                    timer += dt;
                    routeHint.Show(hintFrom, PickupPos, false);
                    // The car left before the passenger got in: back to the stop, keep the same trip.
                    if (Flat(carPos, PickupPos) > config.stopRadius + 3f)
                    {
                        Enter(State.Waiting);
                        break;
                    }
                    if (!car.IsStopped) { passenger.SetWalking(false); break; }   // wait for the car to settle
                    bool there = passenger.WalkTowards(DoorPoint(passenger.transform.position), dt);
                    if (there || timer >= config.passengerWalkMaxTime) Enter(State.Riding);
                    break;
                }

                case State.Riding:
                {
                    Vector3 stop = DropoffPos;
                    routeHint.Show(hintFrom, stop, true);
                    if (car.IsStopped && Flat(carPos, stop) <= config.stopRadius) Enter(State.Alighting);
                    break;
                }

                case State.Alighting:
                    timer += dt;
                    if (passenger.WalkTowards(DropoffPos, dt) || timer >= config.passengerWalkMaxTime)
                    {
                        timer = config.nextTripDelay;
                        Enter(State.NoTrip);
                    }
                    break;
            }
        }

        void PlanNext()
        {
            if (!TripPlanner.TryPlan(stopInfos, isZoneOpen, zones.HighestOpen, zones.PriorityZone, rules, rng, lastDropoff, scratch, out var trip,
                    recentRoutes, recentCount))
            {
                if (!warnedNoTrip) { Debug.LogWarning("[Taxi] No trip possible (fewer than two stops in open zones)"); warnedNoTrip = true; }
                timer = 1f;
                return;
            }
            zones.ConsumePriorityTrip();
            if (recentRoutes.Length > 0)
            {
                recentRoutes[recentNext] = TripPlanner.RouteKey(trip.pickup, trip.dropoff);
                recentNext = (recentNext + 1) % recentRoutes.Length;
                if (recentCount < recentRoutes.Length) recentCount++;
            }
            CurrentTrip = trip;
            passenger = passengers[rng.Next(passengers.Length)];
            passenger.SetVisible(true);
            Vector3 stop = PickupPos, toCar = car.transform.position - stop;
            passenger.PlaceAt(stop, Mathf.Atan2(toCar.x, toCar.z) * Mathf.Rad2Deg);
            Enter(State.Waiting);
        }

        void Enter(State s)
        {
            Current = s;
            timer = s == State.NoTrip ? timer : 0f;
            switch (s)
            {
                case State.NoTrip:
                    hud.HideFare();
                    routeHint.Hide();
                    if (passenger != null) passenger.SetVisible(false);
                    break;

                case State.Boarding:
                    hud.HideFare();
                    passenger.SetWave(false);
                    break;

                case State.Riding:
                    passenger.SetVisible(false);
                    break;

                case State.Alighting:
                {
                    Vector3 door = DoorPoint(DropoffPos);
                    passenger.SetVisible(true);
                    passenger.PlaceAt(door, car.transform.eulerAngles.y);
                    routeHint.Hide();
                    lastDropoff = CurrentTrip.dropoff;
                    wallet.Add(CurrentTrip.fare, door);
                    hud.PlayPayment(door + Vector3.up * 1.5f, wallet.Money);
                    break;
                }
            }
        }

        Vector3 PickupPos => stops[CurrentTrip.pickup].transform.position;
        Vector3 DropoffPos => stops[CurrentTrip.dropoff].transform.position;

        /// <summary>The car side (left or right door) closest to a point.</summary>
        Vector3 DoorPoint(Vector3 near)
        {
            Transform t = car.transform;
            Vector3 l = t.TransformPoint(new Vector3(-1.4f, 0f, 0f)), r = t.TransformPoint(new Vector3(1.4f, 0f, 0f));
            Vector3 p = (l - near).sqrMagnitude <= (r - near).sqrMagnitude ? l : r;
            p.y = near.y;
            return p;
        }

        static float Flat(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
