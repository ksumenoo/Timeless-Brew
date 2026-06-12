using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TimelessBrew.UI
{
    /// <summary>
    /// Книга гостей (§4). Открывается на Tab ПОВЕРХ всего на отдельной непрозрачной канве
    /// (не видно надписей кухни сквозь неё). Для каждого гостя — заказы, скрытое предпочтение и
    /// кнопки «Вызвать утро / вечер»: гость приходит перед столом. Пока книга открыта, рука выключена.
    /// </summary>
    public class GuestBookUI : MonoBehaviour
    {
        private GuestLibrary _library;
        private GuestService _service;
        private Hand _hand;

        private GameObject _canvasGo;
        private GameObject _panelGo;
        private bool _open;
        private bool _subscribed;

        private readonly List<(RectTransform rect, Action act)> _buttons = new();

        private void Start()
        {
            _library = FindFirstObjectByType<GuestLibrary>();
            _service = FindFirstObjectByType<GuestService>();
            _hand = FindFirstObjectByType<Hand>();
            BuildUi();
        }

        private void Update()
        {
            if (_subscribed || GameInput.Instance == null) return;
            GameInput.Instance.OnToggleBook += Toggle;
            GameInput.Instance.OnActionPressed += OnPress;
            _subscribed = true;
        }

        private void OnDestroy()
        {
            if (GameInput.Instance == null) return;
            GameInput.Instance.OnToggleBook -= Toggle;
            GameInput.Instance.OnActionPressed -= OnPress;
        }

        // ===================== UI =====================

        private void BuildUi()
        {
            // Своя канва с высоким sortingOrder — гарантированно поверх HUD.
            _canvasGo = new GameObject("Guest Book Canvas");
            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            var scaler = _canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // Непрозрачный затемнитель на весь экран — кухню за книгой не видно.
            var dim = UguiUtil.Rect(_canvasGo.transform, "Dim", new Color(0.05f, 0.04f, 0.03f, 0.98f));
            ((RectTransform)dim.transform).Anchor(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            var panel = UguiUtil.Rect(_canvasGo.transform, "Panel", new Color(0.18f, 0.13f, 0.09f, 1f));
            _panelGo = panel.gameObject;
            var prt = (RectTransform)panel.transform;
            prt.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1040f, 800f));

            var title = UguiUtil.Label(prt, "Title", 30, TextAnchor.UpperCenter);
            ((RectTransform)title.transform).Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(-40f, 40f));
            title.text = "<b>Книга гостей</b>   <size=16>(Tab — закрыть · кнопки — вызвать гостя)</size>";

            BuildRows(prt);

            _canvasGo.SetActive(false);
        }

        private void BuildRows(RectTransform panel)
        {
            if (_library == null || _library.guests == null) return;

            float rowH = 92f, top = -78f;
            for (int i = 0; i < _library.guests.Length; i++)
            {
                var g = _library.guests[i];
                if (g == null) continue;

                var row = UguiUtil.Rect(panel, "Row" + i, new Color(1f, 1f, 1f, i % 2 == 0 ? 0.05f : 0.02f));
                var rrt = (RectTransform)row.transform;
                rrt.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f), new Vector2(0f, top - i * rowH), new Vector2(-32f, rowH - 8f));

                var info = UguiUtil.Label(rrt, "Info", 16, TextAnchor.MiddleLeft);
                ((RectTransform)info.transform).Anchor(new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), new Vector2(16f, 0f), new Vector2(-320f, 0f));
                info.text = RowText(g);

                Button(rrt, "Утро", new Vector2(-168f, 0f), () => CallGuest(g, true));
                Button(rrt, "Вечер", new Vector2(-58f, 0f), () => CallGuest(g, false));
            }
        }

        private string RowText(GuestSO g)
        {
            string secret = !string.IsNullOrEmpty(g.secretSpice)
                ? $"  <color=#ffd479>секрет: {Mixture.NameRu(g.secretSpice)}</color>"
                : (g.likesLatteArt ? "  <color=#ffd479>любит латте-арт</color>" : "");
            return $"<b>{g.displayName}</b>{secret}\n" +
                   $"<size=13>Утро: {g.morning.Summary().Replace("\n", "; ")}\nВечер: {g.evening.Summary().Replace("\n", "; ")}</size>";
        }

        private void Button(RectTransform parent, string label, Vector2 pos, Action act)
        {
            var bg = UguiUtil.Rect(parent, "Btn_" + label, new Color(0.45f, 0.3f, 0.15f, 1f));
            var rt = (RectTransform)bg.transform;
            rt.Anchor(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), pos, new Vector2(100f, 40f));
            var t = UguiUtil.Label(rt, "L", 16, TextAnchor.MiddleCenter);
            ((RectTransform)t.transform).Anchor(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            t.text = label;
            _buttons.Add((rt, act));
        }

        // ===================== ЛОГИКА =====================

        private void Toggle()
        {
            _open = !_open;
            if (_canvasGo != null) _canvasGo.SetActive(_open);
            if (_hand != null) _hand.enabled = !_open;   // пока книга открыта — рука выключена
        }

        private void OnPress()
        {
            if (!_open) return;
            Vector2 p = GameInput.Instance != null ? GameInput.Instance.Pointer : Vector2.zero;
            foreach (var (rect, act) in _buttons)
                if (RectTransformUtility.RectangleContainsScreenPoint(rect, p, null)) { act(); break; }
        }

        private void CallGuest(GuestSO g, bool morning)
        {
            if (_service != null) _service.Call(g, morning);
            Toggle();   // закрываем книгу, чтобы готовить
        }
    }
}
