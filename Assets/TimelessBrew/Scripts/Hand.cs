using UnityEngine;

namespace TimelessBrew
{
    /// <summary>
    /// Рука игрока — единая модель взаимодействия (§4.1). Несёт ПРЕДМЕТ (Grabbable), ГОРСТЬ
    /// молотого кофе или ЩИПЦЫ с сахаром. Клик: взять/поставить/высыпать; удержание над целью —
    /// налив; удержание пустой рукой над мехами/ручкой — крутить (IHandHold).
    /// Постановка: на стол/полки (Surface) и на подносы (TraySurface — предмет прицепляется
    /// к подносу и едет вместе с ним). Мусорка: непустой сосуд опустошается, пустой предмет
    /// выбрасывается целиком (с предупреждением).
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

        /// <summary>Имя того, что под/в руке — для подсказки у курсора.</summary>
        public string CursorLabel { get; private set; }

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

            if (Carried != null && _pouring && h.vessel != null)
            {
                if (Carried.Vessel != null)
                    Carried.Vessel.PourInto(h.vessel, Time.deltaTime);
                else if (Carried.GetComponent<Spoon>() != null && h.vessel.kind == Vessel.Kind.Cezve)
                    h.vessel.SettleFoam(Time.deltaTime);
            }
            else if (Carried == null && !HasScoop && _sugarBowl == null && held && h.hold != null)
                h.hold.Hold(Time.deltaTime);

            if (scoopVisual != null) scoopVisual.SetActive(HasScoop);
        }

        // ===================== КЛИК =====================

        private void OnPress()
        {
            var h = Probe();

            // Щипцы с сахаром: клик по чашке/турке — кубик добавлен; любой другой клик — отмена.
            if (_sugarBowl != null)
            {
                if (h.vessel != null && (h.vessel.kind == Vessel.Kind.Cup || h.vessel.kind == Vessel.Kind.Cezve))
                    h.vessel.mix.sugar++;
                _sugarBowl.ReturnTongs();
                _sugarBowl = null;
                return;
            }

            if (Carried != null)
            {
                if (h.trash != null) { ThrowIntoTrash(); return; }

                var adder = Carried.GetComponent<Adder>();
                if (adder != null && h.vessel != null) { adder.Apply(h.vessel); return; }

                if (Carried.Vessel != null && h.vessel != null
                    && Carried.Vessel.PourType != Ingredient.None
                    && h.vessel.CanAccept(Carried.Vessel.PourType))
                { _pouring = true; return; }

                // Ложка над туркой — усаживаем пенку удержанием.
                if (Carried.GetComponent<Spoon>() != null && h.vessel != null && h.vessel.kind == Vessel.Kind.Cezve)
                { _pouring = true; return; }

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
                if (h.vessel != null && h.vessel.AddGrounds(ScoopGrind, ScoopRoast)) { DropScoop(); return; }
                return;
            }

            // ----- Пустая рука -----
            if (h.bell != null) { h.bell.Ring(); return; }                       // звоночек — отдать заказ

            if (h.sugar != null) { _sugarBowl = h.sugar; _sugarBowl.TakeTongs(); return; }   // щипцы из сахарницы

            if (h.fridge != null)                                                // баночка из холодильника
            {
                var jar = h.fridge.TakeJar();
                if (jar != null) Pick(jar);
                return;
            }

            // Зачерпнуть молотый — кликом по КОРПУСУ кофемолки (не по ручке: ручка — IHandHold).
            if (h.grinder != null && h.grinder.HasGrounds && h.hold == null)
            {
                h.grinder.TakeGrounds(out var g, out var r);
                HasScoop = true; ScoopGrind = g; ScoopRoast = r;
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
        }

        private void Place(in Hit h)
        {
            if (!h.surface) return;                   // ставим только на поверхность
            if (h.surfaceNormal.y < 0.5f) return;     // на стенки/наклонные грани не ставим

            // Поднос: предмет прицепляется и едет вместе с ним.
            var traySurf = h.surfaceCollider != null ? h.surfaceCollider.GetComponentInParent<TraySurface>() : null;
            if (traySurf != null && traySurf.tray != null && traySurf.tray != Carried)
                Carried.transform.SetParent(traySurf.tray.transform, true);

            Carried.transform.position = h.point;
            SetColliders(Carried, true);
            Carried.GetComponent<Spoon>()?.OnPlaced();   // ложка ложится горизонтально
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
            Carried = null;
            _pouring = false;
        }

        /// <summary>
        /// Мусорка: сперва выливается всё непустое (вкл. чашки НА подносе); предмет выбрасывается
        /// целиком только если он пуст И на нём не стоят другие предметы (поднос/стопка).
        /// </summary>
        private void ThrowIntoTrash()
        {
            // Содержимое всех сосудов в руке (сам предмет + прицепленные к нему) — в мусорку.
            bool emptiedSomething = false;
            foreach (var v in Carried.GetComponentsInChildren<Vessel>())
                if (!v.mix.IsEmpty) { v.Empty(); emptiedSomething = true; }
            if (emptiedSomething) return;   // предметы остаются в руке

            // На предмете стоят другие предметы — целиком не выкидываем (уникальные вещи не теряем).
            if (Carried.GetComponentsInChildren<Grabbable>().Length > 1)
            {
                Message("Сначала сними с него предметы!");
                return;
            }

            Message("Ты дурашка!");
            GameObject go = Carried.gameObject;
            Carried = null;
            _pouring = false;
            Destroy(go);
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
            if (_sugarBowl != null) { CursorLabel = "✋ Щипцы с сахаром"; return; }
            if (HasScoop) { CursorLabel = "✋ Молотый кофе"; return; }
            if (Carried != null) { CursorLabel = "✋ " + Name(Carried); return; }
            if (h.sugar != null) { CursorLabel = "Сахарница"; return; }
            if (h.fridge != null) { CursorLabel = "Холодильник — взять баночку"; return; }
            if (h.grab != null) { CursorLabel = Name(h.grab); return; }
            if (h.bell != null) { CursorLabel = "Звонок — отдать заказ"; return; }
            if (h.grinder != null) { CursorLabel = "Кофемолка"; return; }
            if (h.trash != null) { CursorLabel = "Мусорка"; return; }
            CursorLabel = null;
        }

        private static string Name(Grabbable g) => !string.IsNullOrEmpty(g.displayName) ? g.displayName : g.name;

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
