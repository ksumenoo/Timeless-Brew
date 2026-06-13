using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TimelessBrew.UI
{
    /// <summary>
    /// Латте-арт (этап 5). Открывается кликом ПИТЧЕРА по чашке с готовым кофе. Круг кофе: держишь/
    /// водишь ЛКМ — наливается молоко из питчера (кофе светлеет), движения оставляют молочный узор.
    /// Справа — сколько молока в питчере (тратится при наливе; кончилось — рисовать нельзя). «ОК» →
    /// в чашку молоко + (если был узор) латте-арт. Без узора — просто молоко.
    /// </summary>
    public class LatteArtUI : MonoBehaviour
    {
        private static LatteArtUI _inst;
        private static Texture2D _circle;

        private Hand _hand;
        private GameObject _canvasGo;
        private RectTransform _cup, _dotsParent;
        private RawImage _cupImg, _milkFill;
        private Text _milkText;
        private Vessel _target, _pitcher;
        private bool _open, _subscribed, _ignoreHold;
        private float _milk;
        private int _dots;
        private Vector2 _lastDot;

        private readonly List<(RectTransform rect, Action act)> _buttons = new();

        private static readonly Color Coffee = new(0.30f, 0.19f, 0.11f);
        private static readonly Color Latte = new(0.82f, 0.72f, 0.58f);
        private static readonly Color MilkDot = new(0.97f, 0.95f, 0.9f, 0.95f);
        private static readonly Color Wood = new(0.17f, 0.12f, 0.08f);
        private static readonly Color Gold = new(0.85f, 0.66f, 0.28f);
        private static readonly Color Ink = new(0.97f, 0.92f, 0.8f);

        private const float CupSize = 520f, MilkTrackH = 300f;

        private void Awake() => _inst = this;
        private void OnDestroy()
        {
            if (_open) ModalGuard.Pop();
            if (_inst == this) _inst = null;
            if (GameInput.Instance != null) GameInput.Instance.OnActionPressed -= OnPress;
        }

        public static void Open(Vessel cup, Vessel pitcher) { if (_inst != null) _inst.OpenInternal(cup, pitcher); }

        private void Start()
        {
            _hand = FindFirstObjectByType<Hand>();
            BuildUi();
        }

        private void Update()
        {
            if (!_subscribed && GameInput.Instance != null)
            {
                GameInput.Instance.OnActionPressed += OnPress;
                _subscribed = true;
            }
            if (!_open) return;

            bool held = GameInput.Instance != null && GameInput.Instance.ActionHeld;
            if (!held) _ignoreHold = false;   // отпустили нажатие, которым открыли — теперь можно рисовать

            // Наливаем молоко и рисуем — пока держим внутри круга и в питчере есть молоко.
            if (held && !_ignoreHold && _pitcher != null && _pitcher.mix.milk > 0.001f &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_cup, GameInput.Instance.Pointer, null, out Vector2 local))
            {
                float r = _cup.rect.width * 0.5f;
                if (local.magnitude <= r)
                {
                    float d = Mathf.Min(Time.unscaledDeltaTime * 0.5f, _pitcher.mix.milk);   // не больше, чем есть
                    _milk = Mathf.Min(1f, _milk + d);
                    _pitcher.mix.milk = Mathf.Max(0f, _pitcher.mix.milk - d);
                    _cupImg.color = Color.Lerp(Coffee, Latte, _milk);
                    if (_dots == 0 || Vector2.Distance(local, _lastDot) > 14f) { PlaceDot(local, r); _lastDot = local; }
                }
            }

            // Шкала молока питчера справа.
            if (_pitcher != null && _milkFill != null)
            {
                _milkFill.rectTransform.sizeDelta = new Vector2(28f, Mathf.Clamp01(_pitcher.mix.milk) * (MilkTrackH - 4f));
                _milkText.text = $"молоко\n{_pitcher.mix.milk * 100f:0}%";
            }
        }

        private void OnPress()
        {
            if (!_open) return;
            Vector2 p = GameInput.Instance != null ? GameInput.Instance.Pointer : Vector2.zero;
            foreach (var (rect, act) in _buttons)
                if (RectTransformUtility.RectangleContainsScreenPoint(rect, p, null)) { act(); return; }
        }

        // ===================== ЛОГИКА =====================

        private void OpenInternal(Vessel cup, Vessel pitcher)
        {
            _target = cup; _pitcher = pitcher;
            _milk = 0f; _dots = 0; _lastDot = Vector2.zero;
            _ignoreHold = true;   // нажатие, которым открыли мини-игру, не рисует (иначе случайная точка)
            foreach (Transform c in _dotsParent) Destroy(c.gameObject);
            _cupImg.color = Coffee;

            _open = true;
            _canvasGo.SetActive(true);
            ModalGuard.Push();
            if (_hand != null) _hand.enabled = !ModalGuard.AnyOpen;
        }

        private void Apply()
        {
            if (_target != null && _milk > 0.03f)
            {
                _target.mix.milk = Mathf.Clamp01(Mathf.Max(_target.mix.milk, _milk));
                _target.mix.latteArt = _dots > 6;   // нарисовали узор → латте-арт, иначе просто молоко
            }
            Close();
        }

        private void Close()
        {
            _open = false;
            if (_canvasGo != null) _canvasGo.SetActive(false);
            ModalGuard.Pop();
            if (_hand != null) _hand.enabled = !ModalGuard.AnyOpen;
            _target = null; _pitcher = null;
        }

        private void PlaceDot(Vector2 local, float r)
        {
            if (_dots > 140) return;
            Vector2 cl = local.magnitude > r - 20f ? local.normalized * (r - 20f) : local;
            var dot = UguiUtil.Rect(_dotsParent, "Dot" + _dots, MilkDot);
            dot.texture = Circle();
            var rt = dot.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(34f, 34f);
            rt.anchoredPosition = cl;
            _dots++;
        }

        // ===================== UI =====================

        private void BuildUi()
        {
            _canvasGo = new GameObject("Latte Art Canvas");
            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 355;
            var scaler = _canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var dim = UguiUtil.Rect(_canvasGo.transform, "Dim", new Color(0f, 0f, 0f, 0.55f));
            ((RectTransform)dim.transform).Anchor(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            var title = UguiUtil.Label(_canvasGo.transform, "Title", 34, TextAnchor.UpperCenter);
            title.fontStyle = FontStyle.Bold; title.color = Ink;
            ((RectTransform)title.transform).Anchor(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(900f, 50f));
            title.text = "Латте-арт — води молоком по кофе";

            _cupImg = UguiUtil.Rect(_canvasGo.transform, "Cup", Coffee);
            _cupImg.texture = Circle();
            _cup = _cupImg.rectTransform;
            _cup.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(CupSize, CupSize));

            _dotsParent = (RectTransform)UguiUtil.Rect(_cup, "Dots", new Color(0, 0, 0, 0)).transform;
            _dotsParent.Anchor(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            // Шкала молока питчера — справа от круга.
            var track = UguiUtil.Rect(_canvasGo.transform, "MilkTrack", new Color(0f, 0f, 0f, 0.5f));
            ((RectTransform)track.transform).Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(CupSize * 0.5f + 110f, 30f), new Vector2(32f, MilkTrackH));
            _milkFill = UguiUtil.Rect((RectTransform)track.transform, "Fill", new Color(0.95f, 0.93f, 0.86f));
            _milkFill.rectTransform.Anchor(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(28f, 0f));
            _milkText = UguiUtil.Label((RectTransform)track.transform, "L", 16, TextAnchor.UpperCenter);
            _milkText.color = Ink;
            ((RectTransform)_milkText.transform).Anchor(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(120f, 46f));

            Button("ОК", new Vector2(-130f, -CupSize * 0.5f - 80f), Apply, Gold);
            Button("Отмена", new Vector2(130f, -CupSize * 0.5f - 80f), Close, Wood);

            _canvasGo.SetActive(false);
        }

        private void Button(string label, Vector2 pos, Action act, Color color)
        {
            var bg = UguiUtil.Rect(_canvasGo.transform, "Btn_" + label, color);
            var rt = (RectTransform)bg.transform;
            rt.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(220f, 56f));
            var t = UguiUtil.Label(rt, "L", 22, TextAnchor.MiddleCenter);
            t.color = Ink; t.fontStyle = FontStyle.Bold; t.text = label;
            ((RectTransform)t.transform).Anchor(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _buttons.Add((rt, act));
        }

        private static Texture2D Circle()
        {
            if (_circle != null) return _circle;
            const int s = 128;
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[s * s];
            float rad = s * 0.5f;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = x - rad + 0.5f, dy = y - rad + 0.5f;
                    float a = Mathf.Clamp01((rad - Mathf.Sqrt(dx * dx + dy * dy)) / 2f);
                    px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            t.SetPixels32(px); t.Apply();
            _circle = t;
            return t;
        }
    }
}
