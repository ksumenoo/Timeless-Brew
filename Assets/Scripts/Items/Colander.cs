using System;
using UnityEngine;
using TimelessBrew.Data;
namespace TimelessBrew.Items
{
    /// <summary>
    /// Медный дуршлаг + просеивание шелухи (§4.2 Этап 2, §6.3).
    ///   1. Обжаренные зёрна пересыпаются из сковороды в дуршлаг (TryReceive от RoastingPan).
    ///      В этот момент фиксируется обжарка (pan.EmptyToDurshlag → запись в BrewSession).
    ///   2. Игрок берёт дуршлаг, подносит к раковине и удерживает ЛКМ над ней — шелуха улетает.
    ///   3. Когда зерно очищено, чистота пишется в сессию (раковина зовёт Shake).
    /// </summary>
    public class Colander : InteractableItem
    {
        [Header("Состояния (§6.3)")]
        [SerializeField] private ItemStateSO stateEmpty;   // пустой (принимает зёрна)
        [SerializeField] private ItemStateSO stateBeans;   // с зёрнами (есть шелуха)
        [SerializeField] private ItemStateSO stateClean;   // просеяно

        [Header("Просеивание")]
        [SerializeField] private float shakeToClean = 1.5f;  // секунд тряски до полной очистки

        public bool HasBeans { get; private set; }

        // Чистота 0..1 по времени потряхивания.
        public float SiftProgress01
        {
            get
            {
                if (shakeToClean <= 0f) return 1f;
                float v = _shakeTime / shakeToClean;
                if (v > 1f) v = 1f;
                return v;
            }
        }

        public event Action OnSifted;

        private float _shakeTime;

        private void Start()
        {
            if (stateEmpty != null) SetState(stateEmpty);
        }

        // Сковорода пересыпает обжаренные зёрна в дуршлаг.
        public override bool TryReceive(InteractableItem source)
        {
            if (HasBeans || source == null) return false;
            if (source.itemType != "Pan") return false;
            if (!base.TryReceive(source)) return false;

            // Пересыпали из сковороды — фиксируем обжарку в сессии.
            RoastingPan pan = source as RoastingPan;
            if (pan != null) pan.EmptyToDurshlag();

            HasBeans = true;
            _shakeTime = 0f;
            if (stateBeans != null) SetState(stateBeans);
            return true;
        }

        // Потрясти дуршлаг у раковины — вызывает Sink, пока держат ЛКМ над раковиной.
        public void Shake(float deltaTime)
        {
            if (!HasBeans) return;

            _shakeTime += deltaTime;
            if (_shakeTime >= shakeToClean) FinishSift();
        }

        private void FinishSift()
        {
            if (BrewSession.Instance != null)
                BrewSession.Instance.RecordSift(SiftProgress01);

            Debug.Log("[Colander] Просеяно, чистота: " + SiftProgress01.ToString("0.00"), this);

            HasBeans = false;
            if (stateClean != null) SetState(stateClean);
            else if (stateEmpty != null) SetState(stateEmpty);

            if (OnSifted != null) OnSifted.Invoke();
        }
    }
}
