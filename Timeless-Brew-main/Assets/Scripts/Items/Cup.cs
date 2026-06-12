using UnityEngine;
using TimelessBrew.Core;
using TimelessBrew.Data;
namespace TimelessBrew.Items
{
    /// <summary>
    /// Чашка (§6.14). Два этапа:
    ///   • Этап 4 «Разлив»: берётся пустой, наводишь готовую турку и держишь ЛКМ — кофе наполняет чашку.
    ///   • Этап 5 «Латте-арт»: в чашку с кофе льёшь молоко из молочника, водя курсором рисуешь узор.
    ///     Качество латте-арта пишется в BrewSession (SetMilk).
    /// Оба налива идут штатным конвейером (источник canPour + чашка canReceive).
    /// </summary>
    public class Cup : InteractableItem
    {
        [Header("Состояния")]
        [SerializeField] private ItemStateSO stateEmpty;   // пустая (принимает кофе)
        [SerializeField] private ItemStateSO stateCoffee;  // с кофе (принимает молоко/добавки)
        [SerializeField] private ItemStateSO stateMilk;    // молоко налито (латте готов)

        [Header("Разлив кофе (Этап 4)")]
        [SerializeField] private float coffeeFillRate = 1.2f;  // доля/сек при удержании ЛКМ

        [Header("Латте-арт (Этап 5)")]
        [SerializeField] private float minPourTime = 0.4f;
        [SerializeField] private float maxPourTime = 2.5f;
        [SerializeField] private float travelForFull = 600f;

        public bool HasCoffee { get; private set; }
        public float CoffeeFill { get; private set; }
        public bool HasMilk { get; private set; }
        public bool IsPouring { get; private set; }
        public float LatteArtQuality { get; private set; } = -1f;

        private bool _milkStarted;
        private float _pourTime;
        private float _travel;
        private Vector2 _lastPointer;
        private float _lastTickTime;

        private void Start()
        {
            if (stateEmpty != null) SetState(stateEmpty);
        }

        public override bool TryReceive(InteractableItem source)
        {
            if (source == null) return false;

            // Этап 4: налив кофе из готовой турки.
            Cezve cezve = source as Cezve;
            if (cezve != null)
            {
                if (HasCoffee || !cezve.Ready) return false;
                if (!base.TryReceive(source)) return false; // CupEmpty.canReceive + турка canPour

                CoffeeFill += coffeeFillRate * Time.deltaTime;
                if (CoffeeFill >= 1f)
                {
                    CoffeeFill = 1f;
                    HasCoffee = true;
                    if (stateCoffee != null) SetState(stateCoffee);
                    Debug.Log("[Cup] Чашка наполнена кофе (Этап 4).", this);
                }
                return true;
            }

            // Этап 5: молоко из молочника (только после кофе).
            if (source.itemType == "Pitcher")
            {
                if (!HasCoffee || HasMilk) return false;
                if (!base.TryReceive(source)) return false;

                Vector2 p = GameInput.Instance != null ? GameInput.Instance.PointerScreenPosition : Vector2.zero;
                if (!_milkStarted)
                {
                    _milkStarted = true;
                    _pourTime = 0f;
                    _travel = 0f;
                    _lastPointer = p;
                    IsPouring = true;
                }
                _pourTime += Time.deltaTime;
                _travel += Vector2.Distance(p, _lastPointer);
                _lastPointer = p;
                _lastTickTime = Time.time;
                return true;
            }

            return false;
        }

        private void Update()
        {
            // Струя молока прервалась (отпустили ЛКМ) — считаем латте-арт.
            if (_milkStarted && IsPouring && Time.time - _lastTickTime > 0.12f)
                FinishLatte();
        }

        private void FinishLatte()
        {
            IsPouring = false;

            // Достаточно ли молока (по времени струи).
            float timeFactor;
            if (_pourTime < minPourTime) timeFactor = _pourTime / minPourTime;
            else if (_pourTime <= maxPourTime) timeFactor = 1f;
            else timeFactor = 1f - (_pourTime - maxPourTime) / maxPourTime;
            if (timeFactor < 0f) timeFactor = 0f;
            if (timeFactor > 1f) timeFactor = 1f;

            // Насколько уверенно вёл рисунок (по движению курсора).
            float drawFactor = _travel / travelForFull;
            if (drawFactor > 1f) drawFactor = 1f;

            LatteArtQuality = 0.4f * timeFactor + 0.6f * drawFactor;

            HasMilk = true;
            if (BrewSession.Instance != null)
                BrewSession.Instance.SetMilk(true, LatteArtQuality);

            if (stateMilk != null) SetState(stateMilk);
            Debug.Log("[Cup] Латте-арт: " + LatteArtQuality.ToString("0.00"), this);
        }
    }
}
