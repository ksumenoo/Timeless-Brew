using System;
using UnityEngine;

namespace TimelessBrew.Day
{
    /// <summary>
    /// Простой учёт результатов дня: деньги и репутация (§4.3: «идеальная подача даёт
    /// денежный бонус и репутацию»). Намеренно минимальный — экономику кофейни (§4.6)
    /// расширим позже. Сейчас задача — чтобы у дня был осязаемый итог.
    /// </summary>
    public class CafeLedger : MonoBehaviour
    {
        public static CafeLedger Instance { get; private set; }

        public int Money { get; private set; }
        public int Reputation { get; private set; }

        /// <summary>Изменилось состояние: (деньги, репутация).</summary>
        public event Action<int, int> OnChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void Add(int money, int reputation)
        {
            Money += money;
            Reputation += reputation;
            OnChanged?.Invoke(Money, Reputation);
            Debug.Log($"[CafeLedger] +{money} монет, +{reputation} реп. Итого: {Money} монет, {Reputation} реп.", this);
        }
    }
}
