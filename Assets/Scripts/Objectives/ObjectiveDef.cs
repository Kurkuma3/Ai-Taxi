using System;

namespace Taxi
{
    /// <summary>What a ride must be to count for an objective.</summary>
    public enum ObjectiveKind
    {
        AnyRide,            // every paid ride
        LuggageRide,        // a ride with a suitcase
        CleanRide,          // a ride without bumps (a bumped ride is a failed attempt, progress kept)
        ZoneRide,           // a ride starting or ending in `zone`; shows "open Zone N" until that zone is bought
        EngineThenRide,     // buy Engine level `upgradeLevel` first, then rides count
        TimedRides,         // `count` rides within `timeLimit` seconds; the window restarts (new attempt) when it runs out
        CleanLuggageRide,   // a luggage ride without bumps (bumped = failed attempt)
        PerfectStreak,      // `count` five-star rides in a row (anything less resets to 0, new attempt)
    }

    /// <summary>One objective: title shown in the top line and what completes it. Tuned in GameplayConfig.</summary>
    [Serializable]
    public class ObjectiveDef
    {
        public string title = "Objective";
        public ObjectiveKind kind;
        public int count = 1;
        public int zone;
        public int upgradeLevel = 1;
        public float timeLimit = 90f;

        public ObjectiveDef() { }

        public ObjectiveDef(string title, ObjectiveKind kind, int count, int zone = 0, int upgradeLevel = 1, float timeLimit = 90f)
        {
            this.title = title; this.kind = kind; this.count = count;
            this.zone = zone; this.upgradeLevel = upgradeLevel; this.timeLimit = timeLimit;
        }
    }
}
