using System;
using UnityEngine;

namespace Taxi
{
    /// <summary>
    /// Which zones are open. Zone 1 is open from the start. Opening a zone makes it the "new zone": for the next
    /// newZonePriorityTrips trips the planner prefers pairs that start or end there.
    /// Buying zones at barriers is not written yet; in the editor, keys 1-4 open a zone for testing
    /// (and hide that zone's barriers so you can drive in).
    /// </summary>
    public class ZoneProgress : MonoBehaviour
    {
        public const int ZoneCount = 4;

        [SerializeField] GameplayConfig config;

        public event Action<int> ZoneOpened;

        readonly bool[] open = new bool[ZoneCount + 1];
        public int HighestOpen { get; private set; } = 1;
        /// <summary>Most recently opened zone while its priority lasts, otherwise 0.</summary>
        public int PriorityZone => priorityTripsLeft > 0 ? newestZone : 0;

        int newestZone;
        int priorityTripsLeft;

        void Awake()
        {
            open[1] = true;
            HighestOpen = 1;
        }

        public bool IsOpen(int zone) => zone >= 1 && zone <= ZoneCount && open[zone];

        /// <summary>Opens a zone. Ignored if it is already open or out of range.</summary>
        public bool Open(int zone)
        {
            if (zone < 1 || zone > ZoneCount || open[zone]) return false;
            open[zone] = true;
            if (zone > HighestOpen) HighestOpen = zone;
            newestZone = zone;
            priorityTripsLeft = config.newZonePriorityTrips;
            ZoneOpened?.Invoke(zone);
            return true;
        }

        /// <summary>Called by the trip manager once per planned trip.</summary>
        public void ConsumePriorityTrip()
        {
            if (priorityTripsLeft > 0) priorityTripsLeft--;
        }

#if UNITY_EDITOR
        void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame) DebugOpen(1);
            if (kb.digit2Key.wasPressedThisFrame) DebugOpen(2);
            if (kb.digit3Key.wasPressedThisFrame) DebugOpen(3);
            if (kb.digit4Key.wasPressedThisFrame) DebugOpen(4);
        }

        void DebugOpen(int zone)
        {
            if (!Open(zone)) return;
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var b in root.GetComponentsInChildren<ZoneBarrier>())
                    if (b.closesZone == zone) b.gameObject.SetActive(false);
            Debug.Log("[Taxi] Debug: zone " + zone + " opened");
        }
#endif
    }
}
