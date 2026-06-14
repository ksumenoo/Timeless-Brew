using System.Collections.Generic;
using UnityEngine;

namespace TimelessBrew.Data
{
    // Реакция гостя на напиток (§4.3): значение = число звёзд.
    public enum GuestReaction
    {
        Disappointed = 1, // разочарован (минус к репутации)
        Unhappy      = 2, // недоволен
        Okay         = 3, // нормально
        Pleased      = 4, // доволен
        Delighted    = 5  // восторг
    }

    // Оценка напитка (§4.3): звёзды 1..5. Старт 5★, за каждый промах минус звезда (грубый брак -2).
    // faults — список причин снятия звёзд, для лога и подсказок.
    [System.Serializable]
    public struct DrinkRating
    {
        [Range(1, 5)] public int stars;
        public List<string> faults;

        public const int MaxStars = 5;
        public const int MinStars = 1;

        public GuestReaction ToReaction()
        {
            if (stars >= 5) return GuestReaction.Delighted;
            if (stars == 4) return GuestReaction.Pleased;
            if (stars == 3) return GuestReaction.Okay;
            if (stars == 2) return GuestReaction.Unhappy;
            return GuestReaction.Disappointed;
        }

        // Идеальный напиток — 5★ без замечаний.
        public static DrinkRating Perfect
        {
            get
            {
                DrinkRating r = new DrinkRating();
                r.stars = MaxStars;
                r.faults = new List<string>();
                return r;
            }
        }

        public override string ToString()
        {
            string s = "";
            for (int i = 0; i < MaxStars; i++)
                s += (i < stars) ? "★" : "☆";

            if (faults != null && faults.Count > 0)
                s += " (" + string.Join("; ", faults) + ")";
            return s;
        }
    }
}
