using UnityEngine;

namespace Taxi
{
    /// <summary>
    /// With GameplayConfig.resetProgressOnLaunch on, every launch starts from scratch: open zones (barriers back),
    /// upgrade levels and objective progress are deleted before anything reads them, so the game starts with the intro.
    /// Money is never saved (the wallet starts with startMoney). Runs first (execution order -1000) so ZoneProgress and
    /// Upgrades, which load in Awake, already see the cleared save.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class LaunchReset : MonoBehaviour
    {
        [SerializeField] GameplayConfig config;

        void Awake()
        {
            if (config == null || !config.resetProgressOnLaunch) return;
            PlayerPrefs.DeleteKey(ZoneProgress.SaveKey);
            for (int i = 0; i < Upgrades.Count; i++) PlayerPrefs.DeleteKey(Upgrades.Key((UpgradeType)i));
            PlayerPrefs.DeleteKey(ObjectiveTracker.SaveKey);
            PlayerPrefs.DeleteKey(ObjectiveTracker.TotalKey);
            PlayerPrefs.Save();
            Debug.Log("[Taxi] resetProgressOnLaunch: zones, upgrades and objectives reset, starting with the intro");
        }
    }
}
