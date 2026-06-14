using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TimelessBrew;
using TimelessBrew.UI;

namespace TimelessBrew.EditorTools
{
    /// <summary>
    /// Сборщик сцены кухни. Меню «TimelessBrew → Build Kitchen Scene».
    ///
    /// Версия с МОДЕЛЯМИ: вместо примитивов используются FBX из Art/Models (экспорт пользователя
    /// из Blender). Каждая модель авто-нормализуется: масштаб до целевого размера, дно в точке
    /// родителя (пивот у основания), BoxCollider по габаритам. Если модель не найдена/не
    /// импортирована — фолбэк на примитив, сцена собирается всегда.
    ///
    /// Карта моделей (уточнена пользователем):
    ///   STOL_V2 — стол с полками;  tumba — КОФЕМОЛКА;  kofe — банка зёрен;  banki — банка молока;
    ///   pech — печь вместе с мехами (удержание по печи = качать);  CUP — кружка;  CUPSIZE — чашка
    ///   полукруглая;  rakovina — раковина (тумбочка под ней — примитив);  звонок — примитив.
    /// </summary>
    public static class CafeSceneBuilder
    {
        private const string Root = "Assets/TimelessBrew";
        private const string ModelsDir = Root + "/Art/Models";
        private const string GuestsDir = Root + "/Data/Guests";
        private const string ScenePath = Root + "/Scenes/Kitchen.unity";

        private static readonly Dictionary<Color, Material> _mats = new();
        private static Shader _lit;

        [MenuItem("TimelessBrew/Build Kitchen Scene", priority = 0)]
        public static void Build()
        {
            _mats.Clear();
            _lit = Shader.Find("Universal Render Pipeline/Lit");
            int itemLayer = EnsureLayer("Item");
            int surfaceLayer = EnsureLayer("Surface");
            var guests = GenerateGuests();
            BuildScene(itemLayer, surfaceLayer, guests);
            Debug.Log("[TimelessBrew] Сцена кухни собрана: " + ScenePath);
        }

        /// <summary>
        /// Правит ОТКРЫТУЮ сцену (без пересборки!): вешает MeshCollider'ы на весь меш стола и
        /// переводит его на слой Surface — после этого ставить предметы можно на ЛЮБУЮ
        /// горизонтальную часть стола, включая все полки. Старый невидимый TableSurface удаляется,
        /// чтобы не загораживал полки лучу.
        /// </summary>
        [MenuItem("TimelessBrew/Make Table Shelves Placeable", priority = 1)]
        public static void MakeTableShelvesPlaceable()
        {
            int surfaceLayer = EnsureLayer("Surface");

            GameObject table = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.parent == null && t.name.StartsWith("Table")) { table = t.gameObject; break; }
            if (table == null)
            {
                Debug.LogError("[TimelessBrew] Не нашёл корневой объект стола (имя начинается с 'Table').");
                return;
            }

            ApplyTableColliders(table, surfaceLayer);

            var oldBox = GameObject.Find("TableSurface");
            if (oldBox != null) Object.DestroyImmediate(oldBox);

            EditorSceneManager.MarkSceneDirty(table.scene);
            Debug.Log("[TimelessBrew] Полки стола теперь принимают предметы (mesh-коллайдеры, слой Surface). Сохрани сцену (Ctrl+S).");
        }

        /// <summary>MeshCollider на каждый меш стола + слой Surface: постановка работает по реальной геометрии.</summary>
        private static void ApplyTableColliders(GameObject table, int surfaceLayer)
        {
            int added = 0;
            foreach (var mf in table.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.GetComponent<Collider>() == null && mf.sharedMesh != null)
                {
                    mf.gameObject.AddComponent<MeshCollider>();
                    added++;
                }
                mf.gameObject.layer = surfaceLayer;
            }
            Debug.Log($"[TimelessBrew] Стол: добавлено {added} mesh-коллайдеров.");
        }

        // ===================== ГОСТИ (§1.6) =====================

        private static List<GuestSO> GenerateGuests()
        {
            EnsureFolder(GuestsDir);
            var list = new List<GuestSO>();

            OrderSpec O(string strength, bool milk, int sugar, string[] syrups, string[] spices)
                => new OrderSpec
                {
                    strengthNote = strength, roast = RoastLevel.Dark, grind = GrindSize.Powder, water = WaterType.Hot,
                    milk = milk, sugar = sugar,
                    syrups = new List<string>(syrups ?? new string[0]),
                    spices = new List<string>(spices ?? new string[0])
                };

            list.Add(G("bear", "Медведь", O("крепкий", false, 0, null, new[] { "cardamom" }), O("крепкий", true, 1, null, new[] { "star_anise" })));
            list.Add(G("squirrel", "Белка", O("средний", false, 0, new[] { "caramel" }, null), O("средний", true, 0, new[] { "caramel", "vanilla" }, null)));
            list.Add(G("goat", "Козёл", O("крепкий", false, 2, null, new[] { "saffron", "nutmeg" }), O("средний", false, 0, null, new[] { "star_anise", "lemon_zest" })));
            list.Add(G("dragon", "Дракон", O("крепкий", false, 1, null, new[] { "rosemary", "cardamom" }), O("средний", true, 0, new[] { "vanilla" }, new[] { "rosemary" }), secret: "rosemary"));
            list.Add(G("stork", "Аист", O("средний", false, 2, null, new[] { "cardamom" }), O("лёгкий", true, 0, new[] { "vanilla" }, new[] { "lavender" }), latteArt: true));
            list.Add(G("weasel", "Ласка", O("крепкий", true, 0, null, new[] { "cinnamon", "cardamom" }), O("средний", true, 0, null, new[] { "cinnamon", "nutmeg" })));
            list.Add(G("rat", "Крыса", O("крепкий", true, 0, null, new[] { "cocoa", "nutmeg" }), O("средний", true, 1, null, new[] { "cardamom", "cocoa" }), takeaway: true, secret: "cocoa"));

            AssetDatabase.SaveAssets();
            return list;
        }

        private static GuestSO G(string id, string name, OrderSpec m, OrderSpec e, bool takeaway = false, string secret = null, bool latteArt = false)
        {
            string path = $"{GuestsDir}/{name}.asset";
            var g = AssetDatabase.LoadAssetAtPath<GuestSO>(path);
            if (g == null) { g = ScriptableObject.CreateInstance<GuestSO>(); AssetDatabase.CreateAsset(g, path); }
            g.guestId = id; g.displayName = name; g.morning = m; g.evening = e;
            g.takeawayOnly = takeaway; g.secretSpice = secret; g.likesLatteArt = latteArt;
            EditorUtility.SetDirty(g);
            return g;
        }

        // ===================== СЦЕНА =====================

        private static void BuildScene(int itemLayer, int surfaceLayer, List<GuestSO> guests)
        {
            EnsureFolder(Root + "/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // --- Стол: STOL_V2 (с полками), масштаб подгоняется под ширину старого stol,
            //     чтобы зафиксированная камера пользователя кадрировала так же ---
            GameObject table = SpawnTable();
            Bounds b = GetBounds(table);
            Vector3 c = b.center;
            float ex = Mathf.Max(0.2f, b.extents.x);
            float ez = Mathf.Max(0.2f, b.extents.z);
            float top = b.max.y, groundY = b.min.y;
            float u = Mathf.Max(ex, ez);
            float itemY = top + 0.02f * u;
            Vector3 P(float dx, float dz) => new Vector3(c.x + dx * ex, itemY, c.z + dz * ez);

            // Поверхность постановки = реальная геометрия стола (включая все полки).
            ApplyTableColliders(table, surfaceLayer);

            // Пол
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = new Vector3(c.x, groundY, c.z);
            ground.transform.localScale = Vector3.one * (u * 1.6f);
            AssignMat(ground, new Color(0.2f, 0.22f, 0.26f));

            // Камера — зафиксированный пользователем ракурс (НЕ менять).
            var cam = Camera.main;
            if (cam == null) { var cg = new GameObject("Main Camera"); cg.tag = "MainCamera"; cam = cg.AddComponent<Camera>(); }
            cam.transform.position = new Vector3(0f, 1.061f, 1.938f);
            cam.transform.rotation = Quaternion.Euler(29.496f, 180f, 0f);
            cam.fieldOfView = 36f;
            if (cam.GetComponent<CameraSwivel>() == null) cam.gameObject.AddComponent<CameraSwivel>();   // FNAF-доворот
            var light = Object.FindFirstObjectByType<Light>();
            if (light != null) light.transform.rotation = Quaternion.Euler(50f, 200f, 0f);

            // --- Системы ---
            var sys = new GameObject("— Systems —").transform;
            Make(sys, "GameInput").gameObject.AddComponent<GameInput>();

            var hand = Make(sys, "Hand").gameObject.AddComponent<Hand>();
            Wire(hand, so =>
            {
                so.FindProperty("cam").objectReferenceValue = cam;
                so.FindProperty("itemMask").intValue = 1 << itemLayer;
                so.FindProperty("surfaceMask").intValue = 1 << surfaceLayer;
            });

            // Горсть молотого у курсора — модель совка (фолбэк: сфера).
            GameObject scoop = NewModelObject("ScoopVisual", Vector3.zero, "sovok", 0.1f * u, 0, withCollider: false);
            if (scoop == null)
            {
                scoop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                scoop.name = "ScoopVisual";
                scoop.transform.localScale = Vector3.one * 0.05f * u;
                DestroyCollider(scoop);
                AssignMat(scoop, new Color(0.3f, 0.2f, 0.12f));
            }
            hand.SetScoopVisual(scoop);

            var lib = Make(sys, "GuestLibrary").gameObject.AddComponent<GuestLibrary>();
            lib.guests = guests.ToArray();

            var counterPt = Make(sys, "CounterPoint");
            counterPt.position = new Vector3(c.x, groundY + 1f, c.z - (ez + 0.55f * u));
            var gservice = Make(sys, "GuestService").gameObject.AddComponent<GuestService>();
            gservice.counterPoint = counterPt;
            gservice.gameObject.AddComponent<DayClock>();   // часы рабочего дня (§4.4)

            // --- UI ---
            var ui = new GameObject("— UI —");
            ui.AddComponent<HudUI>();
            ui.AddComponent<CursorTooltip>();      // тестовый режим: подсказка у курсора
            ui.AddComponent<VesselPanel>();        // тестовый режим: текстовые панели состава
            ui.AddComponent<StoveGaugeUI>();       // датчик печи (оба режима)
            ui.AddComponent<HoverLabelUI>();       // обычный режим: подпись над предметом
            ui.AddComponent<VesselGaugeUI>();      // обычный режим: вертикальные шкалы у сосудов
            ui.AddComponent<SettingsGearUI>();     // шестерёнка настроек (режим UI/громкости/сложность)
            ui.AddComponent<NotebookUI>();         // дневник на Tab (заменяет книгу гостей)
            ui.AddComponent<ToastUI>();            // крупные всплывашки («Ты дурашка!», результат)
            ui.AddComponent<CheckReaderUI>();      // читалка чека слева при клике по приколотому чеку
            ui.AddComponent<LatteArtUI>();         // мини-игра латте-арта (клик питчером по готовому кофе)
            ui.AddComponent<DayClockUI>();         // часы дня + кнопки скорости времени
            ui.AddComponent<EndOfDayUI>();         // итоги смены в конце дня
            ui.AddComponent<TutorialController>(); // пошаговое обучение при первом запуске

            // ===================== ПРЕДМЕТЫ =====================
            var items = new GameObject("— Items —").transform;

            // ПЕЧЬ (pech — печь+мехи одной моделью). Удержание пустой рукой по печи = качать мехи.
            var stoveGo = NewModelObject("Stove", P(0.52f, -0.42f), "pech", 0.34f * u, itemLayer, withCollider: true, parent: items);
            if (stoveGo == null)
            {
                stoveGo = new GameObject("Stove");
                stoveGo.transform.SetParent(items, false);
                stoveGo.transform.position = P(0.52f, -0.42f);
                AddPrimitiveVisual(stoveGo, PrimitiveType.Cube, new Vector3(0.3f * u, 0.085f * u, 0.26f * u), new Color(0.22f, 0.22f, 0.25f), itemLayer, true);
            }
            var stove = stoveGo.AddComponent<Stove>();
            stoveGo.AddComponent<Bellows>().SetStove(stove);
            Bounds sb = GetBounds(stoveGo);
            var burner = Make(stoveGo.transform, "BurnerPoint");
            burner.position = new Vector3(sb.center.x, sb.max.y + 0.02f * u, sb.center.z);
            Surface("StoveTop", new Vector3(sb.center.x, sb.max.y, sb.center.z),
                new Vector3(Mathf.Max(sb.size.x, 0.2f * u), 0.02f * u, Mathf.Max(sb.size.z, 0.2f * u)), surfaceLayer);
            Wire(stove, so =>
            {
                so.FindProperty("burnerPoint").objectReferenceValue = burner;
                so.FindProperty("burnerRadius").floatValue = Mathf.Max(sb.size.x, sb.size.z) * 0.55f;
            });

            // КОФЕМОЛКА (tumba!) + невидимая ручка-коллайдер сверху.
            var grinderGo = NewModelObject("Grinder", P(-0.55f, -0.42f), "tumba", 0.26f * u, itemLayer, withCollider: true, parent: items);
            if (grinderGo == null)
            {
                grinderGo = new GameObject("Grinder");
                grinderGo.transform.SetParent(items, false);
                grinderGo.transform.position = P(-0.55f, -0.42f);
                AddPrimitiveVisual(grinderGo, PrimitiveType.Cube, new Vector3(0.12f * u, 0.16f * u, 0.12f * u), new Color(0.5f, 0.36f, 0.2f), itemLayer, true);
            }
            var gv = grinderGo.AddComponent<Vessel>(); gv.kind = Vessel.Kind.Grinder;
            var grinder = grinderGo.AddComponent<Grinder>();
            Bounds gb = GetBounds(grinderGo);
            var crankGo = new GameObject("GrinderCrank");
            crankGo.transform.SetParent(grinderGo.transform, false);
            crankGo.transform.position = new Vector3(gb.center.x, gb.max.y, gb.center.z);
            crankGo.layer = itemLayer;
            var crankCol = crankGo.AddComponent<SphereCollider>();
            crankCol.radius = 0.3f * Mathf.Max(gb.size.x, gb.size.z);
            crankGo.AddComponent<GrinderCrank>().SetGrinder(grinder);

            // МУСОРКА
            var trashGo = NewModelObject("Trash", P(-0.82f, 0.12f), "musorka", 0.16f * u, itemLayer, withCollider: true, parent: items);
            if (trashGo == null)
            {
                trashGo = new GameObject("Trash");
                trashGo.transform.SetParent(items, false);
                trashGo.transform.position = P(-0.82f, 0.12f);
                AddPrimitiveVisual(trashGo, PrimitiveType.Cylinder, new Vector3(0.12f * u, 0.18f * u, 0.12f * u), new Color(0.3f, 0.32f, 0.34f), itemLayer, true);
            }
            trashGo.AddComponent<Trash>();

            // ЗОНА ВЫДАЧИ: зелёная скатерть + поднос + звоночек (примитив).
            float matZ = -0.05f;
            Decor(PrimitiveType.Cube, new Vector3(c.x, top + 0.006f * u, c.z + matZ * ez), new Vector3(0.3f * u, 0.012f * u, 0.24f * u), new Color(0.2f, 0.5f, 0.25f));
            var matPt = Make(items, "ServeMatPoint");
            matPt.position = new Vector3(c.x, itemY, c.z + matZ * ez);
            Surface("ServeMat", new Vector3(c.x, top + 0.02f * u, c.z + matZ * ez), new Vector3(0.3f * u, 0.02f * u, 0.24f * u), surfaceLayer);
            // Поднос — полноценный предмет (берётся, стопкуется с другими подносами).
            var trayGo = NewModelObject("Podnos", new Vector3(c.x, top + 0.012f * u, c.z + matZ * ez), "podnos", 0.24f * u, itemLayer, withCollider: true, parent: items);
            if (trayGo != null)
            {
                var trayGrab = trayGo.AddComponent<Grabbable>();
                trayGrab.displayName = "Поднос";
                trayGrab.stackGroup = "tray";
            }
            var bellGo = new GameObject("Bell");
            bellGo.transform.SetParent(items, false);
            bellGo.transform.position = new Vector3(c.x + 0.22f * ex, itemY, c.z + matZ * ez);
            AddPrimitiveVisual(bellGo, PrimitiveType.Sphere, new Vector3(0.07f * u, 0.055f * u, 0.07f * u), new Color(0.85f, 0.7f, 0.2f), itemLayer, true);
            var bell = bellGo.AddComponent<Bell>();
            bell.matPoint = matPt; bell.serveRadius = 0.22f * u;

            // СОСУДЫ-МОДЕЛИ (пивот у дна, авто-масштаб).
            MakeVessel(items, "BeanJar", "Банка зёрен", Vessel.Kind.Jar, P(-0.66f, -0.08f), "kofe", 0.12f * u, itemLayer);
            MakeVessel(items, "Pan", "Сковорода", Vessel.Kind.Pan, P(-0.46f, -0.13f), "skovoroda", 0.2f * u, itemLayer);
            MakeVessel(items, "Kettle", "Чайник", Vessel.Kind.Kettle, P(0.7f, -0.1f), "chainik", 0.16f * u, itemLayer);
            MakeVessel(items, "Cezve", "Турка", Vessel.Kind.Cezve, P(0.4f, -0.08f), "turka", 0.13f * u, itemLayer);
            MakeVessel(items, "Pitcher", "Питчер", Vessel.Kind.Pitcher, P(-0.28f, -0.06f), "pitcher", 0.12f * u, itemLayer);
            MakeVessel(items, "Cup", "Чашка", Vessel.Kind.Cup, P(-0.16f, 0.18f), "CUP", 0.11f * u, itemLayer);
            MakeVessel(items, "Cup 2", "Чашка (пузатая)", Vessel.Kind.Cup, P(-0.34f, 0.2f), "CUPSIZE", 0.11f * u, itemLayer);

            // ЛОЖКА
            var spoonGo = NewModelObject("Spoon", P(0.16f, 0.18f), "lozhka", 0.14f * u, itemLayer, withCollider: true, parent: items);
            if (spoonGo == null)
            {
                spoonGo = new GameObject("Spoon");
                spoonGo.transform.SetParent(items, false);
                spoonGo.transform.position = P(0.16f, 0.18f);
                AddPrimitiveVisual(spoonGo, PrimitiveType.Capsule, new Vector3(0.028f * u, 0.16f * u, 0.028f * u), new Color(0.7f, 0.72f, 0.75f), itemLayer, true);
            }
            var spoonGrab = spoonGo.AddComponent<Grabbable>(); spoonGrab.displayName = "Ложка";
            spoonGo.AddComponent<Spoon>();

            // ХОЛОДИЛЬНАЯ КАМЕРА (morozilka) — справа от стола, на её верху банка молока.
            var frost = DecorModel("morozilka", new Vector3(c.x + ex + 0.3f * u, groundY, c.z), (top - groundY) * 1.05f);
            Vector3 milkPos;
            if (frost != null)
            {
                Bounds fb = GetBounds(frost);
                Surface("FrostTop", new Vector3(fb.center.x, fb.max.y, fb.center.z), new Vector3(fb.size.x, 0.02f * u, fb.size.z), surfaceLayer);
                milkPos = new Vector3(fb.center.x, fb.max.y + 0.01f * u, fb.center.z);
            }
            else milkPos = P(0.85f, -0.28f);
            MakeVessel(items, "MilkJar", "Молоко", Vessel.Kind.MilkJar, milkPos, "banki", 0.11f * u, itemLayer);

            // РАКОВИНА на тумбочке-примитиве — слева от стола (декор, без механики пока).
            float cabH = top - groundY;
            Vector3 cabPos = new Vector3(c.x - ex - 0.3f * u, groundY, c.z);
            var cab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cab.name = "SinkCabinet (тумбочка)";
            cab.transform.position = cabPos + Vector3.up * cabH * 0.5f;
            cab.transform.localScale = new Vector3(0.5f * u, cabH, 0.5f * u);
            AssignMat(cab, new Color(0.4f, 0.3f, 0.2f));
            Surface("SinkCabinetTop", new Vector3(cabPos.x, groundY + cabH, cabPos.z), new Vector3(0.5f * u, 0.02f * u, 0.5f * u), surfaceLayer);
            DecorModel("rakovina", new Vector3(cabPos.x, groundY + cabH, cabPos.z), 0.45f * u);

            // ДЕКОР: дуршлаг, ступка, блюдце, чекхолдер (механика — позже).
            DecorModel("durshlag", P(-0.78f, -0.32f), 0.15f * u);
            DecorModel("stupka", P(0.7f, 0.2f), 0.12f * u);
            DecorModel("bludse1", P(0.0f, 0.22f), 0.12f * u);
            DecorModel("zakazi", P(0.0f, -0.62f), 0.3f * u);

            // ДОБАВКИ: сахарница и сиропницы — модели; специи — кубики.
            float a = 0.07f * u;
            var adders = new (string disp, Adder.Kind kind, string id, Color col, string model, float size)[]
            {
                ("Сахар",           Adder.Kind.Sugar, "",           new Color(0.95f, 0.95f, 0.95f), "sahar",  0.11f * u),
                ("Сироп: карамель", Adder.Kind.Syrup, "caramel",    new Color(0.85f, 0.55f, 0.2f),  "syrup",  0.12f * u),
                ("Сироп: ваниль",   Adder.Kind.Syrup, "vanilla",    new Color(0.92f, 0.86f, 0.6f),  "syrup",  0.12f * u),
                ("Кардамон",        Adder.Kind.Spice, "cardamom",   new Color(0.55f, 0.7f, 0.4f),   null, a),
                ("Бадьян",          Adder.Kind.Spice, "star_anise", new Color(0.5f, 0.35f, 0.2f),   null, a),
                ("Шафран",          Adder.Kind.Spice, "saffron",    new Color(0.9f, 0.55f, 0.15f),  null, a),
                ("Мускат",          Adder.Kind.Spice, "nutmeg",     new Color(0.6f, 0.45f, 0.3f),   null, a),
                ("Цедра лимона",    Adder.Kind.Spice, "lemon_zest", new Color(0.9f, 0.85f, 0.3f),   null, a),
                ("Розмарин",        Adder.Kind.Spice, "rosemary",   new Color(0.3f, 0.55f, 0.3f),   null, a),
                ("Корица",          Adder.Kind.Spice, "cinnamon",   new Color(0.6f, 0.3f, 0.2f),    null, a),
                ("Лаванда",         Adder.Kind.Spice, "lavender",   new Color(0.6f, 0.5f, 0.8f),    null, a),
                ("Какао",           Adder.Kind.Spice, "cocoa",      new Color(0.4f, 0.25f, 0.15f),  null, a),
            };
            float[] cols = { 0.30f, 0.42f, 0.54f, 0.66f };
            float[] rows = { 0.22f, 0.12f, 0.02f };
            for (int i = 0; i < adders.Length; i++)
            {
                var ad = adders[i];
                Vector3 pos = P(cols[i % 4], rows[i / 4]);
                GameObject go = ad.model != null ? NewModelObject(ad.disp, pos, ad.model, ad.size, itemLayer, withCollider: true, parent: items) : null;
                if (go == null)
                {
                    go = new GameObject(ad.disp);
                    go.transform.SetParent(items, false);
                    go.transform.position = pos;
                    AddPrimitiveVisual(go, PrimitiveType.Cube, Vector3.one * ad.size, ad.col, itemLayer, true);
                }
                var grab = go.AddComponent<Grabbable>(); grab.displayName = ad.disp;
                go.AddComponent<Adder>().Configure(ad.kind, ad.id);
                if (ad.kind == Adder.Kind.Spice) grab.stackGroup = "spice";          // специи стопкуются
                if (ad.kind == Adder.Kind.Sugar) go.AddComponent<SugarBowl>();       // щипцы сахарницы
            }

            // Финальный слой механик (ручка из Cube.002, холодильник, конфорка-бокс, подносы) —
            // тот же код, что чинит и вручную расставленную сцену.
            SceneUpgrader.UpgradeOpenScene();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // ===================== СТОЛ =====================

        /// <summary>STOL_V2 с подгонкой ширины под старый stol (чтобы сохранить кадр камеры). Фолбэки: stol → куб.</summary>
        private static GameObject SpawnTable()
        {
            var v2 = LoadModel("STOL_V2");
            var v1 = LoadModel("stol");

            if (v2 != null)
            {
                var t = (GameObject)PrefabUtility.InstantiatePrefab(v2);
                t.name = "Table (STOL_V2)";

                if (v1 != null)
                {
                    // Меряем старый стол и подгоняем ширину нового под него.
                    var probe = (GameObject)PrefabUtility.InstantiatePrefab(v1);
                    float oldW = GetBounds(probe).size.x;
                    Object.DestroyImmediate(probe);

                    float newW = GetBounds(t).size.x;
                    if (newW > 0.0001f && oldW > 0.0001f)
                        t.transform.localScale *= oldW / newW;
                }

                // Дно нового стола — на y=0 (как у старого), центр в origin по XZ.
                Bounds nb = GetBounds(t);
                t.transform.position += new Vector3(-nb.center.x, -nb.min.y, -nb.center.z);
                return t;
            }

            if (v1 != null)
            {
                var t = (GameObject)PrefabUtility.InstantiatePrefab(v1);
                t.name = "Table (stol)";
                return t;
            }

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Table (fallback)";
            cube.transform.localScale = new Vector3(2.4f, 0.2f, 1.2f);
            return cube;
        }

        // ===================== МОДЕЛИ =====================

        private static GameObject LoadModel(string name)
            => AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelsDir}/{name}.fbx");

        /// <summary>
        /// Новый объект с моделью-визуалом: масштаб до targetMax по наибольшему габариту,
        /// дно модели в точке pos (пивот у основания), опционально BoxCollider по габаритам.
        /// null, если модель не найдена (вызывающий делает фолбэк на примитив).
        /// </summary>
        private static GameObject NewModelObject(string name, Vector3 pos, string modelName, float targetMax,
                                                 int layer, bool withCollider, Transform parent = null)
        {
            var asset = LoadModel(modelName);
            if (asset == null) return null;

            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = pos;

            var vis = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            vis.name = $"Visual ({modelName})";
            vis.transform.SetParent(go.transform, false);

            // Нормализация масштаба.
            Bounds wb = GetBounds(vis);
            float maxDim = Mathf.Max(wb.size.x, wb.size.y, wb.size.z);
            if (maxDim > 0.0001f) vis.transform.localScale *= targetMax / maxDim;

            // Дно модели — в точку родителя.
            wb = GetBounds(vis);
            vis.transform.position += go.transform.position - new Vector3(wb.center.x, wb.min.y, wb.center.z);

            SetLayer(go, layer);
            if (withCollider)
            {
                wb = GetBounds(vis);
                var box = go.AddComponent<BoxCollider>();
                box.center = wb.center - go.transform.position;
                box.size = wb.size;
            }
            return go;
        }

        /// <summary>Декоративная модель без логики и без слоя Item (null, если модели нет).</summary>
        private static GameObject DecorModel(string modelName, Vector3 pos, float targetMax)
            => NewModelObject("Decor (" + modelName + ")", pos, modelName, targetMax, 0, withCollider: false);

        private static void MakeVessel(Transform root, string name, string display, Vessel.Kind kind,
                                       Vector3 pos, string modelName, float targetMax, int itemLayer)
        {
            GameObject go = NewModelObject(name, pos, modelName, targetMax, itemLayer, withCollider: true, parent: root);
            if (go == null)
            {
                go = new GameObject(name);
                go.transform.SetParent(root, false);
                go.transform.position = pos;
                AddPrimitiveVisual(go, PrimitiveType.Cylinder, new Vector3(targetMax, targetMax, targetMax), new Color(0.7f, 0.7f, 0.7f), itemLayer, true);
            }
            var grab = go.AddComponent<Grabbable>(); grab.displayName = display;
            var v = go.AddComponent<Vessel>(); v.kind = kind;
        }

        // ===================== ПРИМИТИВЫ (фолбэк/служебное) =====================

        /// <summary>Дочерний примитив-визуал с пивотом у дна.</summary>
        private static void AddPrimitiveVisual(GameObject parent, PrimitiveType prim, Vector3 size, Color color, int itemLayer, bool collider)
        {
            var vis = GameObject.CreatePrimitive(prim);
            vis.name = "Visual";
            vis.transform.SetParent(parent.transform, false);
            Vector3 scale = (prim == PrimitiveType.Cylinder || prim == PrimitiveType.Capsule)
                ? new Vector3(size.x, size.y * 0.5f, size.z) : size;
            vis.transform.localScale = scale;
            vis.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
            SetLayer(vis, itemLayer);
            AssignMat(vis, color);
            if (!collider) DestroyCollider(vis);
        }

        private static void Decor(PrimitiveType prim, Vector3 center, Vector3 size, Color color)
        {
            var vis = GameObject.CreatePrimitive(prim);
            vis.name = "Decor";
            vis.transform.position = center;
            vis.transform.localScale = (prim == PrimitiveType.Cylinder || prim == PrimitiveType.Capsule)
                ? new Vector3(size.x, size.y * 0.5f, size.z) : size;
            DestroyCollider(vis);
            AssignMat(vis, color);
        }

        private static void Surface(string name, Vector3 pos, Vector3 scale, int surfaceLayer)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            DestroyRenderer(go);
            go.layer = surfaceLayer;
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.AddComponent<PlaceSurface>();
        }

        private static Transform Make(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void Wire(Object component, System.Action<SerializedObject> set)
        {
            var so = new SerializedObject(component);
            set(so);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material Mat(Color c)
        {
            if (_mats.TryGetValue(c, out var m)) return m;
            m = new Material(_lit != null ? _lit : Shader.Find("Standard"));
            m.SetColor("_BaseColor", c);
            m.color = c;
            _mats[c] = m;
            return m;
        }

        private static void AssignMat(GameObject go, Color c)
        {
            var mat = Mat(c);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
        }

        private static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayer(t.gameObject, layer);
        }

        private static void DestroyCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
        }

        private static void DestroyRenderer(GameObject go)
        {
            var r = go.GetComponent<MeshRenderer>();
            if (r != null) Object.DestroyImmediate(r);
        }

        private static Bounds GetBounds(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }

        private static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0) return existing;
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            var so = new SerializedObject(assets[0]);
            var layers = so.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                var el = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(el.stringValue))
                {
                    el.stringValue = name;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.SaveAssets();
                    return i;
                }
            }
            return 0;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
