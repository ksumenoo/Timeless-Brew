using UnityEngine;
using TimelessBrew.Audio;
using TimelessBrew.UI;

namespace TimelessBrew
{
    /// <summary>
    /// Рука игрока — единая модель взаимодействия (§4.1). Несёт ПРЕДМЕТ (Grabbable), ГОРСТЬ
    /// молотого кофе или ЩИПЦЫ с сахаром. Клик: взять/поставить/высыпать; удержание над целью —
    /// налив; удержание пустой рукой над мехами/ручкой — крутить (IHandHold).
    /// Постановка: на стол/полки (Surface) и на подносы (TraySurface — предмет прицепляется
    /// к подносу и едет вместе с ним). Раковина: клик сосудом — набрать холодную воду,
    /// удержание дуршлагом — просеять шелуху. Мусорка: непустой сосуд опустошается; сам предмет
    /// выкинуть нельзя — он возвращается на место с репликой «Ты дурашка!» (защита от дураков).
    /// </summary>
    public class Hand : MonoBehaviour
    {
        [SerializeField] private Camera cam;
        [SerializeField] private LayerMask itemMask;
        [SerializeField] private LayerMask surfaceMask;
        [SerializeField] private float carryHeight = 0.06f;
        [SerializeField] private float followSmoothing = 18f;

        public Grabbable Carried { get; private set; }
        public bool HasScoop { get; private set; }
        public GrindSize ScoopGrind { get; private set; }
        public RoastLevel ScoopRoast { get; private set; }
        public bool ScoopSifted { get; private set; }

        /// <summary>Имя того, что под/в руке — для подсказки у курсора.</summary>
        public string CursorLabel { get; private set; }

        // Чистое имя и мировая точка наведённого объекта — для красивой подписи над ним (обычный UI).
        public string HoverName { get; private set; }
        public Vector3 HoverWorld { get; private set; }
        public bool HasHover { get; private set; }

        [Tooltip("Визуал горсти молотого кофе (совок). Пусто — найдётся по имени 'ScoopVisual'.")]
        [SerializeField] private GameObject scoopVisual;

        private bool _pouring;
        private SugarBowl _sugarBowl;    // щипцы с сахаром в руке (null — нет)
        private Transform _itemsRoot;    // куда возвращать предметы, снятые с подноса

        private void Awake()
        {
            if (cam == null) cam = Camera.main;
            var root = GameObject.Find("— Items —");
            _itemsRoot = root != null ? root.transform : null;

            // Фолбэк: объект совка сохранён выключенным, обычный Find его не видит.
            if (scoopVisual == null)
            {
                foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (t.name == "ScoopVisual") { scoopVisual = t.gameObject; break; }
            }
        }

        private void OnEnable()
        {
            if (GameInput.Instance != null)
            {
                GameInput.Instance.OnActionPressed += OnPress;
                GameInput.Instance.OnActionReleased += OnRelease;
            }
        }

        private void OnDisable()
        {
            if (GameInput.Instance != null)
            {
                GameInput.Instance.OnActionPressed -= OnPress;
                GameInput.Instance.OnActionReleased -= OnRelease;
            }
        }

        private void Start()
        {
            // Подстраховка, если GameInput появился позже руки.
            if (GameInput.Instance != null)
            {
                GameInput.Instance.OnActionPressed -= OnPress;
                GameInput.Instance.OnActionReleased -= OnRelease;
                GameInput.Instance.OnActionPressed += OnPress;
                GameInput.Instance.OnActionReleased += OnRelease;
            }
        }

        private void Update()
        {
            var h = Probe();
            UpdateCursorLabel(h);

            if (Carried != null) MoveCarried();

            bool held = GameInput.Instance != null && GameInput.Instance.ActionHeld;

            if (Carried != null && _pouring)
            {
                var spoon = Carried.GetComponent<Spoon>();
                if (spoon != null)
                {
                    // Ложка усаживает пенку: турка под курсором ИЛИ ближайшая рядом — не нужно точно
                    // целиться в маленькую турку (раньше курсор соскакивал и звук обрывался).
                    Vessel cz = (h.vessel != null && h.vessel.kind == Vessel.Kind.Cezve)
                        ? h.vessel : NearestCezve(Carried.transform.position, 0.8f);
                    if (cz != null)
                    {
                        // Звук помешивания/сбития пенки — пока работаем ложкой в турке (надёжно слышен).
                        AudioManager.Instance.Loop("spoon", Sfx.SpoonStir);
                        cz.SettleFoam(Time.deltaTime);
                    }
                }
                else if (h.vessel != null && Carried.Vessel != null)
                {
                    Ingredient ing = Carried.Vessel.PourType;
                    if (ing != Ingredient.None && h.vessel.CanAccept(ing))
                        AudioManager.Instance.Loop("pour", PourKey(ing));   // звук налива/сыпи, пока льём
                    Carried.Vessel.PourInto(h.vessel, Time.deltaTime);
                }
                else if (h.sink != null)
                {
                    // Чайник/турка у крана: продолжительный набор холодной воды.
                    if (Carried.Vessel != null && Carried.Vessel.CanFillWater)
                    {
                        AudioManager.Instance.Loop("sink", Sfx.SinkFill);
                        Carried.Vessel.FillWaterGradual(WaterType.Cold, Time.deltaTime);
                    }
                    else
                    {
                        // Дуршлаг над раковиной: трясём, шелуха улетает в слив (§4.2, этап 2).
                        var colander = Carried.GetComponent<Colander>();
                        if (colander != null)
                        {
                            if (Carried.Vessel != null && Carried.Vessel.mix.beans > 0.001f && !Carried.Vessel.mix.sifted)
                                AudioManager.Instance.Loop("sift", Sfx.Sift);
                            colander.Sift(Time.deltaTime);
                        }
                    }
                }
            }
            else if (Carried == null && !HasScoop && _sugarBowl == null && held && h.hold != null)
                h.hold.Hold(Time.deltaTime);

            if (scoopVisual != null) scoopVisual.SetActive(HasScoop);
        }

        // ===================== КЛИК =====================

        private void OnPress()
        {
            // Клик по интерактивному UI (шестерёнка) или при открытой модалке — не уходит в кухню.
            if (GameInput.Instance != null && ModalGuard.BlocksHand(GameInput.Instance.Pointer)) return;

            var h = Probe();

            // Щипцы с сахаром: клик по чашке/турке — кубик добавлен; любой другой клик — отмена.
            if (_sugarBowl != null)
            {
                if (h.vessel != null && (h.vessel.kind == Vessel.Kind.Cup || h.vessel.kind == Vessel.Kind.Cezve))
                {
                    h.vessel.mix.sugar++;
                    AudioManager.Instance.Play(Sfx.SugarCube);
                }
                _sugarBowl.ReturnTongs();
                _sugarBowl = null;
                return;
            }

            if (Carried != null)
            {
                // Чек в руке + клик по чекхолдеру — повесить чек.
                var carriedCheck = Carried.GetComponent<Check>();
                if (carriedCheck != null && h.chekholder != null)
                {
                    if (h.chekholder.Pin(carriedCheck)) { SetColliders(Carried, true); Carried = null; _pouring = false; return; }
                }

                if (h.trash != null) { ThrowIntoTrash(); return; }

                // Раковина: набрать холодную воду (чайник/турка) или начать просеивание дуршлага.
                // Набор воды теперь ПРОДОЛЖИТЕЛЬНЫЙ — удержанием (как налив), а не за один клик.
                if (h.sink != null)
                {
                    if (Carried.Vessel != null && Carried.Vessel.CanFillWater) { _pouring = true; return; }
                    if (Carried.GetComponent<Colander>() != null) { _pouring = true; return; }
                }

                var adder = Carried.GetComponent<Adder>();
                if (adder != null && h.vessel != null) { adder.Apply(h.vessel); return; }

                // Латте-арт (§4.2 этап 5): клик ПИТЧЕРОМ с молоком по чашке с ГОТОВЫМ кофе — мини-игра.
                // Именно так молоко попадает в кофейную чашку (а не обычным наливом).
                if (Carried.Vessel != null && Carried.Vessel.kind == Vessel.Kind.Pitcher && Carried.Vessel.mix.milk > 0.001f
                    && h.vessel != null && h.vessel.kind == Vessel.Kind.Cup && h.vessel.mix.coffee > 0.05f)
                { LatteArtUI.Open(h.vessel, Carried.Vessel); return; }

                if (Carried.Vessel != null && h.vessel != null
                    && Carried.Vessel.PourType != Ingredient.None
                    && h.vessel.CanAccept(Carried.Vessel.PourType))
                { _pouring = true; return; }

                // Ложка над туркой (или рядом с ней) — усаживаем пенку удержанием.
                if (Carried.GetComponent<Spoon>() != null)
                {
                    bool overCezve = h.vessel != null && h.vessel.kind == Vessel.Kind.Cezve;
                    if (overCezve || NearestCezve(Carried.transform.position, 0.8f) != null) { _pouring = true; return; }
                }

                // Стопка: предметы одной группы (специи, подносы) ставятся друг на друга.
                if (h.grab != null && !string.IsNullOrEmpty(Carried.stackGroup)
                    && Carried.stackGroup == h.grab.stackGroup)
                { StackOnto(h.grab); return; }

                Place(h);
                return;
            }

            if (HasScoop)
            {
                if (h.trash != null) { DropScoop(); return; }
                if (h.vessel != null && h.vessel.AddGrounds(ScoopGrind, ScoopRoast, ScoopSifted)) { DropScoop(); return; }
                return;
            }

            // ----- Пустая рука -----
            if (h.check != null && h.check.Pinned) { h.check.ToggleReader(); return; }   // приколотый чек — читалка

            if (h.bell != null) { h.bell.Ring(); return; }                       // звоночек — отдать заказ

            if (h.sugar != null) { _sugarBowl = h.sugar; _sugarBowl.TakeTongs(); AudioManager.Instance.Play(Sfx.SugarOpen); return; }   // щипцы из сахарницы

            if (h.fridge != null)                                                // баночка из холодильника
            {
                var jar = h.fridge.TakeJar();
                if (jar != null) Pick(jar);
                return;
            }

            // Зачерпнуть молотый — кликом по КОРПУСУ кофемолки (не по ручке: ручка — IHandHold).
            if (h.grinder != null && h.grinder.HasGrounds && h.hold == null)
            {
                h.grinder.TakeGrounds(out var g, out var r, out var s);
                HasScoop = true; ScoopGrind = g; ScoopRoast = r; ScoopSifted = s;
                return;
            }

            if (h.grab != null && h.grab.pickable) Pick(h.grab);
        }

        private void OnRelease() => _pouring = false;

        // ===================== ДЕЙСТВИЯ =====================

        private void Pick(Grabbable g)
        {
            Carried = g;
            g.transform.SetParent(_itemsRoot, true);   // если был на подносе — отцепляем
            SetColliders(g, false);
            g.GetComponent<Spoon>()?.OnPicked();       // ложка в руке встаёт вертикально
            AudioManager.Instance.Play(ItemSoundKey(g, placing: false));
        }

        private void Place(in Hit h)
        {
            if (!h.surface) return;                   // ставим только на поверхность
            if (h.surfaceNormal.y < 0.5f) return;     // на стенки/наклонные грани не ставим

            // Поднос/блюдце: предмет прицепляется и едет вместе с ним.
            var traySurf = h.surfaceCollider != null ? h.surfaceCollider.GetComponentInParent<TraySurface>() : null;
            Vector3 pos = h.point;
            bool magnet = false;
            if (traySurf != null && traySurf.tray != null && traySurf.tray != Carried)
            {
                Carried.transform.SetParent(traySurf.tray.transform, true);
                // Магнитим чашку к ЦЕНТРУ блюдца. Считаем по РЕАЛЬНЫМ габаритам (пивоты моделей смещены).
                if (traySurf.tray.displayName == "Блюдце")
                {
                    Bounds sb = RendererBoundsOf(traySurf.tray.gameObject);
                    var sc = traySurf.GetComponent<Collider>();
                    float topY = sc != null ? sc.bounds.max.y : sb.center.y;
                    pos = new Vector3(sb.center.x, topY, sb.center.z);
                    magnet = true;
                }
            }

            Carried.transform.position = pos;
            // Выравниваем НИЗ-ЦЕНТР габаритов чашки к точке магнита (чтобы не «парила» из-за пивота).
            if (magnet)
            {
                Bounds cb = RendererBoundsOf(Carried.gameObject);
                if (cb.size != Vector3.zero)
                    Carried.transform.position += pos - new Vector3(cb.center.x, cb.min.y, cb.center.z);
            }
            SetColliders(Carried, true);
            Carried.GetComponent<Spoon>()?.OnPlaced();   // ложка ложится горизонтально
            AudioManager.Instance.Play(ItemSoundKey(Carried, placing: true));
            Carried = null;
            _pouring = false;
        }

        /// <summary>Поставить несомый предмет НА другой предмет той же стопочной группы.</summary>
        private void StackOnto(Grabbable target)
        {
            var cols = target.GetComponentsInChildren<Collider>();
            if (cols.Length == 0) return;
            Bounds tb = cols[0].bounds;
            for (int i = 1; i < cols.Length; i++) tb.Encapsulate(cols[i].bounds);

            Carried.transform.SetParent(target.transform, true);   // стопка едет вместе с нижним
            Carried.transform.position = new Vector3(tb.center.x, tb.max.y, tb.center.z);
            SetColliders(Carried, true);
            AudioManager.Instance.Play(ItemSoundKey(Carried, placing: true));
            Carried = null;
            _pouring = false;
        }

        /// <summary>
        /// Мусорка: сперва выливается всё непустое (вкл. чашки НА подносе). Сам предмет выкинуть
        /// нельзя — он возвращается на своё место со старта сцены, а сверху пишется «Ты дурашка!»
        /// (защита от дураков: уникальный инвентарь не теряется).
        /// </summary>
        private void ThrowIntoTrash()
        {
            // Содержимое всех сосудов в руке (сам предмет + прицепленные к нему) — в мусорку.
            bool emptiedSomething = false;
            foreach (var v in Carried.GetComponentsInChildren<Vessel>())
                if (!v.mix.IsEmpty) { v.Empty(); emptiedSomething = true; }
            if (emptiedSomething) { AudioManager.Instance.Play(Sfx.Trash); return; }   // предметы остаются в руке

            // На предмете стоят другие предметы — сперва разобрать стопку/поднос.
            if (Carried.GetComponentsInChildren<Grabbable>().Length > 1)
            {
                Message("Сначала сними с него предметы!");
                return;
            }

            var g = Carried;
            Carried = null;
            _pouring = false;
            SetColliders(g, true);
            g.ReturnHome();
            g.GetComponent<Spoon>()?.OnPlaced();   // ложка дома лежит, а не стоит
            ToastUI.Toast("Ты дурашка!", ToastUI.Bad, 2f);   // крупно и красным сверху
        }

        private static void Message(string text)
        {
            var service = FindFirstObjectByType<GuestService>();
            if (service != null) service.Message(text);
        }

        private void MoveCarried()
        {
            bool ok = SurfaceCast(out RaycastHit hit);
            Vector3 target = ok ? hit.point + Vector3.up * carryHeight : Carried.transform.position;
            Carried.transform.position = followSmoothing <= 0f
                ? target
                : Vector3.Lerp(Carried.transform.position, target, followSmoothing * Time.deltaTime);
        }

        private void DropScoop() => HasScoop = false;

        private void UpdateCursorLabel(in Hit h)
        {
            HasHover = false;   // подпись над объектом — только при наведении пустой рукой

            if (_sugarBowl != null) { CursorLabel = "✋ Щипцы с сахаром"; return; }
            if (HasScoop) { CursorLabel = "✋ Молотый кофе"; return; }
            if (Carried != null)
            {
                string label = "✋ " + Name(Carried);
                if (h.sink != null)
                {
                    if (Carried.Vessel != null && Carried.Vessel.CanFillWater) label += "  →  зажми: набрать воду";
                    else if (Carried.GetComponent<Colander>() != null) label += "  →  зажми: просеять";
                }
                CursorLabel = label;
                return;
            }
            if (h.sugar != null) { SetHover("Сахарница", h.sugar.transform); CursorLabel = "Сахарница"; return; }
            if (h.fridge != null) { SetHover("Холодильник", h.fridge.transform); CursorLabel = "Холодильник — взять баночку"; return; }
            if (h.grab != null) { SetHover(Name(h.grab), h.grab.transform); CursorLabel = Name(h.grab); return; }
            if (h.bell != null) { SetHover("Звонок", h.bell.transform); CursorLabel = "Звонок — отдать заказ"; return; }
            if (h.grinder != null) { SetHover("Кофемолка", h.grinder.transform); CursorLabel = "Кофемолка"; return; }
            if (h.trash != null) { SetHover("Мусорка", h.trash.transform); CursorLabel = "Мусорка"; return; }
            if (h.sink != null) { SetHover("Раковина", h.sink.transform); CursorLabel = "Раковина"; return; }
            CursorLabel = null;
        }

        private void SetHover(string label, Transform t)
        {
            HoverName = label;
            HoverWorld = RendererTop(t);
            HasHover = true;
        }

        /// <summary>Габариты объекта по его рендерам (устойчиво к смещённым пивотам моделей).</summary>
        private static Bounds RendererBoundsOf(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }

        /// <summary>Верх-центр габаритов объекта в мире — над ним вешается подпись.</summary>
        private static Vector3 RendererTop(Transform t)
        {
            if (t == null) return Vector3.zero;
            var rends = t.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return t.position;
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return new Vector3(b.center.x, b.max.y, b.center.z);
        }

        private static string Name(Grabbable g) => !string.IsNullOrEmpty(g.displayName) ? g.displayName : g.name;

        /// <summary>Ближайшая турка к точке в пределах радиуса (для прощения прицела ложкой).</summary>
        private static Vessel NearestCezve(Vector3 p, float radius)
        {
            Vessel best = null;
            float bd = radius;
            foreach (var v in Vessel.All)
            {
                if (v == null || v.kind != Vessel.Kind.Cezve) continue;
                float d = Vector3.Distance(p, v.transform.position);
                if (d <= bd) { bd = d; best = v; }
            }
            return best;
        }

        /// <summary>Звук налива/сыпи по типу содержимого.</summary>
        private static Sfx PourKey(Ingredient ing) => ing switch
        {
            Ingredient.Beans => Sfx.PourBeans,
            Ingredient.Water => Sfx.PourWater,
            Ingredient.Coffee => Sfx.PourCoffee,
            Ingredient.Milk => Sfx.PourMilk,
            _ => Sfx.None
        };

        /// <summary>
        /// Звук «взять/поставить» по объекту (озвучка идёт по предметам). Файлы «Взять или поставить»
        /// (молоко/сковорода/банка) играют и при взятии, и при постановке; у чайника/чашки/подноса
        /// озвучена только постановка, взятие — тихо. Прочее при постановке — мягкий стук посуды.
        /// </summary>
        private static Sfx ItemSoundKey(Grabbable g, bool placing)
        {
            var v = g != null ? g.Vessel : null;
            if (v != null)
                switch (v.kind)
                {
                    case Vessel.Kind.MilkJar: return Sfx.TakePlaceMilk;
                    case Vessel.Kind.Pan: return placing ? Sfx.TakePlacePan : Sfx.None;   // только постановка
                    case Vessel.Kind.Jar: return Sfx.TakePlaceBeanJar;
                    case Vessel.Kind.Kettle: return placing ? Sfx.PlaceKettle : Sfx.None;
                    case Vessel.Kind.Cup: return placing ? Sfx.PlaceCupSaucer : Sfx.None;
                }
            if (g != null && g.stackGroup == "tray") return placing ? Sfx.PlaceTray : Sfx.None;
            if (g != null && g.displayName == "Блюдце") return placing ? Sfx.PlaceCupSaucer : Sfx.None;
            return placing ? Sfx.PlaceCupSaucer : Sfx.None;
        }

        // ===================== РЕЙКАСТ =====================

        private struct Hit
        {
            public Grabbable grab;
            public Vessel vessel;
            public IHandHold hold;
            public Trash trash;
            public Grinder grinder;
            public Bell bell;
            public SugarBowl sugar;
            public Fridge fridge;
            public Sink sink;
            public Chekholder chekholder;
            public Check check;
            public bool surface;
            public Vector3 point;
            public Vector3 surfaceNormal;
            public Collider surfaceCollider;
        }

        private Hit Probe()
        {
            var h = new Hit();
            if (SurfaceCast(out RaycastHit sh))
            {
                h.surface = true;
                h.point = sh.point;
                h.surfaceNormal = sh.normal;
                h.surfaceCollider = sh.collider;
            }
            else h.surfaceNormal = Vector3.up;

            if (Physics.Raycast(Ray(), out RaycastHit hit, 100f, itemMask, QueryTriggerInteraction.Ignore))
            {
                var col = hit.collider;
                h.grab = col.GetComponentInParent<Grabbable>();
                h.vessel = col.GetComponentInParent<Vessel>();
                h.hold = col.GetComponentInParent<IHandHold>();
                h.trash = col.GetComponentInParent<Trash>();
                h.grinder = col.GetComponentInParent<Grinder>();
                h.bell = col.GetComponentInParent<Bell>();
                h.sugar = col.GetComponentInParent<SugarBowl>();
                h.fridge = col.GetComponentInParent<Fridge>();
                h.sink = col.GetComponentInParent<Sink>();
                h.chekholder = col.GetComponentInParent<Chekholder>();
                h.check = col.GetComponentInParent<Check>();
                if (h.grab == Carried) { h.grab = null; h.vessel = null; }   // себя не считаем целью
            }
            return h;
        }

        private bool SurfaceCast(out RaycastHit hit)
            => Physics.Raycast(Ray(), out hit, 100f, surfaceMask, QueryTriggerInteraction.Ignore);

        private Ray Ray()
        {
            Vector2 p = GameInput.Instance != null
                ? GameInput.Instance.Pointer
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            return cam.ScreenPointToRay(p);
        }

        private static void SetColliders(Grabbable g, bool on)
        {
            foreach (var c in g.GetComponentsInChildren<Collider>()) c.enabled = on;
        }

        /// <summary>Куда поставить визуал горсти (вызывает сборщик сцены, опционально).</summary>
        public void SetScoopVisual(GameObject go) { scoopVisual = go; if (go != null) go.SetActive(false); }

        private void LateUpdate()
        {
            bool needSurface = (scoopVisual != null && HasScoop) || _sugarBowl != null;
            if (!needSurface) return;

            if (SurfaceCast(out RaycastHit hit))
            {
                Vector3 p = hit.point + Vector3.up * (carryHeight + 0.05f);
                if (scoopVisual != null && HasScoop) scoopVisual.transform.position = p;
                if (_sugarBowl != null) _sugarBowl.MoveTongs(p);
            }
        }
    }
}
