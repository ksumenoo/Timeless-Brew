using System;
using System.Collections.Generic;
using UnityEngine;
using TimelessBrew.Data;

namespace TimelessBrew.Items
{
    /// <summary>
    /// Базовый компонент любого интерактивного предмета на столе (§8.1: каждый предмет — Prefab
    /// с InteractableItem). Состояние хранится как ItemStateSO, а не зашито в код, поэтому
    /// новые состояния добавляются данными.
    ///
    /// Модель взаимодействия (§4.1):
    ///   - предмет берётся на курсор (CursorHandler решает это),
    ///   - при наведении на цель + удержании ЛКМ источник "льёт/высыпает" в цель,
    ///   - цель принимает содержимое через TryReceive.
    ///
    /// Этот скрипт намеренно тонкий: конкретная логика этапов (обжарка, помол, варка)
    /// будет в наследниках или отдельных компонентах. Здесь — общий контракт.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class InteractableItem : MonoBehaviour
    {
        [Header("Идентификация")]
        [Tooltip("Тип предмета, напр. 'BeanJar', 'Pan', 'Cezve'. Используется для валидации действий.")]
        public string itemType;

        [Header("Состояния")]
        [Tooltip("Все возможные состояния этого предмета (ScriptableObject'ы).")]
        public List<ItemStateSO> states = new();

        [Tooltip("Стартовое состояние (должно входить в states).")]
        public ItemStateSO initialState;

        [Header("Визуал")]
        [Tooltip("Renderer, которому применяется visualOverride из состояния (опционально).")]
        public Renderer targetRenderer;

        public ItemStateSO CurrentState { get; private set; }

        /// <summary>Срабатывает при смене состояния. (старое, новое)</summary>
        public event Action<ItemStateSO, ItemStateSO> OnStateChanged;

        /// <summary>Срабатывает, когда предмет принял содержимое от источника.</summary>
        public event Action<InteractableItem> OnReceived;

        private Collider _collider;

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            if (initialState != null)
                SetState(initialState);
            else if (states.Count > 0)
                SetState(states[0]);
        }

        // --- Свойства текущего состояния, к которым обращается CursorHandler ---
        public bool IsPickable => CurrentState != null && CurrentState.pickable;
        public bool CanReceive => CurrentState != null && CurrentState.canReceive;
        public bool CanPour    => CurrentState != null && CurrentState.canPour;

        /// <summary>Сменить состояние по ссылке на SO.</summary>
        public void SetState(ItemStateSO next)
        {
            if (next == null) return;
            var prev = CurrentState;
            CurrentState = next;
            ApplyVisual(next);
            OnStateChanged?.Invoke(prev, next);
        }

        /// <summary>Сменить состояние по строковому id (удобно из другой логики).</summary>
        public bool SetStateById(string id)
        {
            foreach (var s in states)
            {
                if (s != null && s.stateId == id)
                {
                    SetState(s);
                    return true;
                }
            }
            Debug.LogWarning($"[{name}] Состояние '{id}' не найдено в states.", this);
            return false;
        }

        private void ApplyVisual(ItemStateSO state)
        {
            if (targetRenderer != null && state.visualOverride != null)
                targetRenderer.material = state.visualOverride;

            if (state.fillLevel >= 0f && targetRenderer != null)
            {
                // Договорённость по shader fill-level (§7.2): свойство _Fill в материале.
                var mpb = new MaterialPropertyBlock();
                targetRenderer.GetPropertyBlock(mpb);
                mpb.SetFloat("_Fill", state.fillLevel);
                targetRenderer.SetPropertyBlock(mpb);
            }
        }

        /// <summary>
        /// Источник (предмет на курсоре) пытается передать содержимое этому предмету.
        /// Возвращает true, если приём состоялся. Базовая логика — только проверка флагов;
        /// конкретные переходы состояний навешиваются через OnReceived или в наследнике.
        /// </summary>
        public virtual bool TryReceive(InteractableItem source)
        {
            if (!CanReceive) return false;
            if (source == null || !source.CanPour) return false;

            OnReceived?.Invoke(source);
            return true;
        }

        /// <summary>Подсказка для UI при наведении.</summary>
        public string HoverHint => CurrentState != null ? CurrentState.hoverHint : string.Empty;
    }
}
