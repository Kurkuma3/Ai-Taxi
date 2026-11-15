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

        public Camera WorldCamera => worldCamera;

        Color knobColor;
        static readonly Color reverseKnobColor = new Color(1f, 0.65f, 0.2f, 0.85f);

        void Awake()
        {
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
    }
}
