using UnityEngine;

namespace Taxi
{
    /// <summary>Fixed-angle top-down follow camera. Yaw is 0, so screen-up equals world +Z.</summary>
    public class TopDownCamera : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector3 offset = new Vector3(0f, 18f, -8f);
        [SerializeField] float smoothing = 6f;

        public Transform Target { get => target; set => target = value; }

        void Start()
        {
            transform.rotation = Quaternion.LookRotation(-offset.normalized, Vector3.up);
            if (target != null) transform.position = target.position + offset;
        }

        void LateUpdate()
        {
            if (target == null) return;
            float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, target.position + offset, t);
        }
    }
}
