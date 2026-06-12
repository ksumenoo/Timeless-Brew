using UnityEngine;
using UnityEngine.UI;

namespace TimelessBrew.UI
{
    /// <summary>Подсказка у курсора: показывает имя предмета/содержимого в руке или под курсором.</summary>
    public class CursorTooltip : MonoBehaviour
    {
        private Hand _hand;
        private RectTransform _panel;
        private Text _label;

        private void Start()
        {
            _hand = FindFirstObjectByType<Hand>();
            var canvas = UguiUtil.EnsureCanvas();
            var bg = UguiUtil.Rect(canvas.transform, "CursorTooltip", new Color(0f, 0f, 0f, 0.75f));
            _panel = (RectTransform)bg.transform;
            _panel.Anchor(Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(230f, 34f));
            _label = UguiUtil.Label(_panel, "Text", 20, TextAnchor.MiddleCenter);
            ((RectTransform)_label.transform).Anchor(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-12f, 0f));
            _panel.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_hand == null) { _hand = FindFirstObjectByType<Hand>(); return; }

            string text = _hand.CursorLabel;
            bool show = !string.IsNullOrEmpty(text);
            if (_panel.gameObject.activeSelf != show) _panel.gameObject.SetActive(show);
            if (!show) return;

            _label.text = text;
            Vector2 p = GameInput.Instance != null ? GameInput.Instance.Pointer : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            _panel.position = new Vector3(p.x + 18f, p.y + 18f, 0f);
        }
    }
}
