using UnityEngine;
using UnityEngine.UI;

namespace TimelessBrew.UI
{
    /// <summary>
    /// Крупное всплывающее сообщение по центру сверху экрана (поверх всего). Для заметных реплик:
    /// «Ты дурашка!» — красным, результат подачи — зелёным, подсказки — нейтрально. Само гаснет.
    /// Вызывается из любого места статикой <see cref="Toast"/>.
    /// </summary>
    public class ToastUI : MonoBehaviour
    {
        private static ToastUI _inst;

        private RectTransform _root;
        private Text _label;
        private Color _color = Color.white;
        private float _hideAt = -1f;
        private float _alpha;

        public static readonly Color Bad = new(0.95f, 0.3f, 0.25f);
        public static readonly Color Good = new(0.5f, 0.9f, 0.5f);
        public static readonly Color Neutral = new(0.97f, 0.92f, 0.8f);

        private void Awake() => _inst = this;
        private void OnDestroy() { if (_inst == this) _inst = null; }

        private void Start()
        {
            var go = new GameObject("Toast Canvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 360;   // выше настроек/дневника
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            _label = UguiUtil.Label(canvas.transform, "ToastText", 44, TextAnchor.MiddleCenter);
            _label.fontStyle = FontStyle.Bold;
            _label.horizontalOverflow = HorizontalWrapMode.Wrap;   // длинный результат подачи переносится
            _root = (RectTransform)_label.transform;
            _root.Anchor(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(1500f, 170f));
            _root.gameObject.SetActive(false);
        }

        /// <summary>Показать крупное сообщение указанным цветом на seconds секунд.</summary>
        public static void Toast(string text, Color color, float seconds = 2f)
        {
            if (_inst != null) _inst.ShowInternal(text, color, seconds);
        }

        private void ShowInternal(string text, Color color, float seconds)
        {
            if (_root == null) return;
            _label.text = text;
            _color = color;
            _alpha = 1f;
            _hideAt = Time.unscaledTime + seconds;
            _root.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (_root == null || !_root.gameObject.activeSelf) return;

            if (_hideAt > 0f && Time.unscaledTime >= _hideAt)
                _alpha = Mathf.MoveTowards(_alpha, 0f, Time.unscaledDeltaTime * 3f);

            _label.color = new Color(_color.r, _color.g, _color.b, _alpha);
            if (_alpha <= 0.001f) _root.gameObject.SetActive(false);
        }
    }
}
