using System;
using UnityEngine;

namespace Taxi
{
    /// <summary>The player's money. Spending (zone barriers, upgrades) will call TrySpend.</summary>
    public class Wallet : MonoBehaviour
    {
        [SerializeField] GameplayConfig config;

        public int Money { get; private set; }

        void Awake()
        {
            if (config != null) Money = Mathf.Max(0, config.startMoney);
        }

        /// <summary>(amount added, world position it came from).</summary>
        public event Action<int, Vector3> Earned;
        public event Action<int> Spent;

        public void Add(int amount, Vector3 fromWorld)
        {
            if (amount <= 0) return;
            Money += amount;
            Earned?.Invoke(amount, fromWorld);
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0 || amount > Money) return false;
            Money -= amount;
            Spent?.Invoke(amount);
            return true;
        }
    }
}
