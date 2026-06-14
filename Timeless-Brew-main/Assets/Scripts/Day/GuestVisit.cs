using System;
using TimelessBrew.Data;

namespace TimelessBrew.Day
{
    /// <summary>
    /// Один визит гостя в конкретную фазу дня (§4.4). Это «единица работы» для всего
    /// дневного цикла: спавнер выпускает гостя по визиту, чекхолдер вешает тикет
    /// по его заказу, оценка сравнивает поданный напиток с <see cref="Order"/>.
    ///
    /// Один и тот же GuestSO может породить два визита за день (утро и вечер) —
    /// это будут два разных экземпляра GuestVisit с разными фазами и заказами.
    /// </summary>
    [Serializable]
    public class GuestVisit
    {
        public GuestSO guest;
        public DayPhase phase;

        public GuestVisit(GuestSO guest, DayPhase phase)
        {
            this.guest = guest;
            this.phase = phase;
        }

        /// <summary>Эталонный заказ этого визита (берётся из гостя по фазе).</summary>
        public DrinkSpec Order => guest != null ? guest.OrderFor(phase) : null;

        public string DisplayName => guest != null ? guest.displayName : "—";
        public bool TakeawayOnly => guest != null && guest.takeawayOnly;
    }
}
