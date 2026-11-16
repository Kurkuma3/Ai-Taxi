using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Taxi
{
    /// <summary>
    /// Minimal HUD: one hold-progress ring over the car (fills both when getting in and when getting out)
    /// and the virtual stick visuals. No buttons: every action is a touch gesture in the world.
    /// Order markers: one per waiting order, a price pill (with a suitcase icon for luggage) above the passenger's
    /// head while they are on screen, or pinned to the screen edge with an arrow pointing at them while off screen.
    /// Runs after the camera (execution order) so world-anchored UI does not lag a frame behind.
    /// </summary>
    [DefaultExecutionOrder(100)]
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
        [SerializeField] RectTransform fareLabel;     // template for the order price pills (stays hidden)
        [SerializeField] Text fareText;
        [SerializeField] RectTransform moneyRoot;     // counter at the top of the screen
        [SerializeField] Text moneyText;
        [SerializeField] Image coinTemplate;          // inactive template, cloned into a pool
        [SerializeField] AudioSource sfx;

        public Camera WorldCamera => worldCamera;

        Color knobColor;
        static readonly Color reverseKnobColor = new Color(1f, 0.65f, 0.2f, 0.85f);

        // Payment animation state (no allocations per frame; the money string changes only when the shown value does).
        struct Coin { public RectTransform rt; public Vector2 from, ctrl, to; public bool toCounter; public float start; public bool flying; }
        Coin[] coins;
        int shownMoney, countFrom, countTo;
        float countStart = -1f, punchUntil;

        // Order markers (pill + edge arrow per order slot), requested every frame like the action ring.
        struct OrderMarker
        {
            public RectTransform pill, arrow, icon;
            public Image bg, arrowImg;
            public Text text;
            public int fareShown;
            public float pulseStart;
            public bool requested, luggage;
            public Vector3 head, from;
            public Color color;
        }
        OrderMarker[] orderMarkers;
        float pillWidth;
        const float IconRoom = 70f;   // extra pill width (reference px) for the suitcase icon
        static readonly Color pillTextColor = new Color(0.08f, 0.08f, 0.1f);
        AudioClip coinClip;
        int coinIndexInBurst;

        // Action ring (suitcase, trunk, barrier): a second ring cloned from the hold ring. Systems request it every
        // frame they need it; LateUpdate hides it when nobody asked, so two systems never fight over it.
        Image actionBg, actionFill;
        bool actionRequested;
        float actionProgress;
        Vector3 actionWorldPos;
        static readonly Color actionFillColor = new Color(0.35f, 0.95f, 0.5f, 1f);

        void Awake()
        {
            SetupTripUi();
            if (uiInput != null && uiInput.actionsAsset == null) uiInput.AssignDefaultActions();
            knobColor = stickKnob.color;

            var ring = UiSprites.Ring(128, 0.78f);
            var disc = UiSprites.Ring(128, 0f);
            enterRingBackground.sprite = ring;
            enterRingFill.sprite = ring;
            actionBg = Instantiate(enterRingBackground, enterRingBackground.transform.parent);
            actionBg.name = "ActionRingBg";
            actionFill = Instantiate(enterRingFill, enterRingFill.transform.parent);
            actionFill.name = "ActionRingFill";
            actionFill.color = actionFillColor;
            actionBg.gameObject.SetActive(false);
            actionFill.gameObject.SetActive(false);
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
            if (fareLabel != null) fareLabel.gameObject.SetActive(false);
            BuildOrderMarkers();
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

        void BuildOrderMarkers()
        {
            if (fareLabel == null) return;
            int n = Mathf.Max(1, config.offerCount);
            orderMarkers = new OrderMarker[n];
            pillWidth = fareLabel.sizeDelta.x;
            var arrowSprite = UiSprites.Arrow(96);
            var caseSprite = UiSprites.Suitcase(64);
            Transform parent = fareLabel.parent;
            for (int i = 0; i < n; i++)
            {
                ref var m = ref orderMarkers[i];
                var go = Instantiate(fareLabel.gameObject, parent);
                go.name = "Order" + i;
                m.pill = (RectTransform)go.transform;
                m.pill.pivot = new Vector2(0.5f, 0.5f);
                m.bg = go.GetComponent<Image>();
                m.text = go.GetComponentInChildren<Text>(true);
                m.text.color = pillTextColor;

                var icon = new GameObject("Suitcase", typeof(RectTransform), typeof(Image));
                m.icon = (RectTransform)icon.transform;
                m.icon.SetParent(m.pill, false);
                m.icon.anchorMin = m.icon.anchorMax = new Vector2(0f, 0.5f);
                m.icon.pivot = new Vector2(0f, 0.5f);
                m.icon.anchoredPosition = new Vector2(14f, 0f);
                m.icon.sizeDelta = new Vector2(56f, 56f);
                var iconImg = icon.GetComponent<Image>();
                iconImg.sprite = caseSprite;
                iconImg.color = pillTextColor;
                iconImg.raycastTarget = false;

                var arrow = new GameObject("OrderArrow" + i, typeof(RectTransform), typeof(Image));
                m.arrow = (RectTransform)arrow.transform;
                m.arrow.SetParent(parent, false);
                m.arrow.sizeDelta = Vector2.one * config.offerArrowSize;
                m.arrowImg = arrow.GetComponent<Image>();
                m.arrowImg.sprite = arrowSprite;
                m.arrowImg.raycastTarget = false;
                arrow.AddComponent<Outline>().effectDistance = new Vector2(3f, -3f);

                m.fareShown = -1;
                m.luggage = true;   // forces SetLuggage below to lay the pill out
                SetLuggage(ref m, false);
                go.SetActive(false);
                arrow.SetActive(false);
            }
        }

        /// <summary>
        /// Shows the marker of an order slot this frame: price pill over the passenger's head when on screen,
        /// otherwise pinned to the screen edge (inside the safe band) with an arrow pointing from 'fromWorld' toward them.
        /// </summary>
        public void ShowOrder(int slot, int fare, bool luggage, Color color, Vector3 headWorld, Vector3 fromWorld)
        {
            if (orderMarkers == null || slot < 0 || slot >= orderMarkers.Length) return;
            ref var m = ref orderMarkers[slot];
            m.requested = true;
            m.head = headWorld;
            m.from = fromWorld;
            if (fare != m.fareShown) { m.fareShown = fare; m.text.text = "$" + fare; }
            if (luggage != m.luggage) SetLuggage(ref m, luggage);
            if (color != m.color) { m.color = color; m.bg.color = color; m.arrowImg.color = color; }
        }

        /// <summary>Punches an order's pill (its destination and fare just changed).</summary>
        public void PulseOrder(int slot)
        {
            if (orderMarkers == null || slot < 0 || slot >= orderMarkers.Length) return;
            orderMarkers[slot].pulseStart = Time.unscaledTime;
        }

        void SetLuggage(ref OrderMarker m, bool on)
        {
            m.luggage = on;
            m.icon.gameObject.SetActive(on);
            m.pill.sizeDelta = new Vector2(pillWidth + (on ? IconRoom : 0f), m.pill.sizeDelta.y);
            var t = m.text.rectTransform;
            t.offsetMin = new Vector2(on ? IconRoom : 0f, 0f);
            t.offsetMax = Vector2.zero;
        }

        void LayoutOrderMarkers()
        {
            if (orderMarkers == null) return;
            float sc = canvas.scaleFactor, w = Screen.width, h = Screen.height;
            float margin = config.offerEdgeMargin * sc;
            var band = new Rect(margin, h * config.offerEdgeBottom, w - 2f * margin,
                h * (1f - config.offerEdgeBottom - config.offerEdgeTop));
            for (int i = 0; i < orderMarkers.Length; i++)
            {
                ref var m = ref orderMarkers[i];
                bool on = m.requested;
                m.requested = false;
                if (m.pill.gameObject.activeSelf != on) m.pill.gameObject.SetActive(on);
                if (!on) { if (m.arrow.gameObject.activeSelf) m.arrow.gameObject.SetActive(false); continue; }

                float pk = m.pulseStart > 0f ? (Time.unscaledTime - m.pulseStart) / Mathf.Max(0.05f, config.offerPulseTime) : 1f;
                float ps = pk < 1f ? 1f + 0.35f * Mathf.Sin(pk * Mathf.PI) : 1f;
                if (m.pill.localScale.x != ps) m.pill.localScale = new Vector3(ps, ps, 1f);

                Vector2 size = m.pill.sizeDelta * sc;
                Vector3 sp = worldCamera.WorldToScreenPoint(m.head);
                bool inView = sp.z > 0f && sp.x >= 0f && sp.x <= w && sp.y >= 0f && sp.y <= h;
                if (m.arrow.gameObject.activeSelf == inView) m.arrow.gameObject.SetActive(!inView);
                if (inView)
                {
                    m.pill.position = new Vector3(sp.x, sp.y + size.y * 0.5f, 0f);
                    continue;
                }

                // Ray from the car (clamped into the band) toward the passenger, cut by the band edge.
                Vector3 fs = worldCamera.WorldToScreenPoint(m.from);
                var o = new Vector2(Mathf.Clamp(fs.x, band.xMin, band.xMax), Mathf.Clamp(fs.y, band.yMin, band.yMax));
                Vector2 dir = new Vector2(sp.x, sp.y) - o;
                if (sp.z < 0f) dir = -dir;
                if (dir.sqrMagnitude < 1f) dir = Vector2.up;
                dir.Normalize();
                float t = float.MaxValue;
                if (dir.x > 1e-4f) t = Mathf.Min(t, (band.xMax - o.x) / dir.x);
                else if (dir.x < -1e-4f) t = Mathf.Min(t, (band.xMin - o.x) / dir.x);
                if (dir.y > 1e-4f) t = Mathf.Min(t, (band.yMax - o.y) / dir.y);
                else if (dir.y < -1e-4f) t = Mathf.Min(t, (band.yMin - o.y) / dir.y);
                if (t == float.MaxValue) t = 0f;
                Vector2 edge = o + dir * t;

                m.arrow.position = edge;
                m.arrow.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
                // The pill sits just inside the arrow, its near side touching the arrow's base.
                float back = config.offerArrowSize * sc * 0.5f + Mathf.Abs(dir.x) * size.x * 0.5f
                             + Mathf.Abs(dir.y) * size.y * 0.5f + 6f * sc;
                m.pill.position = edge - dir * back;
            }
        }

        /// <summary>
        /// Shows the action ring this frame above a world point (empty ring = "stand here", fills with progress 0..1).
        /// Call every frame while needed; the last caller in a frame wins.
        /// </summary>
        public void RequestActionRing(float progress, Vector3 worldPos)
        {
            actionRequested = true;
            actionProgress = progress;
            actionWorldPos = worldPos;
        }

        void LateUpdate()
        {
            LayoutOrderMarkers();
            bool on = actionRequested && actionBg != null;
            actionRequested = false;
            if (on)
            {
                Vector3 sp = worldCamera.WorldToScreenPoint(actionWorldPos);
                on = sp.z > 0f;
                sp.z = 0f;
                actionBg.rectTransform.position = sp;
                actionFill.rectTransform.position = sp;
                actionFill.fillAmount = Mathf.Clamp01(actionProgress);
            }
            if (actionBg != null && actionBg.gameObject.activeSelf != on)
            {
                actionBg.gameObject.SetActive(on);
                actionFill.gameObject.SetActive(on);
            }
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
                c.to = to;
                c.toCounter = true;
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

        /// <summary>Spending: the counter counts down at once and coins fly from it to the world point (a barrier).</summary>
        public void PlaySpend(Vector3 toWorld, int newTotal)
        {
            if (coins == null) { SetMoney(newTotal); return; }
            Vector3 sp = worldCamera.WorldToScreenPoint(toWorld);
            Vector2 target = sp.z > 0f ? (Vector2)sp : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 from = moneyRoot.position;
            float now = Time.unscaledTime;
            float spread = 0.06f * Screen.height;
            for (int i = 0; i < coins.Length; i++)
            {
                ref var c = ref coins[i];
                c.from = from;
                c.to = target + Random.insideUnitCircle * spread * 0.5f;
                c.ctrl = Vector2.Lerp(from, target, 0.5f) + new Vector2(Random.Range(-1f, 1f) * spread * 2f, spread);
                c.toCounter = false;
                c.start = now + i * config.coinStagger;
                c.flying = true;
                c.rt.position = c.from;
                c.rt.gameObject.SetActive(true);
            }
            coinIndexInBurst = 0;
            countFrom = shownMoney;
            countTo = newTotal;
            countStart = now;
            punchUntil = now + 0.12f;
        }

        /// <summary>One-shot UI sound (procedural clips from UiSprites).</summary>
        public void PlaySfx(AudioClip clip, float pitch = 1f, float volume = 0.7f)
        {
            if (sfx == null || clip == null) return;
            sfx.pitch = pitch;
            sfx.PlayOneShot(clip, volume);
        }

        void Update()
        {
            if (coins == null) return;
            float now = Time.unscaledTime;
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
                    if (!c.toCounter) continue;   // spent coins just vanish at the barrier
                    punchUntil = now + 0.12f;
                    if (sfx != null && coinClip != null)
                    {
                        sfx.pitch = 1f + 0.06f * coinIndexInBurst++;   // rising "ding-ding-ding"
                        sfx.PlayOneShot(coinClip, 0.6f);
                    }
                    continue;
                }
                float e = t * t;   // ease-in: starts lazy, snaps into the target
                Vector2 a = Vector2.Lerp(c.from, c.ctrl, e), b = Vector2.Lerp(c.ctrl, c.to, e);
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

        /// <summary>Solid isosceles triangle pointing up (+Y), anti-aliased edges.</summary>
        public static Sprite Arrow(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            // Counter-clockwise corners: tip, bottom-left, bottom-right.
            Vector2 a = new Vector2(size * 0.5f, size - 2f), b = new Vector2(3f, 3f), c = new Vector2(size - 3f, 3f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float d = Mathf.Min(InsideDist(p, a, b), Mathf.Min(InsideDist(p, b, c), InsideDist(p, c, a)));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(d + 0.5f) * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Distance from p to the edge a->b, positive on the left side (inside a counter-clockwise polygon).</summary>
        static float InsideDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 e = (b - a).normalized;
            Vector2 left = new Vector2(-e.y, e.x);
            return Vector2.Dot(p - a, left);
        }

        /// <summary>Suitcase icon: rounded body with a seam and a handle on top, white (tinted by the Image colour).</summary>
        public static Sprite Suitcase(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            float s = size;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;
                float body = RoundRect(fx, fy, s * 0.08f, s * 0.06f, s * 0.92f, s * 0.72f, s * 0.1f);
                float outer = RoundRect(fx, fy, s * 0.32f, s * 0.66f, s * 0.68f, s * 0.92f, s * 0.08f);
                float inner = RoundRect(fx, fy, s * 0.41f, s * 0.66f, s * 0.59f, s * 0.84f, s * 0.03f);
                float handle = Mathf.Min(outer, 1f - inner);
                float seam = Mathf.Clamp01(Mathf.Abs(fy - s * 0.40f) - s * 0.025f);
                float a = Mathf.Max(body * seam, handle);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Coverage 0..1 of a pixel by a rounded rectangle (1 px soft edge).</summary>
        static float RoundRect(float x, float y, float x0, float y0, float x1, float y1, float r)
        {
            float cx = Mathf.Clamp(x, x0 + r, x1 - r), cy = Mathf.Clamp(y, y0 + r, y1 - r);
            float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;
            return Mathf.Clamp01(0.5f - d);
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

        /// <summary>Barrier "pop": a short noise burst over a falling-then-rising sine sweep, then a bright chime tail.</summary>
        public static AudioClip PopClip()
        {
            const int rate = 44100;
            const float len = 0.45f;
            int n = (int)(rate * len);
            var data = new float[n];
            var rnd = new System.Random(7);
            float phase = 0f, phase2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                float f = t < 0.08f ? Mathf.Lerp(420f, 160f, t / 0.08f) : Mathf.Lerp(520f, 1046f, Mathf.Clamp01((t - 0.08f) / 0.12f));
                phase += 2f * Mathf.PI * f / rate;
                phase2 += 2f * Mathf.PI * 1568f / rate;
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 60f) * 0.5f;
                float body = Mathf.Sin(phase) * Mathf.Exp(-t * 9f) * 0.6f;
                float chime = t > 0.1f ? Mathf.Sin(phase2) * Mathf.Exp(-(t - 0.1f) * 10f) * 0.35f : 0f;
                data[i] = (noise + body + chime) * Mathf.Clamp01(t * 600f);
            }
            var clip = AudioClip.Create("Pop", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
