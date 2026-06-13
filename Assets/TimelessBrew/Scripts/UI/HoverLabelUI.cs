using UnityEngine;
using UnityEngine.UI;

namespace TimelessBrew.UI
{
    /// <summary>
    /// Обычный UI: аккуратная подпись с названием предмета, всплывающая НАД ним только при наведении
    /// пустой рукой (тёплая «деревянная» плашка, золотая черта-акцент, мягкое появление). В тестовом
    /// режиме скрыта — там работает <see cref="CursorTooltip"/>.
    /// </summary>
    public class HoverLabelUI : MonoBehaviour
    {
        private Hand _hand;
        private Camera _cam;
        private RectTransform _root;
        private RawImage _bg;
        private RawImage _accent;
        private Text _label;
        private float _alpha;

        private static readonly Color Bg = new(0.16f, 0.11f, 0.07f, 0.92f);
        private static readonly Color Accent = new(0.85f, 0.66f, 0.28f, 1f);
        private static readonly Color Ink = new(0.97f, 0.92f, 0.80f, 1f);

        private void Start()
        {
            _hand = FindFirstObjectByType<Hand>();
            _cam = Camera.main;
            var canvas = UguiUtil.EnsureCanvas();

            _bg = UguiUtil.Rect(canvas.transform, "HoverLabel", Bg);
            _root = (RectTransform)_bg.transform;
            _root.Anchor(Vector2.zero, Vector2.zero, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(180f, 40f));

            _accent = UguiUtil.Rect(_root, "Accent", Accent);
            ((RectTransform)_accent.transform).Anchor(new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(-10f, 3f));

            _label = UguiUtil.Label(_root, "Text", 20, TextAnchor.MiddleCenter);
            _label.fontStyle = FontStyle.Bold;
            ((RectTransform)_label.transform).Anchor(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(-22f, -8f));

            _root.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_hand == null) { _hand = FindFirstObjectByType<Hand>(); return; }
            if (_cam == null) { _cam = Camera.main; if (_cam == null) return; }

            bool show = UiSettings.IsNormal && !ModalGuard.AnyOpen && _hand.HasHover && !string.IsNullOrEmpty(_hand.HoverName);
            Vector3 sp = show ? _cam.WorldToScreenPoint(_hand.HoverWorld) : Vector3.zero;
            if (show && sp.z < 0f) show = false;   // объект за камерой

            // Мягкое появление/исчезание.
            _alpha = Mathf.MoveTowards(_alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 8f);
            bool active = _alpha > 0.001f;
            if (_root.gameObject.activeSelf != active) _root.gameObject.SetActive(active);
            if (!active) return;

            if (show)
            {
                _label.text = _hand.HoverName;
                float w = Mathf.Max(120f, _label.preferredWidth + 36f);
                _root.sizeDelta = new Vector2(w, 40f);
                _root.position = new Vector3(sp.x, sp.y + 26f, 0f);   // чуть выше верхушки объекта
            }

            _bg.color = new Color(Bg.r, Bg.g, Bg.b, Bg.a * _alpha);
            _accent.color = new Color(Accent.r, Accent.g, Accent.b, _alpha);
            _label.color = new Color(Ink.r, Ink.g, Ink.b, _alpha);
        }
    }
}
