using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TimelessBrew.UI
{
    /// <summary>
    /// Часы сверху по центру: фаза дня (Утро/День/Вечер), время и полоса прогресса дня. Рядом —
    /// кнопки скорости времени: ⏪ отмотать, ▶ обычно, ⏩ ускорить (чтобы гости приходили быстрее).
    /// Клики — вручную через GameInput (без EventSystem).
    /// </summary>
    public class DayClockUI : MonoBehaviour
    {
        private DayClock _clock;
        private Text _label;
        private Text _playLabel;       // значок средней кнопки: ▶ / II
        private RectTransform _playRect;   // зона средней кнопки (на паузе активна только она)
        private GameObject _pauseCanvas;   // затемнение + табличка «ПАУЗА» (отдельная канва ниже часов)
        private RawImage _progressFill;
        private RectTransform _progressTrack;
        private bool _subscribed;

        private readonly List<(RectTransform rect, Action act, float speedTag)> _buttons = new();

        private static readonly Color Bg = new(0.12f, 0.09f, 0.06f, 0.78f);
        private static readonly Color Gold = new(0.85f, 0.66f, 0.28f);
        private static readonly Color Wood = new(0.30f, 0.21f, 0.13f);
        private static readonly Color Ink = new(0.97f, 0.92f, 0.8f);

        private void Start()
        {
            _clock = FindFirstObjectByType<DayClock>();
            BuildUi();
        }

        private void Update()
        {
            if (!_subscribed && GameInput.Instance != null)
            {
                GameInput.Instance.OnActionPressed += OnPress;
                _subscribed = true;
            }
            if (_clock == null) { _clock = FindFirstObjectByType<DayClock>(); return; }

            float f = _clock.DayFraction;
            int totalMin = Mathf.RoundToInt(Mathf.Lerp(6 * 60, 22 * 60, f));   // рабочий день 06:00–22:00
            _label.text = $"{PhaseRu(_clock.Phase)}   {totalMin / 60:00}:{totalMin % 60:00}";
            _progressFill.rectTransform.sizeDelta = new Vector2(_progressTrack.sizeDelta.x * f, 8f);

            // Подсветка активной кнопки по ЗНАКУ скорости (а не по точному значению множителя).
            int spd = _clock.Speed < 0f ? -1 : _clock.Speed > 1.001f ? 1 : 0;
            foreach (var (rect, _, tag) in _buttons)
            {
                int cls = tag < 0f ? -1 : tag > 1.001f ? 1 : 0;
                var img = rect.GetComponent<RawImage>();
                if (img != null) img.color = cls == spd ? Gold : Wood;
            }

            // Средняя кнопка: ▶ (играть/вернуть норму) или II (поставить паузу); крупная «ПАУЗА».
            if (_playLabel != null)
                _playLabel.text = _clock.IsPaused || Mathf.Abs(_clock.Speed - 1f) > 0.001f ? "▶" : "II";
            if (_pauseCanvas != null && _pauseCanvas.activeSelf != _clock.IsPaused)
                _pauseCanvas.SetActive(_clock.IsPaused);
        }

        private void OnDestroy()
        {
            if (GameInput.Instance != null) GameInput.Instance.OnActionPressed -= OnPress;
        }

        private void OnPress()
        {
            // Под ЧУЖОЙ модалкой (её затемнение закрывает часы) клики игнорируем. Исключение — НАША
            // пауза (единственный слот в ModalGuard), чтобы её можно было снять кнопкой.
            bool paused = _clock != null && _clock.IsPaused;
            bool selfOnly = paused && ModalGuard.OpenCount == 1;
            if (ModalGuard.AnyOpen && !selfOnly) return;

            Vector2 p = GameInput.Instance != null ? GameInput.Instance.Pointer : Vector2.zero;
            foreach (var (rect, act, _) in _buttons)
            {
                if (paused && rect != _playRect) continue;   // на паузе активна только кнопка ▶
                if (RectTransformUtility.RectangleContainsScreenPoint(rect, p, null)) { act(); return; }
            }
        }

        private static string PhaseRu(DayPhase p) => p switch
        { DayPhase.Morning => "Утро", DayPhase.Day => "День", _ => "Вечер" };

        private void BuildUi()
        {
            var canvas = UguiUtil.EnsureCanvas();

            var bg = UguiUtil.Rect(canvas.transform, "DayClock", Bg);
            var root = (RectTransform)bg.transform;
            root.Anchor(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(520f, 64f));

            _label = UguiUtil.Label(root, "Time", 24, TextAnchor.MiddleLeft);
            _label.fontStyle = FontStyle.Bold; _label.color = Ink;
            ((RectTransform)_label.transform).Anchor(new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(16f, 6f), new Vector2(200f, -16f));

            _progressTrack = (RectTransform)UguiUtil.Rect(root, "Track", new Color(0f, 0f, 0f, 0.5f)).transform;
            _progressTrack.Anchor(new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(16f, 8f), new Vector2(210f, 8f));
            _progressFill = UguiUtil.Rect(_progressTrack, "Fill", Gold);
            _progressFill.rectTransform.Anchor(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(0f, 8f));

            SpeedButton(root, "⏪", -240f, () => _clock?.SetRewind(), -8f);
            _playLabel = SpeedButton(root, "II", -180f, () => _clock?.TogglePlayPause(), 1f);   // play/pause
            _playRect = (RectTransform)_playLabel.transform.parent;
            SpeedButton(root, "⏩", -120f, () => _clock?.SetFast(), 12f);

            BuildPauseCanvas();
        }

        /// <summary>
        /// Отдельная канва паузы: затемняющая плашка во весь экран (под слоем часов, sortingOrder 95 —
        /// часы остаются кликабельны и видимы) + деревянная табличка «ПАУЗА». Затемнение + ModalGuard
        /// (его ставит DayClock) не дают брать предметы.
        /// </summary>
        private void BuildPauseCanvas()
        {
            var go = new GameObject("Pause Canvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 95;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var dim = UguiUtil.Rect(go.transform, "Dim", new Color(0.06f, 0.06f, 0.09f, 0.62f));
            ((RectTransform)dim.transform).Anchor(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            var frame = UguiUtil.Rect(go.transform, "Plaque", new Color(0.72f, 0.55f, 0.25f));
            var frt = (RectTransform)frame.transform;
            frt.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 230f));
            var wood = UguiUtil.Rect(frt, "Wood", new Color(0.2f, 0.14f, 0.08f, 0.96f));
            ((RectTransform)wood.transform).Anchor(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-14f, -14f));

            var t = UguiUtil.Label((RectTransform)wood.transform, "T", 92, TextAnchor.MiddleCenter);
            t.fontStyle = FontStyle.Bold; t.color = new Color(0.95f, 0.82f, 0.4f);
            ((RectTransform)t.transform).Anchor(new Vector2(0, 0.3f), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            t.text = "ПАУЗА";

            var hint = UguiUtil.Label((RectTransform)wood.transform, "H", 24, TextAnchor.MiddleCenter);
            hint.color = new Color(0.9f, 0.85f, 0.7f);
            ((RectTransform)hint.transform).Anchor(new Vector2(0, 0), new Vector2(1, 0.3f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            hint.text = "нажми ▶ сверху, чтобы продолжить";

            go.SetActive(false);
            _pauseCanvas = go;
        }

        private Text SpeedButton(RectTransform root, string label, float xFromRight, Action act, float speedTag)
        {
            var bg = UguiUtil.Rect(root, "Spd_" + label, Wood);
            var rt = (RectTransform)bg.transform;
            rt.Anchor(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(xFromRight, 0f), new Vector2(50f, 44f));
            var t = UguiUtil.Label(rt, "L", 22, TextAnchor.MiddleCenter);
            t.color = Ink; t.fontStyle = FontStyle.Bold; t.text = label;
            ((RectTransform)t.transform).Anchor(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _buttons.Add((rt, act, speedTag));
            return t;
        }
    }
}
