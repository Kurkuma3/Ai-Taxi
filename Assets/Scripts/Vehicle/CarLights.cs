using UnityEngine;

namespace Taxi
{
    /// <summary>
    /// Cheap car lights, no real-time light: six small unlit plates on the body, one shared unlit material,
    /// colours set through a MaterialPropertyBlock only when they change.
    /// - Headlights (front): on while the driver is in the car (car input enabled), dim otherwise.
    /// - Brake lights (rear, outer): bright red while braking, dim red otherwise.
    /// - Reverse lights (rear, inner): white while backing up, off (dark) otherwise.
    /// Colours and sizes come from GameplayConfig (header Car lights).
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class CarLights : MonoBehaviour
    {
        [SerializeField] GameplayConfig config;
        [Tooltip("Unlit material (URP Unlit), tinted per plate through _BaseColor.")]
        [SerializeField] Material material;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        CarController car;
        Renderer[] head, brake, reverse;
        MaterialPropertyBlock block;
        int headState = -1, brakeState = -1, reverseState = -1;

        void Awake()
        {
            car = GetComponent<CarController>();
            block = new MaterialPropertyBlock();
            // Body: 1.8 x 0.7 x 3.8 m, from y 0.2 to 0.9, nose at +Z 1.9.
            head = new[]
            {
                Plate("Headlight_L", new Vector3(-0.58f, 0.62f, 1.905f), new Vector3(0.42f, 0.18f, 0.03f)),
                Plate("Headlight_R", new Vector3(0.58f, 0.62f, 1.905f), new Vector3(0.42f, 0.18f, 0.03f)),
            };
            brake = new[]
            {
                Plate("Brake_L", new Vector3(-0.66f, 0.64f, -1.905f), new Vector3(0.36f, 0.2f, 0.03f)),
                Plate("Brake_R", new Vector3(0.66f, 0.64f, -1.905f), new Vector3(0.36f, 0.2f, 0.03f)),
            };
            reverse = new[]
            {
                Plate("Reverse_L", new Vector3(-0.32f, 0.64f, -1.905f), new Vector3(0.2f, 0.16f, 0.03f)),
                Plate("Reverse_R", new Vector3(0.32f, 0.64f, -1.905f), new Vector3(0.2f, 0.16f, 0.03f)),
            };
        }

        Renderer Plate(string name, Vector3 pos, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        void LateUpdate()
        {
            int h = car.InputEnabled ? 1 : 0;
            int b = car.IsBraking ? 1 : 0;
            int rv = car.IsReversing && car.Speed < -0.1f ? 1 : 0;
            if (h != headState) { headState = h; Tint(head, h == 1 ? config.headlightOn : config.headlightOff); }
            if (b != brakeState) { brakeState = b; Tint(brake, b == 1 ? config.brakeLightOn : config.brakeLightOff); }
            if (rv != reverseState) { reverseState = rv; Tint(reverse, rv == 1 ? config.reverseLightOn : config.reverseLightOff); }
        }

        void Tint(Renderer[] rs, Color c)
        {
            block.SetColor(BaseColorId, c);
            foreach (var r in rs) r.SetPropertyBlock(block);
        }
    }
}
