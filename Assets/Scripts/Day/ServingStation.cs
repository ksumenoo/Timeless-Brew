using System;
using UnityEngine;
using TimelessBrew.Data;
using TimelessBrew.Items;

namespace TimelessBrew.Day
{
    /// <summary>
    /// Точка подачи (§4.4, §4.3 v4.3). Принимает фактически собранный напиток, находит гостя
    /// у стойки и его заказ на чекхолдере, считает звёздную оценку (1..5) и завершает визит:
    /// гость реагирует и уходит, в учёт идут деньги (масштаб по звёздам) и репутация.
    ///
    /// Бонус-чаевые (§4.3): за попадание в скрытое предпочтение гостя (специя) и за аккуратный
    /// латте-арт ценителю. Это шов интеграции с готовкой: мини-игры пишут результат в BrewSession,
    /// игрок подносит чашку к гостю — вызывается <see cref="ServeFromSession"/>.
    /// </summary>
    public class ServingStation : MonoBehaviour
    {
        [Header("Ссылки (пусто — найдётся в сцене)")]
        [SerializeField] private GuestSpawner spawner;
        [SerializeField] private Checkholder checkholder;

        [Header("Экономика (черновые значения)")]
        [Tooltip("Доход за напиток при 5★. Меньше звёзд — пропорционально меньше.")]
        [SerializeField] private int basePrice = 100;
        [Tooltip("Бонус-чаевые за попадание в скрытое предпочтение гостя.")]
        [SerializeField] private int preferenceTip = 20;
        [Tooltip("Бонус-чаевые за аккуратный латте-арт ценителю.")]
        [SerializeField] private int latteArtTip = 15;
        [Tooltip("Минимум звёзд, при котором гость вообще оставляет чаевые.")]
        [SerializeField, Range(1, 5)] private int tipMinStars = 4;

        /// <summary>Напиток подан: (гость, оценка, реакция, заработано всего монет с чаевыми).</summary>
        public event Action<GuestController, DrinkRating, GuestReaction, int> OnDrinkServed;

        private void Start()
        {
            if (spawner == null) spawner = FindObjectOfType<GuestSpawner>();
            if (checkholder == null) checkholder = FindObjectOfType<Checkholder>();
        }

        /// <summary>Подать напиток из текущей сессии готовки (BrewSession) и начать следующий.</summary>
        [ContextMenu("Serve from BrewSession")]
        public bool ServeFromSession()
        {
            if (BrewSession.Instance == null)
            {
                Debug.LogWarning("[ServingStation] Нет BrewSession в сцене.", this);
                return false;
            }
            return ServeDrink(BrewSession.Instance.TakeResult());
        }

        /// <summary>Совместимость: подать по одному категориальному составу (без данных о качестве).</summary>
        public bool ServeDrink(DrinkSpec made) => ServeDrink(BrewResult.FromSpec(made));

        /// <summary>
        /// Подать собранный напиток текущему гостю. Возвращает false, если подавать некому
        /// (нет гостя у стойки) или нет его заказа.
        /// </summary>
        public bool ServeDrink(BrewResult made)
        {
            GuestController guest = spawner != null ? spawner.ActiveGuest : null;
            if (guest == null || guest.CurrentState != GuestController.State.AtCounter)
            {
                Debug.LogWarning("[ServingStation] Подавать некому — у стойки нет готового гостя.", this);
                return false;
            }

            DrinkSpec order = ResolveOrder(guest);
            if (order == null)
            {
                Debug.LogWarning("[ServingStation] Не найден заказ гостя на чекхолдере.", this);
                return false;
            }

            DrinkRating rating = DrinkScorer.Score(order, made);
            GuestReaction reaction = rating.ToReaction();

            int pay = Mathf.RoundToInt(basePrice * rating.stars / (float)DrinkRating.MaxStars);
            int tips = TipsFor(guest, made, rating);
            int total = pay + tips;
            int rep = ReputationFor(rating.stars);

            // Завершаем визит: гость забирает напиток и уходит (чекхолдер снимет тикет по OnLeft).
            guest.CompleteVisit(rating);

            if (CafeLedger.Instance != null) CafeLedger.Instance.Add(total, rep);

            OnDrinkServed?.Invoke(guest, rating, reaction, total);
            string tipStr = tips > 0 ? $" (+{tips} чаевых)" : string.Empty;
            Debug.Log($"[ServingStation] {guest.Visit.DisplayName}: {rating} → {reaction}. +{total} монет{tipStr}.", this);
            return true;
        }

        /// <summary>Бонус-чаевые за скрытое предпочтение и латте-арт (§4.3). Только при достаточной оценке.</summary>
        private int TipsFor(GuestController guest, BrewResult made, DrinkRating rating)
        {
            if (rating.stars < tipMinStars) return 0;

            GuestSO g = guest.Visit != null ? guest.Visit.guest : null;
            if (g == null || made?.spec == null) return 0;

            int tips = 0;

            if (!string.IsNullOrEmpty(g.hiddenPreferenceSpice)
                && made.spec.spices != null
                && made.spec.spices.Contains(g.hiddenPreferenceSpice))
                tips += preferenceTip;

            if (g.appreciatesLatteArt && made.spec.withMilk && made.latteArtQuality >= 0.5f)
                tips += latteArtTip;

            return tips;
        }

        private DrinkSpec ResolveOrder(GuestController guest)
        {
            if (checkholder != null)
            {
                var ticket = checkholder.GetTicketFor(guest);
                if (ticket != null && ticket.Order != null) return ticket.Order;
            }
            // Фолбэк: заказ прямо из визита, если чекхолдера нет в сцене.
            return guest.Visit != null ? guest.Visit.Order : null;
        }

        private static int ReputationFor(int stars)
        {
            if (stars >= 5) return 2;
            if (stars == 4) return 1;
            if (stars >= 2) return 0;   // 2-3 звезды
            return -1;                   // 1★ — минус к репутации (§4.3)
        }

        // --- Отладка без готовки: подать идеальный/намеренно неверный напиток текущему гостю ---

        [ContextMenu("DEBUG: Serve PERFECT drink")]
        private void DebugServePerfect()
        {
            var guest = spawner != null ? spawner.ActiveGuest : null;
            var order = guest != null ? ResolveOrder(guest) : null;
            if (order != null) ServeDrink(order.Clone()); // копия заказа = идеал
        }

        [ContextMenu("DEBUG: Serve WRONG drink")]
        private void DebugServeWrong()
        {
            // Заведомо «не то»: пережаренный крупный помол на горячей воде, без добавок.
            ServeDrink(new DrinkSpec
            {
                drinkName = "Брак",
                roast = RoastLevel.Burnt,
                grind = GrindSize.Coarse,
                water = WaterType.Hot,
                withMilk = false,
                sugarCubes = 0,
                syrup = SyrupType.None
            });
        }
    }
}
