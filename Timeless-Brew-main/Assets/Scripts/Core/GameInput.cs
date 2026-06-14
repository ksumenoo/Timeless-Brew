using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TimelessBrew.Core
{
    // Единый источник ввода (§3.1). Оборачивает Unity Input System: остальные системы читают
    // ввод отсюда, а не напрямую. В проекте нужен Input Actions asset с действиями
    // Point, Interact, Inventory, Watch, Notebook, Pause — ссылки на них назначь в инспекторе.
    public class GameInput : MonoBehaviour
    {
        public static GameInput Instance;

        public enum Device { KeyboardMouse, Gamepad }

        [Header("Действия (назначь в инспекторе)")]
        [SerializeField] private InputActionReference pointAction;
        [SerializeField] private InputActionReference interactAction;
        [SerializeField] private InputActionReference inventoryAction;
        [SerializeField] private InputActionReference watchAction;
        [SerializeField] private InputActionReference notebookAction;
        [SerializeField] private InputActionReference pauseAction;

        public Device CurrentDevice { get; private set; } = Device.KeyboardMouse;
        public bool InteractHeld { get; private set; }   // удерживается ли ЛКМ (для §4.1)

        // События — для UI, часов, паузы и т.п.
        public event Action<Device> OnDeviceChanged;
        public event Action OnInteractPressed;
        public event Action OnInteractReleased;
        public event Action OnInventoryPressed;
        public event Action OnWatchPressed;
        public event Action OnNotebookPressed;
        public event Action OnPausePressed;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
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
            if (inventoryAction != null) inventoryAction.action.performed += HandleInventory;
            if (watchAction != null) watchAction.action.performed += HandleWatch;
            if (notebookAction != null) notebookAction.action.performed += HandleNotebook;
            if (pauseAction != null) pauseAction.action.performed += HandlePause;

            InputSystem.onActionChange += HandleActionChange;
        }

        private void OnDisable()
        {
            if (interactAction != null)
            {
                interactAction.action.started -= HandleInteractStarted;
                interactAction.action.canceled -= HandleInteractCanceled;
            }
            if (inventoryAction != null) inventoryAction.action.performed -= HandleInventory;
            if (watchAction != null) watchAction.action.performed -= HandleWatch;
            if (notebookAction != null) notebookAction.action.performed -= HandleNotebook;
            if (pauseAction != null) pauseAction.action.performed -= HandlePause;

            InputSystem.onActionChange -= HandleActionChange;
        }

        private void HandleInteractStarted(InputAction.CallbackContext ctx)
        {
            InteractHeld = true;
            if (OnInteractPressed != null) OnInteractPressed();
        }

        private void HandleInteractCanceled(InputAction.CallbackContext ctx)
        {
            InteractHeld = false;
            if (OnInteractReleased != null) OnInteractReleased();
        }

        private void HandleInventory(InputAction.CallbackContext ctx)
        {
            if (OnInventoryPressed != null) OnInventoryPressed();
        }

        private void HandleWatch(InputAction.CallbackContext ctx)
        {
            if (OnWatchPressed != null) OnWatchPressed();
        }

        private void HandleNotebook(InputAction.CallbackContext ctx)
        {
            if (OnNotebookPressed != null) OnNotebookPressed();
        }

        private void HandlePause(InputAction.CallbackContext ctx)
        {
            if (OnPausePressed != null) OnPausePressed();
        }

        // Позиция курсора в экранных координатах. На геймпаде — виртуальный курсор от стика.
        public Vector2 PointerScreenPosition
        {
            get
            {
                if (pointAction != null) return pointAction.action.ReadValue<Vector2>();
                return Input.mousePosition;   // запасной вариант (старый ввод)
            }
        }

        // Определяем активное устройство, чтобы менять подсказки кнопок (§3).
        private void HandleActionChange(object obj, InputActionChange change)
        {
            if (change != InputActionChange.ActionPerformed) return;

            InputAction action = obj as InputAction;
            if (action == null) return;

            var control = action.activeControl;
            if (control == null) return;

            Device detected;
            if (control.device is Gamepad) detected = Device.Gamepad;
            else detected = Device.KeyboardMouse;

            if (detected != CurrentDevice)
            {
                CurrentDevice = detected;
                if (OnDeviceChanged != null) OnDeviceChanged(CurrentDevice);
            }
        }

        private static void Enable(InputActionReference r)
        {
            if (r != null) r.action.Enable();
        }
    }
}
