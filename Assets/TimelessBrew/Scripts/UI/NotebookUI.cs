using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TimelessBrew.UI
{
    /// <summary>
    /// Дневник/альбом на Tab (почти во весь экран). Листается стрелками ◀ ▶. Разделы:
    ///   • Оборудование — на каждой странице слева ЖИВОЙ 3D-предпросмотр предмета (рендерится с реальной
    ///     модели из сцены через отдельную камеру в RenderTexture, медленно вращается), справа — описание
    ///     и что с ним делать;
    ///   • Рецепты — заказы каждого гостя;
    ///   • Гости (последняя страница) — управление: вызвать гостя на утро/вечер.
    /// Пока дневник открыт — рука выключена. Клики/листание — вручную через GameInput (без EventSystem).
    /// </summary>
    public class NotebookUI : MonoBehaviour
    {
        private GuestLibrary _library;
        private GuestService _service;
        private Hand _hand;

        private GameObject _canvasGo;
        private bool _open, _subscribed;
        private int _page;

        // Раскладки: информационная страница (предпросмотр + текст) и страница «Гости».
        private GameObject _infoRoot, _guestRoot;
        private RawImage _preview;
        private RawImage _portrait;             // картинка зверя на рецепте — по высоте, без растяжения
        private AspectRatioFitter _portraitFit;
        private GameObject _previewPlaceholder;
        private GameObject _prevBtn, _nextBtn;   // стрелки ◀ ▶ — прячем на крайних страницах
        private Text _previewPlaceholderLabel, _infoTitle, _infoBody, _pageLabel;

        private readonly List<(RectTransform rect, Action act)> _buttons = new();

        // 3D-предпросмотр.
        private Transform _rig;
        private Camera _previewCam;
        private RenderTexture _rt;
        private Transform _snap;

        private static readonly Color Paper = new(0.93f, 0.87f, 0.74f, 1f);
        private static readonly Color PaperDark = new(0.84f, 0.76f, 0.6f, 1f);
        private static readonly Color Wood = new(0.20f, 0.14f, 0.09f, 1f);
        private static readonly Color Ink = new(0.16f, 0.1f, 0.05f, 1f);
        private static readonly Color Gold = new(0.62f, 0.45f, 0.18f, 1f);

        private class Entry { public string title, body; public Func<GameObject> model; public Texture2D portrait; public bool guestPage; }
        private readonly List<Entry> _pages = new();

        // ===================== ЖИЗНЕННЫЙ ЦИКЛ =====================

        private void Start()
        {
            _library = FindFirstObjectByType<GuestLibrary>();
            _service = FindFirstObjectByType<GuestService>();
            _hand = FindFirstObjectByType<Hand>();

            BuildPages();
            BuildPreviewRig();
            BuildUi();
        }

        private void Update()
        {
            if (!_subscribed && GameInput.Instance != null)
            {
                GameInput.Instance.OnToggleBook += Toggle;
                GameInput.Instance.OnActionPressed += OnPress;
                _subscribed = true;
            }
            if (_open && _snap != null) _snap.Rotate(0f, 28f * Time.unscaledDeltaTime, 0f, Space.Self);
        }

        private void OnDestroy()
        {
            if (_open) ModalGuard.Pop();
            if (GameInput.Instance != null)
            {
                GameInput.Instance.OnToggleBook -= Toggle;
                GameInput.Instance.OnActionPressed -= OnPress;
            }
            // Не копить висящие RenderTexture и риги при пересоздании/Stop-Play.
            if (_previewCam != null) _previewCam.targetTexture = null;
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
            if (_rig != null) Destroy(_rig.gameObject);
        }

        // ===================== СОДЕРЖАНИЕ =====================

        private void BuildPages()
        {
            // Оборудование (§6). Модель ищется в сцене — предпросмотр строится из её мешей.
            Add("Печь с мехами",
                "Кованая чугунная печь с мехами — источник тепла для всех процессов готовки. Раздув мехов поднимает жар; перебор раскаляет печь до предела, и пока она не остынет (~10 c), готовить нельзя.",
                "Удержание пустой рукой на мехах — раздуть огонь. Следи за круглым датчиком: белая зона — рабочий режим, края (красные) — мало огня или перегрев.",
                () => Obj(FindFirstObjectByType<Stove>()));
            Add("Сковорода",
                "Чугунная сковорода для сухой обжарки зёрен (этап 1). Заполняй не больше чем на четверть.",
                "Зажми банку с зёрнами над сковородой, поставь её на печь. Цвет зерна меняется: зелёный → жёлтый → коричневый → чёрный (брак). Сними, когда цвет совпал с эталоном.",
                () => FindVessel(Vessel.Kind.Pan));
            Add("Дуршлаг",
                "Медный дуршлаг для просеивания шелухи (этап 2). Непросеянная шелуха — минус звезда к оценке.",
                "Пересыпь обжаренные зёрна в дуршлаг, поднеси к раковине и зажми — потряси, шелуха улетит в слив.",
                () => VesselComp<Colander>());
            Add("Банка зёрен",
                "Стеклянная банка с зёрнами. В прототипе — бесконечный источник одного сорта.",
                "Зажми над сковородой, дуршлагом или кофемолкой, чтобы насыпать зёрна.",
                () => FindVessel(Vessel.Kind.Jar));
            Add("Кофемолка",
                "Ручная кофемолка (этап 3). Чем дольше крутишь ручку — тем мельче помол (до «в пыль»).",
                "Насыпь зёрна, зажми ручку пустой рукой — мели. Готовый молотый зачерпни рукой и пересыпь в турку.",
                () => VesselComp<Grinder>());
            Add("Турка",
                "Медная турка для варки кофе (этап 3). Следи за пенкой — убежит через край, будет брак.",
                "Пересыпь молотый, добавь воду (горячую из чайника или холодную из раковины), поставь на печь. В начале размешай ложкой. Готовый кофе разлей в чашку.",
                () => FindVessel(Vessel.Kind.Cezve));
            Add("Чайник",
                "Чайник для горячей воды (§6.7). Свистит, когда вода вскипела.",
                "Набери воду у раковины (удержание), поставь на печь. Дождись свиста — кипяток готов. Лей в турку.",
                () => FindVessel(Vessel.Kind.Kettle));
            Add("Ложка",
                "Длинная мешальная ложка (§6.8).",
                "Удержание над туркой — усадить поднимающуюся пенку (спасательная мера). В начале варки — один раз размешать.",
                () => Obj(FindFirstObjectByType<Spoon>()));
            Add("Сахарница",
                "Сахарница со щипцами (§6.9).",
                "Клик по сахарнице — достать щипцы с кубиком. Клик по чашке или турке — добавить кубик сахара.",
                () => Obj(FindFirstObjectByType<SugarBowl>()));
            Add("Сиропница",
                "Сироп для готового напитка (§6.11): карамель, ваниль и др.",
                "Возьми сиропницу, наведи на чашку, клик — добавить порцию сиропа.",
                () => FindSyrup());
            Add("Питчер",
                "Металлический молочник-питчер для латте-арта (§6.13).",
                "Налей в питчер молоко из баночки, затем лей в чашку с кофе, ведя струю по поверхности — рисуй узор.",
                () => FindVessel(Vessel.Kind.Pitcher));
            Add("Чашка и блюдце",
                "Чашка для подачи и блюдце-подставка под неё (§6.14). На блюдце чашка едет вместе с ним.",
                "Поставь чашку (можно на блюдце), налей кофе из турки, добавь молоко/сахар/сироп. Готовую — на зелёную скатерть и позвони в звоночек.",
                () => FindVessel(Vessel.Kind.Cup));
            Add("Баночка молока",
                "Баночка молока в холодильной камере (§6.12).",
                "Возьми баночку из холодильника, перелей молоко в питчер.",
                () => FindVessel(Vessel.Kind.MilkJar));
            Add("Мусорка",
                "Мусорка (§6.18). Защита от дураков: сам предмет не выкинешь.",
                "Поднеси сосуд и кликни по мусорке — содержимое выльется. Предмет вернётся на место с репликой.",
                () => Obj(FindFirstObjectByType<Trash>()));

            // Правила подачи (текстовая страница без модели).
            Add("Правила подачи",
                "<b>Какая чашка:</b>\n" +
                "•  Обычная — только кофе и специи.\n" +
                "•  Пузатая — молоко и сиропы.\n\n" +
                "<b>Сборка:</b>  поднос → блюдце → чашка.\n\n" +
                "<b>Латте-арт:</b>  клик питчером по чашке с готовым кофе. Без узора — просто молоко.\n\n" +
                "<b>Чек:</b>  повесь на чекхолдер, клик — открыть; кнопка меняет статус.",
                (Func<GameObject>)null);

            // Рецепты гостей.
            if (_library != null && _library.guests != null)
                foreach (var g in _library.guests)
                {
                    if (g == null) continue;
                    string secret = !string.IsNullOrEmpty(g.secretSpice)
                        ? $"Секрет: {Mixture.NameRu(g.secretSpice)}\n" : (g.likesLatteArt ? "Любит латте-арт\n" : "");
                    Add($"Рецепт · {g.displayName}",
                        $"{secret}\n<b>Утро:</b>\n{g.morning.Summary()}\n\n<b>Вечер:</b>\n{g.evening.Summary()}",
                        null);
                    // Картинка зверя на странице рецепта (статичная, без вращения).
                    if (!string.IsNullOrEmpty(g.guestId))
                        _pages[_pages.Count - 1].portrait = Resources.Load<Texture2D>("Characters/" + g.guestId);
                }

            _pages.Add(new Entry { guestPage = true, title = "Гости" });
        }

        private void Add(string title, string desc, string usage, Func<GameObject> model)
            => _pages.Add(new Entry { title = title, body = desc + "\n\n<b>Как пользоваться:</b>\n" + usage, model = model });

        private void Add(string title, string body, Func<GameObject> model)
            => _pages.Add(new Entry { title = title, body = body, model = model });

        private static GameObject Obj(Component c) => c != null ? c.gameObject : null;
        private static GameObject FindVessel(Vessel.Kind k)
        {
            foreach (var v in Vessel.All) if (v != null && v.kind == k) return v.gameObject;
            return null;
        }
        private static GameObject VesselComp<T>() where T : Component
        {
            var c = FindFirstObjectByType<T>();
            return c != null ? c.gameObject : null;
        }
        private static GameObject FindSyrup()
        {
            foreach (var a in FindObjectsByType<Adder>(FindObjectsSortMode.None))
                if (a.AdderKind == Adder.Kind.Syrup) return a.gameObject;
            return null;
        }

        // ===================== 3D-ПРЕДПРОСМОТР =====================

        private void BuildPreviewRig()
        {
            _rig = new GameObject("— Notebook Preview Rig —").transform;
            _rig.position = new Vector3(6000f, 6000f, 6000f);   // далеко от кухни — обе камеры не мешают

            var camGo = new GameObject("NotebookPreviewCam");
            camGo.transform.SetParent(_rig, false);
            camGo.transform.localPosition = new Vector3(0f, 0.3f, -3.5f);   // дальше — модель в кадре с запасом
            camGo.transform.localRotation = Quaternion.Euler(7f, 0f, 0f);
            _previewCam = camGo.AddComponent<Camera>();
            _previewCam.clearFlags = CameraClearFlags.SolidColor;
            _previewCam.backgroundColor = new Color(0.22f, 0.16f, 0.1f, 1f);
            _previewCam.fieldOfView = 32f;
            _previewCam.nearClipPlane = 0.03f;
            _previewCam.farClipPlane = 10f;     // кухня в 6000 ед. — гарантированно вне поля
            _previewCam.enabled = false;

            // ВАЖНО: точечный свет, а не направленный — направленный осветил бы и кухню (он глобален).
            // Точечный на 6000 ед. с конечным радиусом до кухни не достаёт.
            var lightGo = new GameObject("NotebookPreviewLight");
            lightGo.transform.SetParent(_rig, false);
            lightGo.transform.localPosition = new Vector3(1.4f, 2.0f, -2.2f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 16f;
            light.intensity = 4.5f;

            _rt = new RenderTexture(720, 720, 16) { name = "NotebookPreviewRT" };
            _previewCam.targetTexture = _rt;
        }

        /// <summary>Построить предпросмотр предмета: снимок его мешей (без скриптов/коллайдеров), центр+масштаб.</summary>
        private void SetPreview(Func<GameObject> finder)
        {
            if (_snap != null) { Destroy(_snap.gameObject); _snap = null; }

            GameObject src = finder?.Invoke();
            if (src == null) { ShowPreview(false); return; }

            var snap = new GameObject("Snap").transform;
            snap.SetParent(_rig, false);

            var filters = src.GetComponentsInChildren<MeshFilter>();
            var bounds = new Bounds();
            bool any = false;
            foreach (var mf in filters)
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mf.sharedMesh == null || mr == null || !mr.enabled) continue;
                var go = new GameObject(mf.name);
                go.transform.SetParent(snap, true);
                go.transform.SetPositionAndRotation(mf.transform.position, mf.transform.rotation);
                go.transform.localScale = mf.transform.lossyScale;
                go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = mr.sharedMaterials;

                Bounds wb = go.GetComponent<MeshRenderer>().bounds;
                if (!any) { bounds = wb; any = true; } else bounds.Encapsulate(wb);
            }
            if (!any) { Destroy(snap.gameObject); ShowPreview(false); return; }

            // Меши построены в координатах кухни (~0), а риг — в 6000. Переносим модель В риг,
            // центрируя её вокруг его начала, затем масштабируем снимок под кадр.
            Vector3 toRig = _rig.position - bounds.center;
            foreach (Transform child in snap) child.position += toRig;
            float maxDim = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            float scale = maxDim > 1e-4f ? 1.5f / maxDim : 1f;
            snap.localPosition = Vector3.zero;
            snap.localScale = Vector3.one * scale;
            snap.localRotation = Quaternion.Euler(0f, 150f, 0f);

            _snap = snap;
            _preview.texture = _rt;   // вернуть рендер-текстуру (могла быть заменена картинкой рецепта)
            ShowPreview(true);
        }

        /// <summary>Статичная картинка (зверь на рецепте): по высоте рамки, пропорции сохранены.</summary>
        private void SetPortrait(Texture2D tex)
        {
            if (_snap != null) { Destroy(_snap.gameObject); _snap = null; }
            _previewCam.enabled = false;
            _preview.gameObject.SetActive(false);
            _previewPlaceholder.SetActive(false);
            _portrait.texture = tex;
            _portraitFit.aspectRatio = tex != null && tex.height > 0 ? (float)tex.width / tex.height : 1f;
            _portrait.gameObject.SetActive(true);
        }

        private void ShowPreview(bool on)
        {
            _previewCam.enabled = on && _open;
            if (_preview != null) _preview.gameObject.SetActive(on);
            if (_portrait != null) _portrait.gameObject.SetActive(false);
            if (_previewPlaceholder != null) _previewPlaceholder.SetActive(!on);
        }

        // ===================== UI =====================

        private void BuildUi()
        {
            _canvasGo = new GameObject("Notebook Canvas");
            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            var scaler = _canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var dim = UguiUtil.Rect(_canvasGo.transform, "Dim", new Color(0.04f, 0.03f, 0.02f, 0.97f));
            ((RectTransform)dim.transform).Anchor(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            // Книга почти во весь экран.
            var book = UguiUtil.Rect(_canvasGo.transform, "Book", Wood);
            var brt = (RectTransform)book.transform;
            brt.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1680f, 940f));

            var paper = UguiUtil.Rect(brt, "Paper", Paper);
            ((RectTransform)paper.transform).Anchor(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-44f, -44f));
            var paperRt = (RectTransform)paper.transform;

            BuildInfoLayout(paperRt);
            BuildGuestLayout(paperRt);
            BuildNav(paperRt);

            _canvasGo.SetActive(false);
        }

        private void BuildInfoLayout(RectTransform paper)
        {
            _infoRoot = UguiUtil.Rect(paper, "Info", new Color(0, 0, 0, 0)).gameObject;
            var rt = (RectTransform)_infoRoot.transform;
            rt.Anchor(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-60f, -120f));
            rt.anchoredPosition = new Vector2(0f, 10f);

            // Левая половина — рамка предпросмотра.
            var frame = UguiUtil.Rect(rt, "Frame", Wood);
            ((RectTransform)frame.transform).Anchor(new Vector2(0, 0), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(8f, 0f), new Vector2(-40f, -20f));

            _preview = UguiUtil.Rect((RectTransform)frame.transform, "Preview", Color.white);
            ((RectTransform)_preview.transform).Anchor(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-16f, -16f));
            _preview.texture = _rt;

            // Картинка зверя (рецепты): высота = высоте рамки, ширина по пропорции (без растяжения вширь).
            _portrait = UguiUtil.Rect((RectTransform)frame.transform, "Portrait", Color.white);
            _portrait.rectTransform.Anchor(new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, -24f));
            _portraitFit = _portrait.gameObject.AddComponent<AspectRatioFitter>();
            _portraitFit.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
            _portrait.gameObject.SetActive(false);

            _previewPlaceholder = UguiUtil.Rect((RectTransform)frame.transform, "Placeholder", PaperDark).gameObject;
            ((RectTransform)_previewPlaceholder.transform).Anchor(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-16f, -16f));
            _previewPlaceholderLabel = UguiUtil.Label((RectTransform)_previewPlaceholder.transform, "L", 40, TextAnchor.MiddleCenter);
            _previewPlaceholderLabel.color = Gold; _previewPlaceholderLabel.fontStyle = FontStyle.Bold;
            ((RectTransform)_previewPlaceholderLabel.transform).Anchor(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            // Правая половина — текст.
            _infoTitle = UguiUtil.Label(rt, "Title", 38, TextAnchor.UpperLeft);
            _infoTitle.fontStyle = FontStyle.Bold; _infoTitle.color = Ink;
            ((RectTransform)_infoTitle.transform).Anchor(new Vector2(0.5f, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(36f, -6f), new Vector2(-36f, 60f));

            _infoBody = UguiUtil.Label(rt, "Body", 23, TextAnchor.UpperLeft);
            _infoBody.color = Ink; _infoBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            ((RectTransform)_infoBody.transform).Anchor(new Vector2(0.5f, 0), new Vector2(1, 1), new Vector2(0, 1), new Vector2(36f, -80f), new Vector2(-48f, -90f));
        }

        private void BuildGuestLayout(RectTransform paper)
        {
            _guestRoot = UguiUtil.Rect(paper, "Guests", new Color(0, 0, 0, 0)).gameObject;
            var rt = (RectTransform)_guestRoot.transform;
            rt.Anchor(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-60f, -120f));
            rt.anchoredPosition = new Vector2(0f, 10f);

            var hint = UguiUtil.Label(rt, "Hint", 22, TextAnchor.UpperCenter);
            hint.color = Gold;
            ((RectTransform)hint.transform).Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0f, -2f), new Vector2(-20f, 34f));
            hint.text = "Вызови гостей — можно до 4 сразу. Tab — закрыть и готовить.";

            if (_library == null || _library.guests == null) return;
            float rowH = 84f, top = -52f;
            int n = _library.guests.Length;
            for (int i = 0; i < n; i++)
            {
                var g = _library.guests[i];
                if (g == null) continue;
                var row = UguiUtil.Rect(rt, "Row" + i, new Color(0f, 0f, 0f, i % 2 == 0 ? 0.06f : 0.02f));
                var rrt = (RectTransform)row.transform;
                rrt.Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0f, top - i * rowH), new Vector2(-12f, rowH - 8f));

                var info = UguiUtil.Label(rrt, "Info", 20, TextAnchor.MiddleLeft);
                info.color = Ink;
                ((RectTransform)info.transform).Anchor(new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), new Vector2(20f, 0f), new Vector2(-320f, 0f));
                info.text = $"<b>{g.displayName}</b>" + (string.IsNullOrEmpty(g.secretSpice) ? "" : $"   <color=#a06a18>секрет: {Mixture.NameRu(g.secretSpice)}</color>");

                NavButton(rrt, "Утро", new Vector2(-170f, 0f), new Vector2(1, 0.5f), () => CallGuest(g, true));
                NavButton(rrt, "Вечер", new Vector2(-50f, 0f), new Vector2(1, 0.5f), () => CallGuest(g, false));
            }
        }

        private void BuildNav(RectTransform paper)
        {
            _prevBtn = NavButton(paper, "◀", new Vector2(40f, 40f), new Vector2(0, 0), () => Flip(-1)).gameObject;
            _nextBtn = NavButton(paper, "▶", new Vector2(-40f, 40f), new Vector2(1, 0), () => Flip(1)).gameObject;

            _pageLabel = UguiUtil.Label(paper, "PageLabel", 22, TextAnchor.LowerCenter);
            _pageLabel.color = Gold; _pageLabel.fontStyle = FontStyle.Bold;
            ((RectTransform)_pageLabel.transform).Anchor(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0f, 18f), new Vector2(520f, 34f));

            var close = UguiUtil.Label(paper, "CloseHint", 18, TextAnchor.UpperRight);
            close.color = Gold;
            ((RectTransform)close.transform).Anchor(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-8f, -2f), new Vector2(260f, 28f));
            close.text = "Tab — закрыть";
        }

        private RectTransform NavButton(RectTransform parent, string label, Vector2 pos, Vector2 pivotAnchor, Action act)
        {
            var bg = UguiUtil.Rect(parent, "Btn_" + label + _buttons.Count, Gold);
            var rt = (RectTransform)bg.transform;
            rt.Anchor(pivotAnchor, pivotAnchor, pivotAnchor, pos, new Vector2(label.Length <= 2 ? 64f : 104f, 46f));
            var t = UguiUtil.Label(rt, "L", 22, TextAnchor.MiddleCenter);
            t.color = Paper; t.fontStyle = FontStyle.Bold;
            ((RectTransform)t.transform).Anchor(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            t.text = label;
            _buttons.Add((rt, act));
            return rt;
        }

        // ===================== ЛОГИКА =====================

        private void Toggle()
        {
            _open = !_open;
            if (_canvasGo != null) _canvasGo.SetActive(_open);
            if (_open) ModalGuard.Push(); else ModalGuard.Pop();
            if (_hand != null) _hand.enabled = !ModalGuard.AnyOpen;   // рука жива, лишь когда модалок нет
            if (_open) ShowPage(_page);
            else ShowPreview(false);   // выключаем камеру предпросмотра, чтобы не рендерить зря
        }

        private void Flip(int dir)
        {
            _page = Mathf.Clamp(_page + dir, 0, _pages.Count - 1);
            ShowPage(_page);
        }

        private void ShowPage(int i)
        {
            if (_pages.Count == 0) return;
            _page = Mathf.Clamp(i, 0, _pages.Count - 1);
            var e = _pages[_page];

            _infoRoot.SetActive(!e.guestPage);
            _guestRoot.SetActive(e.guestPage);
            _pageLabel.text = $"{e.title}      {_page + 1} / {_pages.Count}";

            // Стрелки прячем на краях: на первой странице нет ◀, на последней нет ▶.
            if (_prevBtn != null) _prevBtn.SetActive(_page > 0);
            if (_nextBtn != null) _nextBtn.SetActive(_page < _pages.Count - 1);

            if (e.guestPage) { ShowPreview(false); return; }

            _infoTitle.text = e.title;
            _infoBody.text = e.body;
            if (e.model != null) SetPreview(e.model);
            else if (e.portrait != null) SetPortrait(e.portrait);
            else
            {
                if (_snap != null) { Destroy(_snap.gameObject); _snap = null; }
                _previewPlaceholderLabel.text = e.title;
                ShowPreview(false);
            }
        }

        private void OnPress()
        {
            if (!_open) return;
            Vector2 p = GameInput.Instance != null ? GameInput.Instance.Pointer : Vector2.zero;
            foreach (var (rect, act) in _buttons)
                if (rect.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(rect, p, null))
                { act(); return; }
        }

        private void CallGuest(GuestSO g, bool morning)
        {
            // Дневник НЕ закрываем — можно вызвать несколько гостей подряд (до 4). Закрыть — Tab.
            if (_service != null) _service.Call(g, morning);
        }
    }
}
