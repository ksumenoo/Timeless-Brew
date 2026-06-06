using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TimelessBrew.Core
{
    /// <summary>
    /// Единый источник ввода (см. §3.1). Оборачивает Unity Input System.
    /// Все остальные системы читают ввод отсюда, а не напрямую — это нужно,
    /// чтобы подсказки кнопок и смена устройства (геймпад/клавиатура) жили в одном месте.
    ///
    /// ВАЖНО: этот скрипт ожидает, что в проекте создан Input Actions asset
    /// с Action Map "Gameplay" и действиями:
    ///   Point (Vector2, Passthrough)        — позиция курсора / стик
    ///   Interact (Button)                   — ЛКМ / RT (взять, удержание = действие)
    ///   Inventory (Button)                  — Tab / Y
    ///   Watch (Button)                      — Q / X (карманные часы)
    ///   Notebook (Button)                   — B / Back (блокнот рецептов)
    ///   Pause (Button)                      — Esc / Start
    /// Перетащи сгенерированный C#-класс действий или используй PlayerInput-компонент.
    ///
    /// Здесь сделана "ручная" привязка через InputActionReference, чтобы не зависеть
    /// от конкретного имени сгенерированного класса. Заполни ссылки в инспекторе.
    /// </summary>
    public class GameInput : MonoBehaviour
    {
        public static GameInput Instance { get; private set; }

        public enum Device { KeyboardMouse, Gamepad }

        [Header("Action References (назначь в инспекторе)")]
        [SerializeField] private InputActionReference pointAction;
        [SerializeField] private InputActionReference interactAction;
        [SerializeField] private InputActionReference inventoryAction;
        [SerializeField] private InputActionReference watchAction;
        [SerializeField] private InputActionReference notebookAction;
        [SerializeField] private InputActionReference pauseAction;

        /// <summary>Текущее активное устройство ввода. Слушай это событие, чтобы менять подсказки кнопок.</summary>
        public event Action<Device> OnDeviceChanged;
        public Device CurrentDevice { get; private set; } = Device.KeyboardMouse;

        // Дискретные события — для UI/часов/паузы, где важен момент нажатия.
        public event Action OnInteractPressed;
        public event Action OnInteractReleased;
        public event Action OnInventoryPressed;
        public event Action OnWatchPressed;
        public event Action OnNotebookPressed;
        public event Action OnPausePressed;

        /// <summary>Удерживается ли кнопка взаимодействия прямо сейчас (для "удержания ЛКМ" из §4.1).</summary>
        public bool InteractHeld { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            Enable(pointAction);
            Enable(interactAction);
            Enable(inventoryAction);
            Enable(watchAction);
            Enable(notebookAction);
            Enable(pauseAction);

            if (interactAction != null)
            {
                interactAction.action.started += HandleInteractStarted;
                interactAction.action.canceled += HandleInteractCanceled;
            }
            Bind(inventoryAction, _ => OnInventoryPressed?.Invoke());
            Bind(watchAction, _ => OnWatchPressed?.Invoke());
            Bind(notebookAction, _ => OnNotebookPressed?.Invoke());
            Bind(pauseAction, _ => OnPausePressed?.Invoke());

            InputSystem.onActionChange += HandleActionChange;
        }

        private void OnDisable()
        {
            if (interactAction != null)
            {
                interactAction.action.started -= HandleInteractStarted;
                interactAction.action.canceled -= HandleInteractCanceled;
            }
            InputSystem.onActionChange -= HandleActionChange;
        }

        private void HandleInteractStarted(InputAction.CallbackContext ctx)
        {
            //Debug.Log("CLICK received");   // временно
            InteractHeld = true;
            OnInteractPressed?.Invoke();
        }

        private void HandleInteractCanceled(InputAction.CallbackContext ctx)
        {
            InteractHeld = false;
            OnInteractReleased?.Invoke();
        }

        /// <summary>Позиция курсора в экранных координатах. На геймпаде — виртуальный курсор, двигаемый стиком.</summary>
        public Vector2 PointerScreenPosition =>
            pointAction != null ? pointAction.action.ReadValue<Vector2>() : (Vector2)Input.mousePosition;

        // --- Определение активного устройства для смены подсказок (§3) ---
        private void HandleActionChange(object obj, InputActionChange change)
        {
            if (change != InputActionChange.ActionPerformed) return;
            if (obj is not InputAction action) return;

            var control = action.activeControl;
            if (control == null) return;

            Device detected = control.device is Gamepad ? Device.Gamepad : Device.KeyboardMouse;
            if (detected != CurrentDevice)
            {
                CurrentDevice = detected;
                OnDeviceChanged?.Invoke(CurrentDevice);
            }
        }

        private static void Enable(InputActionReference r)
        {
            if (r != null) r.action.Enable();
        }

        private static void Bind(InputActionReference r, Action<InputAction.CallbackContext> cb)
        {
            if (r != null) r.action.performed += cb;
        }
    }
}
