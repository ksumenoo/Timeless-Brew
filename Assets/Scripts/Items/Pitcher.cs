using UnityEngine;
using TimelessBrew.Data;
namespace TimelessBrew.Items
{
    /// <summary>
    /// Металлический молочник-питчер (§6.13). Принимает молоко из баночки (источник itemType="Milk")
    /// и становится источником молока для латте-арта (Этап 5): полный молочник льётся (canPour).
    /// </summary>
    public class Pitcher : InteractableItem
    {
        [Header("Состояния (§6.13)")]
        [SerializeField] private ItemStateSO stateEmpty;  // пустой (принимает молоко)
        [SerializeField] private ItemStateSO stateFull;   // с молоком (льётся)

        public bool HasMilk { get; private set; }

        private void Start()
        {
            if (stateEmpty != null) SetState(stateEmpty);
        }

        public override bool TryReceive(InteractableItem source)
        {
            if (HasMilk || source == null || source.itemType != "Milk") return false;
            if (!base.TryReceive(source)) return false; // canReceive + молоко canPour

            HasMilk = true;
            if (stateFull != null) SetState(stateFull);
            return true;
        }
    }
}
