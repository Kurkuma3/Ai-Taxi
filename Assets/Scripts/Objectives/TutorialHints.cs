using UnityEngine;

namespace Taxi
{
    /// <summary>
    /// Animated finger hints, only in the tutorial steps:
    /// - intro (objective 0): finger on the taxi, "Walk to your taxi", while on foot;
    /// - Heavy bags (objective 2, the first luggage ride), step by step: get out (finger on the taxi), pick up the bag,
    ///   put it in the trunk; at the drop-off: get out, take it from the trunk, give it to the passenger.
    /// Each hint disappears as soon as its step is done (it is re-evaluated every frame from the game state).
    /// </summary>
    public class TutorialHints : MonoBehaviour
    {
        [SerializeField] ObjectiveTracker tracker;
        [SerializeField] TripManager trips;
        [SerializeField] PlayerStateMachine playerState;
        [SerializeField] CarController car;
        [SerializeField] Hud hud;

        const int IntroIndex = 0;
        const int LuggageTutorialIndex = 2;   // "Heavy bags" in the default objective list

        void LateUpdate()
        {
            if (tracker.Index == IntroIndex)
            {
                if (playerState.Current == PlayerStateMachine.State.OnFoot)
                    hud.RequestHint(car.transform.position + Vector3.up * 1f, "Walk to your taxi");
                return;
            }
            if (tracker.Index != LuggageTutorialIndex) return;
            var step = trips.CurrentLuggageStep(out var target);
            switch (step)
            {
                case TripManager.LuggageStep.GetOutAtPickup:
                case TripManager.LuggageStep.GetOutAtDropoff:
                    hud.RequestHint(car.transform.position + Vector3.up * 1f, "Hold on the taxi to get out");
                    break;
                case TripManager.LuggageStep.PickUpBag:
                    hud.RequestHint(target + Vector3.up * 0.5f, "Pick up the bag");
                    break;
                case TripManager.LuggageStep.ToTrunk:
                    hud.RequestHint(target + Vector3.up * 0.6f, "Put it in the trunk");
                    break;
                case TripManager.LuggageStep.FromTrunk:
                    hud.RequestHint(target + Vector3.up * 0.6f, "Take the bag from the trunk");
                    break;
                case TripManager.LuggageStep.ToPassenger:
                    hud.RequestHint(target + Vector3.up * 1.2f, "Give the bag to the passenger");
                    break;
            }
        }
    }
}
