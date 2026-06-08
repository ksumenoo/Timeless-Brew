using UnityEngine;
using TimelessBrew.Core;
using TimelessBrew.Data;

namespace TimelessBrew.Items
{
    // Ручная кофемолка (Этап 3a, §6.5).
    // 1. В кофемолку ссыпают обжаренные зёрна (из банки).
    // 2. Игрок держит ЛКМ на ручке — прогресс помола растёт: крупный -> средний -> мелкий -> в пыль.
    // 3. Молотый забирают ложкой (TryTakeGround) или кнопкой CollectGround.
    public class Grinder : InteractableItem
    {
        [Header("Ссылки")]
        [SerializeField] private CursorHandler cursorHandler;
        [SerializeField] private InteractableItem crankHandle;   // ручка, которую крутят (держат ЛКМ)

        [Header("Состояния (§6.5)")]
        [SerializeField] private ItemStateSO stateEmpty;    // пустая
        [SerializeField] private ItemStateSO stateBeans;    // зёрна в воронке
        [SerializeField] private ItemStateSO stateGround;   // молотый кофе

        [Header("Помол")]
        [SerializeField] private float grindRate = 0.35f;   // скорость роста прогресса (0..1 в сек)

        [Header("Пороги фракций (0..1)")]
        [SerializeField, Range(0f, 1f)] private float mediumAt = 0.25f;
        [SerializeField, Range(0f, 1f)] private float fineAt = 0.50f;
        [SerializeField, Range(0f, 1f)] private float powderAt = 0.75f;

        public float GrindProgress { get; private set; }   // прогресс помола 0..1
        public bool HasBeans { get; private set; }
        public bool IsGrinding { get; private set; }

        // Текущая фракция по прогрессу — на ней игрок и останавливается.
        public GrindSize CurrentGrind
        {
            get
            {
                if (GrindProgress >= powderAt) return GrindSize.Powder;
                if (GrindProgress >= fineAt) return GrindSize.Fine;
                if (GrindProgress >= mediumAt) return GrindSize.Medium;
                return GrindSize.Coarse;
            }
        }

        private void Start()
        {
            if (cursorHandler == null) cursorHandler = FindObjectOfType<CursorHandler>();
            if (stateEmpty != null) SetState(stateEmpty);
        }

        // Банка с обжаренными зёрнами ссыпает в воронку.
        public override bool TryReceive(InteractableItem source)
        {
            if (HasBeans) return false;
            if (source == null) return false;
            if (source.itemType != "BeanJar") return false;
            if (!base.TryReceive(source)) return false;   // проверка флагов состояния

            HasBeans = true;
            GrindProgress = 0f;
            if (stateBeans != null) SetState(stateBeans);
            return true;
        }

        private void Update()
        {
            if (!HasBeans)
            {
                IsGrinding = false;
                return;
            }

            // Мелем, пока держат ЛКМ на ручке.
            IsGrinding = cursorHandler != null
                         && crankHandle != null
                         && cursorHandler.DirectHoldTarget == crankHandle;

            if (IsGrinding)
            {
                GrindProgress = GrindProgress + grindRate * Time.deltaTime;
                if (GrindProgress > 1f) GrindProgress = 1f;
            }
        }

        // Забрать молотый кнопкой и записать фракцию в сессию (альтернатива ложке).
        public void CollectGround()
        {
            if (!HasBeans) return;

            if (BrewSession.Instance != null)
                BrewSession.Instance.RecordGrind(CurrentGrind);

            HasBeans = false;
            IsGrinding = false;
            if (stateGround != null) SetState(stateGround);
            else if (stateEmpty != null) SetState(stateEmpty);

            Debug.Log("[Grinder] Помол записан: " + CurrentGrind, this);
        }

        // Зачерпнуть молотый ложкой (§6.8). Помол запишет турка при добавлении кофе.
        public bool TryTakeGround(out GrindSize size)
        {
            size = CurrentGrind;
            if (!HasBeans) return false;

            HasBeans = false;
            IsGrinding = false;
            if (stateEmpty != null) SetState(stateEmpty);
            Debug.Log("[Grinder] Молотый зачерпнут ложкой: " + size, this);
            return true;
        }

        // Кнопка в инспекторе для теста.
        [ContextMenu("DEBUG: Collect ground")]
        private void DebugCollect() => CollectGround();
    }
}
