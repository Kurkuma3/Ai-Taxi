using UnityEngine;

namespace Taxi
{
    /// <summary>
    /// Physical barrier across a road at the entrance of a closed zone. Blocks the car and the player.
    /// The unlock logic (pay the price, remove the barrier) is not written yet; this only carries the data
    /// and shows the price on a placeholder sign.
    /// </summary>
    public class ZoneBarrier : MonoBehaviour
    {
        [Tooltip("Zone that opens when this barrier is removed.")]
        public int closesZone = 2;
        public int price;
        [SerializeField] TextMesh priceLabel;

        public void SetPrice(int value)
        {
            price = value;
            if (priceLabel != null) priceLabel.text = "$" + value;
        }
    }
}
