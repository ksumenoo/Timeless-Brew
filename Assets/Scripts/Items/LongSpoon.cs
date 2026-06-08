using UnityEngine;
using TimelessBrew.Core;
using TimelessBrew.Data;

namespace TimelessBrew.Items
{
    /// <summary>
    /// Длинная мешальная ложка (§6.8) — двойная роль:
    ///   1. Забирает готовый молотый кофе из ящичка кофемолки и переносит в турку.
    ///   2. Пустой ложкой выполняется помешивание/усадка пенки в турке (логика — в <see cref="Cezve"/>).
    ///
    /// Зачерпывание: ложка на курсоре, наведена на кофемолку с молотым, нажатие ЛКМ — ложка
    /// зачерпывает фракцию (TryTakeGround) и переходит в состояние «с молотым» (canPour).
    /// Доставка в турку идёт штатным конвейером (ложка canPour + турка canReceive).
    ///
    /// itemType = "Spoon". Состояние различает пустую (помешивание) и полную (доставка) ложку.
    /// </summary>
    public class LongSpoon : InteractableItem
    {
        [Header("Ссылки")]
        [SerializeField] private CursorHandler cursorHandler;

        [Header("Состояния")]
        [SerializeField] private ItemStateSO stateEmpty;   // пустая (помешивание)
        [SerializeField] private ItemStateSO stateGround;  // с молотым (доставка в турку)

        public bool HasGround { get; private set; }
        public GrindSize GroundGrind { get; private set; }

        private bool _heldPrev;

        private void Start()
        {
            if (cursorHandler == null) cursorHandler = FindObjectOfType<CursorHandler>();
            if (stateEmpty != null) SetState(stateEmpty);
        }

        private void Update()
        {
            bool held = GameInput.Instance != null && GameInput.Instance.InteractHeld;

            // Фронт нажатия ЛКМ: пустая ложка на курсоре наведена на кофемолку с молотым → зачерпнуть.
            if (!HasGround && held && !_heldPrev
                && cursorHandler != null && cursorHandler.Carried == this
                && cursorHandler.HoverTarget is Grinder grinder)
            {
                if (grinder.TryTakeGround(out var size))
                {
                    HasGround = true;
                    GroundGrind = size;
                    if (stateGround != null) SetState(stateGround);
                }
            }

            _heldPrev = held;
        }

        /// <summary>Турка забрала молотый из ложки — ложка снова пуста (для помешивания).</summary>
        public void ConsumeGround()
        {
            HasGround = false;
            if (stateEmpty != null) SetState(stateEmpty);
        }
    }
}
