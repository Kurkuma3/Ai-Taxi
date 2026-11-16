using System;
using System.Collections.Generic;
using UnityEngine;

namespace Taxi
{
    /// <summary>One planned trip. Immutable once created: opening a zone mid-ride does not change it.</summary>
    public readonly struct Trip
    {
        public readonly int pickup, dropoff;   // indices into the stop list the planner was given
        public readonly int fromZone, toZone;
        public readonly float distance;        // straight line, metres
        public readonly int fare;              // dollars, zone multiplier included

        public Trip(int pickup, int dropoff, int fromZone, int toZone, float distance, int fare)
        {
            this.pickup = pickup; this.dropoff = dropoff;
            this.fromZone = fromZone; this.toZone = toZone;
            this.distance = distance; this.fare = fare;
        }
    }

    /// <summary>Planner input for one stop: ground position (X, Z) and zone (0 = unknown, never used).</summary>
    public struct StopInfo
    {
        public Vector2 position;
        public int zone;
        public StopInfo(Vector2 position, int zone) { this.position = position; this.zone = zone; }
    }

    /// <summary>The numbers the planner needs, copied from GameplayConfig so the logic stays testable.</summary>
    public struct TripRules
    {
        public float baseFare;
        public float farePerMeter;
        public float[] zoneMultipliers;     // [0] = Zone 1
        public Vector2[] distanceByHighest; // [0] = only Zone 1 open
        public float newZoneChance;
        public int fallbackChoices;         // pick among this many pairs closest to the range when none is inside

        public static TripRules From(GameplayConfig c) => new TripRules
        {
            baseFare = c.baseFare,
            farePerMeter = c.farePerMeter,
            zoneMultipliers = c.zoneFareMultipliers,
            distanceByHighest = c.tripDistanceByHighestZone,
            newZoneChance = c.newZoneTripChance,
            fallbackChoices = c.fallbackPairChoices,
        };

        public float Multiplier(int zone)
        {
            if (zoneMultipliers == null || zoneMultipliers.Length == 0) return 1f;
            return zoneMultipliers[Mathf.Clamp(zone - 1, 0, zoneMultipliers.Length - 1)];
        }

        public Vector2 DistanceRange(int highestOpenZone)
        {
            if (distanceByHighest == null || distanceByHighest.Length == 0) return new Vector2(0f, float.MaxValue);
            return distanceByHighest[Mathf.Clamp(highestOpenZone - 1, 0, distanceByHighest.Length - 1)];
        }
    }

    /// <summary>
    /// Fare formula and trip selection. Pure logic: no scene access, no Unity objects, deterministic for a given Random.
    /// </summary>
    public static class TripPlanner
    {
        /// <summary>(base + perMeter * metres) * the larger multiplier of the two zones, rounded to whole dollars.</summary>
        public static int Fare(in TripRules r, float metres, int fromZone, int toZone)
        {
            float mult = Mathf.Max(r.Multiplier(fromZone), r.Multiplier(toZone));
            // Half rounds up (RoundToInt would round 42.5 to 42).
            return Mathf.Max(1, Mathf.FloorToInt((r.baseFare + r.farePerMeter * metres) * mult + 0.5f));
        }

        /// <summary>
        /// Picks a pickup/drop-off pair. Rules:
        /// - both stops must be in open zones (a closed zone is never a pickup or a destination);
        /// - with probability newZoneChance, while priorityZone > 0, the pair must touch priorityZone (if any such pair exists);
        /// - a random pair whose distance is inside the range for the highest open zone;
        /// - if none is inside, a random one of the fallbackChoices pairs closest to the range (normally the longest ones),
        ///   so the fallback does not repeat one route. Never fails while two usable stops exist.
        /// avoidPickup (-1 = none) is skipped as a pickup when another choice exists, so the next passenger does not
        /// stand exactly where the last one got out.
        /// </summary>
        public static bool TryPlan(IReadOnlyList<StopInfo> stops, Func<int, bool> isZoneOpen, int highestOpenZone,
            int priorityZone, in TripRules rules, System.Random rng, int avoidPickup, List<int> scratch, out Trip trip)
        {
            trip = default;
            int n = stops.Count;
            bool wantPriority = priorityZone > 0 && rng.NextDouble() < rules.newZoneChance;
            Vector2 range = rules.DistanceRange(highestOpenZone);

            // Pass 0: priority filter (if wanted) + avoid pickup; each later pass relaxes one constraint.
            for (int pass = 0; pass < 4; pass++)
            {
                bool usePriority = wantPriority && pass < 2;
                bool useAvoid = avoidPickup >= 0 && (pass == 0 || pass == 2);
                if (pass == 1 && !wantPriority) continue;      // same as pass 2/3 then
                if (pass == 2 && avoidPickup < 0) continue;

                scratch.Clear();
                int k = Mathf.Clamp(rules.fallbackChoices, 1, MaxFallback), found = 0;
                for (int q = 0; q < k; q++) { bestMiss[q] = float.MaxValue; bestCode[q] = -1; }
                for (int i = 0; i < n; i++)
                {
                    int zi = stops[i].zone;
                    if (zi < 1 || !isZoneOpen(zi) || (useAvoid && i == avoidPickup)) continue;
                    for (int j = 0; j < n; j++)
                    {
                        if (j == i) continue;
                        int zj = stops[j].zone;
                        if (zj < 1 || !isZoneOpen(zj)) continue;
                        if (usePriority && zi != priorityZone && zj != priorityZone) continue;
                        float d = Vector2.Distance(stops[i].position, stops[j].position);
                        if (d >= range.x && d <= range.y) { scratch.Add(i * n + j); continue; }
                        if (scratch.Count > 0) continue;   // something fits: the fallback is not needed
                        float miss = d < range.x ? range.x - d : d - range.y;
                        InsertBest(miss, i * n + j, k, ref found);
                    }
                }

                int code;
                if (scratch.Count > 0) code = scratch[rng.Next(scratch.Count)];
                else if (found > 0) code = bestCode[rng.Next(found)];
                else continue;
                int pi = code / n, pj = code % n;

                float dist = Vector2.Distance(stops[pi].position, stops[pj].position);
                int za = stops[pi].zone, zb = stops[pj].zone;
                trip = new Trip(pi, pj, za, zb, dist, Fare(rules, dist, za, zb));
                return true;
            }
            return false;
        }

        // Fixed-size "k best" list for the fallback (sorted by miss, ascending). Main-thread only.
        const int MaxFallback = 8;
        static readonly float[] bestMiss = new float[MaxFallback];
        static readonly int[] bestCode = new int[MaxFallback];

        static void InsertBest(float miss, int code, int k, ref int found)
        {
            if (found == k && miss >= bestMiss[k - 1]) return;
            int pos = found < k ? found++ : k - 1;
            while (pos > 0 && bestMiss[pos - 1] > miss)
            {
                bestMiss[pos] = bestMiss[pos - 1];
                bestCode[pos] = bestCode[pos - 1];
                pos--;
            }
            bestMiss[pos] = miss;
            bestCode[pos] = code;
        }
    }
}
