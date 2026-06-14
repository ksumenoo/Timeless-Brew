using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TimelessBrew;
using TimelessBrew.Audio;
using TimelessBrew.UI;

namespace TimelessBrew.EditorTools
{
    /// <summary>
    /// Утилиты для ОТКРЫТОЙ (вручную расставленной) сцены — без пересборки и потери раскладки:
    ///   • Upgrade Open Scene — камера Q/E, стопки, щипцы-сахарница, холодильник, ручка кофемолки
    ///     (Cube.002), конфорка-бокс у печи, поверхности подносов;
    ///   • Save/Restore Layout Snapshot — JSON-снимок всех трансформов сцены (страховка раскладки).
    /// </summary>
    public static class SceneUpgrader
    {
        private const string SnapshotPath = "Assets/TimelessBrew/Data/layout-snapshot.json";

        // ===================== АПГРЕЙД СЦЕНЫ =====================

        [MenuItem("TimelessBrew/Upgrade Open Scene (механики)", priority = 2)]
        public static void UpgradeOpenScene()
        {
            // Защита от частой ошибки: апгрейд добавляет кухонные механики и HUD и предназначен ТОЛЬКО
            // для сцены кухни. Если открыто меню (в сцене нет руки игрока) — не засоряем его кухонным UI.
            if (Object.FindFirstObjectByType<Hand>() == null)
            {
                EditorUtility.DisplayDialog("Upgrade Open Scene",
                    "Похоже, открыта не сцена кухни (в сцене нет Hand).\n\n" +
                    "Эта команда добавляет кухонные механики и интерфейс (печь, шкалы, подсказки) и " +
                    "предназначена для Kitchen.unity. Откройте сцену кухни и повторите.\n\n" +
                    "Главное меню собирается отдельной командой: TimelessBrew → Build Main Menu.",
                    "Понятно");
                return;
            }

            int itemLayer = EnsureLayer("Item");
            int surfaceLayer = EnsureLayer("Surface");
            int changes = 0;

            // По-шаговый отчёт: сразу видно, какой блок не нашёл свои объекты в сцене.
            var report = new System.Text.StringBuilder("[TimelessBrew] Апгрейд сцены: ");
            void Step(string name, int c) { report.Append($"{name}={c}  "); changes += c; }

            Step("камера", UpgradeCamera());
            Step("добавки", UpgradeAdders());
            Step("подносы", UpgradeTrays(itemLayer, surfaceLayer));
            Step("холодильник", UpgradeFridge(itemLayer, surfaceLayer));
            Step("кофемолка", UpgradeGrinder(itemLayer));
            Step("печь", UpgradeStove(surfaceLayer));
            Step("мехи", UpgradeBellows(itemLayer));
            Step("раковина", UpgradeSink(itemLayer));
            Step("дуршлаг", UpgradeColander(itemLayer));
            Step("блюдца", UpgradeSaucers(itemLayer, surfaceLayer));
            Step("специи-пивот", UpgradeSpicePivots());
            Step("мусорка", UpgradeTrash());
            Step("звоночек", UpgradeBell(itemLayer));
            Step("чекхолдер", UpgradeChekholder(itemLayer));
            Step("скатерть", UpgradeServeMat());
            Step("музыка", UpgradeAudio());
            Step("часы дня", UpgradeDayClock());
            Step("интерфейс", UpgradeUI());

            MarkDirty();
            Debug.Log(report + $"— всего {changes}. Сохрани сцену (Ctrl+S).");
        }

        /// <summary>Камера: фиксированная, поворот по Q/E (CameraSwivel), шаг доворота ±35°.</summary>
        private static int UpgradeCamera()
        {
            var cam = Camera.main;
            if (cam == null) return 0;

            int changes = 0;
            var swivel = cam.GetComponent<CameraSwivel>();
            if (swivel == null) { swivel = cam.gameObject.AddComponent<CameraSwivel>(); changes++; }

            // Существующий компонент хранит старое значение шага в сцене — обновляем до 35°.
            var so = new SerializedObject(swivel);
            var yaw = so.FindProperty("maxYaw");
            if (yaw != null && !Mathf.Approximately(yaw.floatValue, 35f))
            {
                yaw.floatValue = 35f;
                so.ApplyModifiedPropertiesWithoutUndo();
                changes++;
            }
            return changes;
        }

        /// <summary>Специи — стопочная группа; сахарница — щипцы вместо переноски (SugarBowl).</summary>
        private static int UpgradeAdders()
        {
            int changes = 0;

            // Старый компонент щипцов больше не используется.
            foreach (var st in Object.FindObjectsByType<SugarTongs>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(st);
                changes++;
            }

            foreach (var adder in Object.FindObjectsByType<Adder>(FindObjectsSortMode.None))
            {
                var grab = adder.GetComponent<Grabbable>();
                if (grab == null) continue;

                if (adder.AdderKind == Adder.Kind.Spice && grab.stackGroup != "spice")
                {
                    grab.stackGroup = "spice";
                    changes++;
                }
                if (adder.AdderKind == Adder.Kind.Sugar)
                {
                    if (adder.GetComponent<SugarBowl>() == null)
                    {
                        adder.gameObject.AddComponent<SugarBowl>();
                        changes++;
                    }
                    if (grab.pickable) { grab.pickable = false; changes++; }
                }
            }
            return changes;
        }

        /// <summary>Подносы: предмет + стопка + поверхность TraySurface (предметы ездят с подносом).</summary>
        private static int UpgradeTrays(int itemLayer, int surfaceLayer)
        {
            int changes = 0;

            // Декоры podnos → полноценные предметы.
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (!t.name.ToLowerInvariant().Contains("podnos")) continue;
                if (t.GetComponentInParent<Grabbable>() != null) continue;
                // Берём только корень подноса: у дочернего 'Visual (podnos)' родитель тоже содержит 'podnos'.
                if (t.parent != null && t.parent.name.ToLowerInvariant().Contains("podnos")) continue;

                GameObject go = t.gameObject;
                var grab = go.AddComponent<Grabbable>();
                grab.displayName = "Поднос";
                grab.stackGroup = "tray";

                Bounds wb = RendererBounds(go);
                if (go.GetComponent<Collider>() == null && wb.size != Vector3.zero)
                {
                    var box = go.AddComponent<BoxCollider>();
                    box.center = wb.center - go.transform.position;
                    box.size = wb.size;
                }
                SetLayer(go, itemLayer);
                changes++;
            }

            // Поверхность на каждом подносе.
            foreach (var grab in Object.FindObjectsByType<Grabbable>(FindObjectsSortMode.None))
            {
                if (grab.stackGroup != "tray") continue;
                if (grab.transform.Find("TraySurface") != null) continue;

                Bounds wb = RendererBounds(grab.gameObject);
                var surfGo = new GameObject("TraySurface");
                surfGo.transform.SetParent(grab.transform, false);
                surfGo.transform.position = new Vector3(wb.center.x, wb.max.y, wb.center.z);
                surfGo.layer = surfaceLayer;
                var box = surfGo.AddComponent<BoxCollider>();
                box.size = new Vector3(wb.size.x, 0.01f, wb.size.z);
                surfGo.AddComponent<PlaceSurface>();
                surfGo.AddComponent<TraySurface>().tray = grab;
                changes++;
            }
            return changes;
        }

        /// <summary>Холодильник: кликабелен (баночка в руку), сверху можно ставить.</summary>
        private static int UpgradeFridge(int itemLayer, int surfaceLayer)
        {
            // Только КОРЕНЬ модели: у дочернего 'Visual (morozilka)' родитель тоже содержит 'morozilka'.
            Transform fridgeT = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (!t.name.ToLowerInvariant().Contains("morozilka")) continue;
                if (t.parent != null && t.parent.name.ToLowerInvariant().Contains("morozilka")) continue;
                fridgeT = t;
                break;
            }
            if (fridgeT == null) return 0;

            int changes = 0;
            GameObject go = fridgeT.gameObject;

            // Идемпотентность в обе стороны: Fridge мог оказаться и на родителе, и на ребёнке.
            if (go.GetComponentInParent<Fridge>() == null && go.GetComponentInChildren<Fridge>(true) == null)
            {
                Bounds wb = RendererBounds(go);
                if (go.GetComponent<Collider>() == null && wb.size != Vector3.zero)
                {
                    var box = go.AddComponent<BoxCollider>();
                    box.center = wb.center - go.transform.position;
                    box.size = wb.size;
                }
                go.AddComponent<Fridge>();
                SetLayer(go, itemLayer);
                changes++;
            }

            // Старый FrostTop (корневой, от сборщика) мог остаться не там, где холодильник.
            var stale = GameObject.Find("FrostTop");
            if (stale != null && stale.transform.parent == null)
            {
                Object.DestroyImmediate(stale);
                changes++;
            }

            // Поверхность на верху холодильника — дочерняя, едет вместе с ним. Ищем по всей иерархии.
            Transform existingTop = null;
            foreach (var tr in fridgeT.GetComponentsInChildren<Transform>(true))
                if (tr.name == "FridgeTop") { existingTop = tr; break; }

            // Лечим слой существующей поверхности (мог быть затёрт рекурсивным SetLayer).
            if (existingTop != null && existingTop.gameObject.layer != surfaceLayer)
            {
                existingTop.gameObject.layer = surfaceLayer;
                changes++;
            }

            if (existingTop == null)
            {
                Bounds wb = RendererBounds(go);
                var topGo = new GameObject("FridgeTop");
                topGo.transform.SetParent(fridgeT, false);
                topGo.transform.position = new Vector3(wb.center.x, wb.max.y, wb.center.z);
                topGo.layer = surfaceLayer;
                var box = topGo.AddComponent<BoxCollider>();
                box.size = new Vector3(wb.size.x, 0.01f, wb.size.z);
                topGo.AddComponent<PlaceSurface>();
                changes++;
            }
            return changes;
        }

        /// <summary>Кофемолка: ручкой становится меш Cube.002; корпус — точные mesh-коллайдеры.</summary>
        private static int UpgradeGrinder(int itemLayer)
        {
            var grinder = Object.FindFirstObjectByType<Grinder>();
            if (grinder == null) return 0;

            int changes = 0;
            GameObject go = grinder.gameObject;

            // Старая невидимая ручка-заглушка больше не нужна.
            var oldCrank = go.transform.Find("GrinderCrank");
            if (oldCrank != null) { Object.DestroyImmediate(oldCrank.gameObject); changes++; }

            // Грубый бокс на корпусе мешал кликать по ручке — заменяем точными mesh-коллайдерами.
            var rootBox = go.GetComponent<BoxCollider>();
            if (rootBox != null) { Object.DestroyImmediate(rootBox); changes++; }

            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.GetComponent<Collider>() == null && mf.sharedMesh != null)
                {
                    mf.gameObject.AddComponent<MeshCollider>();
                    changes++;
                }
            }
            SetLayer(go, itemLayer);

            // Ручка — меш Cube.002 из модели kofemolka.
            Transform crank = null;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("Cube.002")) { crank = t; break; }

            if (crank != null && crank.GetComponent<GrinderCrank>() == null)
            {
                crank.gameObject.AddComponent<GrinderCrank>().SetGrinder(grinder);
                changes++;
            }
            if (crank == null)
                Debug.LogWarning("[TimelessBrew] В кофемолке не найден меш 'Cube.002' — ручка не назначена.");

            return changes;
        }

        /// <summary>Печь: двигаемый бокс StoveTop = и поверхность постановки, и зона нагрева.</summary>
        private static int UpgradeStove(int surfaceLayer)
        {
            var stove = Object.FindFirstObjectByType<Stove>();
            if (stove == null) return 0;

            int changes = 0;

            // Старый корневой StoveTop от сборщика (мог остаться не там, где печь).
            var stale = GameObject.Find("StoveTop");
            if (stale != null && stale.transform.parent == null)
            {
                Object.DestroyImmediate(stale);
                changes++;
            }

            Transform top = stove.transform.Find("StoveTop");
            if (top == null)
            {
                Bounds sb = RendererBounds(stove.gameObject);
                var topGo = new GameObject("StoveTop");
                topGo.transform.SetParent(stove.transform, false);
                // Старт: левая половина верха печи (варочная зона). Пользователь двигает в редакторе.
                topGo.transform.position = new Vector3(sb.center.x - sb.size.x * 0.18f, sb.max.y, sb.center.z);
                topGo.layer = surfaceLayer;
                var box = topGo.AddComponent<BoxCollider>();
                box.size = new Vector3(Mathf.Max(0.05f, sb.size.x * 0.5f), 0.04f, Mathf.Max(0.05f, sb.size.z * 0.7f));
                topGo.AddComponent<PlaceSurface>();
                top = topGo.transform;
                changes++;
            }

            // Лечим слой: раньше рекурсивный SetLayer мехов затирал StoveTop на Item,
            // из-за чего на конфорку нельзя было ничего поставить (SurfaceCast её не видел).
            if (top.gameObject.layer != surfaceLayer)
            {
                top.gameObject.layer = surfaceLayer;
                changes++;
            }

            stove.SetBurnerArea(top.GetComponent<BoxCollider>());
            EditorUtility.SetDirty(stove);
            return changes + 1;
        }

        /// <summary>
        /// Мехи печи: грубый бокс на всю печь заменяется точными mesh-коллайдерами по частям модели,
        /// а компонент Bellows ставится на меш мехов — качается только клик по мехам.
        /// Меш мехов выбирается ДЕТЕРМИНИРОВАННО: мехи стоят правее варочной зоны (StoveTop),
        /// поэтому берётся рендерер с максимальным смещением от центра StoveTop вдоль экранного
        /// «вправо» игровой камеры. Криво назначенный ранее Bellows переносится на верный меш.
        /// </summary>
        private static int UpgradeBellows(int itemLayer)
        {
            var stove = Object.FindFirstObjectByType<Stove>();
            if (stove == null) return 0;

            int changes = 0;
            GameObject go = stove.gameObject;

            // Грубый бокс на корне мешает кликам рядом с печью — заменяем mesh-коллайдерами.
            var rootBox = go.GetComponent<BoxCollider>();
            if (rootBox != null) { Object.DestroyImmediate(rootBox); changes++; }

            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.GetComponent<Collider>() == null && mf.sharedMesh != null)
                {
                    mf.gameObject.AddComponent<MeshCollider>();
                    changes++;
                }
            }
            SetLayer(go, itemLayer);

            Renderer bellowsMesh = PickBellowsMesh(stove);
            if (bellowsMesh == null) return changes;

            // Bellows мог висеть на корне (старый сборщик) или на не том меше (старая эвристика).
            var existing = go.GetComponentInChildren<Bellows>(true);
            if (existing != null && existing.gameObject == bellowsMesh.gameObject)
            {
                existing.SetStove(stove);   // на месте — только освежаем ссылку на печь
                return changes;
            }
            if (existing != null) { Object.DestroyImmediate(existing); changes++; }

            bellowsMesh.gameObject.AddComponent<Bellows>().SetStove(stove);
            changes++;
            Debug.Log($"[TimelessBrew] Мехи назначены на меш «{bellowsMesh.name}» (правее варочной зоны). " +
                      "Раздув: удержание пустой руки на мехах.");
            return changes;
        }

        /// <summary>
        /// Меш мехов в модели печи. Опора — варочная зона StoveTop (пользователь держит её над
        /// конфоркой), мехи находятся правее неё на экране. Сравниваем центры рендереров по
        /// горизонтальной проекции «вправо» игровой камеры; без StoveTop или камеры — фолбэк:
        /// самый удалённый по XZ от центра модели меш (корпус в центре, мехи с краю).
        /// </summary>
        private static Renderer PickBellowsMesh(Stove stove)
        {
            var rends = stove.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return null;

            Bounds all = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) all.Encapsulate(rends[i].bounds);

            Transform top = stove.transform.Find("StoveTop");
            var cam = Camera.main;
            bool byCamera = top != null && cam != null;

            Vector3 origin = all.center;
            if (top != null)
            {
                var area = top.GetComponent<BoxCollider>();
                origin = area != null ? area.bounds.center : top.position;
            }

            Vector3 right = byCamera ? cam.transform.right : Vector3.left;
            right.y = 0f;
            if (right.sqrMagnitude < 1e-6f) { byCamera = false; right = Vector3.left; }
            right.Normalize();

            Renderer bestMesh = null;
            float best = float.MinValue;
            var report = new System.Text.StringBuilder("[TimelessBrew] Кандидаты на мехи (смещение от варочной зоны вправо по камере):");
            foreach (var r in rends)
            {
                Vector3 d = r.bounds.center - origin; d.y = 0f;
                float score = byCamera ? Vector3.Dot(d, right) : d.magnitude;
                report.Append($"\n  {r.name}:  {Vector3.Dot(d, right):+0.###;-0.###} м");
                if (score > best) { best = score; bestMesh = r; }
            }
            Debug.Log(report.ToString());
            return bestMesh;
        }

        /// <summary>Раковина (rakovina): кран воды и зона просеивания — Sink + mesh-коллайдеры, слой Item.</summary>
        private static int UpgradeSink(int itemLayer)
        {
            Transform sinkT = FindModelRoot("rakovina");
            if (sinkT == null) return 0;

            int changes = 0;
            GameObject go = sinkT.gameObject;

            if (go.GetComponentInParent<Sink>() == null && go.GetComponentInChildren<Sink>(true) == null)
            {
                go.AddComponent<Sink>();
                changes++;
            }
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.GetComponent<Collider>() == null && mf.sharedMesh != null)
                {
                    mf.gameObject.AddComponent<MeshCollider>();
                    changes++;
                }
            }
            SetLayer(go, itemLayer);
            return changes;
        }

        /// <summary>Дуршлаг (durshlag): из декора — полноценный сосуд этапа просеивания (§6.3).</summary>
        private static int UpgradeColander(int itemLayer)
        {
            Transform t = FindModelRoot("durshlag");
            if (t == null) return 0;

            int changes = 0;
            GameObject go = t.gameObject;

            if (go.GetComponent<Grabbable>() == null)
            {
                var grab = go.AddComponent<Grabbable>();
                grab.displayName = "Дуршлаг";
                changes++;
            }
            if (go.GetComponent<Vessel>() == null)
            {
                var v = go.AddComponent<Vessel>();
                v.kind = Vessel.Kind.Colander;
                changes++;
            }
            if (go.GetComponent<Colander>() == null) { go.AddComponent<Colander>(); changes++; }

            if (go.GetComponent<Collider>() == null)
            {
                Bounds wb = RendererBounds(go);
                if (wb.size != Vector3.zero)
                {
                    var box = go.AddComponent<BoxCollider>();
                    box.center = wb.center - go.transform.position;
                    box.size = wb.size;
                    changes++;
                }
            }
            SetLayer(go, itemLayer);
            return changes;
        }

        /// <summary>
        /// Блюдца (bludse1): из декора — носимые подставки под чашку (§6.14). На каждое сверху
        /// кладётся дочерний бокс-поверхность (слой Surface) с <see cref="TraySurface"/> — чашка,
        /// поставленная на блюдце, прицепляется к нему и едет вместе с ним (как на подносе).
        /// Идемпотентно: повторный запуск не плодит поверхности.
        /// </summary>
        private static int UpgradeSaucers(int itemLayer, int surfaceLayer)
        {
            int changes = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (!t.name.ToLowerInvariant().Contains("bludse")) continue;
                // Только КОРЕНЬ: у дочернего 'Visual (bludse1)' родитель тоже содержит 'bludse'.
                if (t.parent != null && t.parent.name.ToLowerInvariant().Contains("bludse")) continue;

                GameObject go = t.gameObject;

                var grab = go.GetComponent<Grabbable>();
                if (grab == null)
                {
                    grab = go.AddComponent<Grabbable>();
                    grab.displayName = "Блюдце";
                    changes++;
                }

                Bounds wb = RendererBounds(go);
                if (go.GetComponent<Collider>() == null && wb.size != Vector3.zero)
                {
                    var box = go.AddComponent<BoxCollider>();
                    box.center = wb.center - go.transform.position;
                    box.size = wb.size;
                    changes++;
                }
                SetLayer(go, itemLayer);   // SetLayer не трогает дочерние PlaceSurface (см. ниже)

                // Поверхность под чашку — дочерняя, на слое Surface, едет вместе с блюдцем.
                Transform surfT = go.transform.Find("SaucerSurface");
                if (surfT == null && wb.size != Vector3.zero)
                {
                    var surfGo = new GameObject("SaucerSurface");
                    surfGo.transform.SetParent(go.transform, false);
                    surfGo.layer = surfaceLayer;
                    var box = surfGo.AddComponent<BoxCollider>();
                    box.size = new Vector3(wb.size.x * 0.7f, 0.01f, wb.size.z * 0.7f);
                    surfGo.AddComponent<PlaceSurface>();
                    surfGo.AddComponent<TraySurface>().tray = grab;
                    surfT = surfGo.transform;
                    changes++;
                }
                // ВСЕГДА опускаем поверхность К НИЗУ блюдца — чашка садится в углубление, а не на
                // кромку («парит»). (Лечит и уже созданные блюдца при повторном апгрейде.)
                if (surfT != null && wb.size != Vector3.zero)
                    surfT.position = new Vector3(wb.center.x, wb.min.y + wb.size.y * 0.2f, wb.center.z);
            }
            return changes;
        }

        /// <summary>
        /// Новый UI на объекте «— UI —»: обычный режим (подпись при наведении + шкалы у сосудов),
        /// шестерёнка настроек и дневник на Tab (заменяет книгу гостей). Плюс переименование
        /// молочника в «Питчер». Идемпотентно.
        /// </summary>
        private static int UpgradeUI()
        {
            var ui = GameObject.Find("— UI —");
            int changes = 0;
            if (ui == null)
            {
                ui = new GameObject("— UI —");
                ui.AddComponent<HudUI>();
                ui.AddComponent<CursorTooltip>();
                ui.AddComponent<VesselPanel>();
                ui.AddComponent<StoveGaugeUI>();
                changes++;
            }

            // Книга гостей заменена дневником (NotebookUI включает страницу «Гости»).
            var book = ui.GetComponent<GuestBookUI>();
            if (book != null) { Object.DestroyImmediate(book); changes++; }

            changes += EnsureComp<HoverLabelUI>(ui);
            changes += EnsureComp<VesselGaugeUI>(ui);
            changes += EnsureComp<SettingsGearUI>(ui);
            changes += EnsureComp<NotebookUI>(ui);
            changes += EnsureComp<ToastUI>(ui);
            changes += EnsureComp<CheckReaderUI>(ui);
            changes += EnsureComp<LatteArtUI>(ui);
            changes += EnsureComp<TutorialController>(ui);

            // Молочник → Питчер.
            foreach (var v in Object.FindObjectsByType<Vessel>(FindObjectsSortMode.None))
            {
                if (v.kind != Vessel.Kind.Pitcher) continue;
                var g = v.GetComponent<Grabbable>();
                if (g != null && g.displayName != "Питчер") { g.displayName = "Питчер"; EditorUtility.SetDirty(g); changes++; }
            }

            // Миграция высоты гостя к 3.6: меняем, только если в сцене осталось одно из ПРОШЛЫХ
            // дефолтных значений (1.7 или 5.1) — ручную настройку не затираем.
            var gs = Object.FindFirstObjectByType<GuestService>();
            if (gs != null)
            {
                var so = new SerializedObject(gs);
                var p = so.FindProperty("guestHeight");
                if (p != null && (Mathf.Approximately(p.floatValue, 1.7f) || Mathf.Approximately(p.floatValue, 5.1f)))
                {
                    p.floatValue = 3.6f;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    changes++;
                }
            }
            return changes;
        }

        private static int EnsureComp<T>(GameObject go) where T : Component
        {
            if (go.GetComponent<T>() != null) return 0;
            go.AddComponent<T>();
            return 1;
        }

        /// <summary>
        /// Звоночек: примитив-сфера заменяется моделью Zvonochek и КРАСИТСЯ ЖЁЛТЫМ. Покраска идёт
        /// КАЖДЫЙ запуск (а не только при первой замене) — иначе уже стоящая серая модель с прошлого
        /// апгрейда так и осталась бы непокрашенной.
        /// </summary>
        private static int UpgradeBell(int itemLayer)
        {
            var bell = Object.FindFirstObjectByType<Bell>();
            if (bell == null) return 0;
            GameObject go = bell.gameObject;
            int changes = 0;

            Transform vis = go.transform.Find("Visual (Zvonochek)");
            if (vis == null)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TimelessBrew/Art/Models/Zvonochek.fbx");
                if (asset == null) { Debug.LogWarning("[TimelessBrew] Модель Zvonochek.fbx не найдена в Art/Models."); return 0; }

                Bounds prev = RendererBounds(go);
                float target = prev.size != Vector3.zero ? Mathf.Max(prev.size.x, prev.size.y, prev.size.z) : 0.12f;
                Vector3 basePos = prev.size != Vector3.zero
                    ? new Vector3(prev.center.x, prev.min.y, prev.center.z) : go.transform.position;

                var oldVis = go.transform.Find("Visual");
                if (oldVis != null) { Object.DestroyImmediate(oldVis.gameObject); changes++; }
                var oldCol = go.GetComponent<Collider>();
                if (oldCol != null) Object.DestroyImmediate(oldCol);

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                inst.name = "Visual (Zvonochek)";
                inst.transform.SetParent(go.transform, false);
                Bounds wb = RendererBounds(inst);
                float maxDim = Mathf.Max(wb.size.x, wb.size.y, wb.size.z);
                if (maxDim > 1e-4f) inst.transform.localScale *= target / maxDim;
                wb = RendererBounds(inst);
                inst.transform.position += basePos - new Vector3(wb.center.x, wb.min.y, wb.center.z);
                SetLayer(go, itemLayer);

                wb = RendererBounds(inst);
                var box = go.AddComponent<BoxCollider>();
                box.center = wb.center - go.transform.position;
                box.size = wb.size;
                vis = inst.transform;
                changes++;
            }

            // Жёлтая латунь — назначаем ВСЕГДА.
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader != null ? shader : Shader.Find("Standard"));
            var yellow = new Color(0.95f, 0.78f, 0.16f);
            mat.SetColor("_BaseColor", yellow); mat.color = yellow;
            mat.SetFloat("_Metallic", 0.6f); mat.SetFloat("_Smoothness", 0.6f);
            foreach (var r in vis.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;

            Debug.Log("[TimelessBrew] Звоночек: модель Zvonochek покрашена жёлтым.");
            return changes + 1;
        }

        /// <summary>
        /// Чекхолдер (zakazi): кликабельная цель для развешивания чеков. Вешаем компонент Chekholder
        /// и коллайдер на слой Item (чтобы по нему попадал клик руки с чеком). Логика — в Hand/Check.
        /// </summary>
        private static int UpgradeChekholder(int itemLayer)
        {
            Transform holder = FindModelRoot("zakazi");
            if (holder == null) return 0;

            int changes = 0;
            GameObject go = holder.gameObject;

            if (go.GetComponent<Chekholder>() == null) { go.AddComponent<Chekholder>(); changes++; }

            if (go.GetComponent<Collider>() == null)
            {
                Bounds b = RendererBounds(go);
                if (b.size != Vector3.zero)
                {
                    var box = go.AddComponent<BoxCollider>();
                    box.center = b.center - go.transform.position;
                    box.size = b.size;
                    changes++;
                }
            }
            SetLayer(go, itemLayer);
            return changes;
        }

        /// <summary>
        /// Специи-кубики: чинит сбитый пивот (визуал садится дном на корень). Лечит случай, когда у
        /// кубика (напр. шафрана) пивот оказался сверху — кубик «парит»/тонет. Идемпотентно.
        /// </summary>
        private static int UpgradeSpicePivots()
        {
            int changes = 0;
            foreach (var adder in Object.FindObjectsByType<Adder>(FindObjectsSortMode.None))
            {
                if (adder.AdderKind != Adder.Kind.Spice) continue;
                Bounds b = RendererBounds(adder.gameObject);
                if (b.size == Vector3.zero) continue;
                // Сдвиг, чтобы НИЗ габаритов визуала оказался в точке корня (пивот у основания).
                Vector3 delta = adder.transform.position - new Vector3(b.center.x, b.min.y, b.center.z);
                if (delta.sqrMagnitude < 1e-6f) continue;
                foreach (Transform child in adder.transform) child.position += delta;
                changes++;
            }
            return changes;
        }

        /// <summary>Мусорка: понизить коллайдер (уменьшить высоту, низ у пола), чтобы не задевал предметы.</summary>
        private static int UpgradeTrash()
        {
            var trash = Object.FindFirstObjectByType<Trash>();
            if (trash == null) return 0;
            var box = trash.GetComponent<BoxCollider>();
            if (box == null) return 0;

            Bounds wb = RendererBounds(trash.gameObject);
            if (wb.size == Vector3.zero) return 0;

            float h = wb.size.y * 0.6f;   // ниже исходного, низ остаётся у пола
            box.size = new Vector3(wb.size.x, h, wb.size.z);
            Vector3 worldCenter = new(wb.center.x, wb.min.y + h * 0.5f, wb.center.z);
            box.center = worldCenter - trash.transform.position;
            EditorUtility.SetDirty(trash);
            return 1;
        }

        /// <summary>Часы дня (§4.4): DayClock на объекте GuestService + DayClockUI на «— UI —».</summary>
        private static int UpgradeDayClock()
        {
            int changes = 0;
            var gs = Object.FindFirstObjectByType<GuestService>();
            if (gs != null && gs.GetComponent<DayClock>() == null) { gs.gameObject.AddComponent<DayClock>(); changes++; }
            var ui = GameObject.Find("— UI —");
            if (ui != null)
            {
                changes += EnsureComp<DayClockUI>(ui);
                changes += EnsureComp<EndOfDayUI>(ui);
            }
            return changes;
        }

        /// <summary>Фоновая музыка кухни: вешаем KitchenAudio на главную камеру (там же AudioListener).</summary>
        private static int UpgradeAudio()
        {
            var cam = Camera.main;
            if (cam == null) return 0;
            if (cam.GetComponent<KitchenAudio>() != null) return 0;
            cam.gameObject.AddComponent<KitchenAudio>();
            return 1;
        }

        /// <summary>
        /// Зона выдачи: невидимый бокс постановки ServeMat прижимается вплотную к верху зелёной
        /// скатерти (объект «Decor») — предметы перестают «летать» над ней.
        /// </summary>
        private static int UpgradeServeMat()
        {
            var mat = GameObject.Find("ServeMat");
            if (mat == null || mat.GetComponent<BoxCollider>() == null) return 0;

            // Зелёная скатерть — ближайший к боксу объект с точным именем «Decor».
            Renderer decor = null;
            float best = float.MaxValue;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.name != "Decor") continue;
                var r = t.GetComponent<Renderer>();
                if (r == null) continue;
                float d = (r.bounds.center - mat.transform.position).sqrMagnitude;
                if (d < best) { best = d; decor = r; }
            }
            if (decor == null) return 0;

            // Бокс повторяет скатерть по XZ, его верх — вровень с её верхом (+1 мм).
            Bounds db = decor.bounds;
            const float h = 0.01f;
            mat.transform.rotation = Quaternion.identity;
            mat.transform.localScale = new Vector3(db.size.x, h, db.size.z);
            mat.transform.position = new Vector3(db.center.x, db.max.y + 0.001f - h * 0.5f, db.center.z);
            EditorUtility.SetDirty(mat);
            return 1;
        }

        // ===================== СНИМОК РАСКЛАДКИ =====================

        [System.Serializable]
        private class Entry
        {
            public string path;
            public Vector3 pos;
            public Quaternion rot;
            public Vector3 scale;
        }

        [System.Serializable]
        private class Snapshot { public List<Entry> entries = new(); }

        [MenuItem("TimelessBrew/Save Layout Snapshot", priority = 10)]
        public static void SaveSnapshot()
        {
            var snap = new Snapshot();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                snap.entries.Add(new Entry
                {
                    path = HierarchyPath(t),
                    pos = t.position,
                    rot = t.rotation,
                    scale = t.localScale
                });
            }

            Directory.CreateDirectory(Path.GetDirectoryName(SnapshotPath));
            File.WriteAllText(SnapshotPath, JsonUtility.ToJson(snap, true));
            AssetDatabase.Refresh();
            Debug.Log($"[TimelessBrew] Снимок раскладки сохранён: {SnapshotPath} ({snap.entries.Count} трансформов).");
        }

        [MenuItem("TimelessBrew/Restore Layout Snapshot", priority = 11)]
        public static void RestoreSnapshot()
        {
            if (!File.Exists(SnapshotPath))
            {
                Debug.LogError("[TimelessBrew] Снимок не найден: " + SnapshotPath);
                return;
            }

            var snap = JsonUtility.FromJson<Snapshot>(File.ReadAllText(SnapshotPath));
            var byPath = new Dictionary<string, Entry>();
            foreach (var e in snap.entries) byPath[e.path] = e;

            int restored = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (!byPath.TryGetValue(HierarchyPath(t), out var e)) continue;
                t.position = e.pos;
                t.rotation = e.rot;
                t.localScale = e.scale;
                restored++;
            }

            MarkDirty();
            Debug.Log($"[TimelessBrew] Раскладка восстановлена: {restored} трансформов. Сохрани сцену (Ctrl+S).");
        }

        // ===================== ХЕЛПЕРЫ =====================

        /// <summary>Корень модели по подстроке имени: дочерний 'Visual (xxx)' не считается корнем.</summary>
        internal static Transform FindModelRoot(string nameContains)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (!t.name.ToLowerInvariant().Contains(nameContains)) continue;
                if (t.parent != null && t.parent.name.ToLowerInvariant().Contains(nameContains)) continue;
                return t;
            }
            return null;
        }

        private static Bounds RendererBounds(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }

        private static string HierarchyPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
            return path;
        }

        private static void MarkDirty()
        {
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
        }

        /// <summary>
        /// Слой всему поддереву, КРОМЕ самих поверхностей постановки (PlaceSurface): их боксы живут
        /// на слое Surface — иначе SurfaceCast руки их не видит и на них нельзя ставить предметы
        /// (так конфорка StoveTop однажды «сломалась», уехав на Item). Поддерево обходим ВСЕГДА:
        /// пропускаем только сам Surface-узел, а его возможные дочерние визуалы слой получают.
        /// </summary>
        private static void SetLayer(GameObject go, int layer)
        {
            if (go.GetComponent<PlaceSurface>() == null) go.layer = layer;
            foreach (Transform c in go.transform) SetLayer(c.gameObject, layer);
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
    }
}
