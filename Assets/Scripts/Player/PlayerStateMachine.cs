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
        /// <summary>Set by other systems (carrying a suitcase): the enter ring does not fill while true.</summary>
        public bool EnterBlocked { get; set; }

        float holdProgress; // 0..1, shared by the enter ring and the exit ring
        float timer;
        bool canEnter = true;
        Vector3 exitPos;
        int curbMask, blockMask;

        void Start()
        {
            InputReader.I.IsHoldTarget = IsOverStoppedCar;
            int curb = LayerMask.NameToLayer("Curb"), playerLayer = LayerMask.NameToLayer("Player");
            curbMask = curb >= 0 ? 1 << curb : 0;
            blockMask = Physics.DefaultRaycastLayers & ~(playerLayer >= 0 ? 1 << playerLayer : 0);
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
                    else if (canEnter && car.IsStopped && !EnterBlocked)
                    {
                        holdProgress += dt / config.enterHoldTime;
                    }
                    else Decay(ref holdProgress, dt);
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
            if (Current != State.Driving || !car.IsStopped) return;
            if (TryFindExitPoint(out exitPos)) Enter(State.Exiting);
            else holdProgress = 0f;   // boxed in on every side: stay in the car, drive somewhere else
        }

        /// <summary>
        /// Left door first, then right, then front/back, then a ring search up to exitSearchRadius.
        /// A spot is valid when the line from the car to it crosses no curb or wall (so it is on the road the car is on)
        /// and a player-sized capsule fits there.
        /// </summary>
        bool TryFindExitPoint(out Vector3 result)
        {
            Transform t = car.transform;
            Vector3 left = config.exitLocalOffset;
            if (IsFreeExit(t.TransformPoint(left), out result)) return true;
            if (IsFreeExit(t.TransformPoint(new Vector3(-left.x, left.y, left.z)), out result)) return true;
            if (IsFreeExit(t.TransformPoint(new Vector3(0f, 0f, 3.2f)), out result)) return true;
            if (IsFreeExit(t.TransformPoint(new Vector3(0f, 0f, -3.2f)), out result)) return true;
            for (float r = Mathf.Abs(left.x) + 0.6f; r <= config.exitSearchRadius; r += 0.6f)
                for (int a = 0; a < 360; a += 30)
                {
                    Vector3 local = Quaternion.Euler(0f, a, 0f) * new Vector3(-r, 0f, 0f);
                    if (IsFreeExit(t.TransformPoint(local), out result)) return true;
                }
            result = Vector3.zero;
            return false;
        }

        bool IsFreeExit(Vector3 p, out Vector3 result)
        {
            p.y = 0f;
            result = p;
            Vector3 from = car.transform.position; from.y = 0.5f;
            Vector3 to = p; to.y = 0.5f;
            if (Physics.Linecast(from, to, curbMask | blockMask, QueryTriggerInteraction.Ignore)) return false;
            return !Physics.CheckCapsule(p + Vector3.up * 0.45f, p + Vector3.up * 1.45f, 0.35f, blockMask, QueryTriggerInteraction.Ignore);
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
                    player.Teleport(exitPos, car.transform.rotation);
                    player.SetVisible(true);
                    cameraRig.Target = player.transform;
                    canEnter = false;
                    break;
            }
        }
    }
}
