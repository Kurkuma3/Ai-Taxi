using UnityEngine;

namespace Taxi
{
    /// <summary>
    /// Empty marker in a parking pocket: where a passenger can wait and where the taxi stops to pick up / drop off.
    /// Forward (+Z) points along the road. No behaviour yet; the passenger system will read these.
    /// </summary>
    public class PassengerStop : MonoBehaviour
    {
        [Tooltip("Zone this stop belongs to (1 = start zone).")]
        public int zone = 1;

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.9f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, new Vector3(1f, 1f, 1f));
            Gizmos.DrawLine(transform.position + Vector3.up * 0.5f, transform.position + Vector3.up * 0.5f + transform.forward * 2f);
        }
    }
}
