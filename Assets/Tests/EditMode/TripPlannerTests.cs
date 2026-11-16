using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Taxi.Tests
{
    public class TripPlannerTests
    {
        static TripRules Rules(float newZoneChance = 0.5f, int fallbackChoices = 1) => new TripRules
        {
            fallbackChoices = fallbackChoices,
            baseFare = 10f,
            farePerMeter = 0.15f,
            zoneMultipliers = new[] { 1.0f, 1.2f, 1.4f, 1.7f },
            distanceByHighest = new[] { new Vector2(80, 150), new Vector2(150, 250), new Vector2(200, 300), new Vector2(250, 350) },
            newZoneChance = newZoneChance,
        };

        // Stops on a line: zone 1 at x = 0, 50, 120; zone 2 at x = 200, 300; zone 3 at x = 400.
        static List<StopInfo> Line() => new List<StopInfo>
        {
            new StopInfo(new Vector2(0, 0), 1), new StopInfo(new Vector2(50, 0), 1), new StopInfo(new Vector2(120, 0), 1),
            new StopInfo(new Vector2(200, 0), 2), new StopInfo(new Vector2(300, 0), 2), new StopInfo(new Vector2(400, 0), 3),
        };

        static bool Open(int maxZone, int z) => z <= maxZone;

        [Test]
        public void FareUsesTheMoreExpensiveZone()
        {
            var r = Rules();
            Assert.AreEqual(25, TripPlanner.Fare(r, 100f, 1, 1));            // 10 + 15
            Assert.AreEqual(30, TripPlanner.Fare(r, 100f, 1, 2));            // 25 * 1.2
            Assert.AreEqual(30, TripPlanner.Fare(r, 100f, 2, 1));
            Assert.AreEqual(43, TripPlanner.Fare(r, 100f, 4, 3));            // 25 * 1.7 = 42.5 -> 43
        }

        [Test]
        public void NeverUsesAClosedZone()
        {
            var stops = Line();
            var rng = new System.Random(1);
            var scratch = new List<int>();
            for (int k = 0; k < 200; k++)
            {
                Assert.IsTrue(TripPlanner.TryPlan(stops, z => Open(2, z), 2, 0, Rules(), rng, -1, scratch, out var t));
                Assert.LessOrEqual(t.fromZone, 2);
                Assert.LessOrEqual(t.toZone, 2);
            }
        }

        [Test]
        public void PicksInsideTheRangeOfTheHighestOpenZone()
        {
            var stops = Line();
            var rng = new System.Random(2);
            var scratch = new List<int>();
            for (int k = 0; k < 200; k++)
            {
                TripPlanner.TryPlan(stops, z => Open(2, z), 2, 0, Rules(), rng, -1, scratch, out var t);
                Assert.That(t.distance, Is.InRange(150f, 250f));
            }
        }

        [Test]
        public void FallsBackToTheLongestPairWhenNothingFits()
        {
            // Only zone 1 open, its stops at x = 0, 50, 70: pairs are 50, 20, 70 m, none in 80-150 -> the 70 m pair.
            var stops = Line();
            stops[2] = new StopInfo(new Vector2(70, 0), 1);
            var scratch = new List<int>();
            Assert.IsTrue(TripPlanner.TryPlan(stops, z => Open(1, z), 1, 0, Rules(), new System.Random(3), -1, scratch, out var t));
            Assert.AreEqual(70f, t.distance, 0.01f);
        }

        [Test]
        public void FallbackVariesAmongTheClosestPairs()
        {
            // Same 50/20/70 m layout; with 3 choices the fallback must not always return the single longest pair.
            var stops = Line();
            stops[2] = new StopInfo(new Vector2(70, 0), 1);
            var rng = new System.Random(7);
            var scratch = new List<int>();
            var seen = new HashSet<int>();
            for (int k = 0; k < 100; k++)
            {
                TripPlanner.TryPlan(stops, z => Open(1, z), 1, 0, Rules(0.5f, 3), rng, -1, scratch, out var t);
                Assert.GreaterOrEqual(t.distance, 50f - 0.01f);   // 70, 70 (both directions) or 50: never the 20 m pair
                seen.Add(t.pickup * 10 + t.dropoff);
            }
            Assert.Greater(seen.Count, 1);
        }

        [Test]
        public void NewZoneGetsAboutHalfOfTheTrips()
        {
            var stops = Line();
            var rng = new System.Random(4);
            var scratch = new List<int>();
            int touching = 0, n = 2000;
            for (int k = 0; k < n; k++)
            {
                TripPlanner.TryPlan(stops, z => Open(3, z), 3, 3, Rules(), rng, -1, scratch, out var t);
                if (t.fromZone == 3 || t.toZone == 3) touching++;
            }
            // 50% forced + whatever the unforced half picks by chance.
            Assert.Greater(touching, n * 0.45f);
        }

        [Test]
        public void AvoidsThePreviousDropoffAsPickupWhenPossible()
        {
            var stops = Line();
            var rng = new System.Random(5);
            var scratch = new List<int>();
            for (int k = 0; k < 200; k++)
            {
                TripPlanner.TryPlan(stops, z => Open(2, z), 2, 0, Rules(), rng, 3, scratch, out var t);
                Assert.AreNotEqual(3, t.pickup);
            }
        }

        [Test]
        public void FailsOnlyWithFewerThanTwoUsableStops()
        {
            var stops = new List<StopInfo> { new StopInfo(Vector2.zero, 1), new StopInfo(new Vector2(500, 0), 2) };
            Assert.IsFalse(TripPlanner.TryPlan(stops, z => Open(1, z), 1, 0, Rules(), new System.Random(6), -1, new List<int>(), out _));
        }

        [Test]
        public void StopZoneFromParentName()
        {
            Assert.AreEqual(3, PassengerStop.ParseZoneName("Zone 3 ($300)"));
            Assert.AreEqual(2, PassengerStop.ParseZoneName("Z2"));
            Assert.AreEqual(0, PassengerStop.ParseZoneName("Zones"));
            Assert.AreEqual(0, PassengerStop.ParseZoneName("PassengerStops"));
        }
    }
}
