using System.Collections.Generic;
using UnityEngine;

namespace Taxi
{
    /// <summary>
    /// Several orders wait at once (offerCount, 3 by default; the tutorial trips are single), one passenger rides at a time.
    /// Each order slot has its own colour: edge arrow + price pill in the HUD, ring on the stop.
    /// States of the ride:
    ///   Searching    orders wait at their stops with their fare overhead (edge arrows when off-screen).
    ///                Car stopped within stopRadius of an order, driver inside -> Boarding (that order)
    ///   Boarding     the passenger walks to the nearest car door (time-limited). Car drives off -> Searching,
    ///                the order keeps waiting. Reaches the door -> Riding; the other orders disappear
    ///   Riding       no orders on the map, route hint to the drop-off. Car stopped within stopRadius -> Alighting.
    ///                Ride over -> a fresh set of orders pops in (nextTripDelay + offerSpawnStagger per slot)
    ///   Alighting    passenger steps out and walks to the stop (time-limited); paid at once -> Searching,
    ///                or, with luggage -> AwaitLuggage
    ///   AwaitLuggage passenger waits; player carries the suitcase from the trunk to the drop point -> paid -> Searching
    /// Luggage orders: the suitcase stands next to the passenger. Picking it up commits the player to that order
    /// (the other orders' arrows hide, only it can board): the passenger walks to the car door and gets in at once,
    /// and the player puts the suitcase into the trunk during Riding. While carrying, the player cannot get into the car.
    /// Opening a zone re-plans every waiting order: same passenger and stop, new destination and fare.
    /// Trips in progress are snapshots. No state waits on something the player
    /// cannot do (walks are time-limited, waiting passengers never leave).
    /// </summary>
    public class TripManager : MonoBehaviour
    {
        public enum State { Searching, Boarding, Riding, Alighting, AwaitLuggage }
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
        [Tooltip("Passenger model prefabs (Passenger_A, Passenger_B). A random one is used per order.")]
        [SerializeField] GameObject[] passengerPrefabs;
        [Tooltip("Suitcase prop (pivot at the bottom); instances are pooled.")]
        [SerializeField] GameObject suitcasePrefab;

        /// <summary>One waiting order, or the ride in progress.</summary>
        sealed class Order
        {
            public bool active;
            public Trip trip;
            public Passenger passenger;
            public Transform suitcase;
            public Luggage luggage;
            public float refill;   // seconds until an empty slot gets a new order
            public int slot;       // colour index

            public void CopyFrom(Order o)
            {
                active = o.active; trip = o.trip; passenger = o.passenger; suitcase = o.suitcase;
                luggage = o.luggage; slot = o.slot;
            }

            public void Clear()
            {
                active = false; passenger = null; suitcase = null; luggage = Luggage.None;
            }
        }

        public State Current { get; private set; }
        /// <summary>Passengers picked up this session.</summary>
        public int TripsTaken { get; private set; }
        public int OrdersWaiting
        {
            get { int n = 0; foreach (var o in orders) if (o.active) n++; return n; }
        }

        Order[] orders;
        readonly Order ride = new Order();
        int boardingSlot = -1;
        int committedSlot = -1;   // the order whose suitcase the player has picked up
        int planned;              // orders planned this session (the first ones are the tutorial missions)

        Passenger[] passengerPool;
        bool[] passengerUsed;
        Transform[] suitcasePool;
        bool[] suitcaseUsed;
        Passenger leaving;
        Transform leavingSuitcase;
        float leaveTimer;
        int leavingStop = -1;

        float luggageProgress;
        Vector3 dropPoint;
        float timer;

        readonly List<PassengerStop> stops = new List<PassengerStop>(32);
        readonly List<StopInfo> stopInfos = new List<StopInfo>(32);
        readonly List<PassengerStop> stopBuffer = new List<PassengerStop>(32);
        List<int> scratch;
        System.Random rng;
        TripRules rules;
        System.Func<int, bool> isZoneOpen;
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

            int slots = Mathf.Max(1, config.offerCount);
            orders = new Order[slots];
            for (int i = 0; i < slots; i++)
                orders[i] = new Order { slot = i, refill = i == 0 ? 0.3f : config.nextTripDelay * i };   // staggered

            // Waiting orders + the rider + the one walking away after the drop-off.
            int perPrefab = slots + 2;
            passengerPool = new Passenger[passengerPrefabs.Length * perPrefab];
            passengerUsed = new bool[passengerPool.Length];
            for (int i = 0; i < passengerPool.Length; i++)
            {
                var prefab = passengerPrefabs[i % passengerPrefabs.Length];
                var go = Instantiate(prefab, transform);
                go.name = prefab.name;
                var p = go.AddComponent<Passenger>();
                p.Init(config);
                p.SetVisible(false);
                passengerPool[i] = p;
            }
            int cases = suitcasePrefab != null ? slots + 2 : 0;
            suitcasePool = new Transform[cases];
            suitcaseUsed = new bool[cases];
            for (int i = 0; i < cases; i++)
            {
                suitcasePool[i] = Instantiate(suitcasePrefab, transform).transform;
                suitcasePool[i].name = suitcasePrefab.name;
                suitcasePool[i].gameObject.SetActive(false);
            }
            zones.ZoneOpened += OnZoneOpened;
            Enter(State.Searching);
        }

        void OnDestroy()
        {
            if (zones != null) zones.ZoneOpened -= OnZoneOpened;
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
            if (stops.Count > 64) Debug.LogWarning("[Taxi] More than 64 stops: stops past 63 can be shared by two orders");
        }

        static string PassengerStopPath(PassengerStop s) =>
            (s.transform.parent != null ? s.transform.parent.name + "/" : "") + s.name;

        void Update()
        {
            float dt = Time.deltaTime;
            bool driving = playerState.Current == PlayerStateMachine.State.Driving;
            bool onFoot = playerState.Current == PlayerStateMachine.State.OnFoot;
            Vector3 carPos = car.transform.position;
            Vector3 hintFrom = driving || playerState.Current == PlayerStateMachine.State.Entering ? carPos : player.transform.position;
            // Hands full: cannot get in.
            playerState.EnterBlocked = ride.luggage == Luggage.Carried
                                       || (committedSlot >= 0 && orders[committedSlot].luggage == Luggage.Carried);

            UpdateOrders(dt, driving, carPos, hintFrom);
            UpdateLeaving(dt);

            switch (Current)
            {
                case State.Searching:
                    UpdateSearching(dt, driving, onFoot, carPos, hintFrom);
                    break;

                case State.Boarding:
                {
                    var o = orders[boardingSlot];
                    Vector3 stop = StopPos(o.trip.pickup);
                    timer += dt;
                    routeHint.Show(hintFrom, stop, SlotColor(o.slot));
                    // The car left before the passenger got in: the order keeps waiting.
                    if (Flat(carPos, stop) > config.stopRadius + 3f)
                    {
                        boardingSlot = -1;
                        Enter(State.Searching);
                        break;
                    }
                    if (!car.IsStopped) { o.passenger.SetWalking(false); break; }   // wait for the car to settle
                    bool there = o.passenger.WalkTowards(DoorPoint(o.passenger.transform.position), dt);
                    if (there || timer >= config.passengerWalkMaxTime) StartRide();
                    break;
                }

                case State.Riding:
                {
                    Vector3 stop = DropoffPos;
                    if (ride.luggage == Luggage.Carried)
                    {
                        // The passenger got in while the suitcase was still in hand: into the trunk first
                        // (getting into the car is blocked while carrying, so the ride cannot start without it).
                        routeHint.Show(hintFrom, TrunkPoint, SlotColor(ride.slot));
                        if (onFoot && Interact(TrunkPoint, dt)) StoreInTrunk(ride);
                        break;
                    }
                    routeHint.Show(hintFrom, stop, true);
                    if (car.IsStopped && Flat(carPos, stop) <= config.stopRadius) Enter(State.Alighting);
                    break;
                }

                case State.Alighting:
                    timer += dt;
                    if (ride.passenger.WalkTowards(DropoffPos, dt) || timer >= config.passengerWalkMaxTime)
                    {
                        if (ride.trip.luggage) { Enter(State.AwaitLuggage); break; }
                        FinishRide();
                    }
                    break;

                case State.AwaitLuggage:
                    // Passenger waits at the stop until the suitcase is carried from the trunk to the drop point.
                    ride.passenger.TurnTowards(hintFrom, dt);
                    UpdateUnloading(dt, driving, onFoot, hintFrom);
                    break;
            }
        }

        // ---- Orders ----------------------------------------------------------------------------------------------

        /// <summary>Refills empty slots and animates the waiting passengers; shows their markers while searching.</summary>
        void UpdateOrders(float dt, bool driving, Vector3 carPos, Vector3 hintFrom)
        {
            bool searching = Current == State.Searching;
            for (int i = 0; i < orders.Length; i++)
            {
                var o = orders[i];
                if (!o.active)
                {
                    // New orders appear only while searching: a ride clears the board, its end deals a fresh one.
                    if (!searching || !SlotAllowed(i)) continue;
                    o.refill -= dt;
                    if (o.refill <= 0f) Fill(o);
                    continue;
                }
                if (i == boardingSlot) continue;   // Boarding moves this passenger

                Vector3 stop = StopPos(o.trip.pickup);
                float d = Flat(carPos, stop);
                bool mine = committedSlot < 0 || committedSlot == i;
                // Suitcase picked up and the car is at this stop: the passenger heads for the door and waits there.
                bool goToCar = i == committedSlot && o.luggage != Luggage.AtStop
                               && d <= config.stopRadius + config.passengerFollowExtra;
                Vector3 waitAt = goToCar ? DoorPoint(o.passenger.transform.position) : stop;
                if (Flat(o.passenger.transform.position, waitAt) > 0.05f) o.passenger.WalkTowards(waitAt, dt);
                else
                {
                    o.passenger.SetWave(!goToCar && searching && mine && driving && d <= config.waveDistance);
                    o.passenger.TurnTowards(carPos, dt);
                }

                if (searching && mine)
                {
                    Color c = SlotColor(o.slot);
                    hud.ShowOrder(i, o.trip.fare, o.trip.luggage, c,
                        o.passenger.transform.position + Vector3.up * config.fareLabelHeight, hintFrom);
                    routeHint.ShowOrderRing(i, stop, c);
                }
            }
        }

        /// <summary>The tutorial trips are single: extra slots open once the last tutorial passenger is picked up.</summary>
        bool SlotAllowed(int slot) => slot == 0 || TripsTaken >= config.tutorialTrips;

        void Fill(Order o)
        {
            int mission = planned + 1;
            int forced = mission <= config.tutorialTrips ? TutorialStop(mission) : -1;
            int pIndex = FreePassenger();
            if (pIndex < 0 || !TripPlanner.TryPlan(stopInfos, isZoneOpen, zones.HighestOpen, zones.PriorityZone, rules, rng,
                    lastDropoff, scratch, out var trip, recentRoutes, recentCount, forced, BlockedPickups(), WaitingPickups()))
            {
                if (!warnedNoTrip && pIndex >= 0) { Debug.LogWarning("[Taxi] No order possible right now (free stops in open zones)"); warnedNoTrip = true; }
                o.refill = 1f;
                return;
            }
            planned = mission;
            zones.ConsumePriorityTrip();
            RememberRoute(trip);
            // Tutorial mission 2 always has a suitcase, mission 1 never; afterwards luggageTripChance.
            bool luggageTrip = mission <= config.tutorialTrips ? mission == 2 : rng.NextDouble() < config.luggageTripChance;
            int cIndex = luggageTrip ? FreeSuitcase() : -1;
            if (cIndex >= 0) trip = trip.WithLuggage(config.luggageBonus);

            o.active = true;
            o.trip = trip;
            passengerUsed[pIndex] = true;
            o.passenger = passengerPool[pIndex];
            o.passenger.SetVisible(true);
            Vector3 stop = StopPos(trip.pickup), toCar = car.transform.position - stop;
            float yaw = Mathf.Atan2(toCar.x, toCar.z) * Mathf.Rad2Deg;
            o.passenger.PlaceAt(stop, yaw);
            o.luggage = Luggage.None;
            o.suitcase = null;
            if (cIndex >= 0)
            {
                suitcaseUsed[cIndex] = true;
                o.suitcase = suitcasePool[cIndex];
                o.luggage = Luggage.AtStop;
                PlaceSuitcase(o.suitcase, stop + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0.75f, 0f, 0f), yaw);   // at the passenger's right
            }
        }

        /// <summary>
        /// Stops where a new order may not wait: other waiting pickups, the current ride's drop-off and the stop where
        /// the last passenger is still standing.
        /// </summary>
        long BlockedPickups()
        {
            long mask = WaitingPickups();
            if (ride.active) mask |= Bit(ride.trip.dropoff);
            if (leaving != null && leavingStop >= 0) mask |= Bit(leavingStop);
            return mask;
        }

        /// <summary>
        /// A zone was opened: every waiting order keeps its passenger and stop, but gets a new destination and fare
        /// planned with the new zone (and its priority). The suitcase (and its bonus) stays. An order that cannot be
        /// re-planned keeps its old trip.
        /// </summary>
        void OnZoneOpened(int zone)
        {
            for (int i = 0; i < orders.Length; i++)
            {
                var o = orders[i];
                if (!o.active) continue;
                long others = 0;
                for (int k = 0; k < orders.Length; k++) if (k != i && orders[k].active) others |= Bit(orders[k].trip.pickup);
                if (!TripPlanner.TryPlan(stopInfos, isZoneOpen, zones.HighestOpen, zones.PriorityZone, rules, rng,
                        -1, scratch, out var trip, recentRoutes, recentCount, o.trip.pickup, others, others))
                    continue;
                zones.ConsumePriorityTrip();
                RememberRoute(trip);
                if (o.trip.luggage) trip = trip.WithLuggage(config.luggageBonus);
                o.trip = trip;
                hud.PulseOrder(i);
            }
        }

        void RememberRoute(Trip trip)
        {
            if (recentRoutes.Length == 0) return;
            recentRoutes[recentNext] = TripPlanner.RouteKey(trip.pickup, trip.dropoff);
            recentNext = (recentNext + 1) % recentRoutes.Length;
            if (recentCount < recentRoutes.Length) recentCount++;
        }

        /// <summary>Stops with a waiting passenger (avoided as drop-offs when the distance range allows).</summary>
        long WaitingPickups()
        {
            long mask = 0;
            foreach (var o in orders) if (o.active) mask |= Bit(o.trip.pickup);
            return mask;
        }

        static long Bit(int stop) => stop < 64 ? 1L << stop : 0L;

        int FreePassenger()
        {
            int n = passengerPool.Length, start = rng.Next(n);   // random start = random model
            for (int k = 0; k < n; k++) { int i = (start + k) % n; if (!passengerUsed[i]) return i; }
            return -1;
        }

        int FreeSuitcase()
        {
            for (int i = 0; i < suitcaseUsed.Length; i++) if (!suitcaseUsed[i]) return i;
            return -1;
        }

        void ReleasePassenger(Passenger p)
        {
            if (p == null) return;
            p.SetVisible(false);
            int i = System.Array.IndexOf(passengerPool, p);
            if (i >= 0) passengerUsed[i] = false;
        }

        void ReleaseSuitcase(Transform s)
        {
            if (s == null) return;
            s.SetParent(transform, false);
            s.gameObject.SetActive(false);
            int i = System.Array.IndexOf(suitcasePool, s);
            if (i >= 0) suitcaseUsed[i] = false;
        }

        // ---- Searching: pick an order --------------------------------------------------------------------------

        void UpdateSearching(float dt, bool driving, bool onFoot, Vector3 carPos, Vector3 hintFrom)
        {
            if (committedSlot >= 0)
            {
                // The suitcase of this order is picked up: only this passenger can board.
                var o = orders[committedSlot];
                Vector3 stop = StopPos(o.trip.pickup);
                float d = Flat(carPos, stop);
                Color c = SlotColor(o.slot);
                // Suitcase in hand and the passenger has reached the door: they get in right away; the player loads
                // the trunk afterwards (Riding). Car too far from the stop: they wait there and board normally later.
                bool nearCar = d <= config.stopRadius + config.passengerFollowExtra;
                Vector3 pp = o.passenger.transform.position;
                if (nearCar && Flat(pp, DoorPoint(pp)) <= 0.1f)
                {
                    boardingSlot = committedSlot;
                    StartRide();
                    return;
                }
                if (o.luggage == Luggage.Carried)
                {
                    routeHint.Show(hintFrom, TrunkPoint, c);
                    if (onFoot && Interact(TrunkPoint, dt)) StoreInTrunk(o);
                    return;
                }
                routeHint.Show(hintFrom, stop, c);
                if (driving && car.IsStopped && d <= config.stopRadius) BeginBoarding(committedSlot);
                return;
            }

            int focus = NearestOrder(carPos, config.stopRadius + config.passengerFollowExtra);
            if (focus < 0) { routeHint.Hide(); return; }
            var f = orders[focus];
            float df = Flat(carPos, StopPos(f.trip.pickup));
            if (f.luggage == Luggage.AtStop)
            {
                // Car parked at this stop (or the driver already out): lead to the suitcase.
                if (onFoot || (car.IsStopped && df <= config.stopRadius))
                {
                    routeHint.Show(hintFrom, f.suitcase.position, SlotColor(f.slot));
                    if (onFoot && Interact(f.suitcase.position, dt))
                    {
                        Carry(f.suitcase);
                        f.luggage = Luggage.Carried;
                        committedSlot = focus;
                    }
                }
                else routeHint.Hide();
                return;
            }
            routeHint.Hide();
            if (driving && car.IsStopped && df <= config.stopRadius) BeginBoarding(focus);
        }

        int NearestOrder(Vector3 pos, float maxDist)
        {
            int best = -1;
            float bestD = maxDist;
            for (int i = 0; i < orders.Length; i++)
            {
                if (!orders[i].active) continue;
                float d = Flat(pos, StopPos(orders[i].trip.pickup));
                if (d <= bestD) { bestD = d; best = i; }
            }
            return best;
        }

        void BeginBoarding(int slot)
        {
            boardingSlot = slot;
            Enter(State.Boarding);
        }

        void StartRide()
        {
            var o = orders[boardingSlot];
            ride.CopyFrom(o);
            o.Clear();
            // The other orders are gone: a fresh set is dealt when this ride ends (FinishRide).
            for (int i = 0; i < orders.Length; i++)
            {
                if (i == boardingSlot || !orders[i].active) continue;
                ReleasePassenger(orders[i].passenger);
                ReleaseSuitcase(orders[i].suitcase);
                orders[i].Clear();
            }
            boardingSlot = -1;
            committedSlot = -1;
            TripsTaken++;
            Enter(State.Riding);
        }

        void FinishRide()
        {
            // The passenger (and a delivered suitcase) stays at the stop for a moment, then disappears.
            ReleasePassenger(leaving);
            ReleaseSuitcase(leavingSuitcase);
            leaving = ride.passenger;
            leavingSuitcase = ride.suitcase;
            leavingStop = ride.trip.dropoff;
            leaveTimer = config.leavingPassengerTime;
            ride.Clear();
            // Deal a fresh set of orders, popping in one after another.
            for (int i = 0; i < orders.Length; i++)
                orders[i].refill = config.nextTripDelay + i * config.offerSpawnStagger;
            Enter(State.Searching);
        }

        void UpdateLeaving(float dt)
        {
            if (leaving == null) return;
            leaveTimer -= dt;
            if (leaveTimer > 0f) return;
            ReleasePassenger(leaving);
            ReleaseSuitcase(leavingSuitcase);
            leaving = null;
            leavingSuitcase = null;
        }

        // ---- Luggage ---------------------------------------------------------------------------------------------

        /// <summary>Drop-off: trunk -> carried -> drop point next to the passenger -> paid.</summary>
        void UpdateUnloading(float dt, bool driving, bool onFoot, Vector3 hintFrom)
        {
            if (driving && Flat(car.transform.position, DropoffPos) > config.stopRadius)
            {
                // Drove off with the suitcase still in the trunk: lead back to the waiting passenger.
                routeHint.Show(hintFrom, DropoffPos, true);
                return;
            }
            if (ride.luggage == Luggage.InTrunk)
            {
                routeHint.Show(hintFrom, TrunkPoint, true);
                if (onFoot && Interact(TrunkPoint, dt)) { Carry(ride.suitcase); ride.luggage = Luggage.Carried; }
            }
            else if (ride.luggage == Luggage.Carried)
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

        void Carry(Transform suitcase)
        {
            suitcase.gameObject.SetActive(true);
            suitcase.SetParent(player.transform, false);
            suitcase.localPosition = config.carryLocalOffset;
            suitcase.localRotation = Quaternion.identity;
        }

        void StoreInTrunk(Order o)
        {
            o.luggage = Luggage.InTrunk;
            o.suitcase.SetParent(transform, false);
            o.suitcase.gameObject.SetActive(false);
        }

        void Deliver()
        {
            ride.luggage = Luggage.Delivered;
            PlaceSuitcase(ride.suitcase, dropPoint, ride.passenger.transform.eulerAngles.y);
            wallet.Add(ride.trip.fare, dropPoint);
            hud.PlayPayment(dropPoint + Vector3.up * 1.5f, wallet.Money);
            lastDropoff = ride.trip.dropoff;
            FinishRide();
        }

        void PlaceSuitcase(Transform suitcase, Vector3 pos, float yaw)
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

        // ---- State machine ---------------------------------------------------------------------------------------

        void Enter(State s)
        {
            Current = s;
            timer = 0f;
            luggageProgress = 0f;
            switch (s)
            {
                case State.Searching:
                    routeHint.Hide();
                    break;

                case State.Boarding:
                    orders[boardingSlot].passenger.SetWave(false);
                    break;

                case State.Riding:
                    ride.passenger.SetVisible(false);
                    break;

                case State.Alighting:
                {
                    Vector3 door = DoorPoint(DropoffPos);
                    ride.passenger.SetVisible(true);
                    ride.passenger.PlaceAt(door, car.transform.eulerAngles.y);
                    routeHint.Hide();
                    if (ride.trip.luggage) break;   // paid when the suitcase is delivered (AwaitLuggage)
                    lastDropoff = ride.trip.dropoff;
                    wallet.Add(ride.trip.fare, door);
                    hud.PlayPayment(door + Vector3.up * 1.5f, wallet.Money);
                    break;
                }

                case State.AwaitLuggage:
                {
                    // Drop point: next to the passenger, toward where the car stopped.
                    Vector3 toCar = car.transform.position - DropoffPos;
                    toCar.y = 0f;
                    Vector3 dir = toCar.sqrMagnitude > 0.01f ? toCar.normalized : stops[ride.trip.dropoff].transform.right;
                    dropPoint = DropoffPos + dir * config.luggageDropDistance;
                    dropPoint.y = 0f;
                    break;
                }
            }
        }

        /// <summary>
        /// True while the point is within stopRadius of a stop the trips care about right now (any waiting order
        /// while searching, the boarding pickup, the drop-off during the ride). The barrier shop stays quiet there so
        /// a passenger stop never buys a zone by accident.
        /// </summary>
        public bool IsAtActiveStop(Vector3 p)
        {
            switch (Current)
            {
                case State.Searching:
                    return NearestOrder(p, config.stopRadius) >= 0;
                case State.Boarding:
                    return Flat(p, StopPos(orders[boardingSlot].trip.pickup)) <= config.stopRadius;
                default:
                    return Flat(p, DropoffPos) <= config.stopRadius;
            }
        }

        /// <summary>Index of the stop marked for tutorial mission n, or -1 (then the trip is planned normally).</summary>
        int TutorialStop(int mission)
        {
            for (int i = 0; i < stops.Count; i++) if (stops[i].tutorialMission == mission) return i;
            return -1;
        }

        Color SlotColor(int slot)
        {
            var c = config.offerColors;
            return c != null && c.Length > 0 ? c[slot % c.Length] : Color.green;
        }

        Vector3 StopPos(int stop) => stops[stop].transform.position;
        Vector3 DropoffPos => stops[ride.trip.dropoff].transform.position;

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
