using UnityEngine;

namespace Taxi
{
    /// <summary>
    /// One garage pad: a flat disc on the ground and a sign above it ("Engine Lv1 · $80", or "Engine MAX").
    /// Visual only; UpgradeShop runs the buying logic.
    /// </summary>
    public class UpgradePad : MonoBehaviour
    {
        public UpgradeType type;
        [SerializeField] TextMesh label;
        [SerializeField] Transform disc;

        Color labelColor;
        bool colorCaptured;
        string shown;
        float punchStart = -10f;
        const float PunchTime = 0.35f;

        public void SetText(string text)
        {
            if (label == null || text == shown) return;
            shown = text;
            label.text = text;
        }

        /// <summary>Tints the sign text (red blink when short of money); null restores the original.</summary>
        public void Tint(Color? c)
        {
            if (label == null) return;
            if (!colorCaptured) { labelColor = label.color; colorCaptured = true; }
            Color target = c ?? labelColor;
            if (label.color != target) label.color = target;
        }

        /// <summary>Short scale punch of the disc and the sign after a purchase.</summary>
        public void Punch() => punchStart = Time.time;

        void Update()
        {
            float k = (Time.time - punchStart) / PunchTime;
            if (k > 1.2f) return;
            float s = k < 1f ? 1f + 0.3f * Mathf.Sin(k * Mathf.PI) : 1f;
            if (disc != null) disc.localScale = new Vector3(s, 1f, s);
            if (label != null) label.transform.localScale = new Vector3(s, s, s);
        }
    }
}
