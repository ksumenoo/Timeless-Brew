using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TimelessBrew.Audio;

namespace TimelessBrew.UI
{
    /// <summary>
    /// Подведение итогов смены (§4.4): в конце рабочего дня замораживает игру, затемняет экран и
    /// показывает статистику (обслужено гостей, средняя оценка) + кнопки «В меню» и «Выход».
    /// Вызывается из <see cref="DayClock"/> по завершении дня. Клики — вручную через GameInput.
    /// </summary>
    public class EndOfDayUI : MonoBehaviour
    {
        private static EndOfDayUI _inst;

        private GameObject _canvasGo;
        private Text _stats;
        private readonly List<(RectTransform rect, Action act)> _buttons = new();
        private bool _subscribed, _shown;

        private static readonly Color Wood = new(0.18f, 0.12f, 0.08f, 0.98f);
        private static readonly Color WoodLite = new(0.30f, 0.21f, 0.13f);
        private static readonly Color Gold = new(0.85f, 0.66f, 0.28f);
        private static readonly Color Ink = new(0.97f, 0.92f, 0.8f);

        private void Awake() => _inst = this;

        private void OnDestroy()
        {
            if (_inst == this) _inst = null;
            if (GameInput.Instance != null) GameInput.Instance.OnActionPressed -= OnPress;
            if (_shown) { Time.timeScale = 1f; ModalGuard.Pop(); }   // не оставить мир замороженным/заблокированным
        }

        private void Start() => BuildUi();

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
            if (!_shown) return;
            Vector2 p = GameInput.Instance != null ? GameInput.Instance.Pointer : Vector2.zero;
            foreach (var (rect, act) in _buttons)
                if (RectTransformUtility.RectangleContainsScreenPoint(rect, p, null)) { act(); return; }
        }

        /// <summary>Показать итоги смены (зовёт DayClock в конце дня).</summary>
        public static void Show(int served, int totalStars) { if (_inst != null) _inst.ShowInternal(served, totalStars); }

        private void ShowInternal(int served, int totalStars)
        {
            if (_shown) return;
            _shown = true;
            float avg = served > 0 ? (float)totalStars / served : 0f;
            _stats.text = $"Гостей обслужено: <b>{served}</b>\nСредняя оценка: <b>{avg:0.0}</b> из 5  ★";
            _canvasGo.SetActive(true);
            ModalGuard.Push();
            Time.timeScale = 0f;
            if (AudioManager.Instance != null) AudioManager.Instance.PlayMusic(Music.Endshift);
        }

        // ===================== UI =====================

        private void BuildUi()
        {
            _canvasGo = new GameObject("EndOfDay Canvas");
            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 365;   // выше всего
            var scaler = _canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var dim = UguiUtil.Rect(_canvasGo.transform, "Dim", new Color(0.04f, 0.03f, 0.02f, 0.85f));
            ((RectTransform)dim.transform).Anchor(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            var frame = UguiUtil.Rect(_canvasGo.transform, "Frame", Gold);
            var frt = (RectTransform)frame.transform;
            frt.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 460f));
            var panel = UguiUtil.Rect(frt, "Panel", Wood);
            ((RectTransform)panel.transform).Anchor(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-12f, -12f));
            var prt = (RectTransform)panel.transform;

            var title = UguiUtil.Label(prt, "Title", 52, TextAnchor.UpperCenter);
            title.fontStyle = FontStyle.Bold; title.color = Gold;
            ((RectTransform)title.transform).Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(-40f, 70f));
            title.text = "Смена окончена";

            _stats = UguiUtil.Label(prt, "Stats", 32, TextAnchor.MiddleCenter);
            _stats.color = Ink;
            ((RectTransform)_stats.transform).Anchor(new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(-60f, -150f));

            Button(prt, "В меню", new Vector2(-130f, 40f), ToMenu, WoodLite);
            Button(prt, "Выход", new Vector2(130f, 40f), Quit, new Color(0.5f, 0.2f, 0.15f));

            _canvasGo.SetActive(false);
        }

        private void Button(RectTransform panel, string label, Vector2 pos, Action act, Color color)
        {
            var bg = UguiUtil.Rect(panel, "Btn_" + label, color);
            var rt = (RectTransform)bg.transform;
            rt.Anchor(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), pos, new Vector2(240f, 56f));
            var t = UguiUtil.Label(rt, "L", 24, TextAnchor.MiddleCenter);
            t.color = Ink; t.fontStyle = FontStyle.Bold; t.text = label;
            ((RectTransform)t.transform).Anchor(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _buttons.Add((rt, act));
        }

        private void ToMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("MainMenu");
        }

        private void Quit()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
