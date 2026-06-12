using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TimelessBrew
{
    /// <summary>Что за «текучее/сыпучее» переливается между сосудами.</summary>
    public enum Ingredient { None, Beans, Grounds, Water, Coffee, Milk }

    /// <summary>Степень обжарки зерна (§4.2, этап 1).</summary>
    public enum RoastLevel { Raw, Light, Medium, Dark, Burnt }

    /// <summary>Степень помола (§4.2, этап 3).</summary>
    public enum GrindSize { Whole, Coarse, Medium, Fine, Powder }

    /// <summary>Тип воды (§4.2, §6.7).</summary>
    public enum WaterType { None, Cold, Hot }

    /// <summary>
    /// Содержимое сосуда — физическая смесь, накапливаемая по ходу готовки (§4.2). Хранит и
    /// «объёмы» (0..1), и профиль (обжарка/помол/тип воды), и добавки. Переливание = перенос
    /// содержимого из одной смеси в другую. Итоговая чашка = эта смесь, по ней считается оценка.
    /// </summary>
    [System.Serializable]
    public class Mixture
    {
        [Header("Сыпучее / жидкости (0..1)")]
        public float beans;             // целые зёрна (в банке/сковороде/кофемолке)
        public float grounds;           // молотый кофе
        public float water;             // вода
        public float coffee;            // сваренный кофе (жидкость)
        public float milk;              // молоко
        public float foam;              // пенка при варке 0..1
        public float temperature;       // нагрев 0..1

        [Header("Профиль")]
        public RoastLevel roast = RoastLevel.Raw;   // обжарка зёрен/молотого
        public GrindSize grind = GrindSize.Whole;   // помол
        public WaterType waterType = WaterType.None;

        [Header("Добавки")]
        public int sugar;
        public List<string> syrups = new();
        public List<string> spices = new();

        public bool IsEmpty =>
            beans <= 0.001f && grounds <= 0.001f && water <= 0.001f && coffee <= 0.001f &&
            milk <= 0.001f && sugar == 0 && syrups.Count == 0 && spices.Count == 0;

        public void Clear()
        {
            beans = grounds = water = coffee = milk = foam = temperature = 0f;
            roast = RoastLevel.Raw; grind = GrindSize.Whole; waterType = WaterType.None;
            sugar = 0; syrups.Clear(); spices.Clear();
        }

        /// <summary>Многострочная сводка состава для UI-панели сосуда.</summary>
        public string Summary()
        {
            var sb = new StringBuilder();
            if (beans > 0.001f) sb.AppendLine($"Зёрна ({RoastRu(roast)})");
            if (grounds > 0.001f) sb.AppendLine($"Молотый ({GrindRu(grind)}, {RoastRu(roast)})");
            if (water > 0.001f) sb.AppendLine($"Вода ({(waterType == WaterType.Hot ? "горячая" : "холодная")})");
            if (coffee > 0.001f) sb.AppendLine($"Кофе {(coffee * 100f):0}%");
            if (milk > 0.001f) sb.AppendLine($"Молоко {(milk * 100f):0}%");
            // Пенку здесь НЕ выводим — она показывается в статусе варки (Vessel.StatusLine), иначе дублируется.
            if (sugar > 0) sb.AppendLine($"Сахар ×{sugar}");
            if (syrups.Count > 0) sb.AppendLine("Сироп: " + JoinRu(syrups));
            if (spices.Count > 0) sb.AppendLine("Специи: " + JoinRu(spices));
            if (sb.Length == 0) sb.Append("пусто");
            return sb.ToString().TrimEnd();
        }

        // --- читаемые названия ---
        public static string RoastRu(RoastLevel r) => r switch
        {
            RoastLevel.Raw => "сырая", RoastLevel.Light => "светлая", RoastLevel.Medium => "средняя",
            RoastLevel.Dark => "тёмная", _ => "пережар"
        };
        public static string GrindRu(GrindSize g) => g switch
        {
            GrindSize.Whole => "целые", GrindSize.Coarse => "крупный", GrindSize.Medium => "средний",
            GrindSize.Fine => "мелкий", _ => "в пыль"
        };

        private static readonly Dictionary<string, string> Names = new()
        {
            { "caramel", "карамель" }, { "vanilla", "ваниль" }, { "cardamom", "кардамон" },
            { "cinnamon", "корица" }, { "star_anise", "бадьян" }, { "saffron", "шафран" },
            { "nutmeg", "мускат" }, { "rosemary", "розмарин" }, { "lavender", "лаванда" },
            { "cocoa", "какао" }, { "orange_zest", "цедра апельсина" }, { "lemon_zest", "цедра лимона" },
        };
        public static string NameRu(string id) => Names.TryGetValue(id, out var ru) ? ru : id;
        private static string JoinRu(List<string> ids)
        {
            var parts = new List<string>(ids.Count);
            foreach (var id in ids) parts.Add(NameRu(id));
            return string.Join(", ", parts);
        }
    }
}
