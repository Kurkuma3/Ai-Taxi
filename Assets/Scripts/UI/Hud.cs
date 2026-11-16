using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Taxi
{
    /// <summary>
    /// Minimal HUD: one hold-progress ring over the car (fills both when getting in and when getting out)
    /// and the virtual stick visuals. No buttons: every action is a touch gesture in the world.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        [SerializeField] Canvas canvas;
        [SerializeField] Camera worldCamera;
        [SerializeField] Image enterRingBackground; // hold ring; field names kept for scene serialization
        [SerializeField] Image enterRingFill;
        [SerializeField] Image stickBase;
        [SerializeField] Image stickKnob;
        [SerializeField] InputSystemUIInputModule uiInput;
        [Header("Trips")]
        [SerializeField] GameplayConfig config;
        [SerializeField] RectTransform fareLabel;     // pill above the waiting passenger
        [SerializeField] Text fareText;
        [SerializeField] RectTransform moneyRoot;     // counter at the top of the screen
        [SerializeField] Text moneyText;
        [SerializeField] Image coinTemplate;          // inactive template, cloned into a pool
        [SerializeField] AudioSource sfx;

        public Camera WorldCamera => worldCamera;

        Color knobColor;
        static readonly Color reverseKnobColor = new Color(1f, 0.65f, 0.2f, 0.85f);

        // Payment animation state (no allocations per frame; the money string changes only when the shown value does).
        struct Coin { public RectTransform rt; public Vector2 from, ctrl; public float start; public bool flying; }
        Coin[] coins;
        int shownMoney, countFrom, countTo;
        float countStart = -1f, punchUntil;
        int fareShown = -1;
        AudioClip coinClip;
        int coinIndexInBurst;

        void Awake()
        {
            SetupTripUi();
            if (uiInput != null && uiInput.actionsAsset == null) uiInput.AssignDefaultActions();
            knobColor = stickKnob.color;

            var ring = UiSprites.Ring(128, 0.78f);
            var disc = UiSprites.Ring(128, 0f);
            enterRingBackground.sprite = ring;
            enterRingFill.sprite = ring;
            stickBase.sprite = UiSprites.Ring(128, 0.9f);
            stickKnob.sprite = disc;

            ShowHoldRing(0f, Vector3.zero);
            ShowStick(false, Vector2.zero, Vector2.zero, 0f);
        }

        /// <summary>Radial ring above a world position; hidden while progress is ~0.</summary>
        public void ShowHoldRing(float progress, Vector3 worldPos)
        {
            bool on = progress > 0.01f;
            if (enterRingBackground.gameObject.activeSelf != on)
            {
                enterRingBackground.gameObject.SetActive(on);
                enterRingFill.gameObject.SetActive(on);
            }
            if (!on) return;
            Vector3 sp = worldCamera.WorldToScreenPoint(worldPos);
            sp.z = 0f;
            enterRingBackground.rectTransform.position = sp;
            enterRingFill.rectTransform.position = sp;
            enterRingFill.fillAmount = Mathf.Clamp01(progress);
        }

        // ---- Trips: fare label, money counter, coins -------------------------------------------------------

        void SetupTripUi()
        {
            if (config == null || moneyText == null) return;   // scene not set up for trips yet
            moneyText.text = "$0";
            HideFare();
            int n = Mathf.Clamp(config.coinsPerPayment, 1, 32);
            coins = new Coin[n];
            var disc = UiSprites.Ring(64, 0f);
            coinTemplate.sprite = disc;
            for (int i = 0; i < n; i++)
            {
                var img = Instantiate(coinTemplate, coinTemplate.transform.parent);
                img.gameObject.SetActive(false);
                coins[i].rt = img.rectTransform;
            }
            coinClip = UiSprites.CoinClip();
        }

        /// <summary>Fare pill above a world point (the waiting passenger's head). Hidden when off-screen.</summary>
        public void ShowFare(int fare, Vector3 worldPos)
        {
            if (fareLabel == null) return;
            Vector3 sp = worldCamera.WorldToScreenPoint(worldPos);
            bool on = sp.z > 0f;
            if (fareLabel.gameObject.activeSelf != on) fareLabel.gameObject.SetActive(on);
            if (!on) return;
            if (fare != fareShown) { fareShown = fare; fareText.text = "$" + fare; }
            sp.z = 0f;
            fareLabel.position = sp;
        }

        public void HideFare()
        {
            if (fareLabel != null && fareLabel.gameObject.activeSelf) fareLabel.gameObject.SetActive(false);
        }

        /// <summary>Shows a value at once, no animation (game start, spending).</summary>
        public void SetMoney(int value)
        {
            shownMoney = countTo = value;
            countStart = -1f;
            if (moneyText != null) moneyText.text = "$" + value;
        }

        /// <summary>Coins fly from the world point to the counter, then the counter counts up to newTotal.</summary>
        public void PlayPayment(Vector3 fromWorld, int newTotal)
        {
            if (coins == null) { if (moneyText != null) moneyText.text = "$" + newTotal; return; }
            Vector3 sp = worldCamera.WorldToScreenPoint(fromWorld);
            Vector2 from = sp.z > 0f ? (Vector2)sp : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 to = moneyRoot.position;
            float now = Time.unscaledTime;
            float spread = 0.06f * Screen.height;
            for (int i = 0; i < coins.Length; i++)
            {
                ref var c = ref coins[i];
                c.from = from + Random.insideUnitCircle * spread * 0.5f;
                // Arc control point: out to the side first, then up into the counter.
                c.ctrl = Vector2.Lerp(c.from, to, 0.3f) + new Vector2(Random.Range(-1f, 1f) * spread * 2f, -spread);
                c.start = now + i * config.coinStagger;
                c.flying = true;
                c.rt.position = c.from;
                c.rt.gameObject.SetActive(true);
            }
            coinIndexInBurst = 0;
            countFrom = shownMoney;
            countTo = newTotal;
            countStart = now + config.coinFlightTime;   // the counter starts when the first coin lands
        }

        void Update()
        {
            if (coins == null) return;
            float now = Time.unscaledTime;
            Vector2 to = moneyRoot.position;
            for (int i = 0; i < coins.Length; i++)
            {
                ref var c = ref coins[i];
                if (!c.flying) continue;
                float t = (now - c.start) / config.coinFlightTime;
                if (t < 0f) continue;
                if (t >= 1f)
                {
                    c.flying = false;
                    c.rt.gameObject.SetActive(false);
                    punchUntil = now + 0.12f;
                    if (sfx != null && coinClip != null)
                    {
                        sfx.pitch = 1f + 0.06f * coinIndexInBurst++;   // rising "ding-ding-ding"
                        sfx.PlayOneShot(coinClip, 0.6f);
                    }
                    continue;
                }
                float e = t * t;   // ease-in: starts lazy, snaps into the counter
                Vector2 a = Vector2.Lerp(c.from, c.ctrl, e), b = Vector2.Lerp(c.ctrl, to, e);
                c.rt.position = Vector2.Lerp(a, b, e);
                c.rt.localScale = Vector3.one * Mathf.Lerp(1.2f, 0.7f, e);
            }

            if (countStart >= 0f && now >= countStart)
            {
                float k = Mathf.Clamp01((now - countStart) / Mathf.Max(0.01f, config.moneyCountUpTime));
                int v = Mathf.RoundToInt(Mathf.Lerp(countFrom, countTo, 1f - (1f - k) * (1f - k)));
                if (v != shownMoney) { shownMoney = v; moneyText.text = "$" + v; }
                if (k >= 1f) countStart = -1f;
            }
            moneyRoot.localScale = Vector3.one * (now < punchUntil ? 1.18f : Mathf.MoveTowards(moneyRoot.localScale.x, 1f, Time.unscaledDeltaTime * 3f));
        }

        public void ShowStick(bool active, Vector2 origin, Vector2 knob, float radiusPx, bool reversing = false)
        {
            if (stickBase.gameObject.activeSelf != active)
            {
                stickBase.gameObject.SetActive(active);
                stickKnob.gameObject.SetActive(active);
            }
            if (!active) return;
            // Knob turns amber while the car backs up so the automatic reverse is never a surprise.
            stickKnob.color = reversing ? reverseKnobColor : knobColor;
            float scale = canvas.scaleFactor;
            stickBase.rectTransform.sizeDelta = Vector2.one * (2f * radiusPx / scale);
            stickKnob.rectTransform.sizeDelta = Vector2.one * (0.8f * radiusPx / scale);
            stickBase.rectTransform.position = origin;
            stickKnob.rectTransform.position = knob;
        }
    }

    /// <summary>Procedural anti-aliased circle / ring sprites so the prototype needs no art files.</summary>
    public static class UiSprites
    {
        public static Sprite Ring(int size, float innerRadius01)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            float c = (size - 1) * 0.5f, outer = c, inner = c * innerRadius01;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Clamp01(outer - d) * Mathf.Clamp01(d - inner + 1f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Short bright "ding" (two detuned sines with a fast decay) so payment has a sound without audio files.</summary>
        public static AudioClip CoinClip()
        {
            const int rate = 44100;
            const float len = 0.12f;
            int n = (int)(rate * len);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                float env = Mathf.Exp(-t * 38f) * Mathf.Clamp01(t * 800f);
                data[i] = env * (0.6f * Mathf.Sin(2f * Mathf.PI * 1760f * t) + 0.4f * Mathf.Sin(2f * Mathf.PI * 2637f * t));
            }
            var clip = AudioClip.Create("Coin", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
