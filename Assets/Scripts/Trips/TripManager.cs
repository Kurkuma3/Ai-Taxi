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
    ///   Alighting passenger steps out and walks to the stop (time-limited); paid at once -> NoTrip,
    ///             or, with luggage -> AwaitLuggage
    ///   AwaitLuggage passenger waits; player carries the suitcase from the trunk to the drop point -> paid -> NoTrip
    /// Luggage trips (tutorial mission 2, then luggageTripChance): while Waiting the suitcase stands next to the
    /// passenger; on foot, ring at the suitcase -> carried -> ring at the trunk -> in the trunk; only then can the
    /// passenger board. While carrying, the player cannot get into the car. Driving off leaves the suitcase where it is.
    /// A trip is a snapshot: opening a zone during a ride does not change it. Invalid requests are ignored by the
    /// state checks in Update; no state waits on something the player cannot do (walks are time-limited).
    /// </summary>
    public class TripManager : MonoBehaviour
    {
        public enum State { NoTrip, Waiting, Boarding, Riding, Alighting, AwaitLuggage }
        /// <summary>Where the suitcase of a luggage trip is.</summary>
        public enum Luggage { None, AtStop, Carried, InTrunk, Delivered }

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
        [Tooltip("Suitcase prop (pivot at the bottom); one instance is reused by every luggage trip.")]
        [SerializeField] GameObject suitcasePrefab;

        public State Current { get; private set; }
        public Trip CurrentTrip { get; private set; }
        public bool HasTrip => Current != State.NoTrip;
        /// <summary>1-based number of the current trip (this session).</summary>
        public int TripNumber { get; private set; }
        public bool IsTutorialTrip => TripNumber >= 1 && TripNumber <= config.tutorialTrips;
        public Luggage LuggageState { get; private set; }

        Transform suitcase;
        float luggageProgress;
        Vector3 dropPoint;

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
            if (suitcasePrefab != null)
            {
                suitcase = Instantiate(suitcasePrefab, transform).transform;
                suitcase.name = suitcasePrefab.name;
                suitcase.gameObject.SetActive(false);
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
            playerState.EnterBlocked = LuggageState == Luggage.Carried;   // hands full: cannot get in

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
                    // Luggage trip: the suitcase must be in the trunk before the passenger gets in.
                    bool needLoad = CurrentTrip.luggage && LuggageState != Luggage.InTrunk;
                    if (needLoad) UpdateLoading(dt, driving, d, hintFrom);
                    else routeHint.Show(hintFrom, stop, false);
                    if (!needLoad && driving && car.IsStopped && d <= config.stopRadius) Enter(State.Boarding);
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
                        if (CurrentTrip.luggage) { Enter(State.AwaitLuggage); break; }
                        timer = config.nextTripDelay;
                        Enter(State.NoTrip);
                    }
                    break;

                case State.AwaitLuggage:
                    // Passenger waits at the stop until the suitcase is carried from the trunk to the drop point.
                    passenger.TurnTowards(hintFrom, dt);
                    UpdateUnloading(dt, driving, hintFrom);
                    break;
            }
        }

        // ---- Luggage ---------------------------------------------------------------------------------------------

        /// <summary>Pickup: suitcase at the stop -> carried -> trunk. Works only on foot; driving off leaves it in place.</summary>
        void UpdateLoading(float dt, bool driving, float carToStop, Vector3 hintFrom)
        {
            bool onFoot = playerState.Current == PlayerStateMachine.State.OnFoot;
            Vector3 target;
            if (LuggageState == Luggage.AtStop)
            {
                target = suitcase.position;
                // Far away in the car: lead to the stop. Parked at the stop or on foot: lead to the suitcase.
                if (driving && carToStop > config.stopRadius) { routeHint.Show(hintFrom, PickupPos, false); return; }
                routeHint.Show(hintFrom, target, false);
                if (onFoot && Interact(target, dt)) Carry();
            }
            else if (LuggageState == Luggage.Carried)
            {
                target = TrunkPoint;
                routeHint.Show(hintFrom, target, false);
                if (onFoot && Interact(target, dt)) StoreInTrunk();
            }
        }

        /// <summary>Drop-off: trunk -> carried -> drop point next to the passenger -> paid.</summary>
        void UpdateUnloading(float dt, bool driving, Vector3 hintFrom)
        {
            bool onFoot = playerState.Current == PlayerStateMachine.State.OnFoot;
            if (driving && Flat(car.transform.position, DropoffPos) > config.stopRadius)
            {
                // Drove off with the suitcase still in the trunk: lead back to the waiting passenger.
                routeHint.Show(hintFrom, DropoffPos, true);
                return;
            }
            if (LuggageState == Luggage.InTrunk)
            {
                routeHint.Show(hintFrom, TrunkPoint, true);
                if (onFoot && Interact(TrunkPoint, dt)) Carry();
            }
            else if (LuggageState == Luggage.Carried)
            {
                routeHint.Show(hintFrom, dropPoint, true);
                if (onFoot && Interact(dropPoint, dt)) Deliver();
            }
        }

        /// <summary>
        /// Proximity ring at a ground point: an empty ring shows when the player is close, it fills while inside
        /// luggageInteractRadius and drains outside. Returns true once full.
        /// </summary>
        bool Interact(Vector3 target, float dt)
        {
            float d = Flat(player.transform.position, target);
            if (d <= config.luggageInteractRadius) luggageProgress += dt / Mathf.Max(0.05f, config.luggageHoldTime);
            else luggageProgress = Mathf.MoveTowards(luggageProgress, 0f, dt / Mathf.Max(0.05f, config.luggageHoldTime) * 2f);
            if (d <= config.luggageInteractRadius * 3f) hud.RequestActionRing(luggageProgress, target + Vector3.up * 1.2f);
            if (luggageProgress < 1f) return false;
            luggageProgress = 0f;
            return true;
        }

        void Carry()
        {
            LuggageState = Luggage.Carried;
            suitcase.gameObject.SetActive(true);
            suitcase.SetParent(player.transform, false);
            suitcase.localPosition = config.carryLocalOffset;
            suitcase.localRotation = Quaternion.identity;
        }

        void StoreInTrunk()
        {
            LuggageState = Luggage.InTrunk;
            suitcase.SetParent(transform, false);
            suitcase.gameObject.SetActive(false);
        }

        void Deliver()
        {
            LuggageState = Luggage.Delivered;
            PlaceSuitcase(dropPoint, passenger.transform.eulerAngles.y);
            wallet.Add(CurrentTrip.fare, dropPoint);
            hud.PlayPayment(dropPoint + Vector3.up * 1.5f, wallet.Money);
            lastDropoff = CurrentTrip.dropoff;
            timer = config.nextTripDelay;
            Enter(State.NoTrip);
        }

        void PlaceSuitcase(Vector3 pos, float yaw)
        {
            suitcase.SetParent(transform, false);
            suitcase.gameObject.SetActive(true);
            suitcase.SetPositionAndRotation(new Vector3(pos.x, 0.08f, pos.z), Quaternion.Euler(0f, yaw, 0f));   // just above the pocket decal
        }

        Vector3 TrunkPoint
        {
            get
            {
                Vector3 p = car.transform.TransformPoint(new Vector3(0f, 0f, -config.trunkOffset));
                p.y = 0f;
                return p;
            }
        }

        void PlanNext()
        {
            int next = TripNumber + 1;
            int forced = next <= config.tutorialTrips ? TutorialStop(next) : -1;
            if (!TripPlanner.TryPlan(stopInfos, isZoneOpen, zones.HighestOpen, zones.PriorityZone, rules, rng, lastDropoff, scratch, out var trip,
                    recentRoutes, recentCount, forced))
            {
                if (!warnedNoTrip) { Debug.LogWarning("[Taxi] No trip possible (fewer than two stops in open zones)"); warnedNoTrip = true; }
                timer = 1f;
                return;
            }
            TripNumber = next;
            zones.ConsumePriorityTrip();
            if (recentRoutes.Length > 0)
            {
                recentRoutes[recentNext] = TripPlanner.RouteKey(trip.pickup, trip.dropoff);
                recentNext = (recentNext + 1) % recentRoutes.Length;
                if (recentCount < recentRoutes.Length) recentCount++;
            }
            // Tutorial mission 2 always has a suitcase, mission 1 never; afterwards luggageTripChance.
            bool luggageTrip = next <= config.tutorialTrips ? next == 2 : rng.NextDouble() < config.luggageTripChance;
            if (luggageTrip && suitcase != null) trip = trip.WithLuggage(config.luggageBonus);
            CurrentTrip = trip;

            // The previous passenger (and delivered suitcase) stayed visible during the pause; clear them now.
            if (passenger != null) passenger.SetVisible(false);
            if (suitcase != null) suitcase.gameObject.SetActive(false);

            passenger = passengers[rng.Next(passengers.Length)];
            passenger.SetVisible(true);
            Vector3 stop = PickupPos, toCar = car.transform.position - stop;
            float yaw = Mathf.Atan2(toCar.x, toCar.z) * Mathf.Rad2Deg;
            passenger.PlaceAt(stop, yaw);
            LuggageState = Luggage.None;
            luggageProgress = 0f;
            if (CurrentTrip.luggage)
            {
                LuggageState = Luggage.AtStop;
                PlaceSuitcase(stop + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0.75f, 0f, 0f), yaw);   // at the passenger's right
            }
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
                    break;   // the last passenger stays where they got out until the next trip is planned

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
                    if (CurrentTrip.luggage) break;   // paid when the suitcase is delivered (AwaitLuggage)
                    lastDropoff = CurrentTrip.dropoff;
                    wallet.Add(CurrentTrip.fare, door);
                    hud.PlayPayment(door + Vector3.up * 1.5f, wallet.Money);
                    break;
                }

                case State.AwaitLuggage:
                {
                    // Drop point: next to the passenger, toward where the car stopped.
                    Vector3 toCar = car.transform.position - DropoffPos;
                    toCar.y = 0f;
                    Vector3 dir = toCar.sqrMagnitude > 0.01f ? toCar.normalized : stops[CurrentTrip.dropoff].transform.right;
                    dropPoint = DropoffPos + dir * config.luggageDropDistance;
                    dropPoint.y = 0f;
                    luggageProgress = 0f;
                    break;
                }
            }
        }

        /// <summary>Index of the stop marked for tutorial mission n, or -1 (then the trip is planned normally).</summary>
        int TutorialStop(int mission)
        {
            for (int i = 0; i < stops.Count; i++) if (stops[i].tutorialMission == mission) return i;
            return -1;
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
