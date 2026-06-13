using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TimelessBrew.EditorTools
{
    /// <summary>
    /// Обустройство ЗАЛА кофейни вокруг готовой стойки (открытую сцену не пересобирает!):
    ///   • пол двух вариаций из MedievalTavernPack + «трава» вокруг (пол не тянется далеко);
    ///   • четыре гостевых стола со стульями вокруг каждого (две вариации);
    ///   • кольцо-навес (koltso) над стойкой с подвешенным чекхолдером (zakazi).
    /// Каждый пункт меню идемпотентен: своя группа удаляется и ставится заново, чужое не трогает.
    /// Размеры и позиции считаются от габаритов стойки — масштаб сцены не важен.
    /// </summary>
    public static class HallBuilder
    {
        private const string TavernPrefabs = "Assets/MedievalTavernPack/Prefabs";
        private const string ModelsDir = "Assets/TimelessBrew/Art/Models";

        private const string FloorRoot = "— Hall Floor —";
        private const string FurnitureRoot = "— Hall Furniture —";
        private const string KoltsoName = "Koltso (навес с чекхолдером)";

        // ===================== МЕНЮ =====================

        [MenuItem("TimelessBrew/Зал/Пол: вариант 1 (Floor_01)", priority = 20)]
        public static void Floor1() => BuildFloor("Architecture/Floor_01");

        [MenuItem("TimelessBrew/Зал/Пол: вариант 2 (Floor_02)", priority = 21)]
        public static void Floor2() => BuildFloor("Architecture/Floor_02");

        [MenuItem("TimelessBrew/Зал/Пол: убрать", priority = 22)]
        public static void FloorRemove()
        {
            RemoveGroup(FloorRoot);
            SetGroundActive(true);   // возвращаем старый серый пол-плоскость
            MarkDirty();
            Debug.Log("[TimelessBrew] Пол зала убран, серый Ground возвращён.");
        }

        [MenuItem("TimelessBrew/Зал/Столы и стулья: вариант 1", priority = 40)]
        public static void Furniture1() => BuildFurniture("Furniture/Table_01", "Furniture/Chair_01");

        [MenuItem("TimelessBrew/Зал/Столы и стулья: вариант 2", priority = 41)]
        public static void Furniture2() => BuildFurniture("Furniture/Table_02", "Furniture/Chair_02");

        [MenuItem("TimelessBrew/Зал/Столы и стулья: убрать", priority = 42)]
        public static void FurnitureRemove()
        {
            RemoveGroup(FurnitureRoot);
            MarkDirty();
            Debug.Log("[TimelessBrew] Столы и стулья убраны.");
        }

        [MenuItem("TimelessBrew/Зал/Кольцо-навес над стойкой (с чекхолдером)", priority = 60)]
        public static void PlaceKoltso() => BuildKoltso();

        [MenuItem("TimelessBrew/Зал/Починить материалы паков (URP)", priority = 80)]
        public static void FixPackMaterials() => ConvertPackMaterials(verbose: true);

        // ===================== МАТЕРИАЛЫ ПАКОВ → URP =====================

        /// <summary>
        /// Ассет-паки (таверна, забор) сделаны под Built-in — в URP они мадженовые. Конвертация:
        /// Standard / Standard (Specular setup) / Legacy / битый импорт → URP/Lit с переносом
        /// альбедо, цвета, нормалей и гладкости. Потерянные текстуры перелинковываются по строгому
        /// неймингу пака: материал «Chair_01» → текстуры chair_01_ALBEDO / chair_01_NORMAL.
        /// Вызывается автоматически при постройке пола/мебели, доступна и отдельным пунктом меню.
        /// </summary>
        private static void ConvertPackMaterials(bool verbose = false)
        {
            string[] folders = { "Assets/MedievalTavernPack", "Assets/GuruGames - Fence Pack" };
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) { Debug.LogError("[TimelessBrew] Шейдер URP/Lit не найден."); return; }

            int converted = 0, relinked = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", folders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) continue;

                // Built-in → URP/Lit. Имя сверяем по началу: перезакачанный пак приходит со
                // «Standard (Specular setup)», а не только с чистым «Standard».
                string sn = m.shader != null ? m.shader.name : "";
                bool builtin = m.shader != urpLit
                               && (sn.StartsWith("Standard") || sn.Contains("Legacy") || sn.Contains("Autodesk")
                                   || sn == "Hidden/InternalErrorShader");
                if (builtin)
                {
                    // Читаем свойства СТАРОГО шейдера до переключения.
                    Texture albedo = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
                    Texture bump = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : null;
                    Color color = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
                    float gloss = m.HasProperty("_Glossiness") ? m.GetFloat("_Glossiness") : 0.5f;
                    float metallic = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0f;

                    m.shader = urpLit;
                    m.SetTexture("_BaseMap", albedo);
                    m.SetColor("_BaseColor", color);
                    m.SetTexture("_BumpMap", bump);
                    if (bump != null) m.EnableKeyword("_NORMALMAP");
                    m.SetFloat("_Smoothness", gloss);
                    m.SetFloat("_Metallic", metallic);
                    EditorUtility.SetDirty(m);
                    converted++;
                }

                if (m.shader != urpLit) continue;   // незнакомый шейдер — не трогаем

                // Перелинковка потерянных текстур по неймингу. Лечит материалы, успевшие
                // сконвертироваться с пустым альбедо (битый импорт, пересборка пака).
                if (m.GetTexture("_BaseMap") == null && FindPackTexture(folders, m.name + "_ALBEDO", out var albedoTex))
                {
                    m.SetTexture("_BaseMap", albedoTex);
                    EditorUtility.SetDirty(m);
                    relinked++;
                }
                if (m.GetTexture("_BumpMap") == null && FindPackTexture(folders, m.name + "_NORMAL", out var normalTex))
                {
                    m.SetTexture("_BumpMap", normalTex);
                    m.EnableKeyword("_NORMALMAP");
                    EditorUtility.SetDirty(m);
                }
            }

            if (converted > 0 || relinked > 0) AssetDatabase.SaveAssets();
            if (verbose || converted > 0 || relinked > 0)
                Debug.Log($"[TimelessBrew] Материалы паков: конвертировано в URP — {converted}, перелинковано текстур — {relinked}.");
        }

        /// <summary>Текстура пака по точному имени файла (без расширения), регистр не важен.</summary>
        private static bool FindPackTexture(string[] folders, string name, out Texture tex)
        {
            tex = null;
            foreach (var guid in AssetDatabase.FindAssets($"t:Texture2D {name}", folders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!file.Equals(name, System.StringComparison.OrdinalIgnoreCase)) continue;
                tex = AssetDatabase.LoadAssetAtPath<Texture>(path);
                if (tex != null) return true;
            }
            return false;
        }

        // ===================== ПОЛ + ТРАВА =====================

        private static void BuildFloor(string prefabRel)
        {
            if (!Anchor(out Vector3 c, out float ex, out float ez, out float u, out float groundY, out _)) return;
            ConvertPackMaterials();   // паки сделаны под Built-in — без конвертации всё мадженовое

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{TavernPrefabs}/{prefabRel}.prefab");
            if (prefab == null) { Debug.LogError($"[TimelessBrew] Не найден префаб {TavernPrefabs}/{prefabRel}.prefab"); return; }

            RemoveGroup(FloorRoot);
            var root = new GameObject(FloorRoot).transform;

            // Шаг плитки: меряем префаб и нормируем к удобному размеру относительно стойки.
            var probe = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Bounds pb = RendererBounds(probe);
            Object.DestroyImmediate(probe);
            float tile = 1.0f * u;
            float footprint = Mathf.Max(pb.size.x, pb.size.z);
            float scale = footprint > 1e-4f ? tile / footprint : 1f;

            // Зона пола: зал перед стойкой (в сторону гостей, −Z) и немного под стойку.
            // Дальше пола — «трава», поэтому далеко не тянем.
            float minX = c.x - 3f * u, maxX = c.x + 3f * u;
            float minZ = c.z - ez - 4.2f * u, maxZ = c.z + 1.6f * u;
            int nx = Mathf.CeilToInt((maxX - minX) / tile);
            int nz = Mathf.CeilToInt((maxZ - minZ) / tile);
            for (int ix = 0; ix < nx; ix++)
                for (int iz = 0; iz < nz; iz++)
                {
                    var t = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                    t.name = $"Floor_{ix}_{iz}";
                    t.transform.localScale *= scale;
                    Bounds b = RendererBounds(t);
                    Vector3 want = new Vector3(minX + (ix + 0.5f) * tile, groundY + 0.005f, minZ + (iz + 0.5f) * tile);
                    t.transform.position += want - new Vector3(b.center.x, b.min.y, b.center.z);
                }

            // «Трава» вокруг пола — большая зелёная плоскость чуть ниже плитки.
            var grass = GameObject.CreatePrimitive(PrimitiveType.Plane);
            grass.name = "Grass";
            grass.transform.SetParent(root, false);
            grass.transform.position = new Vector3(c.x, groundY - 0.01f, c.z);
            grass.transform.localScale = Vector3.one * (4f * u);   // Plane = 10×10 м при scale 1
            Object.DestroyImmediate(grass.GetComponent<Collider>());
            AssignMat(grass, new Color(0.33f, 0.52f, 0.27f));

            SetGroundActive(false);   // старый серый Ground прячем — его роль теперь у пола и травы
            MarkDirty();
            Debug.Log($"[TimelessBrew] Пол зала: {nx}×{nz} плиток ({prefabRel}) + трава. Сохрани сцену (Ctrl+S).");
        }

        // ===================== СТОЛЫ И СТУЛЬЯ =====================

        private static void BuildFurniture(string tableRel, string chairRel)
        {
            if (!Anchor(out Vector3 c, out float ex, out float ez, out float u, out float groundY, out _)) return;
            ConvertPackMaterials();   // паки сделаны под Built-in — без конвертации всё мадженовое

            var tablePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{TavernPrefabs}/{tableRel}.prefab");
            var chairPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{TavernPrefabs}/{chairRel}.prefab");
            if (tablePrefab == null || chairPrefab == null)
            { Debug.LogError($"[TimelessBrew] Не найдены префабы {tableRel} / {chairRel}"); return; }

            // Если пол зала уже постелен — мебель ставим на верх плитки, а не на уровень земли.
            var floor = GameObject.Find(FloorRoot);
            if (floor != null)
            {
                Bounds fb = RendererBounds(floor);
                if (fb.size != Vector3.zero) groundY = fb.max.y;
            }

            RemoveGroup(FurnitureRoot);
            var root = new GameObject(FurnitureRoot).transform;

            // Четыре стола двумя рядами в зале (−Z от стойки), мимо точки гостя у стойки.
            Vector3[] spots =
            {
                new Vector3(c.x - 1.05f * u, 0f, c.z - ez - 1.6f * u),
                new Vector3(c.x + 1.05f * u, 0f, c.z - ez - 1.6f * u),
                new Vector3(c.x - 1.05f * u, 0f, c.z - ez - 3.0f * u),
                new Vector3(c.x + 1.05f * u, 0f, c.z - ez - 3.0f * u),
            };

            for (int i = 0; i < spots.Length; i++)
            {
                Vector3 at = new Vector3(spots[i].x, groundY + 0.01f, spots[i].z);
                GameObject table = Place(tablePrefab, root, 0.8f * u, at);
                table.name = $"GuestTable_{i + 1}";

                // Стулья вокруг стола: по одному с каждой стороны, лицом к столу.
                Bounds tb = RendererBounds(table);
                float tableR = Mathf.Max(tb.extents.x, tb.extents.z);
                for (int k = 0; k < 4; k++)
                {
                    float ang = k * 90f * Mathf.Deg2Rad;
                    Vector3 dir = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
                    Vector3 cAt = at + dir * (tableR + 0.18f * u);
                    GameObject chair = Place(chairPrefab, root, 0.55f * u, cAt);
                    chair.name = $"GuestChair_{i + 1}_{k + 1}";
                    chair.transform.rotation = Quaternion.LookRotation(-dir);   // лицом к столу
                }
            }

            MarkDirty();
            Debug.Log($"[TimelessBrew] Зал: 4 стола ({tableRel}) и по 4 стула ({chairRel}). " +
                      "Если стулья смотрят не той стороной — скажи, разверну. Сохрани сцену (Ctrl+S).");
        }

        // ===================== КОЛЬЦО-НАВЕС =====================

        private static void BuildKoltso()
        {
            if (!Anchor(out Vector3 c, out float ex, out float ez, out float u, out _, out float top)) return;

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelsDir}/koltso.fbx");
            if (asset == null) { Debug.LogError($"[TimelessBrew] Не найдена модель {ModelsDir}/koltso.fbx"); return; }

            // Чекхолдер отцепляем (мог висеть на старом кольце) — переподвесим заново.
            Transform zakazi = SceneUpgrader.FindModelRoot("zakazi");
            if (zakazi != null) zakazi.SetParent(null, true);

            var old = GameObject.Find(KoltsoName);
            if (old != null) Object.DestroyImmediate(old);

            var ring = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            ring.name = KoltsoName;

            // Масштаб — чуть уже стойки; низ кольца — над столешницей.
            Bounds rb = RendererBounds(ring);
            float maxDim = Mathf.Max(rb.size.x, rb.size.y, rb.size.z);
            if (maxDim > 1e-4f) ring.transform.localScale *= (2f * ex * 0.92f) / maxDim;
            rb = RendererBounds(ring);
            Vector3 want = new Vector3(c.x, top + 0.55f * u, c.z);
            ring.transform.position += want - new Vector3(rb.center.x, rb.min.y, rb.center.z);

            // Чекхолдер подвешиваем к переднему (к камере, +Z) краю кольца и прицепляем к нему.
            if (zakazi != null)
            {
                rb = RendererBounds(ring);
                Bounds zb = RendererBounds(zakazi.gameObject);
                Vector3 zWant = new Vector3(rb.center.x, rb.min.y + 0.02f * u, rb.max.z - 0.05f * u);
                zakazi.position += zWant - new Vector3(zb.center.x, zb.max.y, zb.center.z);
                zakazi.SetParent(ring.transform, true);
            }
            else Debug.LogWarning("[TimelessBrew] Чекхолдер (zakazi) в сцене не найден — кольцо поставлено без него.");

            MarkDirty();
            Debug.Log("[TimelessBrew] Кольцо-навес над стойкой готово, чекхолдер подвешен. " +
                      "Двигай кольцо целиком — чекхолдер поедет с ним. Сохрани сцену (Ctrl+S).");
        }

        // ===================== ХЕЛПЕРЫ =====================

        /// <summary>Габариты стойки: центр, полуразмеры, юнит масштаба u, уровень пола и верх столешницы.</summary>
        private static bool Anchor(out Vector3 c, out float ex, out float ez, out float u, out float groundY, out float top)
        {
            c = Vector3.zero; ex = ez = u = groundY = top = 0f;

            GameObject table = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.parent == null && t.name.StartsWith("Table")) { table = t.gameObject; break; }
            if (table == null)
            {
                Debug.LogError("[TimelessBrew] Не нашёл корневой объект стойки (имя начинается с 'Table').");
                return false;
            }

            Bounds b = RendererBounds(table);
            c = b.center;
            ex = Mathf.Max(0.2f, b.extents.x);
            ez = Mathf.Max(0.2f, b.extents.z);
            u = Mathf.Max(ex, ez);
            groundY = b.min.y;
            top = b.max.y;
            return true;
        }

        /// <summary>Инстанс префаба: масштаб до targetMax по макс. габариту, дно — в точке pos.</summary>
        private static GameObject Place(GameObject prefab, Transform parent, float targetMax, Vector3 pos)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            Bounds b = RendererBounds(go);
            float maxDim = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (maxDim > 1e-4f) go.transform.localScale *= targetMax / maxDim;
            b = RendererBounds(go);
            go.transform.position += pos - new Vector3(b.center.x, b.min.y, b.center.z);
            return go;
        }

        private static void RemoveGroup(string name)
        {
            var old = GameObject.Find(name);
            if (old != null) Object.DestroyImmediate(old);
        }

        /// <summary>Включить/выключить старый серый пол «Ground» (ищем и среди выключенных).</summary>
        private static void SetGroundActive(bool active)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == "Ground" && t.parent == null) { t.gameObject.SetActive(active); return; }
        }

        private static Bounds RendererBounds(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }

        private static void AssignMat(GameObject go, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(shader != null ? shader : Shader.Find("Standard"));
            m.SetColor("_BaseColor", color);
            m.color = color;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = m;
        }

        private static void MarkDirty()
            => EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
}
