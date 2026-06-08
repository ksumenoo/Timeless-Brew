using System;
using System.Collections.Generic;
using UnityEngine;

namespace TimelessBrew.Data
{
    /// <summary>Степень обжарки зерна — этапы изменения цвета из §4.2 (Этап 1).</summary>
    public enum RoastLevel
    {
        Green,        // сырое зелёное зерно
        Yellow,       // жёлтое
        LightBrown,   // светло-коричневое
        DeepBrown,    // насыщенно-коричневое (обычно эталон)
        Burnt         // чёрное — брак, пережарка
    }

    /// <summary>Степень помола из §4.2 (Этап 3).</summary>
    public enum GrindSize
    {
        Coarse,   // крупный
        Medium,   // средний
        Fine,     // мелкий
        Powder    // в пыль — для классической варки в турке
    }

    /// <summary>Тип воды для варки (§4.2, Этап 3). Влияет на скорость варки и вкус.</summary>
    public enum WaterType
    {
        Cold,   // холодная — медленная варка, лучше раскрывает вкус
        Hot     // горячая — быстрая варка, сильнее по бодрости, грубее по вкусу
    }

    /// <summary>Тип сиропа (§6.11). В прототипе — несколько сиропов разных цветов.</summary>
    public enum SyrupType
    {
        None,
        Caramel,
        Vanilla
    }

    /// <summary>
    /// Эталонный рецепт напитка — то, что гость хочет получить (§4.3: «идеальный рецепт
    /// зависит от гостя, берётся из его записанного заказа на чекхолдере»).
    ///
    /// Тот же тип используется и как описание ФАКТИЧЕСКИ приготовленного напитка
    /// (то, что Нари реально собрал) — для сравнения при оценке. Это упрощает
    /// сравнение «заказ vs результат» в шаге оценки (§4.3).
    ///
    /// Сериализуемый класс, а не ScriptableObject: заказы живут внутри GuestSO,
    /// и нет смысла плодить отдельные ассеты под каждый заказ.
    /// </summary>
    [Serializable]
    public class DrinkSpec
    {
        [Tooltip("Короткое читаемое имя напитка для тикета, напр. 'Крепкий чёрный', 'Латте'.")]
        public string drinkName = "Кофе";

        [Header("Обжарка / помол / варка")]
        public RoastLevel roast = RoastLevel.DeepBrown;
        public GrindSize grind = GrindSize.Powder;
        public WaterType water = WaterType.Hot;

        [Header("Подача / добавки")]
        [Tooltip("Нужно ли молоко (латте-арт, Этап 5). Если false — этап молока пропускается.")]
        public bool withMilk = false;

        [Tooltip("Сколько кубиков сахара (§6.9). 0 — без сахара.")]
        [Min(0)] public int sugarCubes = 0;

        [Tooltip("Тип сиропа (§6.11). None — без сиропа.")]
        public SyrupType syrup = SyrupType.None;

        [Tooltip("Идентификаторы специй из заказа, напр. 'cardamom', 'cocoa', 'orange_zest'. Пусто — без специй.")]
        public List<string> spices = new();

        /// <summary>Удобная копия — пригодится, когда будем собирать фактический напиток на основе шаблона.</summary>
        public DrinkSpec Clone()
        {
            return new DrinkSpec
            {
                drinkName = drinkName,
                roast = roast,
                grind = grind,
                water = water,
                withMilk = withMilk,
                sugarCubes = sugarCubes,
                syrup = syrup,
                spices = new List<string>(spices)
            };
        }
    }
}
