using System.Collections.Generic;
using UnityEngine;

namespace Taxi
{
    /// <summary>
    /// Buying upgrades at the garage pads, on foot. Stand on a pad (within upgradePadRadius):
    /// - enough money: the action ring fills over upgradeBuyTime; when full the price is paid, the level goes up
    ///   (saved), coins fly to the pad with a pop, the sign shows the next level or MAX;
    /// - not enough: an empty ring and the sign blinks red;
    /// - max level: nothing happens, the sign says MAX.
    /// After a purchase the pad re-arms only when the player steps off, so one visit never buys two levels.
    /// </summary>
    public class UpgradeShop : MonoBehaviour
    {
        [SerializeField] GameplayConfig config;
        [SerializeField] PlayerStateMachine playerState;
        [SerializeField] PlayerController player;
        [SerializeField] Wallet wallet;
        [SerializeField] Hud hud;
        [SerializeField] Upgrades upgrades;

        static readonly Color BlinkRed = new Color(0.95f, 0.1f, 0.1f);

        readonly List<UpgradePad> pads = new List<UpgradePad>(4);
        UpgradePad current, blinking, spent;
        float progress;
        AudioClip popClip;

        void Start()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
                pads.AddRange(root.GetComponentsInChildren<UpgradePad>(true));
            popClip = UiSprites.PopClip();
            foreach (var p in pads) RefreshLabel(p);
        }

        void Update()
        {
            UpgradePad near = null;
            if (playerState.Current == PlayerStateMachine.State.OnFoot) near = PadUnder(player.transform.position);
            if (near != current) { StopBlink(); current = near; progress = 0f; }
            if (spent != null && spent != current) spent = null;   // stepped off: that pad sells again
            if (current == null || current == spent || upgrades.IsMax(current.type)) { StopBlink(); return; }

            int price = upgrades.NextPrice(current.type);
            Vector3 ringPos = current.transform.position + Vector3.up * 1.2f;
            if (wallet.Money >= price)
            {
                StopBlink();
                progress += Time.deltaTime / Mathf.Max(0.05f, config.upgradeBuyTime);
                hud.RequestActionRing(progress, ringPos);
                if (progress >= 1f) Buy(current, price);
            }
            else
            {
                progress = 0f;
                hud.RequestActionRing(0f, ringPos);
                blinking = current;
                bool red = Mathf.Repeat(Time.time * config.upgradeBlinkRate, 1f) < 0.5f;
                current.Tint(red ? BlinkRed : (Color?)null);
            }
        }

        void Buy(UpgradePad pad, int price)
        {
            progress = 0f;
            if (!wallet.TrySpend(price)) return;   // safety: money changed this frame
            spent = pad;
            hud.PlaySpend(pad.transform.position + Vector3.up * 1.5f, wallet.Money);
            hud.PlaySfx(popClip, 1.25f, 0.9f);
            upgrades.LevelUp(pad.type);
            pad.Punch();
            RefreshLabel(pad);
        }

        void RefreshLabel(UpgradePad p)
        {
            string name = Upgrades.DisplayName(p.type);
            // Two lines so the sign fits the pad width: "Engine" / "Lv1 · $80" (or "MAX").
            p.SetText(upgrades.IsMax(p.type)
                ? name + "\nMAX"
                : name + "\nLv" + (upgrades.Level(p.type) + 1) + " · $" + upgrades.NextPrice(p.type));
        }

        UpgradePad PadUnder(Vector3 pos)
        {
            float r2 = config.upgradePadRadius * config.upgradePadRadius;
            foreach (var p in pads)
            {
                Vector3 d = p.transform.position - pos;
                d.y = 0f;
                if (d.sqrMagnitude <= r2) return p;
            }
            return null;
        }

        void StopBlink()
        {
            if (blinking == null) return;
            blinking.Tint(null);
            blinking = null;
        }
    }
}
