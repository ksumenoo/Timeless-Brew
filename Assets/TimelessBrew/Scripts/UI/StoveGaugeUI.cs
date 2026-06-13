using UnityEngine;
using UnityEngine.UI;

namespace TimelessBrew.UI
{
    /// <summary>
    /// Круглый датчик печи в плоском виде (§6.1): полоса красный–жёлтый–белый–жёлтый–красный
    /// со стрелкой-указателем. Белый центр = рабочий режим, отклонения = мало огня / перегрев.
    /// Висит над печкой (проекция позиции конфорки на экран).
    /// </summary>
    public class StoveGaugeUI : MonoBehaviour
    {
        private Stove _stove;
        private Camera _cam;
        private RectTransform _root;
        private RectTransform _needle;
        private Text _label;

        private const float Width = 240f, Height = 26f;

        private void Start()
        {
            _stove = FindFirstObjectByType<Stove>();
            _cam = Camera.main;
            var canvas = UguiUtil.EnsureCanvas();

            _root = (RectTransform)UguiUtil.Rect(canvas.transform, "StoveGauge", new Color(0f, 0f, 0f, 0.4f)).transform;
            _root.Anchor(Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Width + 8f, Height + 8f));

            Color[] zones = {
                new(0.85f, 0.2f, 0.15f), new(0.9f, 0.7f, 0.25f), new(0.95f, 0.95f, 0.95f),
                new(0.9f, 0.7f, 0.25f), new(0.85f, 0.2f, 0.15f)
            };
            float seg = Width / zones.Length;
            for (int i = 0; i < zones.Length; i++)
            {
                var z = UguiUtil.Rect(_root, "Zone" + i, zones[i]);
                ((RectTransform)z.transform).Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-Width * 0.5f + seg * (i + 0.5f), 0f), new Vector2(seg - 2f, Height));
            }

            _needle = (RectTransform)UguiUtil.Rect(_root, "Needle", Color.black).transform;
            _needle.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4f, Height + 10f));

            _label = UguiUtil.Label(_root, "Label", 15, TextAnchor.LowerCenter);
            ((RectTransform)_label.transform).Anchor(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(Width, 22f));
        }

        private void LateUpdate()
        {
            if (_stove == null) { _stove = FindFirstObjectByType<Stove>(); return; }
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            float t = Mathf.Clamp01((_stove.Arrow + 1f) * 0.5f);   // -1..1 → 0..1
            _needle.anchoredPosition = new Vector2((t - 0.5f) * Width, 0f);
            _label.text = _stove.IsOverheated
                ? $"Печь  {(_stove.Heat * 100f):0}%  <color=#ff6655>раскалена! остывает…</color>"
                : $"Печь  {(_stove.Heat * 100f):0}%" + (_stove.IsOptimal ? "  <color=#88ff88>оптимум</color>" : "");

            Vector3 sp = _cam.WorldToScreenPoint(_stove.BurnerPosition);
            if (sp.z < 0f) { _root.gameObject.SetActive(false); return; }
            if (!_root.gameObject.activeSelf) _root.gameObject.SetActive(true);
            _root.position = new Vector3(sp.x, sp.y + 110f, 0f);
        }
    }
}
