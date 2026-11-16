using System.Collections.Generic;
using UnityEngine;

namespace Taxi
{
    /// <summary>
    /// Buying zones at barriers. Stop the car (driver inside) within barrierBuyRadius of a barrier:
    /// - enough money: the action ring fills over barrierBuyTime by itself; when full the price is paid, the zone opens
    ///   (and is saved by ZoneProgress), every barrier whose two zones are now open pops out with a sound;
    /// - not enough: the ring stays empty and the price sign blinks red.
    /// Moving or getting out drains the ring, so nothing is bought by accident while driving past.
    /// </summary>
    public class BarrierShop : MonoBehaviour
    {
        [SerializeField] GameplayConfig config;
        [SerializeField] CarController car;
        [SerializeField] PlayerStateMachine playerState;
        [SerializeField] ZoneProgress zones;
        [SerializeField] Wallet wallet;
        [SerializeField] Hud hud;
        [SerializeField] TripManager trips;

        static readonly Color BlinkRed = new Color(0.95f, 0.1f, 0.1f);

        readonly List<ZoneBarrier> barriers = new List<ZoneBarrier>(16);
        struct Vanish { public Transform t; public float start; }
        readonly List<Vanish> vanishing = new List<Vanish>(8);
        ZoneBarrier current;      // barrier the car is stopped at
        ZoneBarrier blinking;
        float progress;
        AudioClip popClip;

        void Start()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
                barriers.AddRange(root.GetComponentsInChildren<ZoneBarrier>(true));
            popClip = UiSprites.PopClip();
            zones.ZoneOpened += OnZoneOpened;
            // Restore the saved state: barriers of already open zones are gone from the start.
            foreach (var b in barriers)
            {
                if (b.BothOpen(zones)) b.gameObject.SetActive(false);
                else RefreshPrice(b);
            }
        }

        void OnDestroy()
        {
            if (zones != null) zones.ZoneOpened -= OnZoneOpened;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            AnimateVanishing();

            ZoneBarrier near = null;
            // Passenger stops win: several pockets sit next to a barrier (Stop_06, 17, 18, 29).
            if (playerState.Current == PlayerStateMachine.State.Driving && car.IsStopped
                && (trips == null || !trips.IsAtActiveStop(car.transform.position)))
                near = InBuyArea(car.transform.position);
            if (near != current) { StopBlink(); current = near; progress = 0f; }

            int zone = current != null ? current.ZoneForSale(zones) : 0;
            if (zone == 0)
            {
                StopBlink();
                return;
            }

            int price = PriceOf(zone);
            Vector3 ringPos = current.transform.position + Vector3.up * 4.2f;
            if (wallet.Money >= price)
            {
                StopBlink();
                progress += dt / Mathf.Max(0.05f, config.barrierBuyTime);
                hud.RequestActionRing(progress, ringPos);
                if (progress >= 1f) Buy(current, zone, price);
            }
            else
            {
                progress = 0f;
                hud.RequestActionRing(0f, ringPos);   // empty ring: "this is the place", it just won't fill
                blinking = current;
                bool red = Mathf.Repeat(Time.time * config.barrierBlinkRate, 1f) < 0.5f;
                current.TintLabel(red ? BlinkRed : (Color?)null);
            }
        }

        void Buy(ZoneBarrier b, int zone, int price)
        {
            if (!wallet.TrySpend(price)) { progress = 0f; return; }   // safety: money changed this frame
            progress = 0f;
            current = null;
            hud.PlaySpend(b.transform.position + Vector3.up * 2f, wallet.Money);
            zones.Open(zone);   // -> OnZoneOpened pops every barrier that is now fully open
        }

        void OnZoneOpened(int zone)
        {
            bool played = false;
            foreach (var b in barriers)
            {
                if (!b.gameObject.activeSelf) continue;
                if (b.BothOpen(zones))
                {
                    foreach (var c in b.GetComponentsInChildren<Collider>()) c.enabled = false;   // passable at once
                    vanishing.Add(new Vanish { t = b.transform, start = Time.time });
                    if (!played) { hud.PlaySfx(popClip, 1f, 0.9f); played = true; }
                }
                else RefreshPrice(b);   // e.g. a Zone 3/4 barrier now sells the other side
            }
        }

        /// <summary>Pop: grow 20% then shrink to nothing, then deactivate.</summary>
        void AnimateVanishing()
        {
            for (int i = vanishing.Count - 1; i >= 0; i--)
            {
                var v = vanishing[i];
                float k = (Time.time - v.start) / Mathf.Max(0.05f, config.barrierVanishTime);
                if (k >= 1f)
                {
                    v.t.gameObject.SetActive(false);
                    v.t.localScale = Vector3.one;
                    vanishing.RemoveAt(i);
                    continue;
                }
                float s = k < 0.3f ? Mathf.Lerp(1f, 1.2f, k / 0.3f) : Mathf.Lerp(1.2f, 0f, (k - 0.3f) / 0.7f);
                v.t.localScale = new Vector3(s, s, s);
            }
        }

        void RefreshPrice(ZoneBarrier b)
        {
            int z = b.ZoneForSale(zones);
            b.SetPrice(PriceOf(z > 0 ? z : b.closesZone));
        }

        int PriceOf(int zone)
        {
            var p = config.zonePrices;
            if (p == null || p.Length == 0) return 0;
            return p[Mathf.Clamp(zone - 1, 0, p.Length - 1)];
        }

        /// <summary>
        /// The barrier whose buy area contains the point: a rectangle across the road in front of it
        /// (|local x| within barrierBuyHalfWidth, |local z| within barrierBuyRadius) on the side away from the zone for sale.
        /// </summary>
        ZoneBarrier InBuyArea(Vector3 pos)
        {
            ZoneBarrier best = null;
            float bestZ = float.MaxValue;
            foreach (var b in barriers)
            {
                if (!b.gameObject.activeSelf) continue;
                Vector3 l = b.transform.InverseTransformPoint(pos);
                if (Mathf.Abs(l.x) > config.barrierBuyHalfWidth || Mathf.Abs(l.z) > config.barrierBuyRadius) continue;
                int sale = b.ZoneForSale(zones);
                if (sale == 0) continue;
                // The zone for sale is on +Z if forwardZone says so; the car must be on the opposite side.
                if (b.forwardZone != 0 && (b.forwardZone == sale ? l.z > 0f : l.z < 0f)) continue;
                if (Mathf.Abs(l.z) < bestZ) { bestZ = Mathf.Abs(l.z); best = b; }
            }
            return best;
        }

        void StopBlink()
        {
            if (blinking == null) return;
            blinking.TintLabel(null);
            blinking = null;
        }
    }
}
