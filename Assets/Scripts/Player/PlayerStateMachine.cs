using UnityEngine;

namespace Taxi
{
    /// <summary>
    /// OnFoot -> Entering -> Driving -> Exiting -> OnFoot.
    /// Enter: stand inside enterRadius of a stopped car until the ring fills.
    /// Exit: hold a finger on the stopped car until the same ring fills (E key in the editor exits at once).
    /// After exiting you must leave the radius once before you can enter again (no instant re-enter loop).
    /// </summary>
    public class PlayerStateMachine : MonoBehaviour
    {
        public enum State { OnFoot, Entering, Driving, Exiting }

        [SerializeField] GameplayConfig config;
        [SerializeField] CarController car;
        [SerializeField] PlayerController player;
        [SerializeField] TopDownCamera cameraRig;
        [SerializeField] Hud hud;

        public State Current { get; private set; }

        float holdProgress; // 0..1, shared by the enter ring and the exit ring
        float timer;
        bool canEnter = true;

        void Start()
        {
            InputReader.I.IsHoldTarget = IsOverStoppedCar;
            Enter(State.OnFoot);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var input = InputReader.I;
            Vector3 ringPos = car.transform.position + Vector3.up * 1.6f;
            switch (Current)
            {
                case State.OnFoot:
                {
                    Vector3 a = player.transform.position, b = car.transform.position;
                    a.y = b.y = 0f;
                    float d = Vector3.Distance(a, b);
                    if (d > config.enterRadius)
                    {
                        canEnter = true;
                        Decay(ref holdProgress, dt);
                    }
                    else if (canEnter && car.IsStopped)
                    {
                        holdProgress += dt / config.enterHoldTime;
                    }
                    hud.ShowHoldRing(holdProgress, ringPos);
                    hud.ShowStick(input.StickActive, input.StickOrigin, input.StickPos, input.StickRadiusPx);
                    if (holdProgress >= 1f) Enter(State.Entering);
                    break;
                }
                case State.Entering:
                case State.Exiting:
                    timer -= dt;
                    if (timer <= 0f) Enter(Current == State.Entering ? State.Driving : State.OnFoot);
                    break;
                case State.Driving:
                    hud.ShowStick(input.StickActive, input.StickOrigin, input.StickPos, input.StickRadiusPx, car.IsReversing);
                    // A finger resting on the stopped car fills the ring; lifting it or moving the car drains it.
                    if (input.HoldActive && car.IsStopped) holdProgress += dt / config.enterHoldTime;
                    else Decay(ref holdProgress, dt);
                    hud.ShowHoldRing(holdProgress, ringPos);
                    if (holdProgress >= 1f || input.ExitPressedThisFrame) RequestExit();
                    break;
            }
        }

        void Decay(ref float progress, float dt)
        {
            progress = Mathf.MoveTowards(progress, 0f, dt / config.enterHoldTime * 2f);
        }

        /// <summary>Hold-target test for InputReader: a finger landing here is "get out", not "drive".</summary>
        bool IsOverStoppedCar(Vector2 screenPos)
        {
            if (Current != State.Driving || !car.IsStopped) return false;
            Vector3 sp = hud.WorldCamera.WorldToScreenPoint(car.transform.position);
            if (sp.z < 0f) return false;
            float r = config.exitHoldRadiusScreenFraction * Screen.height;
            return ((Vector2)sp - screenPos).sqrMagnitude <= r * r;
        }

        void RequestExit()
        {
            if (Current == State.Driving && car.IsStopped) Enter(State.Exiting);
        }

        void Enter(State s)
        {
            Current = s;
            holdProgress = 0f;
            switch (s)
            {
                case State.OnFoot:
                    player.InputEnabled = true;
                    car.InputEnabled = false;
                    cameraRig.Target = player.transform;
                    break;

                case State.Entering:
                    timer = config.enterExitTransition;
                    player.InputEnabled = false;
                    hud.ShowHoldRing(0f, Vector3.zero);
                    hud.ShowStick(false, Vector2.zero, Vector2.zero, 0f);
                    player.SetVisible(false);
                    cameraRig.Target = car.transform;
                    break;

                case State.Driving:
                    car.InputEnabled = true;
                    break;

                case State.Exiting:
                    timer = config.enterExitTransition;
                    car.InputEnabled = false;
                    hud.ShowHoldRing(0f, Vector3.zero);
                    hud.ShowStick(false, Vector2.zero, Vector2.zero, 0f);
                    Vector3 pos = car.transform.TransformPoint(config.exitLocalOffset);
                    pos.y = 0f;
                    player.Teleport(pos, car.transform.rotation);
                    player.SetVisible(true);
                    cameraRig.Target = player.transform;
                    canEnter = false;
                    break;
            }
        }
    }
}
