using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TimelessBrew;

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
            int itemLayer = EnsureLayer("Item");
            int surfaceLayer = EnsureLayer("Surface");
            int changes = 0;

            changes += UpgradeCamera();
            changes += UpgradeAdders();
            changes += UpgradeTrays(itemLayer, surfaceLayer);
            changes += UpgradeFridge(itemLayer, surfaceLayer);
            changes += UpgradeGrinder(itemLayer);
            changes += UpgradeStove(surfaceLayer);

            MarkDirty();
            Debug.Log($"[TimelessBrew] Апгрейд сцены готов: изменений {changes}. Проверь маркеры StoveTop (конфорка) и сохрани сцену (Ctrl+S).");
        }

        /// <summary>Камера: фиксированная, поворот по Q/E (CameraSwivel).</summary>
        private static int UpgradeCamera()
        {
            var cam = Camera.main;
            if (cam == null || cam.GetComponent<CameraSwivel>() != null) return 0;
            cam.gameObject.AddComponent<CameraSwivel>();
            return 1;
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
            bool hasTop = false;
            foreach (var tr in fridgeT.GetComponentsInChildren<Transform>(true))
                if (tr.name == "FridgeTop") { hasTop = true; break; }

            if (!hasTop)
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

            stove.SetBurnerArea(top.GetComponent<BoxCollider>());
            EditorUtility.SetDirty(stove);
            return changes + 1;
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

        private static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
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
