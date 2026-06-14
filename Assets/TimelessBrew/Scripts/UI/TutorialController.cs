using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TimelessBrew.UI
{
    /// <summary>
    /// Пошаговое обучение (§ диздок, первый запуск). Строгий режим: на каждом шаге игроку доступны
    /// только нужные предметы (остальные «не видит» рука), цель подсвечивается контуром
    /// (<see cref="TutorialHighlighter"/>), снизу — баннер с инструкцией и кнопкой «Пропустить».
    /// На время обучения замораживается день (<see cref="DayClock"/>) и зовётся один обучающий гость.
    /// Запускается при первом запуске, если включён тумблер в настройках (<see cref="UiSettings"/>).
    /// </summary>
    public class TutorialController : MonoBehaviour
    {
        private static TutorialController _inst;

        private class Step
        {
            public string text;
            public Func<GameObject> highlight;          // что подсветить (null — ничего)
            public Func<HashSet<GameObject>> allow;     // что разрешено трогать на шаге
            public Func<bool> done;                     // условие завершения шага
            public Action onEnter;                      // действие при входе (напр. вызвать гостя)
        }

        private readonly List<Step> _steps = new();
        private int _i = -1;
        private bool _active;
        private HashSet<GameObject> _allowed = new();

        private TutorialHighlighter _hl;
        private DayClock _clock;
        private Hand _hand;
        private GuestService _service;
        private Chekholder _chekholder;
        private Grinder _grinder;
        private CameraSwivel _cam;
        private Check _check;

        // UI
        private GameObject _canvasGo;
        private Text _text;
        private RectTransform _skipBtn;
        private bool _subscribed;

        private static readonly Color Wood = new(0.12f, 0.09f, 0.06f, 0.92f);
        private static readonly Color Gold = new(0.85f, 0.66f, 0.28f);
        private static readonly Color Ink = new(0.97f, 0.92f, 0.8f);

        // ===================== ВНЕШНИЙ API (для Hand и Vessel) =====================

        /// <summary>Идёт строгое обучение — рука блокирует лишние цели, турка не «убегает».</summary>
        public static bool Active => _inst != null && _inst._active;

        /// <summary>Разрешено ли взаимодействовать с этим объектом на текущем шаге.</summary>
        public static bool Allows(Component c)
        {
            if (_inst == null || !_inst._active) return true;
            if (c == null) return true;
            return _inst.IsAllowedGO(c.gameObject);
        }

        private bool IsAllowedGO(GameObject go)
        {
            var t = go.transform;
            while (t != null)
            {
                if (_allowed.Contains(t.gameObject)) return true;
                t = t.parent;
            }
            return false;
        }

        // ===================== ЖИЗНЕННЫЙ ЦИКЛ =====================

        private void Awake() => _inst = this;

        private void OnDestroy()
        {
            if (_inst == this) _inst = null;
            if (GameInput.Instance != null) GameInput.Instance.OnActionPressed -= OnPress;
        }

        private void Start()
        {
            BuildUi();
            _hl = gameObject.AddComponent<TutorialHighlighter>();
            if (UiSettings.ShouldRunTutorial) Begin();
        }

        private void Begin()
        {
            _hand = Find<Hand>();
            _service = Find<GuestService>();
            _chekholder = Find<Chekholder>();
            _grinder = Find<Grinder>();
            _cam = Find<CameraSwivel>();
            _clock = Find<DayClock>();

            if (_clock == null) Debug.LogWarning("[Tutorial] DayClock не найден — день не будет заморожен на время обучения.");
            if (_service == null) Debug.LogWarning("[Tutorial] GuestService не найден — обучающий гость не появится.");
            if (_hand == null) Debug.LogWarning("[Tutorial] Hand не найден — обучение может работать некорректно.");

            BuildSteps();
            _active = true;
            if (_clock != null) _clock.Suspended = true;
            if (_canvasGo != null) _canvasGo.SetActive(true);
            _i = -1;
            Advance();
        }

        private void Update()
        {
            if (!_subscribed && GameInput.Instance != null)
            {
                GameInput.Instance.OnActionPressed += OnPress;
                _subscribed = true;
            }
            if (!_active || _i < 0 || _i >= _steps.Count) return;
            var s = _steps[_i];
            if (s.done != null && s.done()) Advance();
        }

        private void Advance()
        {
            _i++;
            if (_i >= _steps.Count) { Finish(); return; }
            var s = _steps[_i];
            s.onEnter?.Invoke();
            _allowed = s.allow != null ? (s.allow() ?? new HashSet<GameObject>()) : new HashSet<GameObject>();
            _allowed.Remove(null);
            if (_hl != null) _hl.SetTarget(s.highlight != null ? s.highlight() : null);
            if (_text != null) _text.text = $"<color=#d9a84a>Обучение · шаг {_i + 1} из {_steps.Count}</color>\n{s.text}";
        }

        private void Finish()
        {
            _active = false;
            if (_hl != null) _hl.SetTarget(null);
            if (_canvasGo != null) _canvasGo.SetActive(false);
            if (_clock != null) _clock.Suspended = false;
        }

        private void OnPress()
        {
            if (!_active || _skipBtn == null) return;
            Vector2 p = GameInput.Instance != null ? GameInput.Instance.Pointer : Vector2.zero;
            if (RectTransformUtility.RectangleContainsScreenPoint(_skipBtn, p, null)) Finish();
        }

        // ===================== ШАГИ =====================

        private void BuildSteps()
        {
            _steps.Clear();

            Add("Осмотрись на рабочем месте: поверни камеру клавишами <b>Q</b> и <b>E</b>.",
                null, () => Set(), () => _cam != null && _cam.HasRotated);

            Add("К стойке подошёл гость и оставил чек с заказом. Возьми чек со стойки (наведись и кликни).",
                null, () => Set(UnpinnedCheck()),
                () => _hand != null && _hand.Carried != null && _hand.Carried.GetComponent<Check>() != null,
                () => { CallTutorGuest(); _check = UnpinnedCheck(); });

            Add("Повесь чек на чекхолдер — наведись на него и кликни.",
                () => GO(_chekholder), () => Set(_chekholder, _check),
                () => _chekholder != null && _chekholder.PinnedCount > 0);

            Add("Кликни по чеку на чекхолдере, нажми «Сменить статус», чтобы поставить «В работе», и кликни по чеку ещё раз, чтобы закрыть его.",
                null, () => Set(PinnedCheck()),
                () => { var c = PinnedCheck(); return c != null && c.State != Check.Status.New && !CheckReaderUI.IsShowing(c); });

            Add("Сначала подготовь воду. Возьми чайник и удерживай ЛКМ над раковиной, чтобы набрать воду.",
                () => GO(V(Vessel.Kind.Kettle)), () => SetKinds2(Vessel.Kind.Kettle, Find<Sink>()),
                () => Any(Vessel.Kind.Kettle, v => v.mix.water > 0.001f));

            Add("Вскипяти воду: поставь чайник на печь и качай мехи (удерживай ЛКМ на мехах), пока вода не закипит.",
                () => GO(Find<Stove>()), () => SetKinds2(Vessel.Kind.Kettle, Find<Stove>(), Find<Bellows>()),
                () => Any(Vessel.Kind.Kettle, v => v.mix.waterType == WaterType.Hot));

            Add("Возьми банку с зёрнами и кликни ею по сковороде, удерживая ЛКМ, — зёрна насыплются.",
                () => GO(V(Vessel.Kind.Jar)), () => SetKinds(V(Vessel.Kind.Jar), Vessel.Kind.Pan),
                () => Any(Vessel.Kind.Pan, v => v.mix.beans > 0.001f));

            Add("Поставь сковороду на печь и качай мехи (удерживай ЛКМ на мехах), пока зёрна не обжарятся.",
                () => GO(Find<Stove>()), () => SetKinds2(Vessel.Kind.Pan, Find<Stove>(), Find<Bellows>()),
                () => Any(Vessel.Kind.Pan, v => v.mix.roast != RoastLevel.Raw));

            Add("Пересыпь обжаренные зёрна в дуршлаг: возьми сковороду и кликни по дуршлагу (удерживай ЛКМ).",
                () => GO(V(Vessel.Kind.Colander)), () => SetTwoKinds(Vessel.Kind.Pan, Vessel.Kind.Colander),
                () => Any(Vessel.Kind.Colander, v => v.mix.beans > 0.001f));

            Add("Просей шелуху: возьми дуршлаг и удерживай ЛКМ над раковиной, пока не просеется.",
                () => GO(Find<Sink>()), () => SetKinds2(Vessel.Kind.Colander, Find<Sink>()),
                () => Any(Vessel.Kind.Colander, v => v.mix.beans > 0.001f && v.mix.sifted));

            Add("Пересыпь зёрна в кофемолку: возьми дуршлаг и кликни по кофемолке (удерживай ЛКМ).",
                () => GO(_grinder), () => SetTwoKinds(Vessel.Kind.Colander, Vessel.Kind.Grinder),
                () => _grinder != null && _grinder.GetComponent<Vessel>().mix.beans > 0.001f);

            Add("Смели зёрна: удерживай ЛКМ на ручке кофемолки, пока помол не станет мелким, почти в пыль.",
                () => GO(_grinder), () => SetKinds2(Vessel.Kind.Grinder, Find<GrinderCrank>()),
                () => _grinder != null && _grinder.GrindAmount >= 0.75f);

            Add("Зачерпни молотый кофе кликом по корпусу кофемолки (не по ручке) и пересыпь его в турку, кликнув по ней.",
                () => GO(V(Vessel.Kind.Cezve)), () => SetTwoKinds(Vessel.Kind.Grinder, Vessel.Kind.Cezve),
                () => Any(Vessel.Kind.Cezve, v => v.mix.grounds > 0.001f));

            Add("Налей кипяток из чайника в турку: возьми чайник и кликни по турке (удерживай ЛКМ).",
                () => GO(V(Vessel.Kind.Cezve)), () => SetTwoKinds(Vessel.Kind.Kettle, Vessel.Kind.Cezve),
                () => Any(Vessel.Kind.Cezve, v => v.mix.water > 0.001f));

            Add("Свари кофе: поставь турку на печь и качай мехи. Следи за шкалой варки; если пенка поднимается — сбивай её ложкой. Когда варка дойдёт до конца, СНИМИ турку с печи (поставь на стол) — кофе будет готов.",
                () => GO(Find<Stove>()), () => SetKinds2(Vessel.Kind.Cezve, Find<Stove>(), Find<Bellows>(), Find<Spoon>()),
                () => Any(Vessel.Kind.Cezve, v => v.CoffeeReady));

            Add("Перелей готовый кофе в чашку: возьми турку и кликни по чашке (удерживай ЛКМ).",
                () => GO(V(Vessel.Kind.Cup)), () => SetTwoKinds(Vessel.Kind.Cezve, Vessel.Kind.Cup),
                () => Any(Vessel.Kind.Cup, v => v.mix.coffee > 0.05f));

            Add("Сначала поставь турку на стол. Затем собери подачу на зелёной скатерти: поставь поднос, на него блюдце, в блюдце — чашку с кофе. И позвони в звоночек, чтобы отдать заказ гостю.",
                () => GO(Find<Bell>()), ServeAllow,
                () => _service != null && _service.GuestCount == 0);
        }

        private void Add(string text, Func<GameObject> hi, Func<HashSet<GameObject>> allow, Func<bool> done, Action onEnter = null)
            => _steps.Add(new Step { text = text, highlight = hi, allow = allow, done = done, onEnter = onEnter });

        // ===================== ОБУЧАЮЩИЙ ГОСТЬ =====================

        private void CallTutorGuest()
        {
            if (_service == null) _service = Find<GuestService>();
            if (_service == null || _service.GuestCount > 0) return;
            _service.Call(MakeTutorGuest(), true);
        }

        private GuestSO MakeTutorGuest()
        {
            var g = ScriptableObject.CreateInstance<GuestSO>();
            g.displayName = "Гость";
            var lib = Find<GuestLibrary>();
            if (lib != null && lib.guests != null)
                foreach (var src in lib.guests)
                    if (src != null) { g.guestId = src.guestId; break; }
            g.morning = new OrderSpec();   // простой заказ — обучение засчитывается по факту подачи
            g.evening = new OrderSpec();
            return g;
        }

        // ===================== РЕЗОЛВЕРЫ =====================

        private static T Find<T>() where T : Component => UnityEngine.Object.FindFirstObjectByType<T>();
        private static GameObject GO(Component c) => c != null ? c.gameObject : null;

        private static Vessel V(Vessel.Kind k)
        {
            foreach (var v in Vessel.All) if (v != null && v.kind == k) return v;
            return null;
        }

        private static bool Any(Vessel.Kind k, Func<Vessel, bool> p)
        {
            foreach (var v in Vessel.All) if (v != null && v.kind == k && p(v)) return true;
            return false;
        }

        private static Check UnpinnedCheck()
        {
            foreach (var c in UnityEngine.Object.FindObjectsByType<Check>(FindObjectsSortMode.None))
                if (c != null && !c.Pinned) return c;
            return null;
        }

        private static Check PinnedCheck()
        {
            foreach (var c in UnityEngine.Object.FindObjectsByType<Check>(FindObjectsSortMode.None))
                if (c != null && c.Pinned) return c;
            return null;
        }

        private static HashSet<GameObject> Set(params Component[] comps)
        {
            var s = new HashSet<GameObject>();
            foreach (var c in comps) if (c != null) s.Add(c.gameObject);
            return s;
        }

        private static void AddKind(HashSet<GameObject> s, Vessel.Kind k)
        {
            foreach (var v in Vessel.All) if (v != null && v.kind == k) s.Add(v.gameObject);
        }

        /// <summary>Разрешить один сосуд-инструмент + все сосуды указанного вида.</summary>
        private static HashSet<GameObject> SetKinds(Vessel tool, Vessel.Kind k)
        {
            var s = Set(tool);
            AddKind(s, k);
            return s;
        }

        /// <summary>Разрешить все сосуды вида k + перечисленные компоненты.</summary>
        private static HashSet<GameObject> SetKinds2(Vessel.Kind k, params Component[] comps)
        {
            var s = Set(comps);
            AddKind(s, k);
            return s;
        }

        private static HashSet<GameObject> SetTwoKinds(Vessel.Kind a, Vessel.Kind b)
        {
            var s = new HashSet<GameObject>();
            AddKind(s, a);
            AddKind(s, b);
            return s;
        }

        private static HashSet<GameObject> ServeAllow()
        {
            var s = new HashSet<GameObject>();
            AddKind(s, Vessel.Kind.Cup);
            foreach (var g in UnityEngine.Object.FindObjectsByType<Grabbable>(FindObjectsSortMode.None))
                if (g != null && (g.displayName == "Блюдце" || g.stackGroup == "tray")) s.Add(g.gameObject);
            var bell = Find<Bell>();
            if (bell != null) s.Add(bell.gameObject);
            return s;
        }

        // ===================== UI =====================

        private void BuildUi()
        {
            _canvasGo = new GameObject("Tutorial Canvas");
            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 345;   // над HUD и читалкой чека (340), чтобы баннер был виден поверх
            var scaler = _canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = UguiUtil.Rect(_canvasGo.transform, "TutPanel", Wood);
            var prt = (RectTransform)panel.transform;
            prt.Anchor(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1180f, 132f));

            var stripe = UguiUtil.Rect(prt, "Stripe", Gold);
            ((RectTransform)stripe.transform).Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 5f));

            _text = UguiUtil.Label(prt, "Text", 26, TextAnchor.MiddleLeft);
            _text.color = Ink;
            _text.horizontalOverflow = HorizontalWrapMode.Wrap;
            _text.verticalOverflow = VerticalWrapMode.Overflow;
            ((RectTransform)_text.transform).Anchor(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(-95f, 0f), new Vector2(-230f, -18f));

            var skip = UguiUtil.Rect(prt, "Skip", new Color(0.42f, 0.2f, 0.15f));
            _skipBtn = (RectTransform)skip.transform;
            _skipBtn.Anchor(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(190f, 80f));
            var sl = UguiUtil.Label(_skipBtn, "L", 18, TextAnchor.MiddleCenter);
            sl.color = Ink; sl.fontStyle = FontStyle.Bold; sl.text = "Пропустить\nобучение";
            ((RectTransform)sl.transform).Anchor(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            ModalGuard.RegisterBlocker(_skipBtn);   // клик по «Пропустить» не уходит в кухню

            _canvasGo.SetActive(false);
        }
    }
}
