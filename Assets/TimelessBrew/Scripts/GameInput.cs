using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TimelessBrew
{
    /// <summary>
    /// Единый ввод (§3.1) на низкоуровневом Input System: без ассета Actions и ручной привязки.
    /// Действие — ЛКМ (или RT геймпада). Книга рецептов — Tab (§3.1). Курсор — позиция мыши,
    /// на геймпаде виртуальный от левого стика.
    /// </summary>
    public class GameInput : MonoBehaviour
    {
        public static GameInput Instance { get; private set; }

        [SerializeField] private float virtualCursorSpeed = 1200f;

        public bool ActionHeld { get; private set; }
        public Vector2 Pointer { get; private set; }

        public event Action OnActionPressed;
        public event Action OnActionReleased;
        public event Action OnToggleBook;
        public event Action OnPause;

        private bool _prevHeld;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Pointer = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Update()
        {
            var mouse = Mouse.current;
            var pad = Gamepad.current;
            var kb = Keyboard.current;

            // Курсор
            if (mouse != null) Pointer = mouse.position.ReadValue();
            else if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue();
                Vector2 p = Pointer + stick * virtualCursorSpeed * Time.unscaledDeltaTime;
                p.x = Mathf.Clamp(p.x, 0f, Screen.width);
                p.y = Mathf.Clamp(p.y, 0f, Screen.height);
                Pointer = p;
            }

            // Действие
            bool held = (mouse != null && mouse.leftButton.isPressed)
                        || (pad != null && pad.rightTrigger.isPressed);
            ActionHeld = held;
            if (held && !_prevHeld) OnActionPressed?.Invoke();
            else if (!held && _prevHeld) OnActionReleased?.Invoke();
            _prevHeld = held;

            // Кнопки
            if ((kb != null && kb.tabKey.wasPressedThisFrame) || (pad != null && pad.buttonNorth.wasPressedThisFrame))
                OnToggleBook?.Invoke();
            if ((kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame))
                OnPause?.Invoke();
        }
    }
}
