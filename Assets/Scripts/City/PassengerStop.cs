using UnityEngine;

namespace Taxi
{
    /// <summary>
    /// Empty marker in a parking pocket: where a passenger waits and where the taxi stops to pick up / drop off.
    /// Forward (+Z) points along the road. The zone comes from the field; if it is left at 0 it is taken from the
    /// nearest parent named "Zone N" or "ZN" (so stops can be grouped by zone in the hierarchy instead).
    /// </summary>
    public class PassengerStop : MonoBehaviour
    {
        [Tooltip("Zone this stop belongs to (1 = start zone). 0 = take it from a parent named 'Zone N'.")]
        public int zone = 1;
        [Tooltip("Tutorial mission N (1-based) always picks up here. 0 = a normal stop.")]
        public int tutorialMission;

        /// <summary>Zone from the field or the parent chain; 0 if neither says.</summary>
        public int ResolvedZone
        {
            get
            {
                if (zone >= 1) return zone;
                for (var t = transform.parent; t != null; t = t.parent)
                {
                    int z = ParseZoneName(t.name);
                    if (z >= 1) return z;
                }
                return 0;
            }
        }

        /// <summary>"Zone 3", "Zone 3 ($300)", "Z3", "Z3_Stops" -> 3; anything else -> 0.</summary>
        public static int ParseZoneName(string n)
        {
            if (string.IsNullOrEmpty(n)) return 0;
            int i;
            if (n.StartsWith("Zone")) i = 4;
            else if (n.Length > 1 && n[0] == 'Z' && char.IsDigit(n[1])) i = 1;
            else return 0;
            while (i < n.Length && n[i] == ' ') i++;
            return i < n.Length && char.IsDigit(n[i]) ? n[i] - '0' : 0;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.9f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, new Vector3(1f, 1f, 1f));
            Gizmos.DrawLine(transform.position + Vector3.up * 0.5f, transform.position + Vector3.up * 0.5f + transform.forward * 2f);
        }
    }
}
