using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TimelessBrew.UI
{
    /// <summary>
    /// Рядом с каждым сосудом, в котором что-то есть, показывает состав и статус готовки
    /// (что налито/насыпано, обжарка, пенка, «кофе готов» и т.п.). Это и есть «UI-столбик
    /// у чашки/турки/кофемолки», о котором речь в задаче.
    /// </summary>
    public class VesselPanel : MonoBehaviour
    {
        private Camera _cam;
        private Canvas _canvas;
        private readonly Dictionary<Vessel, Text> _panels = new();

        private void Start()
        {
            _cam = Camera.main;
            _canvas = UguiUtil.EnsureCanvas();
        }

        private void LateUpdate()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            // Текстовые панели состава — «тестовый» UI. В обычном режиме их заменяют шкалы (VesselGaugeUI).
            if (!UiSettings.IsTesting)
            {
                foreach (var kv in _panels)
                    if (kv.Value != null && kv.Value.transform.parent.gameObject.activeSelf)
                        kv.Value.transform.parent.gameObject.SetActive(false);
                return;
            }

            foreach (var v in Vessel.All)
            {
                if (v == null) continue;
                string text = BuildText(v);
                Text label = GetLabel(v);

                bool show = !string.IsNullOrEmpty(text);
                if (label.transform.parent.gameObject.activeSelf != show)
                    label.transform.parent.gameObject.SetActive(show);
                if (!show) continue;

                Vector3 sp = _cam.WorldToScreenPoint(v.transform.position);
                if (sp.z < 0f) { label.transform.parent.gameObject.SetActive(false); continue; }

                label.text = text;
                ((RectTransform)label.transform.parent).position = new Vector3(sp.x + 28f, sp.y + 24f, 0f);
            }
        }

        private string BuildText(Vessel v)
        {
            if (v.kind == Vessel.Kind.Jar || v.kind == Vessel.Kind.MilkJar) return null;

            var sb = new System.Text.StringBuilder();

            var grinder = v.GetComponent<Grinder>();
            if (grinder != null) sb.AppendLine(grinder.StatusLine());
            else if (!v.mix.IsEmpty) sb.AppendLine(v.mix.Summary());

            var colander = v.GetComponent<Colander>();
            if (colander != null) sb.AppendLine(colander.StatusLine());

            string status = v.StatusLine();
            if (!string.IsNullOrEmpty(status)) sb.AppendLine(status);

            string s = sb.ToString().TrimEnd();
            return string.IsNullOrEmpty(s) ? null : $"<b>{Title(v)}</b>\n{s}";
        }

        private static string Title(Vessel v) => v.kind switch
        {
            Vessel.Kind.Cup => "Чашка", Vessel.Kind.Cezve => "Турка", Vessel.Kind.Pan => "Сковорода",
            Vessel.Kind.Grinder => "Кофемолка", Vessel.Kind.Kettle => "Чайник", Vessel.Kind.Pitcher => "Питчер",
            Vessel.Kind.Colander => "Дуршлаг",
            _ => v.kind.ToString()
        };

        private Text GetLabel(Vessel v)
        {
            if (_panels.TryGetValue(v, out var existing)) return existing;

            var bg = UguiUtil.Rect(_canvas.transform, "Vessel_" + v.name, new Color(0f, 0f, 0f, 0.6f));
            ((RectTransform)bg.transform).Anchor(Vector2.zero, Vector2.zero, new Vector2(0f, 0f), Vector2.zero, new Vector2(220f, 120f));
            var label = UguiUtil.Label(bg.transform, "Text", 15, TextAnchor.LowerLeft);
            ((RectTransform)label.transform).Anchor(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-12f, -8f));
            _panels[v] = label;
            return label;
        }
    }
}
