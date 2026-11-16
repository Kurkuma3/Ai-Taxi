using System;
using UnityEngine;

namespace Taxi
{
    public enum UpgradeType { Engine = 0, Steering = 1, FareMeter = 2 }

    /// <summary>
    /// Upgrade levels (saved in PlayerPrefs, one key per upgrade) and the multipliers they give.
    /// Max level = the length of the price array in GameplayConfig. Bought at garage pads (UpgradeShop).
    /// Taxi > Reset Save clears the keys.
    /// </summary>
    public class Upgrades : MonoBehaviour
    {
        public const int Count = 3;
        public const string KeyPrefix = "Taxi.Upgrade.";

        [SerializeField] GameplayConfig config;

        /// <summary>Raised after a level changes (the trip manager re-prices waiting orders).</summary>
        public event Action<UpgradeType> Changed;

        readonly int[] levels = new int[Count];

        /// <summary>Car top speed for the current Engine level (level 0 = config.maxSpeed).</summary>
        public float MaxSpeed => TopSpeedAt(levels[(int)UpgradeType.Engine]);
        /// <summary>Acceleration grows with the top speed, so the time to reach it stays about the same.</summary>
        public float AccelerationMultiplier => MaxSpeed / Mathf.Max(0.1f, config.maxSpeed);

        /// <summary>Top speed at an Engine level; levels past the configured list keep the last value.</summary>
        public float TopSpeedAt(int level)
        {
            var s = config.engineTopSpeeds;
            if (level <= 0 || s == null || s.Length == 0) return config.maxSpeed;
            return s[Mathf.Min(level, s.Length) - 1];
        }
        public float TurnMultiplier => 1f + config.steeringTurnPerLevel * levels[(int)UpgradeType.Steering];
        /// <summary>Fare bonus fraction (0.1 per Fare Meter level by default).</summary>
        public float FareBonus => config.fareMeterPerLevel * levels[(int)UpgradeType.FareMeter];

        void Awake()
        {
            for (int i = 0; i < Count; i++)
                levels[i] = Mathf.Clamp(PlayerPrefs.GetInt(Key((UpgradeType)i), 0), 0, MaxLevel((UpgradeType)i));
        }

        public static string Key(UpgradeType t) => KeyPrefix + t;

        public int Level(UpgradeType t) => levels[(int)t];

        public int MaxLevel(UpgradeType t)
        {
            var p = Prices(t);
            return p != null ? p.Length : 0;
        }

        public bool IsMax(UpgradeType t) => Level(t) >= MaxLevel(t);

        /// <summary>Price of the next level, or 0 at max.</summary>
        public int NextPrice(UpgradeType t) => IsMax(t) ? 0 : Prices(t)[Level(t)];

        int[] Prices(UpgradeType t)
        {
            switch (t)
            {
                case UpgradeType.Engine: return config.enginePrices;
                case UpgradeType.Steering: return config.steeringPrices;
                default: return config.fareMeterPrices;
            }
        }

        /// <summary>Raises the level by one and saves. The caller has already paid. Ignored at max.</summary>
        public bool LevelUp(UpgradeType t)
        {
            if (IsMax(t)) return false;
            levels[(int)t]++;
            PlayerPrefs.SetInt(Key(t), levels[(int)t]);
            PlayerPrefs.Save();
            Changed?.Invoke(t);
            return true;
        }

        public static string DisplayName(UpgradeType t) => t == UpgradeType.FareMeter ? "Fare Meter" : t.ToString();
    }
}
