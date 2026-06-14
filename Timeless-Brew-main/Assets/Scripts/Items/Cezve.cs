using UnityEngine;
using TimelessBrew.Core;
using TimelessBrew.Data;

namespace TimelessBrew.Items
{
    // Турка + варка (§4.2 Этап 3b, §6.6).
    // 1. Длинной ложкой в турку кладут молотый кофе (§6.8).
    // 2. Добавляют воду: холодную — через кран раковины (Sink.AddColdWater); горячую — из чайника.
    // 3. Турку ставят на печку — поднимается пенка. Перелив (пенка >= 1) = брак «кофе убежал».
    // 4. Пустой ложкой можно усадить пенку. Снял турку с печки у верхней черты — варка готова.
    public class Cezve : InteractableItem
    {
        [Header("Ссылки")]
        [SerializeField] private CursorHandler cursorHandler;
        [SerializeField] private Stove stove;

        [Header("Состояния (§6.6)")]
        [SerializeField] private ItemStateSO stateEmpty;   // пустая (принимает кофе)
        [SerializeField] private ItemStateSO stateCoffee;  // с кофе (принимает воду)
        [SerializeField] private ItemStateSO stateWater;   // с водой (готова к варке)
        [SerializeField] private ItemStateSO stateReady;   // сварено (можно разливать)
        [SerializeField] private ItemStateSO stateRuined;  // перелито (брак)

        [Header("Варка")]
        [SerializeField] private float foamRate = 0.18f;            // скорость пенки на горячей печке
        [SerializeField] private float hotWaterMultiplier = 1.6f;   // горячая вода кипит быстрее
        [SerializeField, Range(0f, 1f)] private float readyMin = 0.7f;  // с этого уровня пенки — удачно

        [Header("Ложка")]
        [SerializeField] private float settleRate = 0.5f;   // скорость усадки пенки ложкой

        public bool HasCoffee { get; private set; }
        public bool HasWater { get; private set; }
        public WaterType Water { get; private set; }
        public float FoamLevel { get; private set; }
        public bool Brewing { get; private set; }
        public bool Ruined { get; private set; }
        public bool Ready { get; private set; }

        // Качество варки 0..1: перелив = 0; недоварено растёт до readyMin; в окне = 1.
        public float BrewQuality01
        {
            get
            {
                if (Ruined) return 0f;
                if (FoamLevel >= readyMin) return 1f;
                if (readyMin <= 0f) return 1f;
                float v = FoamLevel / readyMin;
                if (v > 1f) v = 1f;
                return v;
            }
        }

        private void Start()
        {
            if (cursorHandler == null) cursorHandler = FindObjectOfType<CursorHandler>();
            if (stove == null) stove = FindObjectOfType<Stove>();
            if (stateEmpty != null) SetState(stateEmpty);
        }

        public override bool TryReceive(InteractableItem source)
        {
            if (Ruined || Ready || source == null) return false;

            // Кофе — только длинной ложкой (§6.8). Заодно пишем фракцию помола.
            LongSpoon spoon = source as LongSpoon;
            if (spoon != null && spoon.HasGround)
            {
                if (HasCoffee) return false;
                if (!base.TryReceive(source)) return false;   // CezveEmpty.canReceive + ложка canPour
                HasCoffee = true;
                if (BrewSession.Instance != null)
                    BrewSession.Instance.RecordGrind(spoon.GroundGrind);
                spoon.ConsumeGround();
                if (stateCoffee != null) SetState(stateCoffee);
                return true;
            }

            // Горячая вода — из закипевшего чайника (§6.7). Холодная — через кран (AddColdWater).
            if (source.itemType == "Kettle")
            {
                if (!HasCoffee || HasWater) return false;
                if (!base.TryReceive(source)) return false;   // CezveCoffee.canReceive + чайник canPour
                HasWater = true;
                Water = WaterType.Hot;
                if (stateWater != null) SetState(stateWater);
                return true;
            }

            return false;
        }

        // Налить холодную воду из крана раковины (§6.7) — вызывает Sink. Нужны кофе и пустая по воде турка.
        public bool AddColdWater()
        {
            if (Ruined || Ready || !HasCoffee || HasWater) return false;
            HasWater = true;
            Water = WaterType.Cold;
            if (stateWater != null) SetState(stateWater);
            Debug.Log("[Cezve] Налита холодная вода из крана.", this);
            return true;
        }

        private void Update()
        {
            if (Ruined || Ready || !HasWater)
            {
                Brewing = false;
                return;
            }

            bool onBurner = stove != null
                            && cursorHandler != null
                            && cursorHandler.Carried != this
                            && stove.CanCook
                            && stove.IsOnBurner(transform);

            // Сняли турку с печки после начала варки → готово (Этап 3 -> 4).
            if (!Ready && FoamLevel > 0f && cursorHandler != null && cursorHandler.Carried == this)
            {
                FinishBrew();
                return;
            }

            Brewing = onBurner && stove.Temperature > 0f;

            if (Brewing)
            {
                float waterMul = (Water == WaterType.Hot) ? hotWaterMultiplier : 1f;
                FoamLevel += foamRate * stove.Temperature * waterMul * Time.deltaTime;

                if (FoamLevel >= 1f)
                {
                    FoamLevel = 1f;
                    Ruined = true;
                    Brewing = false;
                    if (stateRuined != null) SetState(stateRuined);
                    Debug.Log("[Cezve] Кофе убежал — перелив через край (брак).", this);
                    return;
                }
            }

            // Пустой ложкой усаживаем пенку (спасательная мера, §6.8).
            if (SpoonStirring())
            {
                FoamLevel -= settleRate * Time.deltaTime;
                if (FoamLevel < 0f) FoamLevel = 0f;
            }
        }

        private bool SpoonStirring()
        {
            InteractableItem c = cursorHandler != null ? cursorHandler.Carried : null;
            if (c == null || c.itemType != "Spoon") return false;

            LongSpoon ls = c as LongSpoon;
            if (ls != null && ls.HasGround) return false;   // полная ложка доставляет кофе, а не мешает

            return cursorHandler.HoverTarget == this
                   && GameInput.Instance != null
                   && GameInput.Instance.InteractHeld;
        }

        // Зафиксировать варку при снятии турки с печки (§4.2): записать воду и качество, сделать «готово».
        private void FinishBrew()
        {
            if (!HasWater || Ready) return;

            if (BrewSession.Instance != null)
                BrewSession.Instance.RecordBrew(Water, BrewQuality01);

            Debug.Log("[Cezve] Варка готова. Вода: " + Water + ", качество: " + BrewQuality01.ToString("0.00"), this);

            Ready = true;
            Brewing = false;
            if (Ruined)
            {
                if (stateRuined != null) SetState(stateRuined);
            }
            else if (stateReady != null) SetState(stateReady);
        }
    }
}
