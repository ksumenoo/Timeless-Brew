using System.Collections.Generic;
using UnityEngine;

namespace TimelessBrew.Data
{
    // Оценка напитка (§4.3 v4.3) — звёзды с вычетами. Старт 5★, за промах минус звезда
    // (грубый брак варки — две). Ниже 1★ не опускается.
    // Качество этапа = -1 (этап не делали) штрафа не даёт.
    public static class DrinkScorer
    {
        private const float QualityFailBelow = 0.5f;   // ниже — промах по качеству
        private const float BrewGrossBelow = 0.2f;     // ниже — кофе убежал (-2)

        // Совместимость: оценка по одному составу (без данных о качестве готовки).
        public static DrinkRating Score(DrinkSpec order, DrinkSpec made)
        {
            return Score(order, BrewResult.FromSpec(made));
        }

        public static DrinkRating Score(DrinkSpec order, BrewResult made)
        {
            List<string> faults = new List<string>();

            DrinkRating result = new DrinkRating();
            result.faults = faults;

            if (order == null || made == null || made.spec == null)
            {
                result.stars = DrinkRating.MinStars;
                return result;
            }

            DrinkSpec m = made.spec;
            int deduct = 0;

            // Компоненты заказа: перепутал/забыл — минус звезда за каждый.
            if (m.grind != order.grind) { deduct += 1; faults.Add("крепость/помол"); }
            if (m.water != order.water) { deduct += 1; faults.Add("тип воды"); }
            if (m.withMilk != order.withMilk) { deduct += 1; faults.Add("молоко"); }
            if (m.sugarCubes != order.sugarCubes) { deduct += 1; faults.Add("сахар"); }
            if (m.syrup != order.syrup) { deduct += 1; faults.Add("сироп"); }
            if (SpiceMismatch(order.spices, m.spices) > 0) { deduct += 1; faults.Add("специи"); }

            // Обжарка: цвет вне заказа, пережар или низкое качество прожарки.
            bool roastWrongColor = m.roast != order.roast || m.roast == RoastLevel.Burnt;
            bool roastPoorQuality = made.roastQuality >= 0f && made.roastQuality < QualityFailBelow;
            if (roastWrongColor || roastPoorQuality) { deduct += 1; faults.Add("обжарка"); }

            // Непросеянная шелуха.
            if (made.siftQuality >= 0f && made.siftQuality < QualityFailBelow)
            {
                deduct += 1;
                faults.Add("шелуха");
            }

            // Варка: грубый брак — две звезды; просто плохо — одна.
            if (made.brewQuality >= 0f)
            {
                if (made.brewQuality < BrewGrossBelow) { deduct += 2; faults.Add("кофе убежал"); }
                else if (made.brewQuality < QualityFailBelow) { deduct += 1; faults.Add("варка"); }
            }

            // Латте-арт: только если заказан напиток с молоком и молоко добавлено.
            if (order.withMilk && m.withMilk && made.latteArtQuality >= 0f && made.latteArtQuality < QualityFailBelow)
            {
                deduct += 1;
                faults.Add("латте-арт");
            }

            int stars = DrinkRating.MaxStars - deduct;
            if (stars < DrinkRating.MinStars) stars = DrinkRating.MinStars;
            if (stars > DrinkRating.MaxStars) stars = DrinkRating.MaxStars;

            result.stars = stars;
            return result;
        }

        // Сколько специй не совпало (не положили нужное + положили лишнее).
        private static int SpiceMismatch(List<string> orderSpices, List<string> madeSpices)
        {
            List<string> a = orderSpices != null ? orderSpices : new List<string>();
            List<string> b = madeSpices != null ? madeSpices : new List<string>();

            int mismatch = 0;
            foreach (string x in a)
                if (!b.Contains(x)) mismatch++;
            foreach (string y in b)
                if (!a.Contains(y)) mismatch++;
            return mismatch;
        }
    }
}
