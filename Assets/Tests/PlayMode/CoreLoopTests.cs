using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Taxi.Tests
{
    /// <summary>Smoke test of the whole R1 loop with simulated input: enter, drive, stop, exit.</summary>
    public class CoreLoopTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator EnterDriveStopExit()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Touchscreen>();
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;

            var fsm = UnityEngine.Object.FindFirstObjectByType<PlayerStateMachine>();
            var car = UnityEngine.Object.FindFirstObjectByType<CarController>();
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(fsm); Assert.IsNotNull(car); Assert.IsNotNull(player);
            Assert.AreEqual(PlayerStateMachine.State.OnFoot, fsm.Current);

            // Walk up to the car: stand inside the enter radius, ring fills, we get in.
            player.Teleport(car.transform.position + new Vector3(-1.5f, 0f, 0f), Quaternion.identity);
            yield return WaitFor(() => fsm.Current == PlayerStateMachine.State.Driving, 5f, "enter car");

            // Stick up -> drive straight (car starts facing +Z).
            Vector3 start = car.transform.position;
            Press(keyboard.wKey);
            yield return WaitFor(() => car.Speed > 3f, 3f, "accelerate");
            yield return new WaitForSeconds(0.5f);
            Assert.Greater(Vector3.Distance(start, car.transform.position), 1f, "car should have moved");

            // Stick up-right -> heading turns right.
            float yawBefore = car.transform.eulerAngles.y;
            Press(keyboard.dKey);
            yield return new WaitForSeconds(0.5f);
            Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(yawBefore, car.transform.eulerAngles.y)), 10f, "car should turn right");

            // Exit must be refused while moving.
            Press(keyboard.eKey); yield return null; Release(keyboard.eKey); yield return null;
            Assert.AreEqual(PlayerStateMachine.State.Driving, fsm.Current, "cannot exit while moving");

            // Release -> stops.
            Release(keyboard.wKey); Release(keyboard.dKey);
            yield return WaitFor(() => car.IsStopped, 5f, "brake to stop");

            // Exit when stopped: hold a finger on the car until the ring fills -> back on foot, player visible.
            Vector2 carOnScreen = Camera.main.WorldToScreenPoint(car.transform.position);
            BeginTouch(1, carOnScreen);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(PlayerStateMachine.State.Driving, fsm.Current, "a short touch on the car is not an exit yet");
            Assert.IsFalse(car.HasInput, "a finger held on the car must not drive it");
            yield return WaitFor(() => fsm.Current == PlayerStateMachine.State.OnFoot, 3f, "exit car by holding on it");
            EndTouch(1, carOnScreen);
            Assert.IsTrue(player.transform.Find("Visual").gameObject.activeSelf, "player visible after exit");

            // No instant re-enter: still on foot a second later.
            yield return new WaitForSeconds(1.5f);
            Assert.AreEqual(PlayerStateMachine.State.OnFoot, fsm.Current, "no instant re-enter after exit");
        }

        /// <summary>Stick pointing behind the car backs it up (how you get out of a wall), front stick returns to forward.</summary>
        [UnityTest]
        public IEnumerator ReverseWhenStickPointsBack()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;

            var fsm = UnityEngine.Object.FindFirstObjectByType<PlayerStateMachine>();
            var car = UnityEngine.Object.FindFirstObjectByType<CarController>();
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            player.Teleport(car.transform.position + new Vector3(-1.5f, 0f, 0f), Quaternion.identity);
            yield return WaitFor(() => fsm.Current == PlayerStateMachine.State.Driving, 5f, "enter car");

            // Car faces +Z; stick down = -Z = straight behind -> reverse, car moves backward along -Z.
            Vector3 start = car.transform.position;
            Press(keyboard.sKey);
            yield return WaitFor(() => car.IsReversing && car.Speed < -1f, 3f, "start reversing");
            yield return new WaitForSeconds(0.5f);
            Assert.Less(car.transform.position.z, start.z - 0.5f, "car should have moved backward");
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(0f, car.transform.eulerAngles.y)), 10f, "straight reverse keeps heading");

            // Stick swings to the front -> back to forward driving.
            Release(keyboard.sKey); Press(keyboard.wKey);
            yield return WaitFor(() => !car.IsReversing && car.Speed > 1f, 4f, "return to forward");
            Release(keyboard.wKey);
            yield return WaitFor(() => car.IsStopped, 5f, "brake to stop");
        }

        static IEnumerator WaitFor(Func<bool> cond, float timeout, string what)
        {
            float t = 0f;
            while (!cond())
            {
                if ((t += Time.deltaTime) > timeout) Assert.Fail("Timeout: " + what);
                yield return null;
            }
        }
    }
}
