using System;
using UnityEngine;

namespace Taxi
{
    /// <summary>
    /// Intro + the objectives from GameplayConfig, in order, with progress in the HUD top line ("Long haul · 1/3").
    /// States: 0 = intro (walk to the taxi and get in), 1..N = objective, N+1 = all done.
    /// Rides are counted from TripManager.RideCompleted; preconditions (open a zone, buy Engine) are read live.
    /// Attempts: a failed try (bumped ride for clean objectives, broken five-star streak, Rush hour window expired)
    /// starts a new attempt. The current index is saved (PlayerPrefs) so a restart continues where it stopped.
    /// Completed is raised for every finished step (intro included) with its timing, for the objective log.
    /// </summary>
    public class ObjectiveTracker : MonoBehaviour
    {
        public const string SaveKey = "Taxi.Objective";
        public const string TotalKey = "Taxi.ObjectiveTotal";

        [SerializeField] GameplayConfig config;
        [SerializeField] TripManager trips;
        [SerializeField] ZoneProgress zones;
        [SerializeField] Upgrades upgrades;
        [SerializeField] PlayerStateMachine playerState;
        [SerializeField] Hud hud;

        /// <summary>number (0 = intro), title, start and finish (seconds since launch, unscaled), attempts.</summary>
        public event Action<int, string, float, float, int> Completed;
        /// <summary>Raised once after the last objective, with the summed duration of objectives 1..N in seconds.</summary>
        public event Action<float> AllCompleted;

        public int Index { get; private set; }
        public int Count => config.objectives != null ? config.objectives.Length : 0;
        public bool AllDone => Index > Count;
        public float TotalSeconds { get; private set; }

        int progress, attempts;
        float startTime, windowStart;
        int lastShownSecond = -1;
        bool dirty = true;

        void Start()
        {
            Index = Mathf.Clamp(PlayerPrefs.GetInt(SaveKey, 0), 0, Count + 1);
            TotalSeconds = PlayerPrefs.GetFloat(TotalKey, 0f);
            trips.RideCompleted += OnRide;
            Begin(Index);
        }

        void OnDestroy()
        {
            if (trips != null) trips.RideCompleted -= OnRide;
        }

        ObjectiveDef Def => Index >= 1 && Index <= Count ? config.objectives[Index - 1] : null;

        void Begin(int index)
        {
            Index = index;
            progress = 0;
            attempts = 1;
            startTime = windowStart = Time.unscaledTime;
            dirty = true;
            var d = Def;
            trips.ForceLuggageOrder = d != null && (d.kind == ObjectiveKind.LuggageRide || d.kind == ObjectiveKind.CleanLuggageRide);
        }

        void Update()
        {
            if (Index == 0)
            {
                // Intro: done the moment the player is in the taxi.
                var s = playerState.Current;
                if (s == PlayerStateMachine.State.Entering || s == PlayerStateMachine.State.Driving) { Complete(); return; }
            }
            var d = Def;
            if (d != null && d.kind == ObjectiveKind.TimedRides)
            {
                float left = d.timeLimit - (Time.unscaledTime - windowStart);
                if (left <= 0f)
                {
                    // Window over: a new attempt with a fresh window.
                    progress = 0;
                    attempts++;
                    windowStart = Time.unscaledTime;
                    dirty = true;
                }
                int sec = Mathf.CeilToInt(Mathf.Max(0f, left));
                if (sec != lastShownSecond) { lastShownSecond = sec; dirty = true; }
            }
            if (d != null && PreconditionMet(d) != lastPrecondition) dirty = true;   // zone bought / Engine bought
            if (dirty) Refresh();
        }

        bool? lastPrecondition;

        bool PreconditionMet(ObjectiveDef d)
        {
            switch (d.kind)
            {
                case ObjectiveKind.ZoneRide: return zones.IsOpen(d.zone);
                case ObjectiveKind.EngineThenRide: return upgrades == null || upgrades.Level(UpgradeType.Engine) >= d.upgradeLevel;
                default: return true;
            }
        }

        void OnRide(TripManager.RideResult r)
        {
            var d = Def;
            if (d == null) return;
            switch (d.kind)
            {
                case ObjectiveKind.AnyRide:
                case ObjectiveKind.TimedRides:
                    progress++;
                    break;
                case ObjectiveKind.LuggageRide:
                    if (r.luggage) progress++;
                    break;
                case ObjectiveKind.CleanRide:
                    if (r.bumps == 0) progress++; else attempts++;
                    break;
                case ObjectiveKind.ZoneRide:
                    if (zones.IsOpen(d.zone) && (r.fromZone == d.zone || r.toZone == d.zone)) progress++;
                    break;
                case ObjectiveKind.EngineThenRide:
                    if (PreconditionMet(d)) progress++;
                    break;
                case ObjectiveKind.CleanLuggageRide:
                    if (r.luggage) { if (r.bumps == 0) progress++; else attempts++; }
                    break;
                case ObjectiveKind.PerfectStreak:
                    if (r.stars >= 5) progress++;
                    else if (progress > 0) { progress = 0; attempts++; }
                    break;
            }
            dirty = true;
            if (progress >= Mathf.Max(1, d.count)) Complete();
        }

        void Complete()
        {
            float now = Time.unscaledTime;
            string title = Index == 0 ? "Intro" : Def.title;
            if (Index >= 1) TotalSeconds += now - startTime;
            Completed?.Invoke(Index, title, startTime, now, attempts);
            int next = Index + 1;
            PlayerPrefs.SetInt(SaveKey, next);
            PlayerPrefs.SetFloat(TotalKey, TotalSeconds);
            PlayerPrefs.Save();
            Begin(next);
            if (AllDone) AllCompleted?.Invoke(TotalSeconds);
        }

        void Refresh()
        {
            dirty = false;
            if (Index == 0)
            {
                hud.SetObjective("Drag to walk to your taxi, stand next to it");
                return;
            }
            var d = Def;
            if (d == null) { hud.SetObjective("All objectives done! Free ride"); return; }

            bool pre = PreconditionMet(d);
            lastPrecondition = pre;
            string line;
            if (!pre && d.kind == ObjectiveKind.ZoneRide)
                line = d.title + " · open Zone " + d.zone + " at a barrier";
            else if (!pre && d.kind == ObjectiveKind.EngineThenRide)
                line = d.title + " · buy Engine Lv" + d.upgradeLevel + " at the garage";
            else
            {
                line = d.title + " · " + progress + "/" + Mathf.Max(1, d.count);
                if (d.kind == ObjectiveKind.TimedRides)
                {
                    int sec = Mathf.Max(0, lastShownSecond);
                    line += " · " + sec / 60 + ":" + (sec % 60).ToString("00");
                }
            }
            hud.SetObjective(line);
        }
    }
}
