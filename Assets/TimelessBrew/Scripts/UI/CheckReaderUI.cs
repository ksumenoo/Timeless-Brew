using UnityEngine;
using UnityEngine.UI;

namespace TimelessBrew.UI
{
    /// <summary>
    /// Крупная читалка заказа слева у края экрана. Клик по чеку на чекхолдере открывает её (большой
    /// шрифт), повторный клик — прячет. Здесь же кнопка статуса заказа: Новый → В работе → Готов.
    /// Клики по самой панели не уходят в кухню (панель — блокер ModalGuard).
    /// </summary>
    public class CheckReaderUI : MonoBehaviour
    {
        private static CheckReaderUI _inst;

        private Check _current;
        private GameObject _canvasGo;
        private RectTransform _panel, _statusBtn;
        private Text _title, _body, _statusText;
        private bool _subscribed;

        private static readonly Color Paper = new(0.95f, 0.91f, 0.78f, 1f);
        private static readonly Color Gold = new(0.62f, 0.45f, 0.18f, 1f);
        private static readonly Color Ink = new(0.14f, 0.09f, 0.05f, 1f);

        private void Awake() => _inst = this;

        private void OnDestroy()
        {
            if (_inst == this) _inst = null;
            if (GameInput.Instance != null) GameInput.Instance.OnActionPressed -= OnPress;
        }

        private void Start()
        {
            _canvasGo = new GameObject("Check Reader Canvas");
            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 340;
            var scaler = _canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = UguiUtil.Rect(_canvasGo.transform, "Panel", Paper);
            _panel = (RectTransform)panel.transform;
            _panel.Anchor(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(560f, 700f));
            ModalGuard.RegisterBlocker(_panel);   // клики по панели не уходят в кухню

            var stripe = UguiUtil.Rect(_panel, "Stripe", Gold);
            ((RectTransform)stripe.transform).Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 16f));

            _title = UguiUtil.Label(_panel, "Title", 44, TextAnchor.UpperCenter);
            _title.fontStyle = FontStyle.Bold; _title.color = Ink;
            ((RectTransform)_title.transform).Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(-30f, 64f));

            _body = UguiUtil.Label(_panel, "Body", 34, TextAnchor.UpperLeft);
            _body.color = Ink; _body.horizontalOverflow = HorizontalWrapMode.Wrap;
            ((RectTransform)_body.transform).Anchor(new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(-48f, -260f));

            _statusText = UguiUtil.Label(_panel, "Status", 30, TextAnchor.MiddleCenter);
            _statusText.fontStyle = FontStyle.Bold;
            ((RectTransform)_statusText.transform).Anchor(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(-40f, 44f));

            var btn = UguiUtil.Rect(_panel, "StatusBtn", Gold);
            _statusBtn = (RectTransform)btn.transform;
            _statusBtn.Anchor(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 92f), new Vector2(360f, 50f));
            var bl = UguiUtil.Label(_statusBtn, "L", 22, TextAnchor.MiddleCenter);
            bl.color = Paper; bl.fontStyle = FontStyle.Bold; bl.text = "Сменить статус";
            ((RectTransform)bl.transform).Anchor(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            var hint = UguiUtil.Label(_panel, "Hint", 18, TextAnchor.LowerCenter);
            hint.color = Gold;
            ((RectTransform)hint.transform).Anchor(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(-30f, 26f));
            hint.text = "клик по чеку — убрать";

            _canvasGo.SetActive(false);
        }

        private void Update()
        {
            if (!_subscribed && GameInput.Instance != null)
            {
                GameInput.Instance.OnActionPressed += OnPress;
                _subscribed = true;
            }
        }

        private void OnPress()
        {
            if (_canvasGo == null || !_canvasGo.activeSelf || _current == null) return;
            Vector2 p = GameInput.Instance != null ? GameInput.Instance.Pointer : Vector2.zero;
            if (RectTransformUtility.RectangleContainsScreenPoint(_statusBtn, p, null))
            {
                _current.CycleStatus();
                RefreshStatus();
            }
        }

        public static void Toggle(Check c) { if (_inst != null) _inst.ToggleInternal(c); }
        public static void HideIf(Check c) { if (_inst != null && _inst._current == c) _inst.HideInternal(); }

        /// <summary>Открыта ли читалка для этого чека сейчас (для обучения).</summary>
        public static bool IsShowing(Check c) =>
            _inst != null && _inst._current == c && _inst._canvasGo != null && _inst._canvasGo.activeSelf;

        private void ToggleInternal(Check c)
        {
            if (_canvasGo == null) return;
            if (_current == c && _canvasGo.activeSelf) { HideInternal(); return; }
            _current = c;
            _title.text = c.Title;
            _body.text = c.Body;
            RefreshStatus();
            _canvasGo.SetActive(true);
        }

        private void HideInternal() { _current = null; if (_canvasGo != null) _canvasGo.SetActive(false); }

        private void RefreshStatus()
        {
            if (_current == null) return;
            _statusText.text = "Статус: " + Check.StatusRu(_current.State);
            _statusText.color = Check.StatusColor(_current.State);
        }
    }
}
