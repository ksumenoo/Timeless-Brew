using System.Collections.Generic;
using UnityEngine;

namespace TimelessBrew
{
    /// <summary>Сравнивает приготовленный напиток (Mixture в чашке) с заказом гостя (§4.3): звёзды 1..5.</summary>
    public static class OrderScorer
    {
        public static int Score(Mixture m, OrderSpec o, out string reaction, out string faults)
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

            stars = Mathf.Clamp(stars, 1, 5);
            reaction = Reaction(stars);
            faults = f.Count > 0 ? string.Join(", ", f) : "идеально";
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
