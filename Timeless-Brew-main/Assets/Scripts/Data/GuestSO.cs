using UnityEngine;
using TimelessBrew.Day;

namespace TimelessBrew.Data
{
    // Именованный гость прототипа (§1.6). Семь гостей с фиксированными утренним и вечерним
    // заказами. Создаётся как .asset через меню TimelessBrew/Guest.
    [CreateAssetMenu(fileName = "Guest", menuName = "TimelessBrew/Guest", order = 1)]
    public class GuestSO : ScriptableObject
    {
        [Header("Идентификация")]
        public string guestId;        // технический id, напр. 'bear'
        public string displayName;    // имя/вид для тикета, напр. 'Медведь'

        [Header("Визуал (опционально)")]
        public Sprite portrait;
        public GameObject visitorPrefab;   // модель гостя у стойки; можно оставить пустым

        [Header("Расписание визитов (§4.4)")]
        public bool visitsMorning = true;
        public bool visitsEvening = true;

        [Header("Заказы")]
        public DrinkSpec morningOrder = new DrinkSpec();
        public DrinkSpec eveningOrder = new DrinkSpec();

        [Header("Особенности")]
        public bool takeawayOnly = false;   // заказ навынос (напр. Крыса)

        [Header("Скрытое предпочтение (§4.3 — бонус-чаевые)")]
        public string hiddenPreferenceSpice;   // напр. 'cocoa' (Крыса), 'rosemary' (Дракон)
        public bool appreciatesLatteArt = false;   // ценит латте-арт (Аист)

        // Приходит ли гость в указанную фазу.
        public bool VisitsIn(DayPhase phase)
        {
            if (phase == DayPhase.Morning) return visitsMorning;
            if (phase == DayPhase.Evening) return visitsEvening;
            return false;   // днём именованные гости из §1.6 не запланированы
        }

        // Заказ для конкретной фазы.
        public DrinkSpec OrderFor(DayPhase phase)
        {
            if (phase == DayPhase.Evening) return eveningOrder;
            return morningOrder;
        }
    }
}
