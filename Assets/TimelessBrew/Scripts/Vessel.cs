using System.Collections.Generic;
using UnityEngine;
using TimelessBrew.Audio;

namespace TimelessBrew
{
    /// <summary>
    /// Сосуд — носитель содержимого (<see cref="Mixture"/>) и его «физика»: переливание и варка.
    /// Тепло сосуду РАЗДАЁТ печь (<see cref="Heat"/> выставляется снаружи в Update печи), а сам
    /// сосуд «доваривается» в LateUpdate — поэтому предметы не опрашивают печку (инверсия управления).
    /// </summary>
    public class Vessel : MonoBehaviour
    {
        // ВАЖНО: новые типы добавлять только В КОНЕЦ — enum сериализован числом в сцене.
        public enum Kind { Jar, Pan, Grinder, Kettle, Cezve, Pitcher, MilkJar, Cup, Colander }

        [Header("Тип")]
        public Kind kind = Kind.Cup;

        [Header("Содержимое")]
        public Mixture mix = new();

        [Header("Тюнинг")]
        // Готовка на плите вдвое неспешнее (по просьбе): ставки уполовинены, баланс пенка/варка сохранён.
        [SerializeField] private float roastRate = 0.07f;   // сковорода
        [SerializeField] private float boilRate = 0.18f;    // чайник
        [SerializeField] private float brewRate = 0.07f;    // скорость варки турки (прогресс) — неспешно
        [SerializeField] private float foamRate = 0.11f;    // пенка растёт медленнее варки; её сбивают ложкой
        [SerializeField] private float waterFillRate = 0.6f;   // продолжительный набор воды из крана (§6.7)
        [SerializeField, Range(0f, 1f)] private float readyBrewMin = 0.6f;   // с этого прогресса варка удачна

        /// <summary>Тепло от печи 0..1 в этот кадр (0 — не на конфорке). Ставит Stove.</summary>
        [System.NonSerialized] public float Heat;

        public bool Ruined { get; private set; }
        public bool CoffeeReady => kind == Kind.Cezve && mix.coffee > 0.001f && !Ruined;

        private float _roastProgress;
        private float _brewProgress;
        private bool _wasBrewing;
        private bool _wasHot;   // чайник уже свистнул на этой воде (свист — один раз при закипании)

        /// <summary>Прогресс обжарки 0..1 (для шкалы сковороды — цвет зерна).</summary>
        public float RoastProgress01 => Mathf.Clamp01(_roastProgress);
        /// <summary>Прогресс варки турки 0..1 (для шкалы).</summary>
        public float BrewProgress01 => Mathf.Clamp01(_brewProgress);

        // --- Реестр всех сосудов (печь раздаёт тепло по нему, без FindObjectsOfType) ---
        public static readonly List<Vessel> All = new();
        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        private void Start()
        {
            // Бесконечные источники. Чайник — НЕ источник: воду в него набирают у раковины (§6.7).
            if (kind == Kind.Jar) { mix.beans = 1f; mix.roast = RoastLevel.Raw; }
            else if (kind == Kind.MilkJar) { mix.milk = 1f; }
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            switch (kind)
            {
                case Kind.Pan: CookPan(dt); break;
                case Kind.Kettle: CookKettle(dt); break;
                case Kind.Cezve: CookCezve(dt); break;
            }
            Heat = 0f;   // печь заново выставит на следующем кадре, если сосуд на конфорке
        }

        private void CookPan(float dt)
        {
            if (mix.beans <= 0.001f || Heat <= 0f) return;
            AudioManager.Instance.Loop("roast" + GetInstanceID(), Sfx.RoastSizzle);   // шипение обжарки
            _roastProgress = Mathf.Min(1.2f, _roastProgress + roastRate * Heat * dt);
            mix.roast = RoastFromProgress(_roastProgress);
            if (mix.roast != RoastLevel.Raw) mix.sifted = false;   // при обжарке отделяется шелуха (§4.2, этап 2)
        }

        private void CookKettle(float dt)
        {
            if (Heat <= 0f || mix.water <= 0.001f) return;   // пустой чайник не «кипятится»
            mix.temperature = Mathf.Min(1f, mix.temperature + boilRate * Heat * dt);
            bool hot = mix.temperature >= 0.6f;
            if (hot) mix.waterType = WaterType.Hot;
            // Свист — РАЗОВО в момент закипания, а не с самого начала нагрева.
            if (hot && !_wasHot) AudioManager.Instance.Play(Sfx.KettleBoil);
            _wasHot = hot;
        }

        private void CookCezve(float dt)
        {
            bool canBrew = mix.grounds > 0.001f && mix.water > 0.001f && !Ruined && mix.coffee <= 0.001f;
            bool brewing = canBrew && Heat > 0f;

            if (brewing)
            {
                AudioManager.Instance.Loop("brew" + GetInstanceID(), Sfx.CezveBrew);   // бурление кофе в турке
                _brewProgress = Mathf.Min(1f, _brewProgress + brewRate * Heat * dt);   // варка — НЕ сбивается ложкой
                mix.temperature = Mathf.Min(1f, mix.temperature + 0.3f * Heat * dt);
                mix.foam += foamRate * Heat * dt;
                // Перелив пенки = брак. На время обучения брак отключён, чтобы новичок не застрял.
                if (mix.foam >= 1f) { mix.foam = 1f; if (!UI.TutorialController.Active) Ruined = true; }
            }

            // Сняли с печки: готовность по ПРОГРЕССУ варки (не по пенке), если не убежал.
            if (_wasBrewing && !brewing && !Ruined && _brewProgress >= readyBrewMin)
                mix.coffee = 1f;   // профиль (помол/обжарка/вода) уже лежит в mix
            _wasBrewing = brewing;
        }

        private static RoastLevel RoastFromProgress(float p) =>
            p >= 0.95f ? RoastLevel.Burnt :
            p >= 0.7f ? RoastLevel.Dark :
            p >= 0.45f ? RoastLevel.Medium :
            p >= 0.2f ? RoastLevel.Light : RoastLevel.Raw;

        // ===================== ПЕРЕЛИВАНИЕ =====================

        /// <summary>Что этот сосуд может отдать при наклоне (наведение + удержание).</summary>
        public Ingredient PourType => kind switch
        {
            Kind.Jar => Ingredient.Beans,
            Kind.Pan => mix.beans > 0.001f ? Ingredient.Beans : Ingredient.None,
            Kind.Colander => mix.beans > 0.001f ? Ingredient.Beans : Ingredient.None,
            Kind.Kettle => mix.water > 0.001f ? Ingredient.Water : Ingredient.None,
            Kind.Cezve => CoffeeReady ? Ingredient.Coffee : Ingredient.None,
            Kind.Pitcher => mix.milk > 0.001f ? Ingredient.Milk : Ingredient.None,
            Kind.MilkJar => Ingredient.Milk,
            _ => Ingredient.None
        };

        /// <summary>Может ли принять данный ингредиент сейчас.</summary>
        public bool CanAccept(Ingredient ing) => kind switch
        {
            Kind.Pan => ing == Ingredient.Beans && mix.beans <= 0.001f,
            Kind.Grinder => ing == Ingredient.Beans && mix.beans <= 0.001f,
            Kind.Colander => ing == Ingredient.Beans && mix.beans <= 0.001f,
            Kind.Kettle => ing == Ingredient.Water && mix.water <= 0.001f,   // только из-под крана раковины
            Kind.Cezve => (ing == Ingredient.Grounds && mix.grounds <= 0.001f && mix.coffee <= 0.001f && !Ruined)
                          || (ing == Ingredient.Water && mix.water < 0.999f && mix.grounds > 0.001f && !Ruined),   // долив до полного
            Kind.Cup => (ing == Ingredient.Coffee && mix.coffee < 0.999f)
                        || (ing == Ingredient.Milk && mix.coffee > 0.001f && mix.milk < 0.999f),
            Kind.Pitcher => ing == Ingredient.Milk && mix.milk < 0.999f,
            _ => false
        };

        /// <summary>Перелить из этого сосуда в цель (вызывает рука, пока держим наклон над целью).</summary>
        public void PourInto(Vessel target, float dt)
        {
            Ingredient ing = PourType;
            if (ing == Ingredient.None || target == null || !target.CanAccept(ing)) return;

            float rate = 0.8f * dt;
            switch (ing)
            {
                case Ingredient.Beans:
                    target.mix.beans = 1f;
                    target.mix.roast = mix.roast;
                    target.mix.sifted = mix.sifted;   // шелуха «едет» вместе с зёрнами
                    if (kind == Kind.Pan || kind == Kind.Colander) { mix.beans = 0f; _roastProgress = 0f; }   // банка — бесконечна
                    break;

                case Ingredient.Water:
                    // Продолжительный налив (а не мгновенно за кадр): уровень в турке растёт постепенно.
                    target.mix.water = Mathf.Min(1f, target.mix.water + rate);
                    target.mix.waterType = mix.waterType;
                    if (kind == Kind.Kettle) mix.water = Mathf.Max(0f, mix.water - rate * 0.34f);   // одной заправки хватает на ~3 варки
                    break;

                case Ingredient.Coffee:
                    target.mix.coffee = Mathf.Min(1f, target.mix.coffee + rate);
                    target.mix.grind = mix.grind;
                    target.mix.roast = mix.roast;
                    target.mix.waterType = mix.waterType;
                    target.mix.sifted = mix.sifted;
                    mix.coffee = Mathf.Max(0f, mix.coffee - rate);
                    if (mix.coffee <= 0.001f) ResetBrew();   // турку вылили — она пуста и готова к новой варке
                    break;

                case Ingredient.Milk:
                    target.mix.milk = Mathf.Min(1f, target.mix.milk + rate);
                    if (kind == Kind.Pitcher) mix.milk = Mathf.Max(0f, mix.milk - rate);
                    break;
            }
        }

        /// <summary>Усадить пенку ложкой (§6.8) — пока держим ложку над туркой при варке.</summary>
        public void SettleFoam(float dt)
        {
            if (kind != Kind.Cezve || Ruined || CoffeeReady) return;
            mix.foam = Mathf.Max(0f, mix.foam - 0.5f * dt);
        }

        /// <summary>Высыпать молотый в сосуд (рука после кофемолки).</summary>
        public bool AddGrounds(GrindSize grind, RoastLevel roast, bool sifted)
        {
            if (!CanAccept(Ingredient.Grounds)) return false;
            mix.grounds = 1f;
            mix.grind = grind;
            mix.roast = roast;
            mix.sifted = sifted;
            return true;
        }

        /// <summary>Можно ли доливать воду из крана прямо сейчас (для продолжительного набора, §6.7).</summary>
        public bool CanFillWater =>
            mix.coffee <= 0.001f && !Ruined && mix.water < 0.999f &&
            (kind == Kind.Kettle || (kind == Kind.Cezve && mix.grounds > 0.001f));

        /// <summary>Долить воды за этот кадр — продолжительный набор удержанием у раковины.</summary>
        public void FillWaterGradual(WaterType type, float dt)
        {
            if (!CanFillWater) return;
            mix.water = Mathf.Min(1f, mix.water + waterFillRate * dt);
            mix.waterType = type;
            mix.temperature = 0f;
            _wasHot = false;
        }

        /// <summary>Турку вылили: гуща/вода/пенка/прогресс обнуляются (содержимое ушло в чашку).</summary>
        private void ResetBrew()
        {
            mix.grounds = 0f; mix.water = 0f; mix.coffee = 0f; mix.foam = 0f; mix.temperature = 0f;
            mix.waterType = WaterType.None;
            _brewProgress = 0f;
            Ruined = false;
            _wasBrewing = false;
        }

        /// <summary>Сброс содержимого (мусорка). Чайник опустошается насовсем — за водой к раковине.</summary>
        public void Empty()
        {
            mix.Clear();
            Ruined = false;
            _roastProgress = 0f;
            _brewProgress = 0f;
            _wasBrewing = false;
            _wasHot = false;
            if (kind == Kind.Jar) mix.beans = 1f;
            else if (kind == Kind.MilkJar) mix.milk = 1f;
        }

        /// <summary>Короткий статус готовки для UI-панели сосуда.</summary>
        public string StatusLine()
        {
            switch (kind)
            {
                case Kind.Pan:
                    if (mix.beans > 0.001f) return $"Обжарка: {Mixture.RoastRu(mix.roast)}";
                    break;
                case Kind.Kettle:
                    if (mix.water <= 0.001f) return "пусто — набери воды у раковины";
                    if (mix.waterType == WaterType.Hot) return "Кипяток готов";
                    return mix.temperature > 0.01f ? $"Греется {(mix.temperature * 100f):0}%" : "Холодная вода";
                case Kind.Cezve:
                    if (Ruined) return "<color=#ff5555>УБЕЖАЛ (брак)</color>";
                    if (CoffeeReady) return "<color=#88ff88>Кофе готов</color>";
                    if (mix.grounds > 0.001f && mix.water > 0.001f)
                        return $"Варка {(_brewProgress * 100f):0}%  ·  пенка {(mix.foam * 100f):0}%";
                    break;
            }
            return null;
        }
    }
}
