using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TimelessBrew.UI
{
    /// <summary>
    /// Обычный UI: вертикальная шкала-хотбар над каждым задействованным сосудом/станцией (снизу
    /// вверх, в процентах). Чайник/чашка/питчер — уровень жидкости; кофемолка — помол; дуршлаг —
    /// просев; сковорода — обжарка ЦВЕТОМ; турка — уровень кофе/прогресс варки + ОТДЕЛЬНАЯ шкала
    /// пенки рядом (красная у края = вот-вот убежит). У чашки и турки показываются «фишки» добавок
    /// (молоко/сахар/сироп/специи) — видно, что положили. В тестовом режиме и под модалкой скрыта.
    /// </summary>
    public class VesselGaugeUI : MonoBehaviour
    {
        private Camera _cam;
        private Canvas _canvas;

        private class Gauge
        {
            public RectTransform root;
            public RawImage fill;
            public Text pct, name;
            public RawImage foamTrack, foamFill;
            public Text foamLabel;
            public RectTransform chips;
            public readonly List<(RawImage bg, Text txt)> chipPool = new();
        }
        private readonly Dictionary<Vessel, Gauge> _gauges = new();

        private const float TrackW = 30f, TrackH = 116f, Pad = 4f, FoamW = 16f;
        private static readonly Color Track = new(0.05f, 0.04f, 0.03f, 0.78f);

        private void Start()
        {
            _cam = Camera.main;
            _canvas = UguiUtil.EnsureCanvas();
        }

        private void LateUpdate()
        {
            if (_cam == null) { _cam = Camera.main; if (_cam == null) return; }

            if (!UiSettings.IsNormal || ModalGuard.AnyOpen)
            {
                foreach (var kv in _gauges)
                    if (kv.Value.root.gameObject.activeSelf) kv.Value.root.gameObject.SetActive(false);
                return;
            }

            foreach (var v in Vessel.All)
            {
                if (v == null) continue;
                bool show = Describe(v, out float value, out Color col, out string label, out string pctText);
                Gauge g = show ? GetGauge(v) : (_gauges.TryGetValue(v, out var ex) ? ex : null);
                if (g == null) continue;

                if (!show) { if (g.root.gameObject.activeSelf) g.root.gameObject.SetActive(false); continue; }

                Vector3 sp = _cam.WorldToScreenPoint(RendererTop(v.transform));
                if (sp.z < 0f) { if (g.root.gameObject.activeSelf) g.root.gameObject.SetActive(false); continue; }
                if (!g.root.gameObject.activeSelf) g.root.gameObject.SetActive(true);

                g.root.position = new Vector3(sp.x, sp.y + 30f, 0f);
                g.fill.rectTransform.sizeDelta = new Vector2(TrackW - Pad * 2f, Mathf.Clamp01(value) * (TrackH - Pad * 2f));
                g.fill.color = col;
                g.pct.text = pctText;
                g.name.text = label;

                UpdateFoam(g, v);
                UpdateChips(g, v);
            }
        }

        /// <summary>Главная шкала: значение 0..1, цвет, подпись, текст. false — сосуд скрыт.</summary>
        private static bool Describe(Vessel v, out float value, out Color col, out string label, out string pct)
        {
            value = 0f; col = Color.white; label = ""; pct = "";

            if (v.kind == Vessel.Kind.Pan)
            {
                if (v.mix.beans <= 0.001f) return false;
                value = Mathf.Max(0.06f, v.RoastProgress01);
                col = RoastColor(v.RoastProgress01);
                label = "Обжарка"; pct = Mixture.RoastRu(v.mix.roast);
                return true;
            }

            var grinder = v.GetComponent<Grinder>();
            if (grinder != null)
            {
                if (!grinder.HasGrounds) return false;
                value = grinder.GrindAmount; col = new Color(0.55f, 0.4f, 0.25f);
                label = "Помол"; pct = Mixture.GrindRu(grinder.CurrentGrind);
                return true;
            }

            var colander = v.GetComponent<Colander>();
            if (colander != null)
            {
                if (v.mix.beans <= 0.001f) return false;
                value = v.mix.sifted ? 1f : colander.SiftProgress;
                col = v.mix.sifted ? new Color(0.5f, 0.8f, 0.45f) : new Color(0.75f, 0.7f, 0.5f);
                label = "Просев"; pct = v.mix.sifted ? "чисто" : $"{value * 100f:0}%";
                return true;
            }

            switch (v.kind)
            {
                case Vessel.Kind.Kettle:
                    if (v.mix.water <= 0.001f) return false;
                    value = v.mix.water;
                    col = Color.Lerp(new Color(0.4f, 0.6f, 0.85f), new Color(0.9f, 0.45f, 0.2f), v.mix.temperature);
                    label = "Вода"; pct = v.mix.waterType == WaterType.Hot ? "кипяток" : $"{value * 100f:0}%";
                    return true;

                case Vessel.Kind.Cezve:
                {
                    if (v.Ruined) { value = 1f; col = new Color(0.8f, 0.2f, 0.15f); label = "Турка"; pct = "убежал"; return true; }
                    if (v.CoffeeReady)
                    {
                        // Готовый кофе: показываем УРОВЕНЬ (убывает при разливе), а не статичную «готов».
                        value = v.mix.coffee; col = new Color(0.36f, 0.22f, 0.13f);
                        label = "Готов"; pct = $"{value * 100f:0}%";
                        return true;
                    }
                    bool hasGrounds = v.mix.grounds > 0.001f, hasWater = v.mix.water > 0.001f;
                    if (!hasGrounds && !hasWater) return false;

                    Color hot = new(0.9f, 0.4f, 0.2f), cold = new(0.45f, 0.6f, 0.9f);
                    // Пока НАЛИВАЕМ воду (не полна, варка ещё не пошла) — шкала ВОДЫ цветом по температуре.
                    if (hasWater && v.mix.water < 0.95f && v.BrewProgress01 < 0.02f)
                    {
                        value = v.mix.water;
                        col = v.mix.waterType == WaterType.Hot ? hot : cold;
                        label = "Вода"; pct = v.mix.waterType == WaterType.Hot ? "горячая" : "холодная";
                        return true;
                    }
                    if (hasGrounds && hasWater)   // варка
                    {
                        value = v.BrewProgress01;
                        col = Color.Lerp(new Color(0.45f, 0.7f, 0.4f), new Color(0.85f, 0.25f, 0.18f), Mathf.InverseLerp(0.55f, 1f, v.mix.foam));
                        label = "Варка"; pct = $"{value * 100f:0}%";
                        return true;
                    }
                    // Только молотый (нет воды) или только вода.
                    value = hasWater ? v.mix.water : 0.06f;
                    col = hasWater ? (v.mix.waterType == WaterType.Hot ? hot : cold) : new Color(0.5f, 0.4f, 0.3f);
                    label = "Турка";
                    pct = hasGrounds ? "нужна вода" : (v.mix.waterType == WaterType.Hot ? "горячая" : "холодная");
                    return true;
                }

                case Vessel.Kind.Cup:
                    if (v.mix.coffee <= 0.001f && v.mix.milk <= 0.001f && v.mix.sugar == 0
                        && v.mix.syrups.Count == 0 && v.mix.spices.Count == 0) return false;
                    value = Mathf.Max(v.mix.coffee, v.mix.milk);
                    col = v.mix.milk > 0.001f ? new Color(0.78f, 0.62f, 0.42f) : new Color(0.36f, 0.22f, 0.13f);
                    label = "Чашка"; pct = $"{v.mix.coffee * 100f:0}%";
                    return true;

                case Vessel.Kind.Pitcher:
                    if (v.mix.milk <= 0.001f) return false;
                    value = v.mix.milk; col = new Color(0.95f, 0.93f, 0.86f);
                    label = "Питчер"; pct = $"{value * 100f:0}%";
                    return true;
            }
            return false;
        }

        /// <summary>Шкала пенки рядом с туркой — только пока варится (виден риск перелива).</summary>
        private static void UpdateFoam(Gauge g, Vessel v)
        {
            bool brewing = v.kind == Vessel.Kind.Cezve && !v.Ruined && !v.CoffeeReady
                           && v.mix.grounds > 0.001f && v.mix.water > 0.001f;
            g.foamTrack.gameObject.SetActive(brewing);
            g.foamLabel.gameObject.SetActive(brewing);
            if (!brewing) return;
            float f = Mathf.Clamp01(v.mix.foam);
            g.foamFill.rectTransform.sizeDelta = new Vector2(FoamW - Pad, f * (TrackH - Pad * 2f));
            g.foamFill.color = Color.Lerp(new Color(0.95f, 0.92f, 0.85f), new Color(0.85f, 0.2f, 0.15f), Mathf.InverseLerp(0.5f, 1f, f));
        }

        /// <summary>Фишки добавок (молоко/сахар/сироп/специи) — видно, что положили в чашку/турку.</summary>
        private static void UpdateChips(Gauge g, Vessel v)
        {
            bool wants = v.kind == Vessel.Kind.Cup || v.kind == Vessel.Kind.Cezve;
            g.chips.gameObject.SetActive(wants);
            if (!wants) return;

            var items = new List<(Color, string)>();
            if (v.kind == Vessel.Kind.Cezve)
            {
                if (v.mix.grounds > 0.001f) items.Add((new Color(0.42f, 0.28f, 0.18f), "Молотый"));
                if (v.mix.water > 0.001f)
                    items.Add((v.mix.waterType == WaterType.Hot ? new Color(0.9f, 0.45f, 0.2f) : new Color(0.45f, 0.6f, 0.85f),
                               v.mix.waterType == WaterType.Hot ? "Кипяток" : "Вода"));
            }
            if (v.kind == Vessel.Kind.Cup)
            {
                if (v.mix.latteArt) items.Add((new Color(0.85f, 0.78f, 0.62f), "Латте-арт"));
                else if (v.mix.milk > 0.001f) items.Add((new Color(0.95f, 0.93f, 0.86f), "Молоко"));
            }
            if (v.mix.sugar > 0) items.Add((Color.white, $"Сахар ×{v.mix.sugar}"));
            foreach (var id in v.mix.syrups) items.Add((ColorForAdd(id), Mixture.NameRu(id)));
            foreach (var id in v.mix.spices) items.Add((ColorForAdd(id), Mixture.NameRu(id)));

            int shown = Mathf.Min(items.Count, 8);
            int n = Mathf.Max(g.chipPool.Count, shown);   // пройти и по старым фишкам, чтобы спрятать лишние
            float y = 0f;
            for (int i = 0; i < n; i++)
            {
                if (i >= g.chipPool.Count)
                {
                    if (i >= 8) break;        // не плодим фишки сверх лимита
                    AddChip(g);
                }
                var (bg, txt) = g.chipPool[i];
                bool on = i < shown;
                bg.gameObject.SetActive(on);
                if (!on) continue;
                var (c, label) = items[i];
                txt.text = label;
                float w = Mathf.Clamp(txt.preferredWidth + 14f, 40f, 130f);
                var rt = bg.rectTransform;
                rt.sizeDelta = new Vector2(w, 20f);
                rt.anchoredPosition = new Vector2(8f, y);
                bg.color = new Color(c.r, c.g, c.b, 0.92f);
                // Тёмный текст на светлой фишке, светлый — на тёмной.
                txt.color = (c.r + c.g + c.b) / 3f > 0.55f ? new Color(0.12f, 0.08f, 0.05f) : Color.white;
                y -= 23f;
            }
        }

        private static void AddChip(Gauge g)
        {
            var bg = UguiUtil.Rect(g.chips, "Chip" + g.chipPool.Count, Color.white);
            bg.rectTransform.Anchor(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(60f, 20f));
            var txt = UguiUtil.Label(bg.rectTransform, "L", 12, TextAnchor.MiddleCenter);
            ((RectTransform)txt.transform).Anchor(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-6f, 0f));
            g.chipPool.Add((bg, txt));
        }

        private static Color ColorForAdd(string id) => id switch
        {
            "caramel" => new(0.85f, 0.55f, 0.2f), "vanilla" => new(0.92f, 0.86f, 0.6f),
            "cardamom" => new(0.55f, 0.7f, 0.4f), "star_anise" => new(0.5f, 0.35f, 0.2f),
            "saffron" => new(0.9f, 0.55f, 0.15f), "nutmeg" => new(0.6f, 0.45f, 0.3f),
            "lemon_zest" => new(0.9f, 0.85f, 0.3f), "orange_zest" => new(0.9f, 0.6f, 0.2f),
            "rosemary" => new(0.3f, 0.55f, 0.3f), "cinnamon" => new(0.6f, 0.3f, 0.2f),
            "lavender" => new(0.6f, 0.5f, 0.8f), "cocoa" => new(0.4f, 0.25f, 0.15f),
            _ => new(0.6f, 0.6f, 0.6f)
        };

        private static Color RoastColor(float t)
        {
            Color green = new(0.45f, 0.62f, 0.3f), yellow = new(0.82f, 0.72f, 0.35f),
                  medium = new(0.6f, 0.42f, 0.22f), dark = new(0.36f, 0.24f, 0.14f), burnt = new(0.12f, 0.09f, 0.07f);
            if (t < 0.2f) return Color.Lerp(green, yellow, t / 0.2f);
            if (t < 0.45f) return Color.Lerp(yellow, medium, (t - 0.2f) / 0.25f);
            if (t < 0.7f) return Color.Lerp(medium, dark, (t - 0.45f) / 0.25f);
            return Color.Lerp(dark, burnt, Mathf.Clamp01((t - 0.7f) / 0.25f));
        }

        private Gauge GetGauge(Vessel v)
        {
            if (_gauges.TryGetValue(v, out var g)) return g;

            var track = UguiUtil.Rect(_canvas.transform, "Gauge_" + v.name, Track);
            var rt = (RectTransform)track.transform;
            rt.Anchor(Vector2.zero, Vector2.zero, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(TrackW, TrackH));

            var fill = UguiUtil.Rect(rt, "Fill", Color.white);
            fill.rectTransform.Anchor(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, Pad), new Vector2(TrackW - Pad * 2f, 0f));

            var pct = UguiUtil.Label(rt, "Pct", 15, TextAnchor.LowerCenter);
            pct.fontStyle = FontStyle.Bold;
            ((RectTransform)pct.transform).Anchor(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(120f, 22f));

            var name = UguiUtil.Label(rt, "Name", 14, TextAnchor.UpperCenter);
            ((RectTransform)name.transform).Anchor(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -2f), new Vector2(120f, 20f));

            // Шкала пенки — слева от основной.
            var foamTrack = UguiUtil.Rect(rt, "Foam", Track);
            foamTrack.rectTransform.Anchor(new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 0.5f), new Vector2(-6f, 0f), new Vector2(FoamW, 0f));
            var foamFill = UguiUtil.Rect(foamTrack.rectTransform, "FoamFill", Color.white);
            foamFill.rectTransform.Anchor(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, Pad), new Vector2(FoamW - Pad, 0f));
            var foamLabel = UguiUtil.Label(foamTrack.rectTransform, "FoamL", 11, TextAnchor.UpperCenter);
            ((RectTransform)foamLabel.transform).Anchor(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -2f), new Vector2(60f, 18f));
            foamLabel.text = "пена";

            // Контейнер фишек — справа от основной шкалы.
            var chipsGo = UguiUtil.Rect(rt, "Chips", new Color(0, 0, 0, 0));
            chipsGo.rectTransform.Anchor(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(8f, 0f), new Vector2(140f, TrackH));

            g = new Gauge { root = rt, fill = fill, pct = pct, name = name, foamTrack = foamTrack, foamFill = foamFill, foamLabel = foamLabel, chips = chipsGo.rectTransform };
            _gauges[v] = g;
            return g;
        }

        private static Vector3 RendererTop(Transform t)
        {
            var rends = t.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return t.position;
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return new Vector3(b.center.x, b.max.y, b.center.z);
        }
    }
}
