using UnityEngine;
using TimelessBrew.Core;
using TimelessBrew.Data;

namespace TimelessBrew.Items
{
    /// <summary>
    /// Источник добавки на столе: сахарница (§6.9), сиропница (§6.11) или банка специи (§6.10).
    /// Модель из диздока: игрок берёт источник на курсор, наводит на цель и КЛИКАЕТ — добавляется
    /// одна порция. Куда что (§4.2 Э3–4):
    ///   • Сахар — в турку (до варки) или в готовую чашку;
    ///   • Сироп — в чашку;
    ///   • Специя (целая) — в турку.
    ///
    /// Запись идёт в BrewSession (AddSugar / SetSyrup / AddSpice). Одна порция за клик
    /// (фронт нажатия ЛКМ). Цель определяется по тому, на что наведён курсор.
    /// </summary>
    public class IngredientAdder : InteractableItem
    {
        public enum Kind { Sugar, Syrup, Spice }

        [Header("Ссылки")]
        [SerializeField] private CursorHandler cursorHandler;
        [SerializeField] private ItemStateSO state; // переносимое состояние (canPour/canReceive не нужны)

        [Header("Тип добавки")]
        [SerializeField] private Kind kind = Kind.Sugar;
        [Tooltip("Для сиропницы: какой сироп добавляется.")]
        [SerializeField] private SyrupType syrup = SyrupType.Caramel;
        [Tooltip("Для банки специи: идентификатор специи, напр. 'cardamom', 'cocoa', 'rosemary'.")]
        [SerializeField] private string spiceId = "cardamom";

        private bool _heldPrev;

        private void Start()
        {
            if (cursorHandler == null) cursorHandler = FindObjectOfType<CursorHandler>();
            if (state != null) SetState(state);
        }

        private void Update()
        {
            bool held = GameInput.Instance != null && GameInput.Instance.InteractHeld;
            if (held && !_heldPrev && cursorHandler != null && cursorHandler.Carried == this)
                TryApply(cursorHandler.HoverTarget);
            _heldPrev = held;
        }

        private void TryApply(InteractableItem target)
        {
            if (target == null || BrewSession.Instance == null) return;

            switch (kind)
            {
                case Kind.Sugar:
                    if (target is Cezve || target is Cup)
                    {
                        BrewSession.Instance.AddSugar(1);
                        Debug.Log($"[Sugar] +1 кубик ({target.name}).", this);
                    }
                    break;

                case Kind.Syrup:
                    if (target is Cup)
                    {
                        BrewSession.Instance.SetSyrup(syrup);
                        Debug.Log($"[Syrup] {syrup} → чашка.", this);
                    }
                    break;

                case Kind.Spice:
                    if (target is Cezve)
                    {
                        BrewSession.Instance.AddSpice(spiceId);
                        Debug.Log($"[Spice] '{spiceId}' → турка.", this);
                    }
                    break;
            }
        }
    }
}
