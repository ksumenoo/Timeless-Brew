using System.Text;

namespace TimelessBrew.Data
{
    // Превращает заказ напитка (DrinkSpec) в читаемый текст для бумажки-заказа и логов.
    public static class DrinkSpecFormatter
    {
        public static string RoastRu(RoastLevel r)
        {
            switch (r)
            {
                case RoastLevel.Green: return "зелёная";
                case RoastLevel.Yellow: return "жёлтая";
                case RoastLevel.LightBrown: return "светлая";
                case RoastLevel.DeepBrown: return "тёмная";
                case RoastLevel.Burnt: return "пережар";
                default: return r.ToString();
            }
        }

        public static string GrindRu(GrindSize g)
        {
            switch (g)
            {
                case GrindSize.Coarse: return "крупный";
                case GrindSize.Medium: return "средний";
                case GrindSize.Fine: return "мелкий";
                case GrindSize.Powder: return "в пыль";
                default: return g.ToString();
            }
        }

        public static string WaterRu(WaterType w)
        {
            if (w == WaterType.Cold) return "холодная вода";
            return "горячая вода";
        }

        public static string SyrupRu(SyrupType s)
        {
            switch (s)
            {
                case SyrupType.Caramel: return "карамель";
                case SyrupType.Vanilla: return "ваниль";
                default: return null;   // None
            }
        }

        // Короткое имя напитка для заголовка бумажки.
        public static string ShortName(DrinkSpec spec)
        {
            if (spec != null && !string.IsNullOrEmpty(spec.drinkName)) return spec.drinkName;
            return "Кофе";
        }

        // Полный состав напитка для тела бумажки.
        public static string Describe(DrinkSpec spec)
        {
            if (spec == null) return "—";

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Обжарка: " + RoastRu(spec.roast));
            sb.AppendLine("Помол: " + GrindRu(spec.grind));
            sb.AppendLine("Варка: " + WaterRu(spec.water));

            if (spec.withMilk) sb.AppendLine("Молоко: да (латте-арт)");
            if (spec.sugarCubes > 0) sb.AppendLine("Сахар: " + spec.sugarCubes + " куб.");

            string syrup = SyrupRu(spec.syrup);
            if (!string.IsNullOrEmpty(syrup)) sb.AppendLine("Сироп: " + syrup);

            if (spec.spices != null && spec.spices.Count > 0)
                sb.AppendLine("Специи: " + string.Join(", ", spec.spices));

            return sb.ToString().TrimEnd();
        }
    }
}
