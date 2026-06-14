using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TimelessBrew
{
    /// <summary>Заказ гостя на одну фазу дня (§1.6). Структурно — для будущей оценки, плюс текст для книги.</summary>
    [System.Serializable]
    public class OrderSpec
    {
        public string strengthNote = "средний";   // крепкий/средний/лёгкий
        public RoastLevel roast = RoastLevel.Dark;
        public GrindSize grind = GrindSize.Powder;
        public WaterType water = WaterType.Hot;
        public bool milk;
        public int sugar;
        public List<string> syrups = new();
        public List<string> spices = new();

        public string Summary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Крепость: {strengthNote}");
            sb.AppendLine($"Обжарка: {Mixture.RoastRu(roast)}, помол: {Mixture.GrindRu(grind)}");
            sb.AppendLine($"Вода: {(water == WaterType.Hot ? "горячая" : "холодная")}");
            if (milk) sb.AppendLine("Молоко: да (латте-арт)");
            if (sugar > 0) sb.AppendLine($"Сахар: ×{sugar}");
            if (syrups.Count > 0) sb.AppendLine("Сироп: " + Join(syrups));
            if (spices.Count > 0) sb.AppendLine("Специи: " + Join(spices));
            return sb.ToString().TrimEnd();
        }

        private static string Join(List<string> ids)
        {
            var p = new List<string>(ids.Count);
            foreach (var id in ids) p.Add(Mixture.NameRu(id));
            return string.Join(", ", p);
        }
    }

    /// <summary>Именованный гость прототипа (§1.6): утренний и вечерний заказы + скрытое предпочтение.</summary>
    [CreateAssetMenu(fileName = "Guest", menuName = "TimelessBrew/Guest")]
    public class GuestSO : ScriptableObject
    {
        public string guestId;
        public string displayName;
        [TextArea] public string about;
        public OrderSpec morning = new();
        public OrderSpec evening = new();
        [Tooltip("Скрытое предпочтение (специя) — бонус-чаевые (§4.3).")]
        public string secretSpice;
        public bool likesLatteArt;
        public bool takeawayOnly;
    }
}
