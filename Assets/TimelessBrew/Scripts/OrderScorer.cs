using System.Collections.Generic;
using UnityEngine;

namespace TimelessBrew
{
    /// <summary>Сравнивает приготовленный напиток (Mixture в чашке) с заказом гостя (§4.3): звёзды 1..5.</summary>
    public static class OrderScorer
    {
        /// <summary>
        /// cupRound — чашка пузатая (полукруглая). Правило подачи (§6.14): молоко/сиропы — в пузатую,
        /// «сухой» кофе (только кофе + специи) — в обычную. Не та чашка — минус звезда.
        /// </summary>
        public static int Score(Mixture m, OrderSpec o, bool cupRound, bool likesLatteArt, out string reaction, out string faults)
        {
            var f = new List<string>();
            if (m == null || o == null) { reaction = "—"; faults = "нет данных"; return 1; }
            if (m.coffee <= 0.05f) { reaction = "разочарован"; faults = "нет кофе"; return 1; }

            int stars = 5;
            if ((m.milk > 0.05f) != o.milk) { stars--; f.Add("молоко"); }
            if (m.sugar != o.sugar) { stars--; f.Add("сахар"); }
            if (SetMismatch(m.syrups, o.syrups) > 0) { stars--; f.Add("сироп"); }
            if (SetMismatch(m.spices, o.spices) > 0) { stars--; f.Add("специи"); }
            if (m.roast != o.roast) { stars--; f.Add("обжарка"); }
            if (m.grind != o.grind) { stars--; f.Add("помол"); }
            if (m.waterType != o.water) { stars--; f.Add("вода"); }
            if (!m.sifted) { stars--; f.Add("шелуха"); }   // пропущен этап просеивания (§4.3)

            // Правило чашки: молоко или сиропы → пузатая; иначе → обычная.
            bool needsRound = m.milk > 0.05f || (m.syrups != null && m.syrups.Count > 0);
            if (needsRound != cupRound) { stars--; f.Add("не та чашка"); }

            stars = Mathf.Clamp(stars, 1, 5);
            reaction = Reaction(stars);
            faults = f.Count > 0 ? string.Join(", ", f) : "идеально";
            // Латте-арт — бонус-приписка ценителю (§4.3, флаг с гостя), на звёзды не влияет.
            if (likesLatteArt && m.latteArt) faults += "  ·  ★ латте-арт";
            return stars;
        }

        private static string Reaction(int stars) => stars switch
        {
            5 => "восторг", 4 => "доволен", 3 => "нормально", 2 => "недоволен", _ => "разочарован"
        };

        private static int SetMismatch(List<string> a, List<string> b)
        {
            a ??= new List<string>(); b ??= new List<string>();
            int n = 0;
            foreach (var x in a) if (!b.Contains(x)) n++;
            foreach (var y in b) if (!a.Contains(y)) n++;
            return n;
        }
    }
}
