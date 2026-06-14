using System;
using UnityEngine;
using TimelessBrew.Core;
using TimelessBrew.Data;
namespace TimelessBrew.Items
{
    /// <summary>
    /// Чайник (§6.7) — источник горячей воды для варки. Стоит на печке отдельно от турки.
    /// Пока стоит на конфорке и печь греет — закипает (BoilProgress 0..1). Закипел → состояние
    /// «горячий» (canPour), и его можно перелить в турку как горячую воду (Этап 3b).
    /// </summary>
    public class Kettle : InteractableItem
    {
        [Header("Ссылки")]
        [SerializeField] private CursorHandler cursorHandler;
        [SerializeField] private Stove stove;

        [Header("Состояния (§6.7)")]
        [SerializeField] private ItemStateSO stateCold;  // с холодной водой (не льётся)
        [SerializeField] private ItemStateSO stateHot;   // закипел (льётся горячая вода)

        [Header("Кипячение")]
        [SerializeField] private float boilRate = 0.25f;

        public float BoilProgress { get; private set; }
        public bool IsHot { get; private set; }

        public event Action OnBoiled;

        private void Start()
        {
            if (cursorHandler == null) cursorHandler = FindObjectOfType<CursorHandler>();
            if (stove == null) stove = FindObjectOfType<Stove>();
            if (stateCold != null) SetState(stateCold);
        }

        private void Update()
        {
            if (IsHot) return;

            bool onBurner = stove != null
                            && cursorHandler != null
                            && cursorHandler.Carried != this
                            && stove.CanCook
                            && stove.IsOnBurner(transform);

            if (onBurner && stove.Temperature > 0f)
            {
                BoilProgress += boilRate * stove.Temperature * Time.deltaTime;
                if (BoilProgress >= 1f)
                {
                    BoilProgress = 1f;
                    IsHot = true;
                    if (stateHot != null) SetState(stateHot);
                    OnBoiled?.Invoke();
                    Debug.Log("[Kettle] Закипел — можно лить горячую воду.", this);
                }
            }
        }
    }
}
