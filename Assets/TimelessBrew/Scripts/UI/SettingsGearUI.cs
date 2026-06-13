using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TimelessBrew.Audio;

namespace TimelessBrew.UI
{
    /// <summary>
    /// Шестерёнка настроек в углу экрана. Открывает панель: переключение режима UI (Обычный/Тестовый),
    /// громкости (общая/эффекты/музыка) и сложности. Пока панель открыта — рука отключена.
    /// Клики и ползунки обрабатываются вручную через GameInput (без EventSystem), как и книга гостей.
    /// </summary>
    public class SettingsGearUI : MonoBehaviour
    {
        private Hand _hand;
        private GameObject _panelCanvas;
        private bool _open, _subscribed;

        private readonly List<(RectTransform rect, Action act)> _buttons = new();
        private class Slider { public RectTransform track; public RawImage fill, handle; public Func<float> get; public Action<float> set; }
        private readonly List<Slider> _sliders = new();
        private Slider _drag;

        private RectTransform _gearRect;     // зона клика по шестерёнке (на HUD-канве, всегда видна)

        private static readonly Color Wood = new(0.17f, 0.12f, 0.08f, 1f);
        private static readonly Color WoodLite = new(0.30f, 0.21f, 0.13f, 1f);
        private static readonly Color Gold = new(0.85f, 0.66f, 0.28f, 1f);
        private static readonly Color Ink = new(0.97f, 0.92f, 0.80f, 1f);

        private void Start()
        {
            _hand = FindFirstObjectByType<Hand>();
            BuildGearButton();
            BuildPanel();
        }

        private void Update()
        {
            if (!_subscribed && GameInput.Instance != null)
            {
                GameInput.Instance.OnActionPressed += OnPress;
                GameInput.Instance.OnActionReleased += OnRelease;
                _subscribed = true;
            }

            if (!_open) return;

            // Тянем активный ползунок, пока удерживаем ЛКМ.
            if (_drag != null && GameInput.Instance != null && GameInput.Instance.ActionHeld)
            {
                float x = GameInput.Instance.Pointer.x;
                _drag.track.GetWorldCorners(_corners);   // overlay-канва: мировые углы = экранные px
                float t = Mathf.InverseLerp(_corners[0].x, _corners[3].x, x);
                _drag.set(Mathf.Clamp01(t));
            }
            foreach (var s in _sliders) RefreshSlider(s);
            RefreshHighlights();
        }

        private void OnDestroy()
        {
            if (_open) ModalGuard.Pop();   // не оставить счётчик «висящим» при разрушении открытым
            if (GameInput.Instance == null) return;
            GameInput.Instance.OnActionPressed -= OnPress;
            GameInput.Instance.OnActionReleased -= OnRelease;
        }

        // ===================== КЛИКИ =====================

        private readonly Vector3[] _corners = new Vector3[4];

        private void OnPress()
        {
            Vector2 p = GameInput.Instance != null ? GameInput.Instance.Pointer : Vector2.zero;

            if (_gearRect != null && RectTransformUtility.RectangleContainsScreenPoint(_gearRect, p, null))
            { Toggle(); return; }

            if (!_open) return;

            foreach (var (rect, act) in _buttons)
                if (RectTransformUtility.RectangleContainsScreenPoint(rect, p, null)) { act(); return; }

            foreach (var s in _sliders)
                if (RectTransformUtility.RectangleContainsScreenPoint(s.track, p, null)) { _drag = s; break; }
        }

        private void OnRelease() => _drag = null;

        /// <summary>Открыть панель настроек извне (напр. кнопкой «Настройки» в меню).</summary>
        public void Open() { if (!_open) Toggle(); }

        private void Toggle()
        {
            _open = !_open;
            if (_panelCanvas != null) _panelCanvas.SetActive(_open);
            if (_open) ModalGuard.Push(); else ModalGuard.Pop();
            if (_hand != null) _hand.enabled = !ModalGuard.AnyOpen;   // рука жива, лишь когда модалок нет
            if (_open) foreach (var s in _sliders) RefreshSlider(s);
        }

        // ===================== ШЕСТЕРЁНКА =====================

        private void BuildGearButton()
        {
            var canvas = UguiUtil.EnsureCanvas();
            var holder = UguiUtil.Rect(canvas.transform, "GearButton", new Color(0f, 0f, 0f, 0.35f));
            _gearRect = (RectTransform)holder.transform;
            _gearRect.Anchor(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(58f, 58f));
            BuildGearIcon(_gearRect, 40f, Gold);
            ModalGuard.RegisterBlocker(_gearRect);   // клик по шестерёнке не уходит в руку
        }

        /// <summary>Иконка-шестерёнка из двух повёрнутых квадратов + тёмная втулка (без спрайтов).</summary>
        private static void BuildGearIcon(RectTransform parent, float size, Color color)
        {
            for (int i = 0; i < 2; i++)
            {
                var teeth = UguiUtil.Rect(parent, "Cog" + i, color);
                var rt = (RectTransform)teeth.transform;
                rt.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
                rt.localRotation = Quaternion.Euler(0, 0, i * 45f);
            }
            var hub = UguiUtil.Rect(parent, "Hub", Wood);
            ((RectTransform)hub.transform).Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.42f, size * 0.42f));
        }

        // ===================== ПАНЕЛЬ =====================

        private void BuildPanel()
        {
            _panelCanvas = new GameObject("Settings Canvas");
            var canvas = _panelCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 350;
            var scaler = _panelCanvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var dim = UguiUtil.Rect(_panelCanvas.transform, "Dim", new Color(0f, 0f, 0f, 0.55f));
            ((RectTransform)dim.transform).Anchor(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            var panel = UguiUtil.Rect(_panelCanvas.transform, "Panel", Wood);
            var prt = (RectTransform)panel.transform;
            prt.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(660f, 840f));
            Stripe(prt, 1f, Gold);   // верхняя золотая черта

            var title = UguiUtil.Label(prt, "Title", 34, TextAnchor.UpperCenter);
            title.fontStyle = FontStyle.Bold; title.color = Ink;
            ((RectTransform)title.transform).Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(-40f, 44f));
            title.text = "Настройки";

            float y = -110f;
            Header(prt, "Интерфейс", ref y);
            ModeButton(prt, "Обычный", UiSettings.Mode.Normal, -150f, y);
            ModeButton(prt, "Тестовый", UiSettings.Mode.Testing, 150f, y);
            y -= 84f;

            Header(prt, "Сложность", ref y);
            DiffButton(prt, "Лёгкая", UiSettings.Difficulty.Easy, -200f, y);
            DiffButton(prt, "Обычная", UiSettings.Difficulty.Normal, 0f, y);
            DiffButton(prt, "Сложная", UiSettings.Difficulty.Hard, 200f, y);
            y -= 96f;

            Header(prt, "Звук", ref y);
            VolumeSlider(prt, "Общая", y, () => AudioManager.Instance.Master, v => AudioManager.Instance.SetMaster(v)); y -= 64f;
            VolumeSlider(prt, "Эффекты", y, () => AudioManager.Instance.SfxVolume, v => AudioManager.Instance.SetSfx(v)); y -= 64f;
            VolumeSlider(prt, "Музыка", y, () => AudioManager.Instance.MusicVolume, v => AudioManager.Instance.SetMusic(v)); y -= 64f;

            // Выход из игры — прямо из настроек.
            MakeButton(prt, "В меню", new Vector2(-130f, 100f), new Vector2(240f, 52f), new Vector2(0.5f, 0f), ToMenu);
            var quit = MakeButton(prt, "Выход", new Vector2(130f, 100f), new Vector2(240f, 52f), new Vector2(0.5f, 0f), QuitGame);
            quit.GetComponent<RawImage>().color = new Color(0.5f, 0.2f, 0.15f);

            var close = MakeButton(prt, "Закрыть", new Vector2(0f, 38f), new Vector2(220f, 50f), new Vector2(0.5f, 0f), Toggle);
            close.GetComponent<RawImage>().color = WoodLite;

            _panelCanvas.SetActive(false);
        }

        private void ToMenu()
        {
            Time.timeScale = 1f;                 // на случай паузы — вернуть ход перед сменой сцены
            SceneManager.LoadScene("MainMenu");
        }

        private void QuitGame()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void Header(RectTransform panel, string text, ref float y)
        {
            var h = UguiUtil.Label(panel, "H_" + text, 22, TextAnchor.MiddleLeft);
            h.fontStyle = FontStyle.Bold; h.color = Gold;
            ((RectTransform)h.transform).Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(-80f, 30f));
            y -= 44f;
        }

        private void ModeButton(RectTransform panel, string label, UiSettings.Mode m, float x, float y)
        {
            var b = MakeButton(panel, label, new Vector2(x, y), new Vector2(260f, 56f), new Vector2(0.5f, 1f), () => UiSettings.SetMode(m));
            b.name = "Mode_" + m;
        }

        private void DiffButton(RectTransform panel, string label, UiSettings.Difficulty d, float x, float y)
        {
            var b = MakeButton(panel, label, new Vector2(x, y), new Vector2(180f, 52f), new Vector2(0.5f, 1f), () => UiSettings.SetDifficulty(d));
            b.name = "Diff_" + d;
        }

        private void VolumeSlider(RectTransform panel, string label, float y, Func<float> get, Action<float> set)
        {
            var lab = UguiUtil.Label(panel, "L_" + label, 18, TextAnchor.MiddleCenter);
            lab.color = Ink;
            lab.text = label;   // подпись над ползунком (раньше текст не выставлялся — пусто)
            ((RectTransform)lab.transform).Anchor(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(360f, 24f));

            var track = UguiUtil.Rect(panel, "Track_" + label, new Color(0f, 0f, 0f, 0.5f));
            var trt = (RectTransform)track.transform;
            trt.Anchor(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y - 28f), new Vector2(360f, 16f));

            var fill = UguiUtil.Rect(trt, "Fill", Gold);
            fill.rectTransform.Anchor(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(0f, 16f));

            var handle = UguiUtil.Rect(trt, "Handle", Ink);
            handle.rectTransform.Anchor(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14f, 30f));

            _sliders.Add(new Slider { track = trt, fill = fill, handle = handle, get = get, set = set });
        }

        private RawImage MakeButton(RectTransform panel, string label, Vector2 pos, Vector2 size, Vector2 pivot, Action act)
        {
            var bg = UguiUtil.Rect(panel, "Btn_" + label, WoodLite);
            var rt = (RectTransform)bg.transform;
            rt.Anchor(new Vector2(0.5f, pivot.y), new Vector2(0.5f, pivot.y), new Vector2(0.5f, pivot.y), pos, size);
            var t = UguiUtil.Label(rt, "L", 19, TextAnchor.MiddleCenter);
            t.color = Ink;
            ((RectTransform)t.transform).Anchor(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            t.text = label;
            _buttons.Add((rt, act));
            return bg;
        }

        private void Stripe(RectTransform panel, float topPivot, Color c)
        {
            var s = UguiUtil.Rect(panel, "Stripe", c);
            ((RectTransform)s.transform).Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 5f));
        }

        // ===================== ОБНОВЛЕНИЕ =====================

        private void RefreshSlider(Slider s)
        {
            float v = Mathf.Clamp01(s.get());
            float w = s.track.sizeDelta.x;
            s.fill.rectTransform.sizeDelta = new Vector2(w * v, 16f);
            s.handle.rectTransform.anchoredPosition = new Vector2(w * v, 0f);
        }

        private void RefreshHighlights()
        {
            // Кнопки режима/сложности переименованы в Mode_*/Diff_* (см. ModeButton/DiffButton).
            foreach (var (rect, _) in _buttons)
            {
                bool isToggle = rect.name.StartsWith("Mode_") || rect.name.StartsWith("Diff_");
                if (!isToggle) continue;
                bool active = rect.name == "Mode_" + UiSettings.UiMode || rect.name == "Diff_" + UiSettings.Diff;
                var img = rect.GetComponent<RawImage>();
                if (img != null) img.color = active ? Gold : WoodLite;
            }
        }
    }
}
