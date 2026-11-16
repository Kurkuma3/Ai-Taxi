using System;
using UnityEngine;

namespace Taxi
{
    /// <summary>
    /// Which zones are open. Zone 1 is open from the start. Open zones are saved (PlayerPrefs bitmask) and restored
    /// on launch without the new-zone priority. Opening a zone makes it the "new zone": for the next
    /// newZonePriorityTrips trips the planner prefers pairs that start or end there.
    /// Zones are bought at barriers (BarrierShop). Editor only: keys 1-4 open a zone for testing; these debug opens
    /// are not saved, so the next Play starts with the real progress and its barriers.
    /// </summary>
    public class ZoneProgress : MonoBehaviour
    {
        public const int ZoneCount = 4;
        public const string SaveKey = "Taxi.OpenZones";

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
            int mask = PlayerPrefs.GetInt(SaveKey, 1 << 1) | (1 << 1);   // Zone 1 is always open
            for (int z = 1; z <= ZoneCount; z++)
            {
                open[z] = (mask & (1 << z)) != 0;
                if (open[z]) HighestOpen = z;
            }
            if (mask != (1 << 1))
                Debug.Log("[Taxi] Restored saved open zones (mask " + mask + "). Their barriers are hidden. Taxi > Reset Save clears it.");
        }

        public bool IsOpen(int zone) => zone >= 1 && zone <= ZoneCount && open[zone];

        /// <summary>Opens a zone and saves (unless save is false). Ignored if it is already open or out of range.</summary>
        public bool Open(int zone, bool save = true)
        {
            if (zone < 1 || zone > ZoneCount || open[zone]) return false;
            open[zone] = true;
            if (zone > HighestOpen) HighestOpen = zone;
            newestZone = zone;
            priorityTripsLeft = config.newZonePriorityTrips;
            if (save) Save();
            ZoneOpened?.Invoke(zone);
            return true;
        }

        /// <summary>Called by the trip manager once per planned trip.</summary>
        public void ConsumePriorityTrip()
        {
            if (priorityTripsLeft > 0) priorityTripsLeft--;
        }

        void Save()
        {
            int mask = 0;
            for (int z = 1; z <= ZoneCount; z++) if (open[z]) mask |= 1 << z;
            PlayerPrefs.SetInt(SaveKey, mask);
            PlayerPrefs.Save();
        }

#if UNITY_EDITOR
        // Debug shortcuts exist only in the editor; release builds compile without them.
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
            if (Open(zone, save: false)) Debug.Log("[Taxi] Debug: zone " + zone + " opened for this session only (not saved)");
        }
#endif
    }
}
