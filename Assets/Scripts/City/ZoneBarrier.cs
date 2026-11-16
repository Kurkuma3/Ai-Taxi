using UnityEngine;

namespace Taxi
{
    /// <summary>
    /// Physical barrier across a road between two zones. Blocks the car and the player while either zone is closed.
    /// It sells whichever of its two zones is still closed (BarrierShop) and disappears once both are open, so a
    /// Zone 3 / Zone 4 border barrier never gives away the other zone.
    /// </summary>
    public class ZoneBarrier : MonoBehaviour
    {
        [Tooltip("Zone behind the barrier (the one it was placed to close).")]
        public int closesZone = 2;
        [Tooltip("Zone on the near side. The barrier stays until both zones are open.")]
        public int otherZone = 1;
        [Tooltip("Zone on the barrier's local +Z side (set by Taxi > Setup Trip System from the zone rectangles). " +
                 "The car must be on the other side of the zone it buys.")]
        public int forwardZone;
        public int price;
        [SerializeField] TextMesh priceLabel;

        Color labelColor = Color.black;
        bool colorCaptured;

        public void SetPrice(int value)
        {
            price = value;
            if (priceLabel != null) priceLabel.text = "$" + value;
        }

        /// <summary>The closed one of the two zones, or 0 when both are open or both closed (unreachable).</summary>
        public int ZoneForSale(ZoneProgress zones)
        {
            bool a = zones.IsOpen(closesZone), b = zones.IsOpen(otherZone);
            if (a == b) return 0;
            return a ? otherZone : closesZone;
        }

        public bool BothOpen(ZoneProgress zones) => zones.IsOpen(closesZone) && zones.IsOpen(otherZone);

        /// <summary>Tints the price text (blink red when the player cannot afford it); null restores the original.</summary>
        public void TintLabel(Color? c)
        {
            if (priceLabel == null) return;
            if (!colorCaptured) { labelColor = priceLabel.color; colorCaptured = true; }
            priceLabel.color = c ?? labelColor;
        }
    }
}
